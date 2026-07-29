#!/usr/bin/env python3
"""Package the generated API Markdown summaries as Windsurf/Devin skills.

Each skill is a folder under .windsurf/skills/<name>/ containing:
  - SKILL.md       : concise instructions + bridge quick reference
  - references/    : the full generated API reference

This keeps the skill body small (fast to load) while making the complete
operations, schemas, auth details, and examples available on demand.
"""
import os
import shutil

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SOURCE_DIR = os.path.join(ROOT, 'skills')
TARGET_ROOT = os.path.join(ROOT, '.windsurf', 'skills')

GALLAGHER_QUICK = """- **Entry point:** `GET /api` returns the root document with `features` links. Do not hard-code URLs; discover them from `features.*.href`.
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
- **Licences:** Most endpoints require `RESTCardholders`. Visitor endpoints also need `VisitorManagement`."""

ONLOCATION_QUICK = """- **Base URL:** `https://api.whosonlocation.com/v1`
- **Authentication:**
  - API key: `Authorization: APIKEY <api_key>` (legacy but supported).
  - Basic auth: `Authorization: Basic <base64(api_key:)>` (password is empty).
  - OAuth 2.0 client credentials (recommended): `POST https://login.whosonlocation.com/oauth2/token` with `grant_type=client_credentials` and scope `domain:read`/`domain:write`. Use returned `access_token` as `Authorization: Bearer <token>`.
- **Media types:** Default response is XML. Set `Accept: application/json` for JSON. Set `Content-Type: application/json` for request bodies.
- **Pagination:** Keyset/cursor pagination. Use `order=id&limit=10` then `q=id>last_id&order=id&limit=10`. Add `If-Modified-Since` header for deltas where supported.
- **Filtering:** `q` query parameter. Operators: `:` equals, `%` starts-with, `>` greater-than, `<` less-than, `~` wildcard (`%` for zero-or-more). Comma-separate for AND.
- **Time zones:** `time_zone` parameter (case-sensitive, e.g. `UTC`, `Pacific/Auckland`).
- **Rate limits:** 100 requests/min per credential. `429 Too Many Requests` with `Retry-After` header.
- **Key bridge endpoints:**
  - Employees: `GET /staff`, `POST /staff`, `PUT /staff/{id}`, `DELETE /staff/{id}`, `POST /staff/movement`
  - Contractors: `GET /sp/member`, `POST /sp/member`, `PUT /sp/member/{id}`, `DELETE /sp/member/{id}`
  - Visitor events: `GET /visitor/event`, `POST /visitor/event`, `PUT /visitor/event/{id}`
  - Pre-registered visitors: `GET /visitor/register`, `POST /visitor/register`
  - Inductions: `GET /induction`, `GET /induction/{id}/holder`, `POST /induction/{id}/holder`
  - Locations / departments / zones: `GET /location`, `GET /department`, `GET /zone`
  - Custom fields: `GET /customfield` (for employees, contractor orgs, contractor members)"""

SKILLS = [
    {
        'name': 'gallagher-cardholder-api',
        'title': 'Gallagher Command Centre Cardholder REST API Skill',
        'description': 'Use when working with the Gallagher Command Centre REST API for cardholders, credentials, access groups, divisions, or related entities.',
        'source': 'gallagher-cardholder-api.md',
        'quick': GALLAGHER_QUICK,
    },
    {
        'name': 'onlocation-api',
        'title': 'MRI OnLocation REST API Skill',
        'description': 'Use when integrating with the MRI OnLocation REST API for employees, contractors, visitors, assets, inductions, certifications, or related entities.',
        'source': 'onlocation-api.md',
        'quick': ONLOCATION_QUICK,
    },
]


def build_skill_md(skill):
    return f"""---
name: {skill['name']}
description: {skill['description']}
---

# {skill['title']}

When you need full endpoint details, request/response schemas, parameter lists, auth details, or JSON/XML examples, read `{skill['source']}` from the `references/` folder in this skill directory.

## Bridge Quick Reference

{skill['quick']}
"""


def main():
    os.makedirs(TARGET_ROOT, exist_ok=True)
    for skill in SKILLS:
        src = os.path.join(SOURCE_DIR, skill['source'])
        skill_dir = os.path.join(TARGET_ROOT, skill['name'])
        refs_dir = os.path.join(skill_dir, 'references')
        os.makedirs(refs_dir, exist_ok=True)

        # Copy the full generated reference into the skill's references folder
        ref_dest = os.path.join(refs_dir, skill['source'])
        shutil.copy2(src, ref_dest)

        # Write the compact SKILL.md
        skill_md_dest = os.path.join(skill_dir, 'SKILL.md')
        with open(skill_md_dest, 'w', encoding='utf-8') as f:
            f.write(build_skill_md(skill))

        print(f"Wrote {skill_md_dest}")
        print(f"  reference: {ref_dest} ({os.path.getsize(ref_dest):,} bytes)")


if __name__ == '__main__':
    main()
