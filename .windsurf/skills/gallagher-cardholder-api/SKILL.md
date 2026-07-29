---
name: gallagher-cardholder-api
description: Use when working with the Gallagher Command Centre REST API for cardholders, credentials, access groups, divisions, or related entities.
---

# Gallagher Command Centre Cardholder REST API Skill

When you need full endpoint details, request/response schemas, parameter lists, auth details, or JSON/XML examples, read `gallagher-cardholder-api.md` from the `references/` folder in this skill directory.

## Bridge Quick Reference

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
