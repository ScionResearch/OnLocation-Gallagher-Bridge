---
name: onlocation-api
description: Use when integrating with the MRI OnLocation REST API for employees, contractors, visitors, assets, inductions, certifications, or related entities.
---

# MRI OnLocation REST API Skill

When you need full endpoint details, request/response schemas, parameter lists, auth details, or JSON/XML examples, read `onlocation-api.md` from the `references/` folder in this skill directory.

## Bridge Quick Reference

- **Base URL:** `https://api.whosonlocation.com/v1`
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
  - Custom fields: `GET /customfield` (for employees, contractor orgs, contractor members)
