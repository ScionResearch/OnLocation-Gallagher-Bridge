#!/usr/bin/env python3
"""Extract a clean, LLM-friendly Markdown skill from the Gallagher
Command Centre Cardholder REST API reference HTML saved locally."""
import html
import os
import re

from bs4 import BeautifulSoup, Comment

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
HTML_PATH = os.path.join(ROOT, 'ref', 'Command Centre REST API_ Cardholders _ API Reference.html')
OUT_DIR = os.path.join(ROOT, 'skills')
OUT_PATH = os.path.join(OUT_DIR, 'gallagher-cardholder-api.md')


def tag_to_md(elem, in_pre=False):
    """Recursively convert a BeautifulSoup element tree to Markdown."""
    if elem is None:
        return ''

    if isinstance(elem, str):
        # Skip HTML comments (e.g. <!-- <div class="hljs"> -->)
        if isinstance(elem, Comment):
            return ''
        # NavigableString / string
        text = elem
        if in_pre:
            return text
        # Normalise whitespace but keep a single space between inline nodes
        text = text.replace('\n', ' ').replace('\r', ' ')
        if not text.strip():
            return ''
        return text

    name = elem.name
    if name is None:
        return tag_to_md(elem.string, in_pre)

    if name in ('script', 'style', 'nav', 'header', 'footer', 'button'):
        return ''

    if name == 'pre':
        # Prefer the inner <code> text; fall back to the whole pre
        code_elem = elem.find('code')
        code = code_elem.get_text() if code_elem else elem.get_text()
        code = code.strip()
        # Some examples have a stray "json" language line from the highlighter
        code = re.sub(r'^(json)\s*\n', '', code, flags=re.IGNORECASE)
        return '\n\n```json\n' + code + '\n```\n\n'

    if name == 'code':
        text = elem.get_text()
        text = text.replace('`', '\\`')
        return '`' + text + '`'

    if name in ('h1', 'h2', 'h3', 'h4', 'h5', 'h6'):
        level = int(name[1])
        text = elem.get_text(' ', strip=True)
        if text:
            return '\n\n' + '#' * level + ' ' + text + '\n\n'
        return ''

    if name == 'p':
        text = elem.get_text(' ', strip=True)
        if text:
            return '\n\n' + text + '\n\n'
        return ''

    if name == 'a':
        text = elem.get_text(' ', strip=True)
        href = elem.get('href', '')
        if text:
            if href and href.startswith('http'):
                return '[' + text + '](' + href + ')'
            return text
        return ''

    if name == 'li':
        text = elem.get_text(' ', strip=True)
        if text:
            return '\n- ' + text
        return ''

    if name in ('ul', 'ol'):
        parts = [tag_to_md(child, in_pre) for child in elem.children]
        return '\n' + ''.join(parts) + '\n'

    if name in ('div', 'section', 'article', 'span', 'strong', 'em', 'b', 'i',
                'table', 'thead', 'tbody', 'tr', 'td', 'th', 'dl', 'dt', 'dd'):
        # For prop-row tables, format as a compact list-like string
        if 'prop-row' in (elem.get('class') or []):
            return '\n' + prop_row_to_md(elem) + '\n'
        parts = []
        for child in elem.children:
            parts.append(tag_to_md(child, in_pre))
        return ''.join(parts)

    # Fallback
    return elem.get_text(' ', strip=True)


def prop_row_to_md(elem):
    """Render a swagger property row as a markdown line."""
    title = ''
    subtitle = ''
    value = ''
    for child in elem.find_all(recursive=False):
        cls = ' '.join(child.get('class', []))
        if 'prop-name' in cls:
            title_tag = child.find(class_='prop-title')
            if title_tag:
                title = title_tag.get_text(' ', strip=True)
            sub_tags = child.find_all(class_='prop-subtitle')
            subtitle = ' · '.join(t.get_text(' ', strip=True) for t in sub_tags)
        elif 'prop-value' in cls:
            value = child.get_text(' ', strip=True)
    if not title:
        return elem.get_text(' ', strip=True)
    line = f'- **{title}**' + (f' ({subtitle})' if subtitle else '')
    if value:
        line += f': {value}'
    return line


def clean_md(text):
    """Final whitespace and entity cleanup."""
    text = html.unescape(text)
    # Remove navigation leftovers
    text = re.sub(r'×\s*', '', text)
    # Remove trailing spaces on lines
    text = re.sub(r' +\n', '\n', text)
    # Collapse multiple blank lines
    text = re.sub(r'\n(?:\s*\n){2,}', '\n\n', text)
    # Remove spaces before inline code closing backtick and after opening
    text = re.sub(r'`\s+([^`]*?)\s+`', r'`\1`', text)
    # Tidy code fences: collapse blank lines around fences but keep language tag on the opening line
    text = re.sub(r'```json\n+', r'```json\n', text)
    text = re.sub(r'\n+```(?:\n+|$)', '\n```\n', text)
    return text.strip()


def extract_intro(soup):
    """Extract topic/intro text from the <article> before the first operation."""
    article = soup.find('article')
    if not article:
        return ''
    intro_parts = []
    for child in article.children:
        if getattr(child, 'name', None) == 'div' and child.get('id', '').startswith('operation--'):
            break
        if getattr(child, 'name', None) == 'h1' and 'doc-title' in (child.get('class') or []):
            continue
        if getattr(child, 'name', None) == 'div' and child.get('id') == 'logo':
            continue
        if getattr(child, 'name', None) in ('div', 'h1'):
            md = tag_to_md(child)
            if md.strip():
                intro_parts.append(md)
    return clean_md('\n'.join(intro_parts))


def extract_operation(block):
    """Return markdown for an operation panel."""
    method = ''
    path = ''
    summary = ''

    path_section = block.find(class_='swagger-operation-path')
    if path_section:
        method_tag = path_section.find(class_='operation-method')
        path_tag = path_section.find(class_='operation-path')
        method = method_tag.get_text(strip=True) if method_tag else ''
        path = path_tag.get_text(strip=True) if path_tag else ''

    summary_tag = block.find(class_='operation-summary')
    if summary_tag:
        summary = summary_tag.get_text(strip=True)

    # Description
    desc = ''
    desc_section = block.find(class_='swagger-operation-description')
    if desc_section:
        desc = clean_md(tag_to_md(desc_section))

    # Parameters
    params_section = block.find(class_='swagger-request-params')
    params = ''
    if params_section:
        rows = params_section.find_all(class_='prop-row')
        if rows:
            params = '\n'.join(prop_row_to_md(r) for r in rows if r.get_text(strip=True))

    # Request body / models
    request_model = block.find(class_='swagger-request-model')
    request = ''
    if request_model:
        request = clean_md(tag_to_md(request_model))

    # Responses
    responses_section = block.find(class_='swagger-responses')
    responses = ''
    if responses_section:
        responses = clean_md(tag_to_md(responses_section))

    # Examples
    examples = block.find(class_='doc-examples')
    example_text = ''
    if examples:
        example_text = clean_md(tag_to_md(examples))

    heading = f'## {method} {path}'
    if summary:
        heading += f' — {summary}'

    parts = [heading]
    if desc:
        parts.append(desc)
    if params:
        parts.append('### Parameters\n\n' + params)
    if request:
        parts.append('### Request body\n\n' + request)
    if responses:
        parts.append('### Responses\n\n' + responses)
    if example_text:
        parts.append('### Examples\n\n' + example_text)
    return clean_md('\n\n'.join(parts))


def extract_definition(block):
    """Return markdown for a schema definition panel."""
    title = ''
    title_tag = block.find(class_='panel-title')
    if title_tag:
        title = title_tag.get_text(' ', strip=True)
    # Remove the trailing colon if present
    title = re.sub(r':\s*$', '', title)
    if not title:
        title = block.get('id', '').replace('definition-', '').replace('-', ' ').title()

    body = clean_md(tag_to_md(block))
    return f'## {title}\n\n{body}'


def main():
    with open(HTML_PATH, 'r', encoding='utf-8') as f:
        html_text = f.read()

    soup = BeautifulSoup(html_text, 'html.parser')

    os.makedirs(OUT_DIR, exist_ok=True)

    operations = []
    definitions = []

    for block in soup.find_all('div', class_='operation panel'):
        op_md = extract_operation(block)
        if op_md.strip():
            operations.append(op_md)

    for block in soup.find_all('div', class_='definition panel'):
        def_md = extract_definition(block)
        if def_md.strip():
            definitions.append(def_md)

    intro = extract_intro(soup)

    BRIDGE_NOTES = """# Bridge Quick Reference

> TL;DR for the OnLocation ↔ Gallagher bridge.

- **Entry point:** `GET /api` returns the root document with `features` links. Do not hard-code URLs; discover them from `features.*.href`.
- **Authentication:** HTTP Basic with the API key as the password. Header: `Authorization: Basic <base64(username:API_KEY)>`.
- **TLS:** The test server may present a self-signed certificate. Either trust the Gallagher root CA or configure your HTTP client to verify it. The web proxy in `proxy.py` already demonstrates one way to do this.
- **Pagination:** Search results are paginated via `next.href`. Follow the `next` link until it is absent. Use `sort=id` and `top=1000` for stable, efficient enumeration.
- **Field specifiers:** Add `fields=...` to search/detail requests to retrieve extra data (e.g. `fields=cards,accessGroups,competencies,personalDataFields`) or suppress default fields.
- **Key flows for a bridge:**
  - List/search cardholders: `GET /api/cardholders`
  - Get full cardholder: `GET {cardholder.href}` or `GET /api/cardholders/{id}`
  - Create: `POST {features.cardholders.cardholders.href}`
  - Update: `PATCH {cardholder.href}` or `PATCH /api/cardholders/{id}`
  - Delete: `DELETE {cardholder.href}`
  - Detect changes: `GET /api/cardholders/changes` (see change tracking)
- **Common sub-resources:** cards, access groups, competencies, operator groups, relationships (roles), lockers, elevator groups, PDFs.
- **Encoding:** UTF-8. Date PDF values are currently locale-formatted; treat them as strings.
- **Licences:** Most endpoints require `RESTCardholders`. Visitor endpoints also need `VisitorManagement`.

"""

    with open(OUT_PATH, 'w', encoding='utf-8') as out:
        out.write('# Gallagher Command Centre Cardholder REST API Skill\n\n')
        out.write('> Generated from `ref/Command Centre REST API_ Cardholders _ API Reference.html`.\n')
        out.write('> Use this reference when implementing a bridge/sync between MRI OnLocation and Gallagher.\n\n')
        out.write('---\n\n')
        out.write(BRIDGE_NOTES)
        out.write('---\n\n')
        out.write('# Introduction\n\n')
        out.write(intro)
        out.write('\n\n---\n\n')
        out.write(f'# Operations ({len(operations)})\n\n')
        for op in operations:
            out.write(op)
            out.write('\n\n---\n\n')
        out.write(f'# Schema Definitions ({len(definitions)})\n\n')
        for d in definitions:
            out.write(d)
            out.write('\n\n---\n\n')

    print(f'Wrote {len(operations)} operations and {len(definitions)} definitions to {OUT_PATH}')


if __name__ == '__main__':
    main()
