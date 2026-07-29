#!/usr/bin/env python3
"""Generate an LLM-friendly Markdown skill from the downloaded OnLocation OpenAPI spec."""
import html
import json
import os
import re
from collections import defaultdict
from urllib.parse import urljoin

from bs4 import BeautifulSoup

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SPEC_PATH = os.path.join(ROOT, 'ref', 'whosonlocation-openapi.json')
OUT_DIR = os.path.join(ROOT, 'skills')
OUT_PATH = os.path.join(OUT_DIR, 'onlocation-api.md')


def html_to_md(text):
    """Convert HTML snippets in OpenAPI descriptions to plain Markdown."""
    if not text:
        return ''
    text = html.unescape(text)
    if not text.strip():
        return ''
    # If there are no tags, just return the text with whitespace normalized
    if '<' not in text or '>' not in text:
        return re.sub(r'\s+', ' ', text).strip()

    soup = BeautifulSoup(text, 'html.parser')

    def _tag_to_md(elem):
        if elem is None:
            return ''
        if isinstance(elem, str):
            s = str(elem)
            s = s.replace('\n', ' ').replace('\r', ' ')
            return s
        name = getattr(elem, 'name', None)
        if name is None:
            return _tag_to_md(elem.string)
        if name in ('script', 'style'):
            return ''
        if name == 'a':
            href = elem.get('href', '')
            txt = elem.get_text(strip=True)
            if href and not href.startswith('#'):
                return f'[{txt}]({href})'
            return txt
        if name in ('p', 'div', 'li'):
            inner = elem.decode_contents()
            inner_soup = BeautifulSoup(inner, 'html.parser')
            sub_md = _tag_to_md(inner_soup)
            sub_md = re.sub(r'\s+', ' ', sub_md).strip()
            if name == 'li':
                return f'- {sub_md}\n'
            return f'{sub_md}\n\n'
        if name in ('ul', 'ol'):
            return ''.join(_tag_to_md(c) for c in elem.children).strip() + '\n\n'
        if name in ('br',):
            return '\n'
        # Default: recurse
        return ''.join(_tag_to_md(c) for c in elem.children)

    md = _tag_to_md(soup)
    # Clean up multiple blanks
    md = re.sub(r'\n{3,}', '\n\n', md)
    md = re.sub(r' +', ' ', md)
    return md.strip()


def load_spec():
    with open(SPEC_PATH, 'r', encoding='utf-8') as f:
        return json.load(f)


def resolve_ref(spec, ref):
    """Resolve a $ref string like '#/components/schemas/Foo'."""
    if not ref.startswith('#/'):
        return {}
    parts = ref.split('/')[1:]
    node = spec
    for p in parts:
        node = node.get(p, {})
    return node


def resolve_schema(spec, schema, visited=None):
    """Return the concrete schema node, following $ref and allOf."""
    if visited is None:
        visited = set()
    if not isinstance(schema, dict):
        return schema
    if '$ref' in schema:
        ref = schema['$ref']
        if ref in visited:
            return {}
        visited.add(ref)
        return resolve_schema(spec, resolve_ref(spec, ref), visited)
    if 'allOf' in schema:
        merged = {'type': 'object', 'properties': {}, 'required': []}
        for part in schema['allOf']:
            part_resolved = resolve_schema(spec, part, visited.copy())
            if not isinstance(part_resolved, dict):
                continue
            if part_resolved.get('type') == 'object':
                merged['properties'].update(part_resolved.get('properties', {}))
                merged['required'] = list(set(merged['required'] + part_resolved.get('required', [])))
            if 'description' in part_resolved:
                merged.setdefault('description', part_resolved['description'])
        return merged
    return schema


def schema_type_name(schema):
    """Human-readable schema type string."""
    if not isinstance(schema, dict):
        return ''
    if '$ref' in schema:
        return schema['$ref'].split('/')[-1]
    t = schema.get('type', '')
    fmt = schema.get('format', '')
    items = schema.get('items', {})
    if t == 'array':
        if isinstance(items, dict):
            return f'array of {schema_type_name(items)}'
        return 'array'
    if 'enum' in schema:
        return f"{t or 'enum'}: {', '.join(repr(e) for e in schema['enum'][:5])}"
    if 'allOf' in schema:
        return 'object (allOf)'
    if t and fmt and fmt != t:
        return f'{t}({fmt})'
    return t or 'object'


def example_for_schema(spec, schema, visited=None):
    """Generate a representative example value for a schema."""
    if visited is None:
        visited = set()
    if not isinstance(schema, dict):
        return schema
    if '$ref' in schema:
        ref = schema['$ref']
        if ref in visited:
            return {}
        visited.add(ref)
        return example_for_schema(spec, resolve_ref(spec, ref), visited)
    if 'example' in schema:
        return schema['example']
    t = schema.get('type')
    if t == 'string':
        return schema.get('example', '') or ''
    if t == 'integer':
        return schema.get('example', 0)
    if t == 'number':
        return schema.get('example', 0.0)
    if t == 'boolean':
        return schema.get('example', False)
    if t == 'array':
        items = schema.get('items', {})
        ex = example_for_schema(spec, items, visited.copy())
        return [ex] if ex != {} else []
    if t == 'object' or 'properties' in schema:
        obj = {}
        props = schema.get('properties', {})
        required = set(schema.get('required', []))
        for name, prop_schema in props.items():
            if name in required or 'example' in prop_schema:
                obj[name] = example_for_schema(spec, prop_schema, visited.copy())
        return obj
    if 'enum' in schema:
        return schema['enum'][0] if schema['enum'] else None
    if 'allOf' in schema:
        merged = example_for_schema(spec, {'allOf': schema['allOf']}, visited)
        return merged
    return None


def build_example_object(spec, schema, visited=None):
    """Build a fuller example object including optional fields with examples."""
    if visited is None:
        visited = set()
    if not isinstance(schema, dict):
        return schema
    if '$ref' in schema:
        ref = schema['$ref']
        if ref in visited:
            return {}
        visited.add(ref)
        return build_example_object(spec, resolve_ref(spec, ref), visited)
    t = schema.get('type')
    if 'example' in schema:
        return schema['example']
    if t == 'string':
        return schema.get('example', '') or ''
    if t == 'integer':
        return schema.get('example', 0)
    if t == 'number':
        return schema.get('example', 0.0)
    if t == 'boolean':
        return schema.get('example', False)
    if t == 'array':
        items = schema.get('items', {})
        ex = build_example_object(spec, items, visited.copy())
        return [ex] if ex != {} else []
    if t == 'object' or 'properties' in schema:
        obj = {}
        for name, prop_schema in schema.get('properties', {}).items():
            ex = build_example_object(spec, prop_schema, visited.copy())
            if ex is not None and ex != {}:
                obj[name] = ex
        return obj
    if 'enum' in schema:
        return schema['enum'][0] if schema['enum'] else None
    if 'allOf' in schema:
        # Merge allOf parts
        obj = {}
        for part in schema['allOf']:
            part_resolved = resolve_schema(spec, part, visited.copy())
            if not isinstance(part_resolved, dict):
                continue
            part_ex = build_example_object(spec, part_resolved, visited.copy())
            if isinstance(part_ex, dict):
                obj.update(part_ex)
        return obj
    return None


def render_parameter(param):
    """Render a single OpenAPI parameter as Markdown bullet."""
    name = param.get('name', '')
    pin = param.get('in', '')
    required = param.get('required', False)
    schema = param.get('schema', {})
    type_name = schema_type_name(schema)
    desc = html_to_md(param.get('description', ''))
    example = param.get('example') or schema.get('example')
    parts = [f'- **{name}** ({pin}']
    if type_name:
        parts.append(f' · {type_name}')
    if required:
        parts.append(' · required')
    parts.append(')')
    line = ''.join(parts)
    if desc:
        line += f': {desc}'
    if example is not None:
        line += f' (example: `{example}`)'
    return line


def render_request_body(spec, request_body):
    """Render request body as Markdown with an example."""
    if not request_body:
        return ''
    desc = html_to_md(request_body.get('description', ''))
    required = request_body.get('required', False)
    lines = ['### Request Body']
    if desc:
        lines.append(desc)
    lines.append(f'**Required:** {required}')
    lines.append('')

    content = request_body.get('content', {})
    for content_type, content_obj in content.items():
        schema = content_obj.get('schema', {})
        schema_name = schema_type_name(schema)
        lines.append(f'- **Content-Type:** `{content_type}`')
        if schema_name:
            lines.append(f'- **Schema:** `{schema_name}`')
        if content_type == 'application/json' or 'json' in content_type:
            example = build_example_object(spec, schema)
            if example:
                lines.append('')
                lines.append('```json')
                lines.append(json.dumps(example, indent=2, ensure_ascii=False))
                lines.append('```')
                lines.append('')
    return '\n'.join(lines)


def render_responses(spec, responses):
    """Render response statuses as Markdown."""
    if not responses:
        return ''
    lines = ['### Responses', '']
    for status, resp in responses.items():
        desc = html_to_md(resp.get('description', ''))
        lines.append(f'#### {status}' + (f' — {desc}' if desc else ''))
        content = resp.get('content', {})
        if not content:
            lines.append('')
            continue
        for content_type, content_obj in content.items():
            schema = content_obj.get('schema', {})
            schema_name = schema_type_name(schema)
            lines.append(f'- **Content-Type:** `{content_type}`')
            if schema_name:
                lines.append(f'- **Schema:** `{schema_name}`')
            if content_type == 'application/json' or 'json' in content_type:
                example = build_example_object(spec, schema)
                if example:
                    lines.append('')
                    lines.append('```json')
                    lines.append(json.dumps(example, indent=2, ensure_ascii=False))
                    lines.append('```')
            lines.append('')
    return '\n'.join(lines)


def render_schema_section(spec, name, schema):
    """Render a single component schema as Markdown."""
    resolved = resolve_schema(spec, schema)
    lines = [f'## {name}', '']
    desc = html_to_md(resolved.get('description', ''))
    if desc:
        lines.append(desc)
        lines.append('')
    if 'allOf' in schema:
        lines.append('*(Composed schema: allOf)*')
        lines.append('')
    props = resolved.get('properties', {})
    required = set(resolved.get('required', []))
    if props:
        lines.append('| Field | Type | Required | Description | Example |')
        lines.append('|-------|------|----------|-------------|---------|')
        for prop_name, prop_schema in props.items():
            type_name = schema_type_name(prop_schema)
            is_req = 'Yes' if prop_name in required else ''
            prop_desc = html_to_md(prop_schema.get('description', ''))
            ex = build_example_object(spec, prop_schema)
            ex_str = json.dumps(ex) if ex is not None and ex != {} else ''
            lines.append(f'| `{prop_name}` | {type_name} | {is_req} | {prop_desc} | `{ex_str[:80]}` |')
        lines.append('')
    return '\n'.join(lines)


def render_operation(spec, path, method, op):
    """Render a single path operation as Markdown."""
    summary = op.get('summary', '')
    lines = [f'## {method.upper()} {path} — {summary}', '']
    desc = html_to_md(op.get('description', ''))
    if desc:
        lines.append(desc)
        lines.append('')

    params = op.get('parameters', [])
    if params:
        lines.append('### Parameters')
        for p in params:
            lines.append(render_parameter(p))
        lines.append('')

    request_body = render_request_body(spec, op.get('requestBody'))
    if request_body:
        lines.append(request_body)
        lines.append('')

    responses = render_responses(spec, op.get('responses', {}))
    if responses:
        lines.append(responses)
        lines.append('')

    return '\n'.join(lines)


def main():
    spec = load_spec()
    info = spec.get('info', {})
    servers = spec.get('servers', [])
    base_url = servers[0].get('url', 'https://api.whosonlocation.com/v1') if servers else 'https://api.whosonlocation.com/v1'
    tags = {t['name']: t for t in spec.get('tags', [])}

    # Group operations by primary tag
    ops_by_tag = defaultdict(list)
    for path, methods in spec.get('paths', {}).items():
        for method, op in methods.items():
            if not isinstance(op, dict):
                continue
            primary_tag = op.get('tags', [None])[0]
            ops_by_tag[primary_tag].append((path, method, op))

    os.makedirs(OUT_DIR, exist_ok=True)
    with open(OUT_PATH, 'w', encoding='utf-8') as out:
        out.write('# MRI OnLocation REST API Skill\n\n')
        out.write('> Generated from `ref/whosonlocation-openapi.json` (apidocs.whosonlocation.com).\n')
        out.write('> Use this reference when implementing a bridge/sync between MRI OnLocation and Gallagher.\n\n')
        out.write('---\n\n')

        out.write('# Bridge Quick Reference\n\n')
        out.write('> TL;DR for the Gallagher ↔ OnLocation bridge.\n\n')
        out.write('- **Base URL:** `' + base_url + '`\n')
        out.write('- **Authentication:**\n')
        out.write('  - API key: `Authorization: APIKEY <api_key>` (legacy but supported).\n')
        out.write('  - Basic auth: `Authorization: Basic <base64(api_key:)>` (password is empty).\n')
        out.write('  - OAuth 2.0 client credentials (recommended): `POST https://login.whosonlocation.com/oauth2/token` with `grant_type=client_credentials` and `scope=domain:read`/`domain:write`. Use returned `access_token` as `Authorization: Bearer <token>`.\n')
        out.write('- **Media types:** Default response is XML. Set `Accept: application/json` for JSON. Set `Content-Type: application/json` for request bodies.\n')
        out.write('- **Pagination:** Keyset/cursor pagination. Use `order=id&limit=10` then `q=id>last_id&order=id&limit=10`. Add `If-Modified-Since` header for deltas where supported.\n')
        out.write('- **Filtering:** `q` query parameter. Operators: `:` equals, `%` starts-with, `>` greater-than, `<` less-than, `~` wildcard (`%` for zero-or-more). Comma-separate for AND.\n')
        out.write('- **Time zones:** `time_zone` parameter (case-sensitive, e.g. `UTC`, `Pacific/Auckland`).\n')
        out.write('- **Rate limits:** 100 requests/min per credential. `429 Too Many Requests` with `Retry-After` header.\n')
        out.write('- **Key bridge endpoints:**\n')
        out.write('  - Employees: `GET /staff`, `POST /staff`, `PUT /staff/{id}`, `DELETE /staff/{id}`, `POST /staff/movement`\n')
        out.write('  - Contractors: `GET /sp/member`, `POST /sp/member`, `PUT /sp/member/{id}`, `DELETE /sp/member/{id}`\n')
        out.write('  - Visitor events: `GET /visitor/event`, `POST /visitor/event`, `PUT /visitor/event/{id}`\n')
        out.write('  - Pre-registered visitors: `GET /visitor/register`, `POST /visitor/register`\n')
        out.write('  - Inductions: `GET /induction`, `GET /induction/{id}/holder`, `POST /induction/{id}/holder`\n')
        out.write('  - Locations / departments / zones: `GET /location`, `GET /department`, `GET /zone`\n')
        out.write('  - Custom fields: `GET /customfield` (for employees, contractor orgs, contractor members)\n')
        out.write('\n---\n\n')

        out.write('# Introduction\n\n')
        out.write(html_to_md(info.get('description', '')) + '\n\n')
        out.write(f'**Version:** {info.get("version", "")}\n\n')
        out.write('---\n\n')

        out.write(f'# Operations ({sum(len(v) for v in ops_by_tag.values())})\n\n')
        tag_order = [t['name'] for t in spec.get('tags', [])]
        # Include any tag names found in operations but missing from the spec tags list
        remaining = sorted([n for n in ops_by_tag.keys() if n not in tag_order])
        ordered_tags = [n for n in tag_order if n in ops_by_tag] + remaining
        for tag_name in ordered_tags:
            tag = tags.get(tag_name, {})
            out.write(f'## Tag: {tag_name}\n\n')
            tag_desc = html_to_md(tag.get('description', ''))
            if tag_desc:
                out.write(tag_desc + '\n\n')
            for path, method, op in ops_by_tag[tag_name]:
                out.write(render_operation(spec, path, method, op))
                out.write('---\n\n')

        schemas = spec.get('components', {}).get('schemas', {})
        out.write(f'# Schema Definitions ({len(schemas)})\n\n')
        for name in sorted(schemas.keys()):
            out.write(render_schema_section(spec, name, schemas[name]))
            out.write('---\n\n')

    print(f'Wrote {OUT_PATH}')
    print(f'  operations: {sum(len(v) for v in ops_by_tag.values())}')
    print(f'  tags: {len(ops_by_tag)}')
    print(f'  schemas: {len(schemas)}')


if __name__ == '__main__':
    main()
