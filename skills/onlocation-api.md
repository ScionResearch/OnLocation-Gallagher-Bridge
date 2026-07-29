# MRI OnLocation REST API Skill

> Generated from `ref/whosonlocation-openapi.json` (apidocs.whosonlocation.com).
> Use this reference when implementing a bridge/sync between MRI OnLocation and Gallagher.

---

# Bridge Quick Reference

> TL;DR for the Gallagher ↔ OnLocation bridge.

- **Base URL:** `https://api.whosonlocation.com/v1`
- **Authentication:**
  - API key: `Authorization: APIKEY <api_key>` (legacy but supported).
  - Basic auth: `Authorization: Basic <base64(api_key:)>` (password is empty).
  - OAuth 2.0 client credentials (recommended): `POST https://login.whosonlocation.com/oauth2/token` with `grant_type=client_credentials` and `scope=domain:read`/`domain:write`. Use returned `access_token` as `Authorization: Bearer <token>`.
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

---

# Introduction

Explore OnLocation's API reference documentation. References include:

- Certifications
- Custom questionnaires
- Custom fields
- Contractors
- Employees
- Inductions
- Locations
- Notifications
- Visitors

**Version:** 0.1

---

# Operations (190)

## Tag: Assets

Manage your organization’s assets with OnLocation. Use the API to retrieve a list of assets, add or update, or delete an asset, or issue or return an asset. Your account owner must first enable the add-on in OnLocation.

Learn more about assets in the [OnLocation Help Center](https://onlocation.elevio.help/en/articles/278).

## GET /asset — List assets

Returns a list of assets.

### Parameters
- **q** (query · string): Filter by any asset value. (example: `name:iPhone4`)
- **limit** (query · integer(int32)): Limits the amount of records to return (maximum 5000). (example: `10`)

### Responses

#### 200 — List of assets
- **Content-Type:** `application/json`
- **Schema:** `array of AssetResponse`

```json
[
  {
    "id": 1,
    "type_id": 1,
    "name": "Laptop 001",
    "tag": "TAG001",
    "reference": "REF001",
    "department_id": 1,
    "notes": "Notes about the asset",
    "geo_lat": "12.34",
    "geo_lon": "56.78",
    "owner_type": "org",
    "owner_staff_id": 1,
    "owner_sp_id": 1,
    "location_id": 1,
    "status": "in"
  }
]
```

- **Content-Type:** `application/xml`
- **Schema:** `array of AssetResponse`

#### 400 — Invalid Input

#### 403 — Access denied to this resource

---

## POST /asset — Create an asset

Creates a new asset with the provided details.

### Request Body
Information used to create a new asset record
**Required:** False

- **Content-Type:** `application/json`
- **Schema:** `AssetRequest`

```json
{
  "type_id": 1,
  "name": "Laptop 001",
  "tag": "TAG001",
  "reference": "REF001",
  "department_id": 1,
  "notes": "Notes about the asset",
  "geo_lat": "12.34",
  "geo_lon": "56.78",
  "owner_type": "org",
  "owner_staff_id": 1583,
  "owner_sp_id": 2700,
  "location_id": 1
}
```


### Responses

#### 201 — Created resource
- **Content-Type:** `application/json`
- **Schema:** `AssetResponse`

```json
{
  "id": 1,
  "type_id": 1,
  "name": "Laptop 001",
  "tag": "TAG001",
  "reference": "REF001",
  "department_id": 1,
  "notes": "Notes about the asset",
  "geo_lat": "12.34",
  "geo_lon": "56.78",
  "owner_type": "org",
  "owner_staff_id": 1,
  "owner_sp_id": 1,
  "location_id": 1,
  "status": "in"
}
```

- **Content-Type:** `application/xml`
- **Schema:** `AssetResponse`

#### 400 — Invalid input

#### 403 — Access denied to this resource

#### 500 — Internal server error

---

## GET /asset/{id} — Retrieve an asset

Retrieves an asset by its ID.

### Parameters
- **id** (path · integer · required): ID of the asset to retrieve

### Responses

#### 200 — Asset found
- **Content-Type:** `application/json`
- **Schema:** `AssetResponse`

```json
{
  "id": 1,
  "type_id": 1,
  "name": "Laptop 001",
  "tag": "TAG001",
  "reference": "REF001",
  "department_id": 1,
  "notes": "Notes about the asset",
  "geo_lat": "12.34",
  "geo_lon": "56.78",
  "owner_type": "org",
  "owner_staff_id": 1,
  "owner_sp_id": 1,
  "location_id": 1,
  "status": "in"
}
```

- **Content-Type:** `application/xml`
- **Schema:** `AssetResponse`

#### 403 — Access denied to this resource

#### 404 — Asset not found

#### 500 — Internal server error

---

## PUT /asset/{id} — Update an asset

Updates the details of an asset.

### Parameters
- **id** (path · integer(int32) · required): ID of the asset to update

### Request Body
Information used to update the asset record
**Required:** False

- **Content-Type:** `application/json`
- **Schema:** `AssetRequest`

```json
{
  "type_id": 1,
  "name": "Laptop 001",
  "tag": "TAG001",
  "reference": "REF001",
  "department_id": 1,
  "notes": "Notes about the asset",
  "geo_lat": "12.34",
  "geo_lon": "56.78",
  "owner_type": "org",
  "owner_staff_id": 1583,
  "owner_sp_id": 2700,
  "location_id": 1
}
```


### Responses

#### 200 — Updated resource
- **Content-Type:** `application/json`
- **Schema:** `AssetResponse`

```json
{
  "id": 1,
  "type_id": 1,
  "name": "Laptop 001",
  "tag": "TAG001",
  "reference": "REF001",
  "department_id": 1,
  "notes": "Notes about the asset",
  "geo_lat": "12.34",
  "geo_lon": "56.78",
  "owner_type": "org",
  "owner_staff_id": 1,
  "owner_sp_id": 1,
  "location_id": 1,
  "status": "in"
}
```

- **Content-Type:** `application/xml`
- **Schema:** `AssetResponse`

#### 400 — Invalid Input

#### 403 — Access denied to this resource

#### 404 — Resource not found

#### 500 — Internal server error

---

## DELETE /asset/{id} — Delete an asset

Deletes an asset by its ID.

### Parameters
- **id** (path · integer · required): ID of the asset to delete

### Responses

#### 204 — Asset deleted successfully

#### 400 — Invalid input

#### 403 — Access denied to this resource

#### 404 — Asset not found

#### 500 — Internal server error

---

## PUT /asset/{id}/issue — Issue an asset

Issue an asset with the provided details, this will create a new asset movement.

### Parameters
- **id** (path · integer(int32) · required): Asset ID

### Request Body
Information used to issue an asset
**Required:** False

- **Content-Type:** `application/json`
- **Schema:** `AssetmovementRequest`

```json
{
  "assigned_type": "staff",
  "assigned_staff_id": 1,
  "assigned_spm_id": 1,
  "assigned_visitor_id": 1,
  "dueback": "2023-05-01 13:10:00"
}
```


### Responses

#### 200 — Issued successfully
- **Content-Type:** `application/json`
- **Schema:** `AssetmovementResponse`

```json
{
  "asset_id": 1,
  "assigned_type": "staff",
  "assigned_staff_id": 1,
  "assigned_spm_id": 1,
  "assigned_visitor_id": 1,
  "signed_out": "2023-04-18 13:15:00",
  "signed_in": "2023-04-18 13:15:00",
  "dueback": "2023-05-01 13:15:07",
  "notes": "Notes about the asset movement",
  "issued_out": "10008",
  "issued_in": "20008"
}
```

- **Content-Type:** `application/xml`
- **Schema:** `AssetmovementResponse`

#### 400 — Invalid input

#### 403 — Access denied to this resource

#### 500 — Internal server error

---

## PUT /asset/{id}/return — Return an asset

Return an asset with the provided details.

### Parameters
- **id** (path · integer(int32) · required): Asset ID

### Responses

#### 200 — Returned successfully
- **Content-Type:** `application/json`
- **Schema:** `AssetmovementResponse`

```json
{
  "asset_id": 1,
  "assigned_type": "staff",
  "assigned_staff_id": 1,
  "assigned_spm_id": 1,
  "assigned_visitor_id": 1,
  "signed_out": "2023-04-18 13:15:00",
  "signed_in": "2023-04-18 13:15:00",
  "dueback": "2023-05-01 13:15:07",
  "notes": "Notes about the asset movement",
  "issued_out": "10008",
  "issued_in": "20008"
}
```

- **Content-Type:** `application/xml`
- **Schema:** `AssetmovementResponse`

#### 400 — Invalid input

#### 403 — Access denied to this resource

#### 500 — Internal server error

---

## Tag: Asset groups

All asset types are associated with an asset group. An asset group connects asset types that have similar characteristics. Use the API to retrieve a list of all asset groups, add or update, or delete an asset group.

Learn more about asset groups in the [OnLocation Help Center](https://onlocation.elevio.help/en/articles/288).

## GET /assetgroup — List asset groups

Returns a list of asset groups.

### Parameters
- **q** (query · string): Filter by any asset group value. (example: `name:desks,class:fixed,description:anydescriptions`)
- **limit** (query · integer(int32)): Limits the amount of records to return (maxiumum 5000). (example: `10`)

### Responses

#### 200 — List of asset groups
- **Content-Type:** `application/json`
- **Schema:** `array of AssetgroupResponse`

```json
[
  {
    "id": 90008,
    "name": "Laptops Group",
    "class": "fixed",
    "description": "This group includes the assets that belong to this location"
  }
]
```

- **Content-Type:** `application/xml`
- **Schema:** `array of AssetgroupResponse`

#### 403 — Access denied to this resource

#### 404 — Resource not found

---

## POST /assetgroup — Create an asset group

Creates a new asset group that can be assigned to an asset.

### Request Body
Information to use when creating a new asset group record.
**Required:** False

- **Content-Type:** `application/json`
- **Schema:** `AssetgroupRequest`

```json
{
  "name": "Laptops Group",
  "class": "fixed",
  "description": "This group includes the assets that belong to this location"
}
```


### Responses

#### 201 — Asset Group created
- **Content-Type:** `application/json`
- **Schema:** `AssetgroupResponse`

```json
{
  "id": 90008,
  "name": "Laptops Group",
  "class": "fixed",
  "description": "This group includes the assets that belong to this location"
}
```

#### 403 — Access denied to this resource

#### 500 — Internal server error

---

## GET /assetgroup/{id} — Retrieve an asset group

Retrieves a single asset group, filtering with the provided ID.

### Parameters
- **id** (path · integer(int32) · required): ID of the asset group to retrieve

### Responses

#### 200
- **Content-Type:** `application/json`
- **Schema:** `AssetgroupResponse`

```json
{
  "id": 90008,
  "name": "Laptops Group",
  "class": "fixed",
  "description": "This group includes the assets that belong to this location"
}
```

- **Content-Type:** `application/xml`
- **Schema:** `AssetgroupResponse`

#### 403 — Access denied to resource

#### 404 — Resource not found

#### 500 — Internal server error

---

## PUT /assetgroup/{id} — Update an asset group

Updates the details of an asset group.

### Parameters
- **id** (path · integer(int32) · required): ID of the asset group to update

### Request Body
Information used to update the asset group record
**Required:** True

- **Content-Type:** `application/json`
- **Schema:** `AssetgroupRequest`

```json
{
  "name": "Laptops Group",
  "class": "fixed",
  "description": "This group includes the assets that belong to this location"
}
```


### Responses

#### 200 — Updated resource
- **Content-Type:** `application/json`
- **Schema:** `AssetgroupResponse`

```json
{
  "id": 90008,
  "name": "Laptops Group",
  "class": "fixed",
  "description": "This group includes the assets that belong to this location"
}
```

- **Content-Type:** `application/xml`
- **Schema:** `AssetgroupResponse`

#### 403 — Access denied to this resource

#### 500 — Internal server error

---

## DELETE /assetgroup/{id} — Delete an asset group

Deletes an asset group using the provided ID.

### Parameters
- **id** (path · integer(int32) · required): ID of the asset group to delete

### Responses

#### 204 — Asset Group deleted

#### 403 — Access denied to resource

#### 404 — Resource not found

#### 500 — Internal server error

---

## Tag: Asset movements

Manage the movement of assets using the API. Retrieve a list of all asset movements, view a specific movement, and update an movement.

Learn more about assets in the [OnLocation Help Center](https://onlocation.elevio.help/en/articles/357).

## GET /assetmovement — List asset movements

Returns a list of asset movements.

### Parameters
- **q** (query · string): Filter by any asset movement value. (example: `asset_id:1,assigned_staff_id:2,assigned_type:staff`)
- **limit** (query · integer(int32)): Limits the amount of records to return (maxiumum 5000). (example: `10`)

### Responses

#### 200 — List of asset movements
- **Content-Type:** `application/json`
- **Schema:** `array of AssetmovementResponse`

```json
[
  {
    "asset_id": 1,
    "assigned_type": "staff",
    "assigned_staff_id": 1,
    "assigned_spm_id": 1,
    "assigned_visitor_id": 1,
    "signed_out": "2023-04-18 13:15:00",
    "signed_in": "2023-04-18 13:15:00",
    "dueback": "2023-05-01 13:15:07",
    "notes": "Notes about the asset movement",
    "issued_out": "10008",
    "issued_in": "20008"
  }
]
```

- **Content-Type:** `application/xml`
- **Schema:** `array of AssetmovementResponse`

#### 400 — Invalid Input

#### 403 — Access denied to this resource

#### 404 — Resource not found

---

## GET /assetmovement/{id} — Get asset movement by ID

Returns a single asset movement.

### Parameters
- **id** (path · integer(int32) · required): ID of the asset movement to return

### Responses

#### 200 — Asset movement data
- **Content-Type:** `application/json`
- **Schema:** `AssetmovementResponse`

```json
{
  "asset_id": 1,
  "assigned_type": "staff",
  "assigned_staff_id": 1,
  "assigned_spm_id": 1,
  "assigned_visitor_id": 1,
  "signed_out": "2023-04-18 13:15:00",
  "signed_in": "2023-04-18 13:15:00",
  "dueback": "2023-05-01 13:15:07",
  "notes": "Notes about the asset movement",
  "issued_out": "10008",
  "issued_in": "20008"
}
```

- **Content-Type:** `application/xml`
- **Schema:** `AssetmovementResponse`

#### 403 — Access denied to this resource

#### 404 — Asset movement not found

---

## PUT /assetmovement/{id} — Update an asset movement

Updates the details of an asset movement.

### Parameters
- **id** (path · integer(int32) · required): ID of the asset movement to update

### Request Body
Information used to update the asset movement record
**Required:** True

- **Content-Type:** `application/json`
- **Schema:** `AssetmovementRequest`

```json
{
  "assigned_type": "staff",
  "assigned_staff_id": 1,
  "assigned_spm_id": 1,
  "assigned_visitor_id": 1,
  "dueback": "2023-05-01 13:10:00"
}
```


### Responses

#### 200 — Updated resource
- **Content-Type:** `application/json`
- **Schema:** `AssetmovementResponse`

```json
{
  "asset_id": 1,
  "assigned_type": "staff",
  "assigned_staff_id": 1,
  "assigned_spm_id": 1,
  "assigned_visitor_id": 1,
  "signed_out": "2023-04-18 13:15:00",
  "signed_in": "2023-04-18 13:15:00",
  "dueback": "2023-05-01 13:15:07",
  "notes": "Notes about the asset movement",
  "issued_out": "10008",
  "issued_in": "20008"
}
```

- **Content-Type:** `application/xml`
- **Schema:** `AssetmovementResponse`

#### 400 — Invalid Input

#### 403 — Access denied to this resource

#### 404 — Resource not found

#### 500 — Internal server error

---

## Tag: Asset types

An asset type is a subset of an asset group. Use the API to retrieve a list of all asset types, add or update, or delete an asset type.

Learn more about assets in the [OnLocation Help Center](https://onlocation.elevio.help/en/articles/321).

## GET /assettype — List asset types

Returns a list of asset types.

### Parameters
- **q** (query · string): Filter by any asset type value. (example: `name:macbooks,description:anydescriptions`)
- **limit** (query · integer(int32)): Limits the amount of records to return (maxiumum 5000). (example: `10`)

### Responses

#### 200 — List of asset types
- **Content-Type:** `application/json`
- **Schema:** `array of AssettypeResponse`

```json
[
  {
    "id": "10008",
    "name": "Laptop",
    "group_id": "20000",
    "description": "A portable device type"
  }
]
```

- **Content-Type:** `application/xml`
- **Schema:** `array of AssettypeResponse`

#### 403 — Access denied to this resource

#### 404 — Resource not found

---

## POST /assettype — Create an asset type

Creates a new asset type that can be assigned to an asset.

### Request Body
Information to use when creating a new asset type record.
**Required:** False

- **Content-Type:** `application/json`
- **Schema:** `AssettypeRequest`

```json
{
  "name": "Macbooks",
  "group_id": "10008",
  "description": "A portable device type"
}
```


### Responses

#### 201 — Asset Type created
- **Content-Type:** `application/json`
- **Schema:** `AssettypeResponse`

```json
{
  "id": "10008",
  "name": "Laptop",
  "group_id": "20000",
  "description": "A portable device type"
}
```

#### 403 — Access denied to this resource

#### 500 — Internal server error

---

## GET /assettype/{id} — Retrieve an asset type

Retrieves a single asset type, filtering with the provided ID.

### Parameters
- **id** (path · integer(int32) · required): ID of the asset type to retrieve

### Responses

#### 200
- **Content-Type:** `application/json`
- **Schema:** `AssettypeResponse`

```json
{
  "id": "10008",
  "name": "Laptop",
  "group_id": "20000",
  "description": "A portable device type"
}
```

- **Content-Type:** `application/xml`
- **Schema:** `AssettypeResponse`

#### 403 — Access denied to resource

#### 404 — Resource not found

#### 500 — Internal server error

---

## PUT /assettype/{id} — Update an asset type

Updates the details of an asset type.

### Parameters
- **id** (path · integer(int32) · required): ID of the asset type to update

### Request Body
Information used to update the asset type record
**Required:** True

- **Content-Type:** `application/json`
- **Schema:** `AssettypeRequest`

```json
{
  "name": "Macbooks",
  "group_id": "10008",
  "description": "A portable device type"
}
```


### Responses

#### 200 — Updated resource
- **Content-Type:** `application/json`
- **Schema:** `AssettypeResponse`

```json
{
  "id": "10008",
  "name": "Laptop",
  "group_id": "20000",
  "description": "A portable device type"
}
```

- **Content-Type:** `application/xml`
- **Schema:** `AssettypeResponse`

#### 403 — Access denied to this resource

#### 404 — Resource not found

#### 500 — Internal server error

---

## DELETE /assettype/{id} — Delete an asset type

Deletes an asset type using the provided ID.

### Parameters
- **id** (path · integer(int32) · required): ID of the asset type to delete

### Responses

#### 204 — Asset Type deleted

#### 403 — Access denied to resource

#### 404 — Resource not found

#### 500 — Internal server error

---

## Tag: Audit log

When records are created, updated or deleted within your account, an audit log is created. Use the audit log API to retrieve the last 30 days worth of data corresponding to changes within your account.

## GET /audit — List audit logs

Returns a list of audit logs.

### Parameters
- **q** (query · string): Filter by any audit log value. (example: `audit_table:staff;sp_member,user_name:john@example.com,audit_timestamp>2022-05-01`)
- **limit** (query · integer(int32)): Limits the amount of records to return (maxiumum 5000). (example: `10`)

### Responses

#### 200 — List of audit logs
- **Content-Type:** `application/json`
- **Schema:** `array of AuditResponse`

```json
[
  {
    "id": 823431,
    "audit_timestamp": "2021-08-12T10:54:13+12:00",
    "audit_event": "UPDATE",
    "audit_table": "staff",
    "user_name": "john@example.com",
    "user_type": "staff",
    "record_id": 823431,
    "data_changed": [
      {
        "column_name": "name",
        "change_from": "John Smith",
        "change_to": "John B Smith"
      },
      {
        "column_name": "from",
        "change_from": "Smith Corp",
        "change_to": "Smith Corporation"
      }
    ],
    "data_old": {
      "id": 588,
      "from": "Smith Corp",
      "name": "John Smith",
      "created": "2022-05-06 00:23:48.000000",
      "modified": null
    },
    "data_new": {
      "id": 588,
      "from": "Smith Corporation",
      "name": "John B Smith",
      "created": "2022-05-06 00:23:48.000000",
      "modified": "2022-05-07 10:33:19.000000"
    }
  }
]
```

- **Content-Type:** `application/xml`
- **Schema:** `array of AuditResponse`

#### 403 — Access denied to this resource

#### 404 — Resource not found

---

## GET /audit/settings — Retrieve available audit settings

Retrieves an audit settings object.

### Responses

#### 200 — Audit settings

#### 403 — Access denied to resource

#### 404 — Resource not found

#### 500 — Internal server error

---

## Tag: Certifications

Certifications are used to record employee and contractor accreditations in OnLocation. Use the API to retrieve the certifications created for your organization, add or update, or delete a certification, and see the number of people who hold the certification.

Learn more about certifications in the [OnLocation Help Center](https://onlocation.elevio.help/en/articles/419).

## GET /certification — List all certifications

Returns a list of all certifications.

A comma separated list of tag:value search parameters, a colon separator performs an exact match, a percent separator performs a starts-with search and greater/less than search number/date ranges.

### Parameters
- **q** (query · string): Filter by any certification value. (example: `holders>5`)

### Responses

#### 200 — List of certifications
- **Content-Type:** `application/json`
- **Schema:** `array of CertificationResponse`

```json
[
  {
    "id": 1206,
    "created": "2021-11-26T14:35:28+13:00",
    "modified": "2021-12-31T14:35:28+13:00",
    "created_by": 35,
    "name": "Certification Name",
    "category": "internal",
    "certification_type_id": 81,
    "description": "This describes the certification",
    "holders": 2,
    "status": "Active",
    "staff_audience": "locations",
    "contractor_audience": "groups",
    "staff_departments": [
      {
        "id": 556,
        "name": "Example Name"
      }
    ],
    "staff_locations": [
      {
        "id": 556,
        "name": "Example Name"
      }
    ],
    "staff_roles": [
      {
        "id": 556,
        "name": "Example Name"
      }
    ],
    "sp_groups": [
      {
        "id": 556,
        "name": "Example Name"
      }
    ],
    "sp_locations": [
      {
        "id": 556,
        "name": "Example Name"
      }
    ],
    "sp_roles": [
      {
        "id": 556,
        "name": "Example Name"
      }
    ]
  }
]
```

- **Content-Type:** `application/xml`
- **Schema:** `array of CertificationResponse`

#### 403 — Access denied to resource

#### 404 — Resource not found

#### 500 — Internal server error

---

## POST /certification — Create a certification

Creates a new certification that can be assigned to employees or contractors.

### Request Body
Information to use when creating a new certification record.
**Required:** True

- **Content-Type:** `application/json`
- **Schema:** `CertificationRequest`

```json
{
  "name": "Updated Request",
  "category": "external",
  "certification_type_id": 81,
  "description": "This describes the certification"
}
```


### Responses

#### 201 — Certification created
- **Content-Type:** `application/json`
- **Schema:** `CertificationResponse`

```json
{
  "id": 1206,
  "created": "2021-11-26T14:35:28+13:00",
  "modified": "2021-12-31T14:35:28+13:00",
  "created_by": 35,
  "name": "Certification Name",
  "category": "internal",
  "certification_type_id": 81,
  "description": "This describes the certification",
  "holders": 2,
  "status": "Active",
  "staff_audience": "locations",
  "contractor_audience": "groups",
  "staff_departments": [
    {
      "id": 556,
      "name": "Example Name"
    }
  ],
  "staff_locations": [
    {
      "id": 556,
      "name": "Example Name"
    }
  ],
  "staff_roles": [
    {
      "id": 556,
      "name": "Example Name"
    }
  ],
  "sp_groups": [
    {
      "id": 556,
      "name": "Example Name"
    }
  ],
  "sp_locations": [
    {
      "id": 556,
      "name": "Example Name"
    }
  ],
  "sp_roles": [
    {
      "id": 556,
      "name": "Example Name"
    }
  ]
}
```

#### 403 — Access denied to this resource

#### 409 — Already exists with that name

#### 500 — Internal server error

---

## GET /certification/{id} — Retrieve a certification

Retrieves a single certification, filtering with the provided ID.

### Parameters
- **id** (path · integer(int32) · required): ID of the certification to retrieve

### Responses

#### 200
- **Content-Type:** `application/json`
- **Schema:** `CertificationResponse`

```json
{
  "id": 1206,
  "created": "2021-11-26T14:35:28+13:00",
  "modified": "2021-12-31T14:35:28+13:00",
  "created_by": 35,
  "name": "Certification Name",
  "category": "internal",
  "certification_type_id": 81,
  "description": "This describes the certification",
  "holders": 2,
  "status": "Active",
  "staff_audience": "locations",
  "contractor_audience": "groups",
  "staff_departments": [
    {
      "id": 556,
      "name": "Example Name"
    }
  ],
  "staff_locations": [
    {
      "id": 556,
      "name": "Example Name"
    }
  ],
  "staff_roles": [
    {
      "id": 556,
      "name": "Example Name"
    }
  ],
  "sp_groups": [
    {
      "id": 556,
      "name": "Example Name"
    }
  ],
  "sp_locations": [
    {
      "id": 556,
      "name": "Example Name"
    }
  ],
  "sp_roles": [
    {
      "id": 556,
      "name": "Example Name"
    }
  ]
}
```

- **Content-Type:** `application/xml`
- **Schema:** `CertificationResponse`

#### 403 — Access denied to resource

#### 404 — Resource not found

#### 500 — Internal server error

---

## PUT /certification/{id} — Update a certification

Updates the details of a certification.

### Parameters
- **id** (path · integer(int32) · required): ID of the certification to update

### Request Body
Information used to create the certification record
**Required:** True

- **Content-Type:** `application/json`
- **Schema:** `CertificationRequest`

```json
{
  "name": "Updated Request",
  "category": "external",
  "certification_type_id": 81,
  "description": "This describes the certification"
}
```


### Responses

#### 200 — Updated resource
- **Content-Type:** `application/json`
- **Schema:** `CertificationResponse`

```json
{
  "id": 1206,
  "created": "2021-11-26T14:35:28+13:00",
  "modified": "2021-12-31T14:35:28+13:00",
  "created_by": 35,
  "name": "Certification Name",
  "category": "internal",
  "certification_type_id": 81,
  "description": "This describes the certification",
  "holders": 2,
  "status": "Active",
  "staff_audience": "locations",
  "contractor_audience": "groups",
  "staff_departments": [
    {
      "id": 556,
      "name": "Example Name"
    }
  ],
  "staff_locations": [
    {
      "id": 556,
      "name": "Example Name"
    }
  ],
  "staff_roles": [
    {
      "id": 556,
      "name": "Example Name"
    }
  ],
  "sp_groups": [
    {
      "id": 556,
      "name": "Example Name"
    }
  ],
  "sp_locations": [
    {
      "id": 556,
      "name": "Example Name"
    }
  ],
  "sp_roles": [
    {
      "id": 556,
      "name": "Example Name"
    }
  ]
}
```

- **Content-Type:** `application/xml`
- **Schema:** `CertificationResponse`

#### 403 — Access denied to this resource

#### 409 — Certification already exists with that name

#### 500 — Internal server error

---

## DELETE /certification/{id} — Delete a certification

Deletes a certification using the provided ID.

### Parameters
- **id** (path · integer(int32) · required): ID of the certification to delete

### Responses

#### 204 — Certification deleted

#### 403 — Access denied to resource

#### 404 — Resource not found

#### 500 — Internal server error

---

## Tag: Certification holders

Certifications can be assigned to employees and contractors. Each certification record includes the dates that the certification is valid for, along with the person's name, email, and if there are any documents attached to the record. Use the API to see which employees and contractors hold each certification, or create, update or delete a certification holder record.

Learn more about certification holders in the [OnLocation Help Center](https://onlocation.elevio.help/en/articles/409).

## GET /certification/{id}/holder — List all certification holders

Returns a list of the employees or contractors that hold the specified certification.

A comma separated list of tag:value search parameters, a colon separator performs an exact match, a percent separator performs a starts-with search and greater/less than search number/date ranges.

### Parameters
- **id** (path · integer(int32) · required): ID of the certification to retrieve holders for (example: `5`)

### Responses

#### 200 — List of employees or contractors that hold the certification
- **Content-Type:** `application/json`
- **Schema:** `array of CertificationHolderResponse`

```json
[
  {
    "id": 218,
    "created": "2021-11-26T14:37:12+13:00",
    "modified": "2021-11-30T14:37:12+13:00",
    "certification_id": 1205,
    "record_id": 823431,
    "record_type": "staff",
    "validfrom": "2021-11-27",
    "validto": "2022-11-27",
    "certification_number": "WFL900",
    "holder_name": "Robert Jordan",
    "holder_email": "example-email@whosonlocation.com",
    "certification_name": "Window Fitting Licence",
    "type": "Licence",
    "documents": 1
  }
]
```

- **Content-Type:** `application/xml`
- **Schema:** `array of CertificationHolderResponse`

#### 403 — Access denied to resource

#### 404 — Resource not found

#### 500 — Internal server error

---

## POST /certification/{id}/holder — Create a certification holder

Adds a new certification holder association.

### Parameters
- **id** (path · integer(int32) · required): ID of the certification to create a holder association for

### Request Body
**Required:** True

- **Content-Type:** `application/json`
- **Schema:** `CertificationHolderRequest`

```json
{
  "record_id": 823431,
  "record_type": "staff",
  "validfrom": "2021-11-27",
  "validto": "2022-11-27",
  "certification_number": "WFL900"
}
```


### Responses

#### 201 — Resource created
- **Content-Type:** `application/json`
- **Schema:** `CertificationHolderResponse`

```json
{
  "id": 218,
  "created": "2021-11-26T14:37:12+13:00",
  "modified": "2021-11-30T14:37:12+13:00",
  "certification_id": 1205,
  "record_id": 823431,
  "record_type": "staff",
  "validfrom": "2021-11-27",
  "validto": "2022-11-27",
  "certification_number": "WFL900",
  "holder_name": "Robert Jordan",
  "holder_email": "example-email@whosonlocation.com",
  "certification_name": "Window Fitting Licence",
  "type": "Licence",
  "documents": 1
}
```

#### 403 — Access denied to this resource

#### 500 — Internal server error

---

## GET /certification/{id}/holder/{hid} — Retrieve a certification holder

Retrieves the details of a specific certification holder

### Parameters
- **id** (path · integer(int32) · required): ID of the certification to retrieve holders for
- **hid** (path · integer(int32) · required): ID of the certification holder to retrieve

### Responses

#### 200 — Details of the certification holder
- **Content-Type:** `application/json`
- **Schema:** `CertificationHolderResponse`

```json
{
  "id": 218,
  "created": "2021-11-26T14:37:12+13:00",
  "modified": "2021-11-30T14:37:12+13:00",
  "certification_id": 1205,
  "record_id": 823431,
  "record_type": "staff",
  "validfrom": "2021-11-27",
  "validto": "2022-11-27",
  "certification_number": "WFL900",
  "holder_name": "Robert Jordan",
  "holder_email": "example-email@whosonlocation.com",
  "certification_name": "Window Fitting Licence",
  "type": "Licence",
  "documents": 1
}
```

- **Content-Type:** `application/xml`
- **Schema:** `CertificationHolderResponse`

#### 403 — Access denied to resource

#### 404 — Resource not found

#### 500 — Internal server error

---

## PUT /certification/{id}/holder/{hid} — Update a certification holder

Updates a certification holder's details.

### Parameters
- **id** (path · integer(int32) · required): ID of the certification
- **hid** (path · integer(int32) · required): ID of the certification holder association to update

### Request Body
Information used to create the certification holder association
**Required:** True

- **Content-Type:** `application/json`
- **Schema:** `CertificationHolderRequest`

```json
{
  "record_id": 823431,
  "record_type": "staff",
  "validfrom": "2021-11-27",
  "validto": "2022-11-27",
  "certification_number": "WFL900"
}
```


### Responses

#### 200 — Updated resource
- **Content-Type:** `application/json`
- **Schema:** `CertificationHolderResponse`

```json
{
  "id": 218,
  "created": "2021-11-26T14:37:12+13:00",
  "modified": "2021-11-30T14:37:12+13:00",
  "certification_id": 1205,
  "record_id": 823431,
  "record_type": "staff",
  "validfrom": "2021-11-27",
  "validto": "2022-11-27",
  "certification_number": "WFL900",
  "holder_name": "Robert Jordan",
  "holder_email": "example-email@whosonlocation.com",
  "certification_name": "Window Fitting Licence",
  "type": "Licence",
  "documents": 1
}
```

- **Content-Type:** `application/xml`
- **Schema:** `CertificationHolderResponse`

#### 403 — Access denied to this resource

#### 500 — Internal server error

---

## DELETE /certification/{id}/holder/{hid} — Delete a certification holder

Deletes the specified certification holder association record.

### Parameters
- **id** (path · integer(int32) · required): ID of the certification
- **hid** (path · integer(int32) · required): ID of the certification holder association to delete

### Responses

#### 204 — Certification holder deleted

#### 403 — Access denied to resource

#### 404 — Resource not found

#### 500 — Internal server error

---

## Tag: Certification holder documents

Each certification added to an employee or contractor profile can have a document attached. Use the API to return a list of certification documents, and update or delete a certification document.

Learn more about certification documents in the [OnLocation Help Center](https://onlocation.elevio.help/en/articles/407).

## GET /certification/{id}/holder/{hid}/document — List of certification documents

Retrieves a list of documents for a specified certification holder and specified certification

### Parameters
- **id** (path · integer(int32) · required): ID of the certification to retrieve holders for
- **hid** (path · integer(int32) · required): ID of the certification holder to retrieve

### Responses

#### 200 — List of certification holder documents
- **Content-Type:** `application/json`
- **Schema:** `array of DocumentResponse`

```json
[
  {
    "id": 70,
    "modified": "2021-11-29T08:12:24+13:00",
    "name": "file-example",
    "type": "pdf",
    "size": 469513,
    "link": "https://your.site.com/storage/file-example.pdf"
  }
]
```

- **Content-Type:** `application/xml`
- **Schema:** `array of DocumentResponse`

#### 403 — Access denied to resource

#### 404 — Resource not found

#### 500 — Internal server error

---

## POST /certification/{id}/holder/{hid}/document — Adds a certification document

Adds a new certification holder document.

### Parameters
- **id** (path · integer(int32) · required): ID of the certification
- **hid** (path · integer(int32) · required): ID of the certification holder

### Request Body
**Required:** True

- **Content-Type:** `multipart/form-data`
- **Schema:** `object`

### Responses

#### 200 — Document added

#### 403 — Access denied to this resource

#### 500 — Internal server error

---

## GET /certification/{id}/holder/{hid}/document/{did} — Retrieve a certification document

Retrieves a certification holder document, filtering with the provided ID

### Parameters
- **id** (path · integer(int32) · required): ID of the certification to retrieve holders for
- **hid** (path · integer(int32) · required): ID of the certification holder to retrieve
- **did** (path · integer(int32) · required): ID of the document to retrieve

### Responses

#### 200 — The specified document to be retrieved
- **Content-Type:** `application/json`
- **Schema:** `DocumentResponse`

```json
{
  "id": 70,
  "modified": "2021-11-29T08:12:24+13:00",
  "name": "file-example",
  "type": "pdf",
  "size": 469513,
  "link": "https://your.site.com/storage/file-example.pdf"
}
```

- **Content-Type:** `application/xml`
- **Schema:** `DocumentResponse`

#### 403 — Access denied to resource.

#### 404 — Resource not found

#### 500 — Internal server error

---

## DELETE /certification/{id}/holder/{hid}/document/{did} — Delete a certification document

Deletes the specified certification holder document.

### Parameters
- **id** (path · integer(int32) · required): ID of the certification
- **hid** (path · integer(int32) · required): ID of the certification holder association
- **did** (path · integer(int32) · required): ID of the document to delete

### Responses

#### 204 — Resource Deleted

#### 403 — Access denied to resource

#### 404 — Resource not found

#### 500 — Internal server error

---

## Tag: Certification types

Certification types are used to group similar types of certifications, for example qualifications, certificates, licenses, degrees. Use the API to return the full list of certification types, or add, update or delete a certification type.

Learn more about certification types in the [OnLocation Help Center](https://onlocation.elevio.help/en/articles/407).

## GET /certification/types — List all certification types

Retrieves a list of all certification types.

A comma separated list of tag:value search parameters, a colon separator performs an exact match, a percent separator performs a starts-with search and greater/less than search number/date ranges.

### Responses

#### 200 — List of certification types
- **Content-Type:** `application/json`
- **Schema:** `array of CertificationTypeResponse`

```json
[
  {
    "id": 79,
    "created": "2021-11-26T14:33:41+13:00",
    "modified": "2022-11-26T14:33:41+13:00",
    "name": "Certificate"
  }
]
```

- **Content-Type:** `application/xml`
- **Schema:** `array of CertificationTypeResponse`

#### 403 — Access denied to this resource

#### 404 — Resource not found

#### 500 — Internal server error

---

## POST /certification/types — Create a certification type

Adds a new certification type to assign to a certification.

### Request Body
Type of certification to add:
**Required:** True

- **Content-Type:** `application/json`
- **Schema:** `CertificationTypeRequest`

```json
{
  "name": "Diploma"
}
```


### Responses

#### 201 — Certification type created
- **Content-Type:** `application/json`
- **Schema:** `CertificationTypeResponse`

```json
{
  "id": 79,
  "created": "2021-11-26T14:33:41+13:00",
  "modified": "2022-11-26T14:33:41+13:00",
  "name": "Certificate"
}
```

- **Content-Type:** `application/xml`
- **Schema:** `CertificationTypeResponse`

#### 403 — Access denied to this resource

#### 409 — Type already exists

#### 500 — Internal server error

---

## GET /certification/types/{id} — Retrieve a certification type

Retrieves a single certification type, filtering with the provided ID.

### Parameters
- **id** (path · integer(int32) · required): ID of the certification type to retrieve

### Responses

#### 200 — Certification type response
- **Content-Type:** `application/json`
- **Schema:** `CertificationTypeResponse`

```json
{
  "id": 79,
  "created": "2021-11-26T14:33:41+13:00",
  "modified": "2022-11-26T14:33:41+13:00",
  "name": "Certificate"
}
```

- **Content-Type:** `application/xml`
- **Schema:** `CertificationTypeResponse`

#### 403 — Access denied to resource

#### 404 — Resource not found

#### 500 — Internal server error

---

## PUT /certification/types/{id} — Update a certification type

Updates a single certification type.

### Parameters
- **id** (path · integer(int32) · required): ID of the certification type to update

### Request Body
Information used to create the certification type record
**Required:** True

- **Content-Type:** `application/json`
- **Schema:** `CertificationTypeRequest`

```json
{
  "name": "Diploma"
}
```


### Responses

#### 200 — Updated certification type
- **Content-Type:** `application/json`
- **Schema:** `CertificationTypeResponse`

```json
{
  "id": 79,
  "created": "2021-11-26T14:33:41+13:00",
  "modified": "2022-11-26T14:33:41+13:00",
  "name": "Certificate"
}
```

- **Content-Type:** `application/xml`
- **Schema:** `CertificationTypeResponse`

#### 403 — Access denied to this resource

#### 500 — Internal server error

---

## DELETE /certification/types/{id} — Delete a certification type

Deletes the specified certification type.

### Parameters
- **id** (path · integer(int32) · required): ID of the Certification Type to delete

### Responses

#### 204 — Certification type deleted

#### 403 — Access denied to resource

#### 404 — Resource not found

#### 500 — Internal server error

---

## Tag: Contractor contact role types

Contractor contact role types are used to categorize contractor members. Use the API to retrieve a list of role types set up at your organization.

Learn more about role types in the [OnLocation Help Center](https://onlocation.elevio.help/en/articles/481).

## GET /sp/role — List all contact role types

Returns a list of all of the contractor contact roles created for your organization.A comma separated list of tag:value search parameters, a colon separator performs an exact match and a percent separator performs a starts-with search.

### Parameters
- **q** (query · string): Filter by any contractor role value (example: `name:ACME`)
- **order** (query · string): Specifies an element to order by. Order by any element returned, prefix with a - to reverse order. (example: `id`)
- **limit** (query · integer(int32)): Limits the number of records to return (example: `10`)
- **If-Modified-Since** (header · string(int32)): A UTC timestamp (yyyy-mm-ddThh:mm:ss) . Only entities created or modified since this timestamp will be returned e.g. 2009-11-12T00:00:00

### Responses

#### 200 — List of contractor roles
- **Content-Type:** `application/json`
- **Schema:** `array of ContractorRoleResponse`

```json
[
  {
    "role": [
      {
        "id": 556,
        "name": "Example Name"
      }
    ]
  }
]
```

- **Content-Type:** `application/xml`
- **Schema:** `array of ContractorRoleResponse`

#### 403 — Access denied to this resource

#### 404 — Resource not found

#### 500 — Internal server error

---

## Tag: Contractor insurance policies

Insurance management in OnLocation is used to manage contractor organizations' insurance policies. Use the API to retrieve a list of all insurance policies, add new ones, and update or delete existing policies.

Learn more about insurances in the [OnLocation Help Center](https://onlocation.elevio.help/en/articles/440).

## GET /sp/insurance — List all insurance policies

Returns a list of all contractor insurance policy records.

A comma separated list of tag:value search parameters, a colon separator performs an exact match and a percent separator performs a starts-with search.

### Parameters
- **q** (query · string): Filter by any contractor insurance value (example: `name:ACME`)
- **order** (query · string): Specifies an element to order by. Order by any element returned, prefix with a - to reverse order. (example: `-org_id`)
- **limit** (query · integer(int32)): Limits the amount of records to return (example: `10`)
- **If-Modified-Since** (header · string(int32)): A UTC timestamp (yyyy-mm-ddThh:mm:ss). Only entities created or modified since this timestamp will be returned e.g. 2009-11-12T00:00:00

### Responses

#### 200 — List of contractor insurance records
- **Content-Type:** `application/json`
- **Schema:** `array of ContractorInsuranceResponse`

```json
[
  {
    "id": 140,
    "created": "2021-11-29T08:10:09+13:00",
    "modified": "2022-11-29T08:10:09+13:00",
    "org_id": 4587,
    "org_name": "Example Organisation Name",
    "type": "other",
    "name": "Policy Underwriter",
    "reference": "Ref002",
    "value": "6000",
    "start": "2021-11-30",
    "expires": "2022-11-29",
    "status": "Active",
    "tags": [
      [
        "asset",
        "insurance",
        "other"
      ]
    ],
    "documents": [
      {
        "id": 70,
        "modified": "2021-11-29T08:12:24+13:00",
        "name": "file-example",
        "type": "pdf",
        "size": 469513,
        "link": "https://your.site.com/storage/file-example.pdf"
      }
    ]
  }
]
```

- **Content-Type:** `application/xml`
- **Schema:** `array of ContractorInsuranceResponse`

#### 403 — Access denied to this resource

#### 404 — Resource not found

#### 500 — Internal server error

---

## POST /sp/insurance — Create an insurance policy

Add a new contractor insurance policy.

### Request Body
The contractor insurance record to add to an organization:
**Required:** True

- **Content-Type:** `application/json`
- **Schema:** `ContractorInsuranceRequest`

```json
{
  "org_id": 4587,
  "type": "other",
  "name": "Policy Underwriter",
  "reference": "Ref002",
  "value": "6000",
  "start": "2021-11-30",
  "expires": "2022-11-29"
}
```


### Responses

#### 201 — Resource created
- **Content-Type:** `application/json`
- **Schema:** `ContractorInsuranceResponse`

```json
{
  "id": 140,
  "created": "2021-11-29T08:10:09+13:00",
  "modified": "2022-11-29T08:10:09+13:00",
  "org_id": 4587,
  "org_name": "Example Organisation Name",
  "type": "other",
  "name": "Policy Underwriter",
  "reference": "Ref002",
  "value": "6000",
  "start": "2021-11-30",
  "expires": "2022-11-29",
  "status": "Active",
  "tags": [
    [
      "asset",
      "insurance",
      "other"
    ]
  ],
  "documents": [
    {
      "id": 70,
      "modified": "2021-11-29T08:12:24+13:00",
      "name": "file-example",
      "type": "pdf",
      "size": 469513,
      "link": "https://your.site.com/storage/file-example.pdf"
    }
  ]
}
```

- **Content-Type:** `application/xml`
- **Schema:** `ContractorInsuranceResponse`

#### 403 — Access denied to this resource

#### 500 — Internal server error

---

## GET /sp/insurance/{id} — Retrieve an insurance policy

Retrieves a single contractor insurance record, filtering with the provided ID.

### Parameters
- **id** (path · integer(int32) · required): ID of the contractor insurance record to retrieve (example: `id=140`)

### Responses

#### 200 — Policy returned
- **Content-Type:** `application/json`
- **Schema:** `ContractorInsuranceResponse`

```json
{
  "id": 140,
  "created": "2021-11-29T08:10:09+13:00",
  "modified": "2022-11-29T08:10:09+13:00",
  "org_id": 4587,
  "org_name": "Example Organisation Name",
  "type": "other",
  "name": "Policy Underwriter",
  "reference": "Ref002",
  "value": "6000",
  "start": "2021-11-30",
  "expires": "2022-11-29",
  "status": "Active",
  "tags": [
    [
      "asset",
      "insurance",
      "other"
    ]
  ],
  "documents": [
    {
      "id": 70,
      "modified": "2021-11-29T08:12:24+13:00",
      "name": "file-example",
      "type": "pdf",
      "size": 469513,
      "link": "https://your.site.com/storage/file-example.pdf"
    }
  ]
}
```

- **Content-Type:** `application/xml`
- **Schema:** `ContractorInsuranceResponse`

#### 403 — Access denied to resource.

#### 404 — Resource not found

#### 500 — Internal server error

---

## PUT /sp/insurance/{id} — Update an insurance policy

Updates a contractor insurance policy using the provided ID.

### Parameters
- **id** (path · integer(int32) · required): ID of the contractor insurance record to update (example: `id=140`)

### Request Body
Information used to create the insurance policy record:
**Required:** True

- **Content-Type:** `application/json`
- **Schema:** `ContractorInsuranceRequest`

```json
{
  "org_id": 4587,
  "type": "other",
  "name": "Policy Underwriter",
  "reference": "Ref002",
  "value": "6000",
  "start": "2021-11-30",
  "expires": "2022-11-29"
}
```


### Responses

#### 200 — Updated policy
- **Content-Type:** `application/json`
- **Schema:** `ContractorInsuranceResponse`

```json
{
  "id": 140,
  "created": "2021-11-29T08:10:09+13:00",
  "modified": "2022-11-29T08:10:09+13:00",
  "org_id": 4587,
  "org_name": "Example Organisation Name",
  "type": "other",
  "name": "Policy Underwriter",
  "reference": "Ref002",
  "value": "6000",
  "start": "2021-11-30",
  "expires": "2022-11-29",
  "status": "Active",
  "tags": [
    [
      "asset",
      "insurance",
      "other"
    ]
  ],
  "documents": [
    {
      "id": 70,
      "modified": "2021-11-29T08:12:24+13:00",
      "name": "file-example",
      "type": "pdf",
      "size": 469513,
      "link": "https://your.site.com/storage/file-example.pdf"
    }
  ]
}
```

- **Content-Type:** `application/xml`
- **Schema:** `ContractorInsuranceResponse`

#### 403 — Access denied to this resource

#### 500 — Internal server error

---

## DELETE /sp/insurance/{id} — Delete an insurance policy

Deletes a contractor insurance policy based on the provided ID.

### Parameters
- **id** (path · integer(int32) · required): ID of the contractor insurance record to delete

### Responses

#### 204 — Policy deleted

#### 403 — Access denied to resource

#### 404 — Resource not found

#### 500 — Internal server error

---

## GET /sp/insurance/type — List contractor insurance types

Returns a list of contractor insurance types configured for your organization.

A comma separated list of tag:value search parameters; a colon separator performs an exact match and a percent separator performs a starts-with search.

### Parameters
- **q** (query · string): Filter by any insurance type value (example: `name:Public Liability`)
- **order** (query · string): Specifies an element to order by. Prefix with - to reverse order. (example: `name`)
- **limit** (query · integer(int32)): Limits the number of records to return. (example: `50`)
- **If-Modified-Since** (header · string): A UTC timestamp (yyyy-mm-ddThh:mm:ss). Only entities created or modified since this timestamp will be returned.

### Responses

#### 200 — List of contractor insurance types
- **Content-Type:** `application/json`
- **Schema:** `array of SpInsuranceTypeResponse`

```json
[
  {
    "id": 1,
    "name": "Public Liability",
    "created": "2025-01-08T06:04:00+00:00",
    "modified": "2025-06-01T10:00:00+00:00"
  }
]
```

- **Content-Type:** `application/xml`
- **Schema:** `array of SpInsuranceTypeResponse`

#### 400 — Invalid input

#### 403 — Access denied to this resource

#### 500 — Internal server error

---

## Tag: Contractor insurance documents

Contractor organization insurance policies can have documents attached to them. Use the API to retrieve a list of all documents attached to a policy, add new documents, and update or delete existing documents.

Learn more about insurances in the [OnLocation Help Center](https://onlocation.elevio.help/en/articles/483).

## GET /sp/insurance/{id}/document — List all documents for a policy

Retrieve all documents attached to a contractor insurance policy.

### Parameters
- **id** (path · integer(int32) · required): ID of the contractor insurance record (example: `id=140`)

### Responses

#### 200 — List of contractor insurance documents
- **Content-Type:** `application/json`
- **Schema:** `array of DocumentResponse`

```json
[
  {
    "id": 70,
    "modified": "2021-11-29T08:12:24+13:00",
    "name": "file-example",
    "type": "pdf",
    "size": 469513,
    "link": "https://your.site.com/storage/file-example.pdf"
  }
]
```

- **Content-Type:** `application/xml`
- **Schema:** `array of DocumentResponse`

#### 403 — Access denied to resource

#### 404 — Resource not found

#### 500 — Internal server error

---

## POST /sp/insurance/{id}/document — Update a policy document

Add a new document to a contractor insurance policy.

### Parameters
- **id** (path · integer(int32) · required): ID of the contractor insurance (example: `id=140`)

### Request Body
**Required:** True

- **Content-Type:** `multipart/form-data`
- **Schema:** `object`

### Responses

#### 200 — Resource created

#### 403 — Access denied to this resource

#### 500 — Internal server error

---

## GET /sp/insurance/{id}/document/{did} — Retrieve a policy document

Retrieves a single contractor insurance policy document, filtering with the provided ID.

### Parameters
- **id** (path · integer(int32) · required): ID of the contractor insurance record (example: `id=140`)
- **did** (path · integer(int32) · required): ID of the contractor insurance document (example: `did=70`)

### Responses

#### 200
- **Content-Type:** `application/json`
- **Schema:** `DocumentResponse`

```json
{
  "id": 70,
  "modified": "2021-11-29T08:12:24+13:00",
  "name": "file-example",
  "type": "pdf",
  "size": 469513,
  "link": "https://your.site.com/storage/file-example.pdf"
}
```

- **Content-Type:** `application/xml`
- **Schema:** `DocumentResponse`

#### 403 — Access denied to resource

#### 404 — Resource not found

#### 500 — Internal server error

---

## DELETE /sp/insurance/{id}/document/{did} — Delete a policy document

Deletes the specified contractor insurance document

### Parameters
- **id** (path · integer(int32) · required): ID of the contractor insurance (example: `id=140`)
- **did** (path · integer(int32) · required): ID of the contractor insurance document to delete (example: `did=70`)

### Responses

#### 204 — Document deleted

#### 403 — Access denied to resource

#### 404 — Resource not found

#### 500 — Internal server error

---

## Tag: Contractor members

A contractor member belongs to an organization that is contracted to provide a service. Each member has a profile in OnLocation that includes their contact details, status, access permission dates, and locations.

Use the contractor member API to retrieve your contractor member list, or add, update or delete a member profile.

To manage which organizations a member belongs to, use the contractor membership endpoint.

Learn more about contractor members in the [OnLocation Help Center](https://onlocation.elevio.help/en/articles/447).

## GET /sp/member — List all members

Returns a list of all contractor organization members.

A comma separated list of tag:value search parameters, a colon separator performs an exact match and a percent separator performs a starts-with search.

A contractor member can be matched to an external token which has been assigned to their profile such as RFID card number, a security turnstile controller may search for an account to update the onsite status, eg q=token:C1BB7F.

### Parameters
- **q** (query · string): Filter by any contractor member value. It is not possible to filter by customfield-elements at this point. (example: `name:ACME`)
- **order** (query · string): Order by any element returned, prefix with a - to reverse order. (example: `-service_provider_id`)
- **limit** (query · integer(int32)): Limits the amount of records to return (example: `10`)
- **If-Modified-Since** (header · string(int32)): A UTC timestamp (yyyy-mm-ddThh:mm:ss) . Only entities created or modified since this timestamp will be returned e.g. 2009-11-12T00:00:00

### Responses

#### 200 — List of contractor organization members
- **Content-Type:** `application/json`
- **Schema:** `array of ContractorMemberResponse`

```json
[
  {
    "id": 16348,
    "created": "2021-09-03T15:17:57+12:00",
    "modified": "2021-11-29T08:39:55+13:00",
    "name": "First Contractor",
    "setup_by": 823431,
    "title": "Contractor",
    "service_provider_id": "contract_047",
    "email": "contractor-email@whosonlocation.com",
    "altemail": "alternative-address@whosonlocation.com",
    "phone": "+1 206 555 0123",
    "mobile": "+1 206 555 0123",
    "status": "active",
    "valid_from": "2021-09-03",
    "valid_to": "2026-09-02",
    "extension": "047",
    "ice": "+1 206 555 0123",
    "onsite_status": "onsite",
    "cur_location_id": 301,
    "cur_sp_org_id": 4587,
    "cur_location": "Head Office",
    "sp_orgs": [
      {
        "id": 4587,
        "name": "Head Office",
        "roles": [
          {
            "id": 556,
            "name": "Example Name"
          }
        ],
        "locations": [
          {
            "id": 301,
            "name": "Sub Office",
            "start": "2021-12-01",
            "expires": "2021-12-01"
          }
        ],
        "locations_same_as_org": true
      }
    ],
    "logs": [
      null
    ],
    "tokens": [
      null
    ],
    "customfields": {
      "123": "custom-field value"
    }
  }
]
```

- **Content-Type:** `application/xml`
- **Schema:** `array of ContractorMemberResponse`

#### 403 — Access denied to this resource

#### 404 — Resource not found

#### 500 — Internal server error

---

## POST /sp/member — Create a member

Add a new contractor organization member to the database.

To add a member with custom fields:

- First, load the available custom fields (GET /customfield/element/sp-member)
- POST Request to /sp/member with a key that is called customfields, holding the id: value pair of the data. Your return will show you the type of contractor member custom field, its ID number, and its name.
- Fire the POST updating the custom field.

### Request Body
Contractor member information:
**Required:** True

- **Content-Type:** `application/json`
- **Schema:** `ContractorMemberRequest`

```json
{
  "name": "New Member",
  "title": "Contractor",
  "email": "example@example.org",
  "altemail": "example-alt@example.org",
  "phone": "+1 206 555 0123",
  "ice": "+1 206 555 0123",
  "cur_location": 301,
  "extension": "047",
  "mobile": "+1 206 555 0123",
  "valid_from": "2021-09-03",
  "valid_to": "2026-09-02",
  "onsite_status": "onsite",
  "service_provider_id": "12345",
  "cf": {
    "123": "custom-field value"
  }
}
```


### Responses

#### 201 — Member created
- **Content-Type:** `application/json`
- **Schema:** `ContractorMemberResponse`

```json
{
  "id": 16348,
  "created": "2021-09-03T15:17:57+12:00",
  "modified": "2021-11-29T08:39:55+13:00",
  "name": "First Contractor",
  "setup_by": 823431,
  "title": "Contractor",
  "service_provider_id": "contract_047",
  "email": "contractor-email@whosonlocation.com",
  "altemail": "alternative-address@whosonlocation.com",
  "phone": "+1 206 555 0123",
  "mobile": "+1 206 555 0123",
  "status": "active",
  "valid_from": "2021-09-03",
  "valid_to": "2026-09-02",
  "extension": "047",
  "ice": "+1 206 555 0123",
  "onsite_status": "onsite",
  "cur_location_id": 301,
  "cur_sp_org_id": 4587,
  "cur_location": "Head Office",
  "sp_orgs": [
    {
      "id": 4587,
      "name": "Head Office",
      "roles": [
        {
          "id": 556,
          "name": "Example Name"
        }
      ],
      "locations": [
        {
          "id": 301,
          "name": "Sub Office",
          "start": "2021-12-01",
          "expires": "2021-12-01"
        }
      ],
      "locations_same_as_org": true
    }
  ],
  "logs": [
    null
  ],
  "tokens": [
    null
  ],
  "customfields": {
    "123": "custom-field value"
  }
}
```

- **Content-Type:** `application/xml`
- **Schema:** `ContractorMemberResponse`

#### 403 — Access denied to this resource

#### 500 — Internal server error

---

## GET /sp/member/{id} — Retrieve a member

Retrieves a single contractor organization member, filtering with the provided ID.

### Parameters
- **id** (path · integer(int32) · required): ID of the contractor organization member to retrieve (example: `id=16348`)

### Responses

#### 200 — Member returned
- **Content-Type:** `application/json`
- **Schema:** `ContractorMemberResponse`

```json
{
  "id": 16348,
  "created": "2021-09-03T15:17:57+12:00",
  "modified": "2021-11-29T08:39:55+13:00",
  "name": "First Contractor",
  "setup_by": 823431,
  "title": "Contractor",
  "service_provider_id": "contract_047",
  "email": "contractor-email@whosonlocation.com",
  "altemail": "alternative-address@whosonlocation.com",
  "phone": "+1 206 555 0123",
  "mobile": "+1 206 555 0123",
  "status": "active",
  "valid_from": "2021-09-03",
  "valid_to": "2026-09-02",
  "extension": "047",
  "ice": "+1 206 555 0123",
  "onsite_status": "onsite",
  "cur_location_id": 301,
  "cur_sp_org_id": 4587,
  "cur_location": "Head Office",
  "sp_orgs": [
    {
      "id": 4587,
      "name": "Head Office",
      "roles": [
        {
          "id": 556,
          "name": "Example Name"
        }
      ],
      "locations": [
        {
          "id": 301,
          "name": "Sub Office",
          "start": "2021-12-01",
          "expires": "2021-12-01"
        }
      ],
      "locations_same_as_org": true
    }
  ],
  "logs": [
    null
  ],
  "tokens": [
    null
  ],
  "customfields": {
    "123": "custom-field value"
  }
}
```

- **Content-Type:** `application/xml`
- **Schema:** `ContractorMemberResponse`

#### 403 — Access denied to resource

#### 404 — Resource not found

#### 500 — Internal server error

---

## PUT /sp/member/{id} — Update a member

Update a contractor organization member profile using the provided ID.

### Parameters
- **id** (path · integer(int32) · required): ID of the contractor member to update (example: `id=16348`)

### Request Body
Information used to create the contractor member record:
**Required:** True

- **Content-Type:** `application/json`
- **Schema:** `ContractorMemberRequest`

```json
{
  "name": "New Member",
  "title": "Contractor",
  "email": "example@example.org",
  "altemail": "example-alt@example.org",
  "phone": "+1 206 555 0123",
  "ice": "+1 206 555 0123",
  "cur_location": 301,
  "extension": "047",
  "mobile": "+1 206 555 0123",
  "valid_from": "2021-09-03",
  "valid_to": "2026-09-02",
  "onsite_status": "onsite",
  "service_provider_id": "12345",
  "cf": {
    "123": "custom-field value"
  }
}
```


### Responses

#### 204 — Updated member

#### 403 — Access denied to this resource

#### 500 — Internal server error

---

## DELETE /sp/member/{id} — Delete a member

Delete a contractor organization member using the provided ID.

### Parameters
- **id** (path · integer(int32) · required): ID of the specified contractor organization to delete (example: `id=16348`)

### Responses

#### 204 — Member deleted

#### 403 — Access denied to resource

#### 404 — Resource not found

#### 500 — Internal server error

---

## Tag: Contractor member movements

Each time a contractor signs in or out, they create a movement. Use the API to return all contractor member movements, or add or update a movement record.

## GET /sp/member/movement — List all member movements

Retrieves a list of contractor movement records. If an 'id' query parameter is supplied it will return a single contractor movement record.

A comma separated list of tag:value search parameters, a colon separator performs an exact match, a percent separator performs a starts-with search and greater/less than search number/date ranges.

### Parameters
- **q** (query · integer(int32)): ID of the contractor movement to retrieve (example: `sp_member_id:123456`)
- **order** (query · string): Order by any element returned, prefix with a - to reverse order. (example: `id`)
- **limit** (query · integer(int32)): Limits the amount of records to return (example: `10`)
- **If-Modified-Since** (header · string(int32)): A UTC timestamp (yyyy-mm-ddThh:mm:ss). Only entities created or modified since this timestamp will be returned e.g. 2009-11-12T00:00:00

### Responses

#### 200 — List of contractor movement records
- **Content-Type:** `application/json`
- **Schema:** `array of ContractorMovementResponse`

```json
[
  {
    "id": 4938,
    "created": "2021-10-28T12:57:14+13:00",
    "modified": "2021-11-15T12:57:14+13:00",
    "location_id": 301,
    "sp_member_id": 16348,
    "sp_org_id": 4587,
    "signed_in": "2021-10-28T12:57:14+13:00",
    "signed_out": "2021-10-28T16:57:14+13:00",
    "mode_in": "Sign In/Out Manager",
    "mode_out": "Sign In/Out Manager",
    "zone_id": 32,
    "zone_other": "zone",
    "signed_in_by": 823493,
    "signed_out_by": 823493,
    "scanned_in": "",
    "scanned_out": "",
    "visiting_staff_id": 823431,
    "cardnumber": "c001",
    "carpark_question": true,
    "carpark_registration": "DRU706",
    "carpark_number": "SP003",
    "pass": 20,
    "other_pass": "opass047",
    "sp_purpose": "Repairs",
    "assistance": false,
    "expected": true,
    "accessdenied": false,
    "accessdenied_reason": "No reason required",
    "loneworker": true,
    "so_staff_id": 455,
    "so_spm_id": 458,
    "interzone": true,
    "breathtest": false,
    "from_zone": 87,
    "selected_language": "english",
    "lacp_in_name": "Reception",
    "lacp_out_name": "Reception",
    "zone_group_name": "Maintenance",
    "signed_in_by_kiosk": false,
    "signed_out_by_kiosk": false,
    "photo_url": "your.site/storage/contractorphoto.pdf",
    "signout_photo_url": "your.site/storage/contractorphoto.pdf",
    "signed_in_lat": "Latitude-value of coordinates for sign-in (Decimal Degrees)",
    "signed_in_lon": "Longitude-value of coordinates for sign-in (Decimal Degrees)",
    "signed_in_accuracy": "Precision for sign-in coordinates",
    "signed_out_lat": "Latitude-value of coordinates for sign-out (Decimal Degrees)",
    "signed_out_lon": "Longitude-value of coordinates for sign-out (Decimal Degrees)",
    "signed_out_accuracy": "Precision for sign-out coordinates"
  }
]
```

- **Content-Type:** `application/xml`
- **Schema:** `array of ContractorMovementResponse`

#### 403 — Access denied to this resource

#### 404 — Resource not found

#### 500 — Internal server error

---

## PUT /sp/member/movement — Update a member movement

Creates or updates a contractor movement record.

### Parameters
- **sp_member_id** (query · integer(int32) · required): ID of the contractor. This can be supplied as either a query parameter or as part of the request body.

### Request Body
Information used to update the contractor movement record
**Required:** True

- **Content-Type:** `application/json`
- **Schema:** `ContractorMovementRequest`

```json
{
  "onsite_status": "onsite",
  "location_id": 301,
  "sp_member_id": 54,
  "sp_org_id": 504,
  "print_kiosk_id": 301,
  "lacp_id": 38,
  "zone_id": 3,
  "zone_other": "",
  "scanned_in": "",
  "scanned_out": "",
  "visiting_staff_id": 8,
  "cardnumber": "cp013",
  "carpark_question": false,
  "carpark_registration": "MRD346",
  "carpark_number": "0014",
  "pass": 1054,
  "other_pass": "Contractor pass",
  "sp_purpose": "Water station repairs",
  "assistance": false,
  "expected": 60,
  "so_staff_id": 40,
  "so_spm_id": 89
}
```


### Responses

#### 200 — Updated resource
- **Content-Type:** `application/json`
- **Schema:** `ContractorMovementResponse`

```json
{
  "id": 4938,
  "created": "2021-10-28T12:57:14+13:00",
  "modified": "2021-11-15T12:57:14+13:00",
  "location_id": 301,
  "sp_member_id": 16348,
  "sp_org_id": 4587,
  "signed_in": "2021-10-28T12:57:14+13:00",
  "signed_out": "2021-10-28T16:57:14+13:00",
  "mode_in": "Sign In/Out Manager",
  "mode_out": "Sign In/Out Manager",
  "zone_id": 32,
  "zone_other": "zone",
  "signed_in_by": 823493,
  "signed_out_by": 823493,
  "scanned_in": "",
  "scanned_out": "",
  "visiting_staff_id": 823431,
  "cardnumber": "c001",
  "carpark_question": true,
  "carpark_registration": "DRU706",
  "carpark_number": "SP003",
  "pass": 20,
  "other_pass": "opass047",
  "sp_purpose": "Repairs",
  "assistance": false,
  "expected": true,
  "accessdenied": false,
  "accessdenied_reason": "No reason required",
  "loneworker": true,
  "so_staff_id": 455,
  "so_spm_id": 458,
  "interzone": true,
  "breathtest": false,
  "from_zone": 87,
  "selected_language": "english",
  "lacp_in_name": "Reception",
  "lacp_out_name": "Reception",
  "zone_group_name": "Maintenance",
  "signed_in_by_kiosk": false,
  "signed_out_by_kiosk": false,
  "photo_url": "your.site/storage/contractorphoto.pdf",
  "signout_photo_url": "your.site/storage/contractorphoto.pdf",
  "signed_in_lat": "Latitude-value of coordinates for sign-in (Decimal Degrees)",
  "signed_in_lon": "Longitude-value of coordinates for sign-in (Decimal Degrees)",
  "signed_in_accuracy": "Precision for sign-in coordinates",
  "signed_out_lat": "Latitude-value of coordinates for sign-out (Decimal Degrees)",
  "signed_out_lon": "Longitude-value of coordinates for sign-out (Decimal Degrees)",
  "signed_out_accuracy": "Precision for sign-out coordinates"
}
```

- **Content-Type:** `application/xml`
- **Schema:** `ContractorMovementResponse`

#### 403 — Access denied to this resource

#### 500 — Internal server error

---

## Tag: Contractor membership

Each contractor organization has individual members that are authorized to work on-site. A contractor member can belong to multiple contractor organizations in OnLocation.

 Use the API to retrieve the organizations each member belongs to, including locations and contact role types; add, update, or delete the connection between a member and an organization.

Learn more about contractor members in the [OnLocation Help Center](https://onlocation.elevio.help/en/articles/447).

## GET /sp/membership — List all membership records

Retrieves a list of the members that belong to each contractor organization.

### Parameters
- **q** (query · string): Filter by any contractor membership element. (example: `org_name:ACME`)
- **order** (query · string): Order by any element returned, prefix with a - to reverse order. (example: `id`)
- **limit** (query · integer(int32)): Limits the amount of records to return. (example: `10`)
- **If-Modified-Since** (header · string(int32)): A UTC timestamp (yyyy-mm-ddThh:mm:ss) . Only entities created or modified since this timestamp will be returned e.g. 2009-11-12T00:00:00

### Responses

#### 200 — List of contractor memberships
- **Content-Type:** `application/json`
- **Schema:** `array of ContractorMembershipResponse`

```json
[
  {
    "id": 13863,
    "created": "2021-09-03T15:17:59+12:00",
    "org_id": 4587,
    "org_name": "Contractor Organisation",
    "member_id": 16348,
    "member_name": "Contractor Name",
    "roles": [
      {
        "id": 556,
        "name": "Example Name"
      }
    ],
    "locations": [
      {
        "id": 301,
        "name": "Sub Office",
        "start": "2021-12-01",
        "expires": "2021-12-01"
      }
    ],
    "locations_same_as_org": true
  }
]
```

- **Content-Type:** `application/xml`
- **Schema:** `array of ContractorMembershipResponse`

#### 403 — Access denied to this resource

#### 404 — Resource not found

#### 500 — Internal server error

---

## POST /sp/membership — Create a membership record

Create a new contractor membership association.

### Request Body
Data to save for this contractor membership.
**Required:** True

- **Content-Type:** `application/json`
- **Schema:** `ContractorMembershipRequest`

```json
{
  "org_id": 405,
  "member_id": 43,
  "roles": [
    [
      44,
      6,
      73
    ]
  ],
  "locations": [
    {
      "id": 1024,
      "start": "2021-11-27",
      "expires": "2021-11-27"
    }
  ]
}
```


### Responses

#### 201 — Resource created
- **Content-Type:** `application/json`
- **Schema:** `ContractorMembershipResponse`

```json
{
  "id": 13863,
  "created": "2021-09-03T15:17:59+12:00",
  "org_id": 4587,
  "org_name": "Contractor Organisation",
  "member_id": 16348,
  "member_name": "Contractor Name",
  "roles": [
    {
      "id": 556,
      "name": "Example Name"
    }
  ],
  "locations": [
    {
      "id": 301,
      "name": "Sub Office",
      "start": "2021-12-01",
      "expires": "2021-12-01"
    }
  ],
  "locations_same_as_org": true
}
```

- **Content-Type:** `application/xml`
- **Schema:** `ContractorMembershipResponse`

#### 403 — Access denied to this resource

#### 409 — Mapping already exists with id

#### 500 — Internal server error

---

## GET /sp/membership/{id} — Retrieve a membership record

Retrieves a single contractor membership record, filtering with the provided ID

### Parameters
- **id** (path · integer(int32) · required): ID of the contractor membership record to retrieve

### Responses

#### 200 — A single contractor membership record
- **Content-Type:** `application/json`
- **Schema:** `ContractorMembershipResponse`

```json
{
  "id": 13863,
  "created": "2021-09-03T15:17:59+12:00",
  "org_id": 4587,
  "org_name": "Contractor Organisation",
  "member_id": 16348,
  "member_name": "Contractor Name",
  "roles": [
    {
      "id": 556,
      "name": "Example Name"
    }
  ],
  "locations": [
    {
      "id": 301,
      "name": "Sub Office",
      "start": "2021-12-01",
      "expires": "2021-12-01"
    }
  ],
  "locations_same_as_org": true
}
```

- **Content-Type:** `application/xml`
- **Schema:** `ContractorMembershipResponse`

#### 403 — Access denied to resource

#### 404 — Resource not found

#### 500 — Internal server error

---

## PUT /sp/membership/{id} — Update a membership record

Updates a contractor membership association

### Parameters
- **id** (path · integer(int32) · required): ID of the contractor membership association to update

### Request Body
Information used to update the contractor membership association record
**Required:** True

- **Content-Type:** `application/json`
- **Schema:** `ContractorMembershipRequest`

```json
{
  "org_id": 405,
  "member_id": 43,
  "roles": [
    [
      44,
      6,
      73
    ]
  ],
  "locations": [
    {
      "id": 1024,
      "start": "2021-11-27",
      "expires": "2021-11-27"
    }
  ]
}
```


### Responses

#### 204 — Updated resource

#### 403 — Access denied to this resource

#### 500 — Internal server error

---

## DELETE /sp/membership/{id} — Delete a membership record

Deletes the specified contractor membership

### Parameters
- **id** (path · integer(int32) · required): ID of the membership to delete

### Responses

#### 204 — Resource Deleted

#### 403 — Access denied to resource

#### 404 — Resource not found

#### 500 — Internal server error

---

## Tag: Contractor organizations

A contractor organization is contracted to provide a service. Each organization has a profile in OnLocation that includes contact information, status, location access, insurance policies, and linked members.

Use the API to retrieve your contractor organization list, or add, update or delete an organization profile.

To manage which organizations a member belongs to, use the contractor membership endpoint.

Learn more about contractor organizations in the [OnLocation Help Center](https://onlocation.elevio.help/en/articles/455).

## GET /sp/org — List all organizations

Returns a list of all contractor organizations.

A comma separated list of tag:value search parameters, a colon separator performs an exact match and a percent separator performs a starts-with search.

### Parameters
- **q** (query · string): Filter by any contractor organization value. It is not possible to filter by customfield-elements at this point. (example: `name:ACME`)
- **order** (query · string): Order by any element returned, prefix with a - to reverse order (example: `id`)
- **limit** (query · integer(int32)): Limits the amount of records to return (example: `10`)
- **If-Modified-Since** (header · string(int32)): A UTC timestamp (yyyy-mm-ddThh:mm:ss) . Only entities created or modified since this timestamp will be returned e.g. 2009-11-12T00:00:00

### Responses

#### 200 — List of contractor organizations
- **Content-Type:** `application/json`
- **Schema:** `array of ContractorOrganisationResponse`

```json
[
  {
    "id": 4587,
    "created": "2021-09-03T15:16:44+12:00",
    "modified": "2021-11-29T09:51:00+13:00",
    "name": "Contractor Organisation Name",
    "setup_by": 653,
    "type": "Charitable organization",
    "tradingas": "Contractor Organisation Name",
    "legalid": "lid005",
    "legalid_type": "legal ID type",
    "address": "2 Water Street, New York, NY, USA",
    "country": "NZ",
    "status": "active",
    "email": "sp.organisation@whosonlocation.com",
    "phone": "+1 206 555 0123",
    "locations": [
      {
        "id": 301,
        "name": "Sub Office",
        "start": "2021-12-01",
        "expires": "2021-12-01"
      }
    ],
    "policies": 2,
    "members": [
      {
        "id": 556,
        "name": "Example Name"
      }
    ],
    "organization_owner": 823431,
    "logs": [
      null
    ],
    "customfields": {
      "123": "custom-field value"
    }
  }
]
```

- **Content-Type:** `application/xml`
- **Schema:** `array of ContractorOrganisationResponse`

#### 400 — Indicating invalid fields

#### 403 — Access denied to this resource

#### 404 — Resource not found

#### 500 — Internal server error

---

## POST /sp/org — Create an organization

Create a new contractor organization.

### Request Body
Information to save to the database
**Required:** True

- **Content-Type:** `application/json`
- **Schema:** `ContractorOrganisationRequest`

```json
{
  "name": "Contractor Organisation Name",
  "type": "",
  "tradingas": "Contractor Organisation Name",
  "legalid": "lid005",
  "legalid_type": "legal ID type",
  "address": "2 Water Street, New York, NY, USA",
  "country": "NZ",
  "email": "sp.organisation@whosonlocation.com",
  "phone": "+1 206 555 0123",
  "organization_owner": 823431,
  "status": "active",
  "locations": [
    {
      "id": 301,
      "name": "Sub Office",
      "start": "2021-12-01",
      "expires": "2021-12-01"
    }
  ],
  "cf": {
    "123": "custom-field value"
  }
}
```


### Responses

#### 201 — Organization created
- **Content-Type:** `application/json`
- **Schema:** `ContractorOrganisationResponse`

```json
{
  "id": 4587,
  "created": "2021-09-03T15:16:44+12:00",
  "modified": "2021-11-29T09:51:00+13:00",
  "name": "Contractor Organisation Name",
  "setup_by": 653,
  "type": "Charitable organization",
  "tradingas": "Contractor Organisation Name",
  "legalid": "lid005",
  "legalid_type": "legal ID type",
  "address": "2 Water Street, New York, NY, USA",
  "country": "NZ",
  "status": "active",
  "email": "sp.organisation@whosonlocation.com",
  "phone": "+1 206 555 0123",
  "locations": [
    {
      "id": 301,
      "name": "Sub Office",
      "start": "2021-12-01",
      "expires": "2021-12-01"
    }
  ],
  "policies": 2,
  "members": [
    {
      "id": 556,
      "name": "Example Name"
    }
  ],
  "organization_owner": 823431,
  "logs": [
    null
  ],
  "customfields": {
    "123": "custom-field value"
  }
}
```

#### 403 — Access denied to this resource

#### 500 — Internal server error

---

## GET /sp/org/{id} — Retrieve an organization

Retrieves a single contractor organization, filtering with the provided ID.

### Parameters
- **id** (path · integer(int32) · required): ID of the contractor organization to retrieve

### Responses

#### 200 — Organization returned
- **Content-Type:** `application/json`
- **Schema:** `ContractorOrganisationResponse`

```json
{
  "id": 4587,
  "created": "2021-09-03T15:16:44+12:00",
  "modified": "2021-11-29T09:51:00+13:00",
  "name": "Contractor Organisation Name",
  "setup_by": 653,
  "type": "Charitable organization",
  "tradingas": "Contractor Organisation Name",
  "legalid": "lid005",
  "legalid_type": "legal ID type",
  "address": "2 Water Street, New York, NY, USA",
  "country": "NZ",
  "status": "active",
  "email": "sp.organisation@whosonlocation.com",
  "phone": "+1 206 555 0123",
  "locations": [
    {
      "id": 301,
      "name": "Sub Office",
      "start": "2021-12-01",
      "expires": "2021-12-01"
    }
  ],
  "policies": 2,
  "members": [
    {
      "id": 556,
      "name": "Example Name"
    }
  ],
  "organization_owner": 823431,
  "logs": [
    null
  ],
  "customfields": {
    "123": "custom-field value"
  }
}
```

- **Content-Type:** `application/xml`
- **Schema:** `ContractorOrganisationResponse`

#### 403 — Access denied to resource

#### 404 — Resource not found

#### 500 — Internal server error

---

## PUT /sp/org/{id} — Update an organization

Update a contractor organization using the provided ID.

### Parameters
- **id** (path · integer(int32) · required): ID of the contractor organization to update

### Request Body
**Required:** True

- **Content-Type:** `application/json`
- **Schema:** `ContractorOrganisationRequest`

```json
{
  "name": "Contractor Organisation Name",
  "type": "",
  "tradingas": "Contractor Organisation Name",
  "legalid": "lid005",
  "legalid_type": "legal ID type",
  "address": "2 Water Street, New York, NY, USA",
  "country": "NZ",
  "email": "sp.organisation@whosonlocation.com",
  "phone": "+1 206 555 0123",
  "organization_owner": 823431,
  "status": "active",
  "locations": [
    {
      "id": 301,
      "name": "Sub Office",
      "start": "2021-12-01",
      "expires": "2021-12-01"
    }
  ],
  "cf": {
    "123": "custom-field value"
  }
}
```


### Responses

#### 204 — Updated organization

#### 403 — Access denied to this resource

#### 500 — Internal server error

---

## DELETE /sp/org/{id} — Delete an organization

Deletes the specified contractor organization

### Parameters
- **id** (path · integer(int32) · required): ID of the contractor organization to delete

### Responses

#### 204 — Organization deleted

#### 403 — Access denied to resource

#### 404 — Resource not found

#### 500 — Internal server error

---

## GET /sp/org/{id}/groups — Retrieve a contractor organization's group

Retrieves a contractor organization's group by Org ID.

### Parameters
- **id** (path · integer · required): ID of the contractor organization to retrieve
- **q** (query · integer(int32)): ID of the category to retrieve particular category groups. (example: `category_id:123456`)

### Responses

#### 200 — Contractor organization's groups
- **Content-Type:** `application/json`
- **Schema:** `object`

```json
{
  "group": [
    {
      "id": 4,
      "name": "Electrical",
      "category_id": 1
    }
  ]
}
```

- **Content-Type:** `application/xml`
- **Schema:** `object`

#### 403 — Access denied to this resource

#### 404 — Contractor organization's group not found

#### 500 — Internal server error

---

## PUT /sp/org/{id}/groups — Update a contractor organization's group

Updates the details of a contractor organization's group.

### Parameters
- **id** (path · integer(int32) · required): ID of the contractor organization to update

### Request Body
Information used to update the contractor organization group record
**Required:** False

- **Content-Type:** `application/json`
- **Schema:** `object`

```json
{
  "groups": [
    {
      "id": 556,
      "name": "Example Name"
    }
  ],
  "function": ""
}
```


### Responses

#### 200 — Updated resource
- **Content-Type:** `application/json`
- **Schema:** `object`

```json
{
  "group": [
    {
      "id": 4,
      "name": "Electrical",
      "category_id": 1
    }
  ]
}
```

- **Content-Type:** `application/xml`
- **Schema:** `object`

#### 400 — Invalid Input

#### 403 — Access denied to this resource

#### 404 — Contractor organization's group not found

#### 500 — Internal server error

---

## Tag: Contractor organization categories

Categories are used to describe the different functions of your contractor organizations. You can use them to sort your organizations and make it easier to report on them and create triggers.

 Use the API to retrieve your categories list or add, update or delete a category.

Learn more about contractor organization categories in the [OnLocation Help Center](https://onlocation.elevio.help/en/articles/487).

## GET /sp/org/category — List of contractor organization categories

Returns a list of contractor organization categories.

### Responses

#### 200 — List of contractor organization categories
- **Content-Type:** `application/json`
- **Schema:** `array of SpCategoryResponse`

```json
[
  {
    "id": 1,
    "name": "Transport Services",
    "created": "2023-05-12 09:30:00"
  }
]
```

- **Content-Type:** `application/xml`
- **Schema:** `array of SpCategoryResponse`

#### 400 — Invalid Input

#### 403 — Access denied to this resource

#### 500 — Internal server error

---

## POST /sp/org/category — Create a contractor organization category

Creates a new contractor organization category with the provided details.

### Request Body
Information used to create a new contractor organization category
**Required:** False

- **Content-Type:** `application/json`
- **Schema:** `SpCategoryRequest`

```json
{
  "name": "Transport Services"
}
```


### Responses

#### 201 — Created resource
- **Content-Type:** `application/json`
- **Schema:** `SpCategoryResponse`

```json
{
  "id": 1,
  "name": "Transport Services",
  "created": "2023-05-12 09:30:00"
}
```

- **Content-Type:** `application/xml`
- **Schema:** `SpCategoryResponse`

#### 400 — Invalid input

#### 403 — Access denied to this resource

#### 500 — Internal server error

---

## GET /sp/org/category/{id} — Retrieve a contractor organization category

Retrieves a contractor organization category by its ID.

### Parameters
- **id** (path · integer · required): ID of the contractor organization category to retrieve

### Responses

#### 200 — Contractor organization category found
- **Content-Type:** `application/json`
- **Schema:** `SpCategoryResponse`

```json
{
  "id": 1,
  "name": "Transport Services",
  "created": "2023-05-12 09:30:00"
}
```

- **Content-Type:** `application/xml`
- **Schema:** `SpCategoryResponse`

#### 403 — Access denied to this resource

#### 404 — Contractor organization category not found

#### 500 — Internal server error

---

## PUT /sp/org/category/{id} — Update a contractor organization category

Updates the details of a contractor organization category.

### Parameters
- **id** (path · integer(int32) · required): ID of the contractor organization category to update

### Request Body
Information used to update the contractor organization category record
**Required:** False

- **Content-Type:** `application/json`
- **Schema:** `SpCategoryRequest`

```json
{
  "name": "Transport Services"
}
```


### Responses

#### 200 — Updated resource
- **Content-Type:** `application/json`
- **Schema:** `SpCategoryResponse`

```json
{
  "id": 1,
  "name": "Transport Services",
  "created": "2023-05-12 09:30:00"
}
```

- **Content-Type:** `application/xml`
- **Schema:** `SpCategoryResponse`

#### 400 — Invalid Input

#### 403 — Access denied to this resource

#### 404 — Contractor organization category not found

#### 500 — Internal server error

---

## DELETE /sp/org/category/{id} — Delete a contractor organization category

Deletes a contractor organization category by its ID.

### Parameters
- **id** (path · integer · required): ID of the contractor organization category to delete

### Responses

#### 204 — Contractor organization category deleted successfully

#### 403 — Access denied to this resource

#### 404 — Contractor organization category not found

#### 409 — Not deleted as contractor organization groups found under this category

#### 500 — Internal server error

---

## Tag: Contractor organization groups

Groups are a subset of contractor organization categories. For example, electrical, cleaning, and maintenance could be groups under the facilities category. They are used to organize your organizations and make it easier to report on them and create triggers.

 Use the API to retrieve your groups list or add, update or delete a group.

 Learn more about contractor organization groups in the [OnLocation Help Center](https://onlocation.elevio.help/en/articles/487).

## GET /sp/org/group — List contractor organization groups

Returns a list of contractor organization groups.

### Parameters
- **q** (query · integer(int32)): ID of the category to retrieve particular category groups. (example: `category_id:123456`)

### Responses

#### 200 — List of contractor organization groups
- **Content-Type:** `application/json`
- **Schema:** `array of SpGroupResponse`

```json
[
  {
    "id": 1,
    "name": "Drivers",
    "category_id": 1,
    "created": "2023-05-12 09:30:00"
  }
]
```

- **Content-Type:** `application/xml`
- **Schema:** `array of SpGroupResponse`

#### 400 — Invalid Input

#### 403 — Access denied to this resource

#### 500 — Internal server error

---

## POST /sp/org/group — Create a contractor organization group

Creates a new contractor organization group with the provided details.

### Request Body
Information used to create a new contractor organization group
**Required:** False

- **Content-Type:** `application/json`
- **Schema:** `SpGroupRequest`

```json
{
  "category_id": 1,
  "name": "Drivers"
}
```


### Responses

#### 201 — Created resource
- **Content-Type:** `application/json`
- **Schema:** `SpGroupRequest`

```json
{
  "category_id": 1,
  "name": "Drivers"
}
```

- **Content-Type:** `application/xml`
- **Schema:** `SpGroupResponse`

#### 400 — Invalid input

#### 403 — Access denied to this resource

#### 500 — Internal server error

---

## GET /sp/org/group/{id} — Retrieve a contractor organization group

Retrieves a contractor organization group by its ID.

### Parameters
- **id** (path · integer · required): ID of the contractor organization group to retrieve

### Responses

#### 200 — Contractor organization group found
- **Content-Type:** `application/json`
- **Schema:** `SpGroupResponse`

```json
{
  "id": 1,
  "name": "Drivers",
  "category_id": 1,
  "created": "2023-05-12 09:30:00"
}
```

- **Content-Type:** `application/xml`
- **Schema:** `SpGroupResponse`

#### 403 — Access denied to this resource

#### 404 — Contractor organization group not found

#### 500 — Internal server error

---

## PUT /sp/org/group/{id} — Update a contractor organization group

Updates the details of a contractor organization group.

### Parameters
- **id** (path · integer(int32) · required): ID of the contractor organization group to update

### Request Body
Information used to update the contractor organization group record
**Required:** False

- **Content-Type:** `application/json`
- **Schema:** `SpGroupPutRequest`

```json
{
  "name": "Drivers"
}
```


### Responses

#### 200 — Updated resource
- **Content-Type:** `application/json`
- **Schema:** `SpGroupResponse`

```json
{
  "id": 1,
  "name": "Drivers",
  "category_id": 1,
  "created": "2023-05-12 09:30:00"
}
```

- **Content-Type:** `application/xml`
- **Schema:** `SpGroupResponse`

#### 400 — Invalid Input

#### 403 — Access denied to this resource

#### 404 — Contractor organization group not found

#### 500 — Internal server error

---

## DELETE /sp/org/group/{id} — Delete a contractor organization group

Deletes a contractor organization group by its ID.

### Parameters
- **id** (path · integer · required): ID of the contractor organization group to delete

### Responses

#### 204 — Contractor organization group deleted successfully

#### 403 — Access denied to this resource

#### 404 — Contractor organization group not found

#### 409 — Not deleted as contractor organizations found attached to the group

#### 500 — Internal server error

---

## Tag: Custom fields

Custom fields are used to record information that is not covered by the the default employee and contractor profile fields. Use the custom fields API to retrieve any employee custom fields, contractor organization custom fields, and contractor member custom fields.

See how to update custom fields in [employee](/developer-portal/example-custom-fields/) or [contractor](/developer-portal/example-contractor-custom-fields/) profiles.

Learn more about custom fields in the [OnLocation Help Center](https://onlocation.elevio.help/en/articles/432).

## GET /customfield/element/{element_type} — List all custom fields

Retrieves a list of all custom fields filtered by the parameter provided: employees, contractor organizations, or contractor members.

A comma separated list of tag:value search parameters, a colon separator performs an exact match, a percent separator performs a starts-with search and greater/less than search number/date ranges.

The endpoint is limited to a maximum of 1000 records.

Each custom field contains a value pair, which consists of the field ID as the key and a string representation as the value. As custom fields contain a range of data types, the API responds with a JSON-string representation for nested data.

The fields that contain nested data are:

- Date and Time fields if they contain a date or time range. The JSON-Format is {“start”: “value”, “end”:”value”}
- Option fields where ‘Multiple Selection’ is activated. The value is a string representation of a JSON-Array in the format. For example, [“value1”, “value2”]
- Person fields where ‘Multiple Selection’ is activated. The format is a string representation of a JSON-Array containing user identifiers. E.g. [1,2,3].ID can either be a contractor member identifier or an employee (staff) identifier. For example, if you add the Person field ‘Contractors from any organization’ to your employee custom fields, we’ll use the GET /customfield/element/staff endpoint to determine whether the identifier represents a contractor member or an employee.
- If the Person field is set to ‘Single Selection’, the results will be a string representation of an identifier, eg 1
- Checkbox fields will have a string representation of 1 if the field is selected or 0 if it is not.

### Parameters
- **element_type** (path · string · required): Custom field element type to filter by. Options: 'staff', 'sp-org', 'sp-member' (example: `element/sp-member`)
- **order** (query · string): Specifies an element to order by. To order in reverse order, prepend the element with '-'. (example: `id`)
- **limit** (query · integer(int32)): Limits the amount of records to return (example: `10`)
- **page** (query · integer(int32)): Specifies which page of results to return, most effective when used together with the 'limit' parameter

### Responses

#### 200 — List of custom field elements
- **Content-Type:** `application/json`
- **Schema:** `array of CustomfieldElement`

```json
[
  {
    "id": 741,
    "created": "2021-11-25T12:17:27+13:00",
    "modified": "2021-11-27T12:17:27+13:00",
    "cf_tab_id": 89,
    "is_visible": true,
    "is_mandatory": true,
    "is_unique": true,
    "title": "Checkbox field",
    "config": [
      null
    ],
    "created_by": 823431,
    "modified_by": 823431,
    "element_type": "checkbox"
  }
]
```

- **Content-Type:** `application/xml`
- **Schema:** `array of CustomfieldElement`

#### 403 — Returned if the account does not have access to custom fields

#### 404 — Resource not found.

---

## Tag: Custom field tabs

Users can add new tabs to organize the custom fields in their employee or contractor profiles. Use the custom fields API to retrieve any employee, contractor organization, and contractor member custom field tabs.

Learn more about custom fields in the [OnLocation Help Center](https://onlocation.elevio.help/en/articles/432).

## GET /customfield/tab/{tab_type} — List all custom field tabs

Retrieves a list of custom field tabs filtered by the parameter provided: employees, contractor organizations, or contractor members.

A comma separated list of tag:value search parameters, a colon separator performs an exact match, a percent separator performs a starts-with search and greater/less than search number/date ranges.

The endpoint is limited to a maximum of 1000 records.

### Parameters
- **tab_type** (path · string · required): Custom field element type to filter by. Options: 'staff', 'sp-org', 'sp-member'. (example: `tab/sp-member`)
- **order** (query · string): Specifies an element to order by. To order in reverse order, prepend the element with '-'. (example: `id`)
- **limit** (query · integer(int32)): Limits the amount of records to return (example: `10`)
- **page** (query · integer(int32)): Specifies which page of results to return, most effective when used together with the 'limit' parameter

### Responses

#### 200 — List of custom tab elements
- **Content-Type:** `application/json`
- **Schema:** `array of CustomfieldTab`

```json
[
  {
    "id": 88,
    "created": "2021-11-25T12:14:32+13:00",
    "modified": "2021-11-25T12:23:34+13:00",
    "title": "Profile Information",
    "sort": 2,
    "created_by": 823,
    "updated_by": 431
  }
]
```

- **Content-Type:** `application/xml`
- **Schema:** `array of CustomfieldTab`

#### 403 — Access denied to this resource

#### 404 — Resource not found.

---

## Tag: Custom questionnaires

A custom questionnaire is a set of custom questions that are asked during sign in, sign out, or visitor pre-registration. The custom questionnaire API retrieves your custom questionnaires and can return single records based on an ID.

Learn more about custom questionnaires in the [OnLocation Help Center](https://onlocation.elevio.help/en/articles/515).

## GET /custom-questionnaire — List all custom questionnaires

Returns a list of all custom questionnaire records. The endpoint is limited to a maximum of 5,000 records.

A comma separated list of tag:value search parameters, a colon separator performs an exact match, a percent separator performs a starts-with search and greater/less than search number/date ranges.

### Parameters
- **q** (query · string): Filter by any custom questionnaire value. (example: `498`)
- **order** (query · string): Specifies an element to order by. To order in reverse order, prepend the element with '-'. (example: `id`)
- **limit** (query · integer(int32)): Limits the amount of records to return (example: `10`)
- **page** (query · integer(int32)): Specifies which page of results to return, most effective when used together with the 'limit' parameter
- **If-Modified-Since** (header · string(int32)): A UTC timestamp (yyyy-mm-ddThh:mm:ss). Only entities created or modified since this timestamp will be returned e.g. 2009-11-12T00:00:00

### Responses

#### 200 — List of custom questionnaire records
- **Content-Type:** `application/json`
- **Schema:** `array of CustomQuestionnaireResponse`

```json
[
  {
    "id": 498,
    "created": "2021-11-25T12:45:45+13:00",
    "modified": "2021-11-25T12:51:48+13:00",
    "active": true,
    "name": "Health Check",
    "record_type": "visitor",
    "setup_by": 823,
    "frequency": "every",
    "frequency_days": 1,
    "location_id": 301
  }
]
```

- **Content-Type:** `application/xml`
- **Schema:** `array of CustomQuestionnaireResponse`

#### 403 — Access denied to this resource

#### 404 — Resource not found

---

## GET /custom-questionnaire/{id} — Retrieve a custom questionnaire

Returns a custom questionnaire record filtered by the provided ID.

### Parameters
- **id** (path · integer(int32) · required): ID of the custom questionnaire to look for

### Responses

#### 200 — The custom questionnaire element matching the passed in ID
- **Content-Type:** `application/json`
- **Schema:** `CustomQuestionnaireResponse`

```json
{
  "id": 498,
  "created": "2021-11-25T12:45:45+13:00",
  "modified": "2021-11-25T12:51:48+13:00",
  "active": true,
  "name": "Health Check",
  "record_type": "visitor",
  "setup_by": 823,
  "frequency": "every",
  "frequency_days": 1,
  "location_id": 301
}
```

- **Content-Type:** `application/xml`
- **Schema:** `CustomQuestionnaireResponse`

#### 403 — Access denied to this resource.

#### 404 — Resource not found.

---

## Tag: Custom questions

A custom questionnaire is made up of a set of custom questions. Use the API to list all custom questions created for your organization. View details including the question type (eg multi-choice, waiver, date picker) and the settings (eg if it's mandatory, if answer share is enabled, and who set it up).

Learn more about custom questions in the [OnLocation Help Center](https://onlocation.elevio.help/en/articles/521).

## GET /custom-questionnaire/question — List all custom questions

Returns a list of all custom questions. The endpoint is limited to a maximum of 5,000 records.

A comma separated list of tag:value search parameters, a colon separator performs an exact match, a percent separator performs a starts-with search and greater/less than search number/date ranges.

### Parameters
- **q** (query · string): Filter by any custom questionnaire question value. (example: `498`)
- **order** (query · string): Specifies an element to order by. To order in reverse order, prepend the element with '-'. (example: `id`)
- **limit** (query · integer(int32)): Limits the amount of records to return (example: `10`)
- **page** (query · integer(int32)): Specifies which page of results to return, most effective when used together with the 'limit' parameter
- **If-Modified-Since** (header · string(int32)): A UTC timestamp (yyyy-mm-ddThh:mm:ss). Only entities created or modified since this timestamp will be returned e.g. 2009-11-12T00:00:00

### Responses

#### 200 — List of custom questions
- **Content-Type:** `application/json`
- **Schema:** `array of CustomQuestionnaireQuestionResponse`

```json
[
  {
    "id": 976,
    "created": "2021-11-25T12:51:26+13:00",
    "modified": "2021-11-30T12:51:26+13:00",
    "questionnaire_id": 498,
    "question": "Example Waiver Question",
    "type": "waiver",
    "compulsory": true,
    "answershare": true,
    "order": 9999,
    "parent_id": 974,
    "parent_key": 0,
    "version": 2,
    "setup_by": 855,
    "settings_options": [
      [
        "I acknowledge",
        "I do not acknowledge"
      ]
    ],
    "settings_options_unique": [
      [
        1,
        2
      ]
    ],
    "settings_waiver": "Example waiver",
    "settings_video": "https://www.youtube.com/watch?v=dQw4w9WgXcQ",
    "settings_image": {
      "title": "Waiver Image",
      "thumb": "https://example.url/storage/cache/waverimage_qoiph7panWllx3_MTIwMCwxMjAw.png",
      "full": "https://example.url/storage/cache/waverimage_qoiph7panWllx3_MTIwMCwxMjAw.png"
    },
    "settings_signature": true,
    "settings_copy": true,
    "settings_copy_host": true,
    "settings_send_recipients": [
      [
        554,
        2754
      ]
    ],
    "settings_email_non_staff": "example-email@whosonlocation.com"
  }
]
```

- **Content-Type:** `application/xml`
- **Schema:** `array of CustomQuestionnaireQuestionResponse`

#### 403 — Access denied to this resource

#### 404 — Resource not found

---

## GET /custom-questionnaire/question/{id} — Retrieve a custom question

Returns a custom question record filtered by the provided ID.

### Parameters
- **id** (path · integer(int32) · required): ID of the question record to look for

### Responses

#### 200 — The custom question record matching the passed in ID
- **Content-Type:** `application/json`
- **Schema:** `CustomQuestionnaireQuestionResponse`

```json
{
  "id": 976,
  "created": "2021-11-25T12:51:26+13:00",
  "modified": "2021-11-30T12:51:26+13:00",
  "questionnaire_id": 498,
  "question": "Example Waiver Question",
  "type": "waiver",
  "compulsory": true,
  "answershare": true,
  "order": 9999,
  "parent_id": 974,
  "parent_key": 0,
  "version": 2,
  "setup_by": 855,
  "settings_options": [
    [
      "I acknowledge",
      "I do not acknowledge"
    ]
  ],
  "settings_options_unique": [
    [
      1,
      2
    ]
  ],
  "settings_waiver": "Example waiver",
  "settings_video": "https://www.youtube.com/watch?v=dQw4w9WgXcQ",
  "settings_image": {
    "title": "Waiver Image",
    "thumb": "https://example.url/storage/cache/waverimage_qoiph7panWllx3_MTIwMCwxMjAw.png",
    "full": "https://example.url/storage/cache/waverimage_qoiph7panWllx3_MTIwMCwxMjAw.png"
  },
  "settings_signature": true,
  "settings_copy": true,
  "settings_copy_host": true,
  "settings_send_recipients": [
    [
      554,
      2754
    ]
  ],
  "settings_email_non_staff": "example-email@whosonlocation.com"
}
```

- **Content-Type:** `application/xml`
- **Schema:** `CustomQuestionnaireQuestionResponse`

#### 403 — Access denied to this resource.

#### 404 — Resource not found.

---

## Tag: Custom questionnaire submissions

Returns a list of all custom questionnaire submissions (excluding the answers). Includes location, kiosk, who submitted the answer, where the movement took place, date and time.

## GET /custom-questionnaire/submission — List all custom questionnaire submissions

Returns a list of custom questionnaire submission records. The endpoint is limited to a maximum of 10,000 records.

A comma separated list of tag:value search parameters, a colon separator performs an exact match, a percent separator performs a starts-with search and greater/less than search number/date ranges.

### Parameters
- **q** (query · string): Filter by any custom questionnaire submission. (example: `498`)
- **order** (query · string): Specifies an element to order by. To order in reverse order, prepend the element with '-'. (example: `-questionnaire_id`)
- **limit** (query · integer(int32)): Limits the amount of records to return (example: `10`)
- **page** (query · integer(int32)): Specifies which page of results to return, most effective when used together with the 'limit' parameter
- **If-Modified-Since** (header · string(int32)): A UTC timestamp (yyyy-mm-ddThh:mm:ss). Only entities created or modified since this timestamp will be returned e.g. 2009-11-12T00:00:00

### Responses

#### 200 — List of custom questionnaire submission records
- **Content-Type:** `application/json`
- **Schema:** `array of CustomQuestionnaireSubmissionResponse`

```json
[
  {
    "id": 1740,
    "created": "2021-11-25T14:47:34+13:00",
    "modified": "2021-12-11T14:47:34+13:00",
    "questionnaire_id": 498,
    "sp_member_id": 55,
    "visitor_id": 138,
    "staff_id": 7,
    "movement_id": 18,
    "source": "kiosk",
    "onsite_status": "onsite"
  }
]
```

- **Content-Type:** `application/xml`
- **Schema:** `array of CustomQuestionnaireSubmissionResponse`

#### 403 — Access denied to this resource.

#### 404 — No records are found matching the passed in filters

---

## GET /custom-questionnaire/submission/{id} — Retrieve a custom questionnaire submission

Returns a custom questionnaire submission record filtered by the passed in ID.

### Parameters
- **id** (path · integer(int32) · required): ID of the custom questionnaire submission to look for

### Responses

#### 200 — The custom questionnaire submission element matching the passed in ID
- **Content-Type:** `application/json`
- **Schema:** `CustomQuestionnaireSubmissionResponse`

```json
{
  "id": 1740,
  "created": "2021-11-25T14:47:34+13:00",
  "modified": "2021-12-11T14:47:34+13:00",
  "questionnaire_id": 498,
  "sp_member_id": 55,
  "visitor_id": 138,
  "staff_id": 7,
  "movement_id": 18,
  "source": "kiosk",
  "onsite_status": "onsite"
}
```

- **Content-Type:** `application/xml`
- **Schema:** `CustomQuestionnaireSubmissionResponse`

#### 403 — Access denied to this resource.

#### 404 — Resource not found.

---

## Tag: Custom question answers

When someone answers a custom question in a questionnaire submission, OnLocation records their submission and links it to the questionnaire submission ID and custom question ID.

Use the API to view the answers for all or specific custom questions.

## GET /custom-questionnaire/submission/answer — List all custom question answers

Returns a list of custom questionnaire submission answer records. The endpoint is limited to a maximum of 10,000 records.

A comma separated list of tag:value search parameters, a colon separator performs an exact match, a percent separator performs a starts-with search and greater/less than search number/date ranges.

### Parameters
- **q** (query · string): Filter by any custom questionnaire submission answer. (example: `submision_id:0`)
- **order** (query · string): Specifies an element to order by. To order in reverse order, prepend the element with '-'. (example: `question_id: 982`)
- **limit** (query · integer(int32)): Limits the amount of records to return (example: `10`)
- **page** (query · integer(int32)): Specifies which page of results to return, most effective when used together with the 'limit' parameter
- **If-Modified-Since** (header · string(int32)): A UTC timestamp (yyyy-mm-ddThh:mm:ss). Only entities created or modified since this timestamp will be returned e.g. 2009-11-12T00:00:00

### Responses

#### 200 — List of custom question answer records
- **Content-Type:** `application/json`
- **Schema:** `array of CustomQuestionnaireSubmissionAnswerResponse`

```json
[
  {
    "id": 3155,
    "created": "2021-11-25T15:01:29+13:00",
    "modified": "2021-12-25T15:01:29+13:00",
    "submission_id": 1741,
    "question_id": 982,
    "answer": "I acknowledge",
    "signature_url": "https://example.url/storage/signature3152png_zr75U796wybjTZ.png",
    "answer_multi": [
      [
        "First floor",
        "Second floor"
      ]
    ]
  }
]
```

- **Content-Type:** `application/xml`
- **Schema:** `array of CustomQuestionnaireSubmissionAnswerResponse`

#### 403 — Access denied to this resource.

#### 404 — Resource not found.

---

## GET /custom-questionnaire/submission/answer/{id} — Retrieve a custom question answer

Returns a custom question answer record filtered by the passed in ID.

### Parameters
- **id** (path · integer(int32) · required): ID of the custom questionnaire submission to look for

### Responses

#### 200 — The custom question answer element matching the passed in ID
- **Content-Type:** `application/json`
- **Schema:** `CustomQuestionnaireSubmissionAnswerResponse`

```json
{
  "id": 3155,
  "created": "2021-11-25T15:01:29+13:00",
  "modified": "2021-12-25T15:01:29+13:00",
  "submission_id": 1741,
  "question_id": 982,
  "answer": "I acknowledge",
  "signature_url": "https://example.url/storage/signature3152png_zr75U796wybjTZ.png",
  "answer_multi": [
    [
      "First floor",
      "Second floor"
    ]
  ]
}
```

- **Content-Type:** `application/xml`
- **Schema:** `CustomQuestionnaireSubmissionAnswerResponse`

#### 403 — Access denied to this resource.

#### 404 — Resource not found.

---

## Tag: Employees

Each employee has a profile in OnLocation. It contains their name, contact information, locations, department, user roles and role types, and tokens.

Use the employee API to return a list of all employees, add, update or delete employees, retrieve photos, and sign employees in or out.We recommend that bulk employee changes are managed using our SyncPortal integration rather than the API. Check the [OnLocation Help Center](https://onlocation.elevio.help/en/articles/592) for more information.

## GET /staff — List all employees

Retrieves a list of all employees.

A comma separated list of tag:value search parameters, a colon separator performs an exact match, a percent separator performs a starts-with search and greater/less than search number/date ranges.

A person can be matched to an external token which has been assigned to their profile such as RFID card number, a security turnstile controller may search for a staff account to update the onsite status, eg q=token:C1BB7F.

### Parameters
- **q** (query · string): Filter by any employee value. It is not possible to filter by custom field elements at this point. (example: `location:sydney,name%john,last_login>2015-02-01`)
- **order** (query · string): Order by any element returned, prefix with a - to reverse order. (example: `id`)
- **limit** (query · integer(int32)): Limits the amount of records to return. (example: `10`)
- **If-Modified-Since** (header · string(int32)): A UTC timestamp (yyyy-mm-ddThh:mm:ss) . Only entities created or modified since this timestamp will be returned e.g. 2009-11-12T00:00:00

### Responses

#### 200 — List of employees
- **Content-Type:** `application/json`
- **Schema:** `array of StaffResponse`

```json
[
  {
    "globalroaming_locations": [
      {
        "id": 1,
        "name": "Head Office"
      }
    ],
    "id": 823562,
    "created": "2021-10-06T15:35:50+13:00",
    "modified": "2021-10-06T16:35:50+13:00",
    "account_status": "new",
    "last_login": "2021-11-15T16:35:50+13:00",
    "onsite_status": "offsite",
    "name": "Staff Name",
    "title": "Mr",
    "altname": "Daffy",
    "email": "john.doe@example.org",
    "phone": "+1 206 555 0123",
    "mobile": "+1 206 555 0123",
    "ice": "+1 206 555 0123",
    "setup_method": "manual",
    "employee_id": "1005",
    "location": "Head Office",
    "cur_location": "Head Office",
    "department": "Administration",
    "roles": [
      [
        "Safety Office",
        "Fire Warden"
      ]
    ],
    "role_types": [
      [
        "Non-Host",
        "Safety Operator"
      ]
    ],
    "tokens": [
      {
        "type": "",
        "number": 0,
        "issued": "",
        "expiry": ""
      }
    ],
    "remote": 1,
    "customfields": {
      "123": "custom-field value"
    },
    "globalroaming": 1
  }
]
```

- **Content-Type:** `application/xml`
- **Schema:** `array of StaffResponse`

#### 403 — Access denied to this resource

#### 404 — Resource not found

#### 500 — Internal server error

---

## PUT /staff — Update multiple employees

Updates a list of multiple employees.

The request body contains a bulk staff list which will be processed by OnLocation for adds/updates/deletes. Set the Content-Type header to identify the the format of the bulk data.

Tokens can be imported by supplying at least tokenXtype and tokenXnumber columns where X is a arbitrary number, ie token1type. Token issued and expires will only be imported if set, multiple tokens are designated by using a different number for each set.

### Parameters
- **group** (query · string): If uploading a subset of all employees then a group name is mandatory, this allows OnLocation to properly track removed items.
- **location** (query · string): Override location for all uploaded employees.
- **content type** (header · integer(int32)): The format of the uploaded bulk, must be one of:

- Text/csv - comma separated values (,)
- Text/psv - pipe separated values (|)
- Application/json - JSON array type containing one or more JSON objects
- Application/xml - XML, a root node with one or more elements

### Request Body
Information used to update an employee or list of employees:
**Required:** True

- **Content-Type:** `application/json`
- **Schema:** `array of StaffRequest`

```json
[
  {
    "name": "Staff Name",
    "title": "Mr",
    "altname": "Daffy",
    "email": "example.staff@whosonlocation.com",
    "phone": "+1 206 555 0123",
    "mobile": "+1 206 555 0123",
    "ice": "+1 206 555 0123",
    "employee_id": 301,
    "onsite_status": "offsite",
    "location": "Head Office",
    "roletype": [
      [
        "Non-Host",
        "Safety Operator"
      ]
    ],
    "customfields": [
      [
        "Deprecated, please use the cf-parameter instead"
      ]
    ],
    "cf": {
      "123": "custom-field value"
    },
    "globalroaming": 1,
    "globalroaming_locations": [
      [
        1500,
        3000,
        2600
      ]
    ]
  }
]
```


### Responses

#### 200 — Updated employee record
- **Content-Type:** `application/json`
- **Schema:** `array of StaffResponse`

```json
[
  {
    "globalroaming_locations": [
      {
        "id": 1,
        "name": "Head Office"
      }
    ],
    "id": 823562,
    "created": "2021-10-06T15:35:50+13:00",
    "modified": "2021-10-06T16:35:50+13:00",
    "account_status": "new",
    "last_login": "2021-11-15T16:35:50+13:00",
    "onsite_status": "offsite",
    "name": "Staff Name",
    "title": "Mr",
    "altname": "Daffy",
    "email": "john.doe@example.org",
    "phone": "+1 206 555 0123",
    "mobile": "+1 206 555 0123",
    "ice": "+1 206 555 0123",
    "setup_method": "manual",
    "employee_id": "1005",
    "location": "Head Office",
    "cur_location": "Head Office",
    "department": "Administration",
    "roles": [
      [
        "Safety Office",
        "Fire Warden"
      ]
    ],
    "role_types": [
      [
        "Non-Host",
        "Safety Operator"
      ]
    ],
    "tokens": [
      {
        "type": "",
        "number": 0,
        "issued": "",
        "expiry": ""
      }
    ],
    "remote": 1,
    "customfields": {
      "123": "custom-field value"
    },
    "globalroaming": 1
  }
]
```

#### 403 — Access denied to this resource

#### 500 — Internal server error

---

## POST /staff — Create an employee

Creates a new employee profile

### Parameters
- **Content-Type** (header · integer(int32) · required): The format of the uploaded image data. It must be one of image/png or image/jpeg.

### Request Body
Information used to create an employee:
**Required:** True

- **Content-Type:** `application/json`
- **Schema:** `StaffRequest`

```json
{
  "name": "Staff Name",
  "title": "Mr",
  "altname": "Daffy",
  "email": "example.staff@whosonlocation.com",
  "phone": "+1 206 555 0123",
  "mobile": "+1 206 555 0123",
  "ice": "+1 206 555 0123",
  "employee_id": 301,
  "onsite_status": "offsite",
  "location": "Head Office",
  "roletype": [
    [
      "Non-Host",
      "Safety Operator"
    ]
  ],
  "customfields": [
    [
      "Deprecated, please use the cf-parameter instead"
    ]
  ],
  "cf": {
    "123": "custom-field value"
  },
  "globalroaming": 1,
  "globalroaming_locations": [
    [
      1500,
      3000,
      2600
    ]
  ]
}
```


### Responses

#### 201 — Employee created
- **Content-Type:** `application/json`
- **Schema:** `StaffResponse`

```json
{
  "globalroaming_locations": [
    {
      "id": 1,
      "name": "Head Office"
    }
  ],
  "id": 823562,
  "created": "2021-10-06T15:35:50+13:00",
  "modified": "2021-10-06T16:35:50+13:00",
  "account_status": "new",
  "last_login": "2021-11-15T16:35:50+13:00",
  "onsite_status": "offsite",
  "name": "Staff Name",
  "title": "Mr",
  "altname": "Daffy",
  "email": "john.doe@example.org",
  "phone": "+1 206 555 0123",
  "mobile": "+1 206 555 0123",
  "ice": "+1 206 555 0123",
  "setup_method": "manual",
  "employee_id": "1005",
  "location": "Head Office",
  "cur_location": "Head Office",
  "department": "Administration",
  "roles": [
    [
      "Safety Office",
      "Fire Warden"
    ]
  ],
  "role_types": [
    [
      "Non-Host",
      "Safety Operator"
    ]
  ],
  "tokens": [
    {
      "type": "",
      "number": 0,
      "issued": "",
      "expiry": ""
    }
  ],
  "remote": 1,
  "customfields": {
    "123": "custom-field value"
  },
  "globalroaming": 1
}
```

#### 403 — Access denied to this resource

#### 409 — Staff already exists as

#### 500 — Internal server error

---

## GET /staff/{id}/photo — Retrieve employee photo

Retrieves an employee's image, filtering with the provided ID. The request body contains the binary image data in the format specified by the Content-Type header.

### Parameters
- **id** (path · integer(int32) · required): ID of the employee

### Responses

#### 200 — The employee's photo, if they have one

#### 403 — Access denied to resource

#### 404 — Resource not found

#### 500 — Internal server error

---

## PUT /staff/{id}/photo — Update an employee photo

Updates an employee profile image.

### Parameters
- **id** (path · integer(int32) · required): ID of the employee to update
- **Content-Type** (header · string · required): The format of the uploaded image data, must be image/png or image/jpg

### Responses

#### 204 — Updated photo

#### 403 — Access denied to this resource

#### 404 — Employee not found

#### 500 — Internal server error

---

## HEAD /staff/{id}/photo — Retrieve employee photo metadata

Retrieves a single employee's profile image metadata.'

### Parameters
- **id** (path · integer(int32) · required): ID of the employee

### Responses

#### 200 — Employee profile image data
- **Content-Type:** `image/png`
- **Schema:** `object`

- **Content-Type:** `image/jpeg`
- **Schema:** `object`

#### 403 — Access denied to resource

#### 404 — Resource not found

#### 500 — Internal server error

---

## GET /staff/{id} — Retrieve an employee

Retrieves a single employee record, filtering with the provided ID.

A person can be matched to an external token which has been assigned to their profile such as RFID card number, a security turnstile controller may search for a staff account to update the onsite status, eg q=token:C1BB7F.

### Parameters
- **id** (path · integer(int32) · required): ID of the employee record to retrieve

### Responses

#### 200 — Employee matching the provided ID
- **Content-Type:** `application/json`
- **Schema:** `StaffResponse`

```json
{
  "globalroaming_locations": [
    {
      "id": 1,
      "name": "Head Office"
    }
  ],
  "id": 823562,
  "created": "2021-10-06T15:35:50+13:00",
  "modified": "2021-10-06T16:35:50+13:00",
  "account_status": "new",
  "last_login": "2021-11-15T16:35:50+13:00",
  "onsite_status": "offsite",
  "name": "Staff Name",
  "title": "Mr",
  "altname": "Daffy",
  "email": "john.doe@example.org",
  "phone": "+1 206 555 0123",
  "mobile": "+1 206 555 0123",
  "ice": "+1 206 555 0123",
  "setup_method": "manual",
  "employee_id": "1005",
  "location": "Head Office",
  "cur_location": "Head Office",
  "department": "Administration",
  "roles": [
    [
      "Safety Office",
      "Fire Warden"
    ]
  ],
  "role_types": [
    [
      "Non-Host",
      "Safety Operator"
    ]
  ],
  "tokens": [
    {
      "type": "",
      "number": 0,
      "issued": "",
      "expiry": ""
    }
  ],
  "remote": 1,
  "customfields": {
    "123": "custom-field value"
  },
  "globalroaming": 1
}
```

- **Content-Type:** `application/xml`
- **Schema:** `StaffResponse`

#### 403 — Access denied to resource

#### 404 — Resource not found

#### 500 — Internal server error

---

## PUT /staff/{id} — Update an employee

Updates a one or more employee profiles.

### Parameters
- **id** (path · integer(int32) · required): ID of the employee or employees to update

### Request Body
Information used to update an employee or list of employees:
**Required:** True

- **Content-Type:** `application/json`
- **Schema:** `StaffRequest`

```json
{
  "name": "Staff Name",
  "title": "Mr",
  "altname": "Daffy",
  "email": "example.staff@whosonlocation.com",
  "phone": "+1 206 555 0123",
  "mobile": "+1 206 555 0123",
  "ice": "+1 206 555 0123",
  "employee_id": 301,
  "onsite_status": "offsite",
  "location": "Head Office",
  "roletype": [
    [
      "Non-Host",
      "Safety Operator"
    ]
  ],
  "customfields": [
    [
      "Deprecated, please use the cf-parameter instead"
    ]
  ],
  "cf": {
    "123": "custom-field value"
  },
  "globalroaming": 1,
  "globalroaming_locations": [
    [
      1500,
      3000,
      2600
    ]
  ]
}
```


### Responses

#### 204 — Updated employee record

#### 403 — Access denied to this resource

#### 500 — Internal server error

---

## DELETE /staff/{id} — Delete an employee

Removes the specified employee.

### Parameters
- **id** (path · integer(int32) · required): ID of the employee to delete

### Responses

#### 204 — Employee deleted

#### 403 — Access denied to resource

#### 404 — Resource not found

#### 500 — Internal server error

---

## GET /staff/roletype — List employee role types

Returns all employee role types for the organization.

Without location_id filter: returns each role type with a locations array showing which locations it has been assigned to.

With q=location_id:N filter: returns only the role types assigned to that location (flat list, no locations array).

### Parameters
- **q** (query · string): Filter parameters. Use location_id:N to return only roles for a specific location. (example: `location_id:1`)
- **order** (query · string): Specifies an element to order by. Prefix with - to reverse order. (example: `name`)
- **limit** (query · integer(int32)): Limits the number of records to return. (example: `100`)
- **If-Modified-Since** (header · string): A UTC timestamp (yyyy-mm-ddThh:mm:ss). Only entities created or modified since this timestamp will be returned.

### Responses

#### 200 — List of employee role types
- **Content-Type:** `application/json`
- **Schema:** `array of StaffRoleTypeResponse`

```json
[
  {
    "id": 5,
    "name": "Account Manager",
    "created": "2025-01-08T06:04:00+00:00",
    "modified": "2025-06-01T10:00:00+00:00",
    "locations": [
      {
        "id": 1,
        "name": "Head Office"
      }
    ]
  }
]
```

- **Content-Type:** `application/xml`
- **Schema:** `array of StaffRoleTypeResponse`

#### 400 — Invalid input

#### 403 — Access denied to this resource

#### 500 — Internal server error

---

## Tag: Employee movements

Each time an employee signs in or out, or moves between zones, they create a movement. Use the API to return all employee movements, or add or update a movement record.

## GET /staff/movement — List all employee movements

Retrieves a list of all employee sign in/sign out events.

A comma separated list of tag:value search parameters, a colon separator performs an exact match, a percent separator performs a starts-with search and greater/less than search number/date ranges.

### Parameters
- **q** (query · string): Filter by any employee movement. (example: `staff_id:1234`)
- **order** (query · string): Order by any element returned, prefix with a - to reverse order. (example: `staff_id`)
- **limit** (query · integer(int32)): Limits the amount of records to return. (example: `10`)
- **If-Modified-Since** (header · string(int32)): A UTC timestamp (yyyy-mm-ddThh:mm:ss). Only entities created or modified since this timestamp will be returned e.g. 2009-11-12T00:00:00

### Responses

#### 200 — List of staff movement records
- **Content-Type:** `application/json`
- **Schema:** `array of StaffMovementResponse`

```json
[
  {
    "id": 167503,
    "created": "2021-09-09T12:52:31+12:00",
    "modified": "2021-09-09T13:52:31+12:00",
    "location_id": 301,
    "staff_id": 823431,
    "signed_in": "2021-09-09T12:52:31+12:00",
    "signed_out": "2021-09-09T14:52:31+12:00",
    "mode_in": "Sign In/Out Manager",
    "mode_out": "Sign In/Out Manager",
    "signed_in_by": 823431,
    "signed_out_by": 823431,
    "scanned_in": "",
    "scanned_out": "",
    "expected": true,
    "assistance": false,
    "accessdenied": false,
    "loneworker": true,
    "so_staff_id": 584,
    "zone_id": 8,
    "interzone": true,
    "remote": false,
    "name": "Harold Brunning",
    "title": "Mr",
    "email": "staff.movement@whosonlocation.com",
    "mobile": "+1 206 555 0123",
    "phone": "+1 206 555 0123",
    "lacp_in_name": "Ground floor entry",
    "lacp_out_name": "Ground floor entry",
    "signed_in_lat": "Latitude-value of coordinates for sign-in (Decimal Degrees)",
    "signed_in_lon": "Longitude-value of coordinates for sign-in (Decimal Degrees)",
    "signed_in_accuracy": "Precision for sign-in coordinates",
    "signed_out_lat": "Latitude-value of coordinates for sign-out (Decimal Degrees)",
    "signed_out_lon": "Longitude-value of coordinates for sign-out (Decimal Degrees)",
    "signed_out_accuracy": "Precision for sign-out coordinates"
  }
]
```

- **Content-Type:** `application/xml`
- **Schema:** `array of StaffMovementResponse`

#### 403 — Access denied to this resource

#### 404 — Resource not found

#### 500 — Internal server error

---

## PUT /staff/movement — Create or update an employee movement

Creates or updates an employee movement record.

### Request Body
Data required to create or update an employee movement:
**Required:** True

- **Content-Type:** `application/json`
- **Schema:** `StaffMovementRequest`

```json
{
  "onsite_status": "onsite",
  "location_id": 38,
  "location": "Head Office",
  "staff_id": 201,
  "lacp_id": 201,
  "zone_id": 201,
  "remote": false,
  "changed_at": "2021-11-25 22:01:09",
  "print_kiosk_id": 301
}
```


### Responses

#### 200 — Updated movement
- **Content-Type:** `application/json`
- **Schema:** `StaffMovementResponse`

```json
{
  "id": 167503,
  "created": "2021-09-09T12:52:31+12:00",
  "modified": "2021-09-09T13:52:31+12:00",
  "location_id": 301,
  "staff_id": 823431,
  "signed_in": "2021-09-09T12:52:31+12:00",
  "signed_out": "2021-09-09T14:52:31+12:00",
  "mode_in": "Sign In/Out Manager",
  "mode_out": "Sign In/Out Manager",
  "signed_in_by": 823431,
  "signed_out_by": 823431,
  "scanned_in": "",
  "scanned_out": "",
  "expected": true,
  "assistance": false,
  "accessdenied": false,
  "loneworker": true,
  "so_staff_id": 584,
  "zone_id": 8,
  "interzone": true,
  "remote": false,
  "name": "Harold Brunning",
  "title": "Mr",
  "email": "staff.movement@whosonlocation.com",
  "mobile": "+1 206 555 0123",
  "phone": "+1 206 555 0123",
  "lacp_in_name": "Ground floor entry",
  "lacp_out_name": "Ground floor entry",
  "signed_in_lat": "Latitude-value of coordinates for sign-in (Decimal Degrees)",
  "signed_in_lon": "Longitude-value of coordinates for sign-in (Decimal Degrees)",
  "signed_in_accuracy": "Precision for sign-in coordinates",
  "signed_out_lat": "Latitude-value of coordinates for sign-out (Decimal Degrees)",
  "signed_out_lon": "Longitude-value of coordinates for sign-out (Decimal Degrees)",
  "signed_out_accuracy": "Precision for sign-out coordinates"
}
```

- **Content-Type:** `application/xml`
- **Schema:** `StaffMovementResponse`

#### 403 — Access denied to this resource

#### 500 — Internal server error

---

## GET /staff/movement/{id} — Retrieve an employee movement

Retrieves a single employee movement record, filtering with the provided ID.

### Parameters
- **id** (path · integer(int32) · required): ID of the employee movement record to retrieve

### Responses

#### 200 — A staff movement record
- **Content-Type:** `application/json`
- **Schema:** `StaffMovementResponse`

```json
{
  "id": 167503,
  "created": "2021-09-09T12:52:31+12:00",
  "modified": "2021-09-09T13:52:31+12:00",
  "location_id": 301,
  "staff_id": 823431,
  "signed_in": "2021-09-09T12:52:31+12:00",
  "signed_out": "2021-09-09T14:52:31+12:00",
  "mode_in": "Sign In/Out Manager",
  "mode_out": "Sign In/Out Manager",
  "signed_in_by": 823431,
  "signed_out_by": 823431,
  "scanned_in": "",
  "scanned_out": "",
  "expected": true,
  "assistance": false,
  "accessdenied": false,
  "loneworker": true,
  "so_staff_id": 584,
  "zone_id": 8,
  "interzone": true,
  "remote": false,
  "name": "Harold Brunning",
  "title": "Mr",
  "email": "staff.movement@whosonlocation.com",
  "mobile": "+1 206 555 0123",
  "phone": "+1 206 555 0123",
  "lacp_in_name": "Ground floor entry",
  "lacp_out_name": "Ground floor entry",
  "signed_in_lat": "Latitude-value of coordinates for sign-in (Decimal Degrees)",
  "signed_in_lon": "Longitude-value of coordinates for sign-in (Decimal Degrees)",
  "signed_in_accuracy": "Precision for sign-in coordinates",
  "signed_out_lat": "Latitude-value of coordinates for sign-out (Decimal Degrees)",
  "signed_out_lon": "Longitude-value of coordinates for sign-out (Decimal Degrees)",
  "signed_out_accuracy": "Precision for sign-out coordinates"
}
```

- **Content-Type:** `application/xml`
- **Schema:** `StaffMovementResponse`

#### 403 — Access denied to resource

#### 404 — Resource not found

#### 500 — Internal server error

---

## Tag: Induction courses

Induction courses are to record if an employee or contractor has completed required training. There are four types of induction course in OnLocation: internal, external, eLearning and SCORM Cloud. The first two options are used to record that training has been completed; last two options are used to deliver online training. An induction course record includes the basic course information, it doesn't include any course content.

Use the induction API to retrieve your induction course list, create an induction course, and update or delete an induction course. You can also return a list of learners that hold a specific induction, add inductions to employee or contractor profiles, and update or delete a specific learner record.

Learn more about induction courses in the [OnLocation Help Center](https://onlocation.elevio.help/en/articles/639).

## GET /induction — List all induction courses

Returns a list of all induction courses.

A comma separated list of tag:value search parameters, a colon separator performs an exact match, a percent separator performs a starts-with search and greater/less than search number/date ranges.

### Parameters
- **q** (query · string): Filter by induction course field. (example: `type:external`)

### Responses

#### 200 — List of induction courses
- **Content-Type:** `application/json`
- **Schema:** `array of InductionResponse`

```json
[
  {
    "id": 184,
    "created": "2021-11-03T13:19:14+13:00",
    "modified": "2021-11-10T12:22:08+13:00",
    "setup_by": 773,
    "name": "Employee induction course",
    "type": "online",
    "description": "Induction course description",
    "status": "inactive",
    "audience_staff": "all",
    "audience_sp": "all",
    "renew": 12,
    "attempts": 1,
    "inductees": 3,
    "audience_staff_member": [
      {
        "id": 556,
        "name": "Example Name"
      }
    ],
    "audience_sp_member": [
      {
        "id": 556,
        "name": "Example Name"
      }
    ]
  }
]
```

- **Content-Type:** `application/xml`
- **Schema:** `array of InductionResponse`

#### 403 — Access denied to this resource

#### 404 — Resource not found

---

## POST /induction — Create an induction course

Create a new induction course, including the name, description, audience, and renewal settings.

### Request Body
Induction course details:
**Required:** True

- **Content-Type:** `application/json`
- **Schema:** `InductionRequest`

```json
{
  "name": "Employee induction course",
  "type": "internal/external",
  "description": "Induction course description",
  "status": "inactive",
  "audience_staff": "all/location/department/role/none",
  "audience_sp": "all/location/group/role/none",
  "renew": 12,
  "audience_staff_member": [
    [
      281,
      533
    ]
  ],
  "audience_sp_member": [
    [
      281,
      533
    ]
  ]
}
```


### Responses

#### 201 — Induction course created
- **Content-Type:** `application/json`
- **Schema:** `InductionResponse`

```json
{
  "id": 184,
  "created": "2021-11-03T13:19:14+13:00",
  "modified": "2021-11-10T12:22:08+13:00",
  "setup_by": 773,
  "name": "Employee induction course",
  "type": "online",
  "description": "Induction course description",
  "status": "inactive",
  "audience_staff": "all",
  "audience_sp": "all",
  "renew": 12,
  "attempts": 1,
  "inductees": 3,
  "audience_staff_member": [
    {
      "id": 556,
      "name": "Example Name"
    }
  ],
  "audience_sp_member": [
    {
      "id": 556,
      "name": "Example Name"
    }
  ]
}
```

#### 403 — Access denied to this resource

#### 409 — Induction already exists with name

#### 500 — Internal server error

---

## GET /induction/{id} — Retrieve an induction course

Retrieves a single induction course, filtering with the provided ID.

### Parameters
- **id** (path · integer(int32) · required): ID of the induction to retrieve

### Responses

#### 200 — Course returned
- **Content-Type:** `application/json`
- **Schema:** `InductionResponse`

```json
{
  "id": 184,
  "created": "2021-11-03T13:19:14+13:00",
  "modified": "2021-11-10T12:22:08+13:00",
  "setup_by": 773,
  "name": "Employee induction course",
  "type": "online",
  "description": "Induction course description",
  "status": "inactive",
  "audience_staff": "all",
  "audience_sp": "all",
  "renew": 12,
  "attempts": 1,
  "inductees": 3,
  "audience_staff_member": [
    {
      "id": 556,
      "name": "Example Name"
    }
  ],
  "audience_sp_member": [
    {
      "id": 556,
      "name": "Example Name"
    }
  ]
}
```

- **Content-Type:** `application/xml`
- **Schema:** `InductionResponse`

#### 403 — Access denied to resource

#### 404 — Resource not found

---

## PUT /induction/{id} — Update an induction course

Updates an induction course based on the provided ID.

### Parameters
- **id** (path · integer(int32) · required): ID of the induction course to update

### Request Body
Induction to save to the database
**Required:** True

- **Content-Type:** `application/json`
- **Schema:** `InductionRequest`

```json
{
  "name": "Employee induction course",
  "type": "internal/external",
  "description": "Induction course description",
  "status": "inactive",
  "audience_staff": "all/location/department/role/none",
  "audience_sp": "all/location/group/role/none",
  "renew": 12,
  "audience_staff_member": [
    [
      281,
      533
    ]
  ],
  "audience_sp_member": [
    [
      281,
      533
    ]
  ]
}
```


### Responses

#### 200 — Updated induction course
- **Content-Type:** `application/json`
- **Schema:** `array of InductionResponse`

```json
[
  {
    "id": 184,
    "created": "2021-11-03T13:19:14+13:00",
    "modified": "2021-11-10T12:22:08+13:00",
    "setup_by": 773,
    "name": "Employee induction course",
    "type": "online",
    "description": "Induction course description",
    "status": "inactive",
    "audience_staff": "all",
    "audience_sp": "all",
    "renew": 12,
    "attempts": 1,
    "inductees": 3,
    "audience_staff_member": [
      {
        "id": 556,
        "name": "Example Name"
      }
    ],
    "audience_sp_member": [
      {
        "id": 556,
        "name": "Example Name"
      }
    ]
  }
]
```

- **Content-Type:** `application/xml`
- **Schema:** `array of InductionResponse`

#### 403 — Access denied to this resource

#### 409 — induction already exists with name

#### 500 — Internal server error

---

## DELETE /induction/{id} — Delete an induction course

Deletes the specified induction course.

### Parameters
- **id** (path · integer(int32) · required): ID of the induction course to delete

### Responses

#### 204 — Induction course deleted

#### 403 — Access denied to resource

#### 404 — Resource not found

---

## Tag: Induction holders

An induction holder is someone who has completed an induction course. Induction holder records include the learner's name, email, status of the course, the number of attempts, when it's due to be renewed. Use the API to retrieve who has completed each induction course, or create, update or delete an induction holder record.

Learn more about induction holders in the [OnLocation Help Center](https://onlocation.elevio.help/en/articles/631).

## GET /induction/{id}/holder — List all induction holders

Retrieves a list of all employees and contractors that hold the specified induction.

A comma separated list of tag:value search parameters, a colon separator performs an exact match, a percent separator performs a starts-with search and greater/less than search number/date ranges.

### Parameters
- **id** (path · integer(int32) · required): ID of the induction course
- **q** (query · string): Filter by induction holder value. (example: `holders>5`)

### Responses

#### 200 — List of induction holder details
- **Content-Type:** `application/json`
- **Schema:** `array of InductionHolderResponse`

```json
[
  {
    "id": 6856,
    "completed": "2021-11-26T00:00:00+13:00",
    "staff_id": 35,
    "sp_member_id": 642,
    "learner_name": "Robert Jordan",
    "learner_email": "example.email@whosonlocation.com",
    "learner_title": "Mr",
    "learner_from": "OnLocation",
    "status": "passed",
    "attempts": 1,
    "attempts_left": 0,
    "renew": "2022-11-26"
  }
]
```

- **Content-Type:** `application/xml`
- **Schema:** `array of InductionHolderResponse`

#### 403 — Access denied to resource

#### 404 — Resource not found

---

## POST /induction/{id}/holder — Create an induction holder

Creates an induction holder association. If an association already exists, the existing record will be updated.

### Parameters
- **id** (path · integer(int32) · required): ID of the induction course

### Request Body
Information used to create induction holder association:
**Required:** True

- **Content-Type:** `application/json`
- **Schema:** `InductionHolderPostRequest`

```json
{
  "staff_id": 55,
  "sp_member_id": 3001,
  "completed": "2021-11-25T22:01:09.385Z"
}
```


### Responses

#### 201 — Induction created
- **Content-Type:** `application/json`
- **Schema:** `InductionHolderResponse`

```json
{
  "id": 6856,
  "completed": "2021-11-26T00:00:00+13:00",
  "staff_id": 35,
  "sp_member_id": 642,
  "learner_name": "Robert Jordan",
  "learner_email": "example.email@whosonlocation.com",
  "learner_title": "Mr",
  "learner_from": "OnLocation",
  "status": "passed",
  "attempts": 1,
  "attempts_left": 0,
  "renew": "2022-11-26"
}
```

- **Content-Type:** `application/xml`
- **Schema:** `InductionHolderResponse`

#### 403 — Access denied to this resource

#### 409 — member is not part of the inductions audience

#### 500 — Internal server error

---

## GET /induction/{id}/holder/{hid} — Retrieve an induction holder

Retrieves a single induction holder record, filtering with the provided ID.

### Parameters
- **id** (path · integer(int32) · required): ID of the induction course
- **hid** (path · integer(int32) · required): ID of the induction holder association to retrieve

### Responses

#### 200 — Induction holder details
- **Content-Type:** `application/json`
- **Schema:** `InductionHolderResponse`

```json
{
  "id": 6856,
  "completed": "2021-11-26T00:00:00+13:00",
  "staff_id": 35,
  "sp_member_id": 642,
  "learner_name": "Robert Jordan",
  "learner_email": "example.email@whosonlocation.com",
  "learner_title": "Mr",
  "learner_from": "OnLocation",
  "status": "passed",
  "attempts": 1,
  "attempts_left": 0,
  "renew": "2022-11-26"
}
```

- **Content-Type:** `application/xml`
- **Schema:** `InductionHolderResponse`

#### 403 — Access denied to resource

#### 404 — Resource not found

#### 500 — Internal server error

---

## PUT /induction/{id}/holder/{hid} — Update induction holder

Updates an induction holder association

### Parameters
- **id** (path · integer(int32) · required): ID of the induction course
- **hid** (path · integer(int32) · required): ID of the induction holder association

### Request Body
Information used to create Induction holder association:
**Required:** True

- **Content-Type:** `application/json`
- **Schema:** `InductionHolderPutRequest`

```json
{
  "completed": "2021-11-25T22:01:09.385Z",
  "staff_id": "123",
  "sp_member_id": "123"
}
```


### Responses

#### 200 — Updated induction holder
- **Content-Type:** `application/json`
- **Schema:** `InductionHolderResponse`

```json
{
  "id": 6856,
  "completed": "2021-11-26T00:00:00+13:00",
  "staff_id": 35,
  "sp_member_id": 642,
  "learner_name": "Robert Jordan",
  "learner_email": "example.email@whosonlocation.com",
  "learner_title": "Mr",
  "learner_from": "OnLocation",
  "status": "passed",
  "attempts": 1,
  "attempts_left": 0,
  "renew": "2022-11-26"
}
```

- **Content-Type:** `application/xml`
- **Schema:** `InductionHolderResponse`

#### 403 — Access denied to this resource

#### 500 — Internal server error

---

## DELETE /induction/{id}/holder/{hid} — Delete an induction holder

Deletes the specified induction holder association.

### Parameters
- **id** (path · integer(int32) · required): ID of the induction course
- **hid** (path · integer(int32) · required): ID of the induction holder association to delete

### Responses

#### 204 — Induction holder association deleted

#### 403 — Access denied to resource

#### 404 — Resource not found

---

## Tag: Locations

Each OnLocation account is made up of one or more locations. A location record contains the name, contact details, timezone, date format, employee count, and the access points set up for that location.

Use the location API to retrieve your location list, add a new location, or update or delete an existing location.

Learn more about the information that's captured for each location in the [OnLocation Help Center](https://onlocation.elevio.help/en/articles/324).

## GET /location — List all locations

Retrieves a list of all locations.

A comma separated list of tag:value search parameters, a colon separator performs an exact match, a percent separator performs a starts-with search and greater/less than search number/date ranges.

### Parameters
- **q** (query · string): Filter by location details, eg country, staff count (example: `staff_count>200`)

### Responses

#### 200 — List of locations
- **Content-Type:** `application/json`
- **Schema:** `array of LocationResponse`

```json
[
  {
    "id": 301,
    "created": "2021-08-12T09:26:54+12:00",
    "modified": "2021-09-03T09:26:54+12:00",
    "name": "Head Office",
    "address": " 1008 Tawa Terrace, Tawa, Wellington, New Zealand",
    "phone": "+1 206 555 0123",
    "external_id": "998",
    "postcode": "5021",
    "country": "NZ",
    "timezone": "Pacific/Auckland",
    "date_format": "d/m/Y",
    "staff_count": 13,
    "lacps": [
      {
        "id": 240,
        "name": "G1 - Entry"
      }
    ]
  }
]
```

- **Content-Type:** `application/xml`
- **Schema:** `array of LocationResponse`

#### 403 — Access denied to this resource

#### 404 — Resource not found

#### 500 — Internal server error

---

## POST /location — Create a location

Adds a new location to an account.

### Request Body
Location details:
**Required:** True

- **Content-Type:** `application/json`
- **Schema:** `LocationRequest`

```json
{
  "name": "Head Office",
  "address": " 1008 Tawa Terrace, Tawa, Wellington, New Zealand",
  "phone": "+1 206 555 0123",
  "postcode": "5021",
  "country": "NZ",
  "timezone": "Pacific/Auckland",
  "date_format": "d/m/Y"
}
```

- **Content-Type:** `application/xml`
- **Schema:** `LocationRequest`

### Responses

#### 201 — Location created
- **Content-Type:** `application/json`
- **Schema:** `LocationResponse`

```json
{
  "id": 301,
  "created": "2021-08-12T09:26:54+12:00",
  "modified": "2021-09-03T09:26:54+12:00",
  "name": "Head Office",
  "address": " 1008 Tawa Terrace, Tawa, Wellington, New Zealand",
  "phone": "+1 206 555 0123",
  "external_id": "998",
  "postcode": "5021",
  "country": "NZ",
  "timezone": "Pacific/Auckland",
  "date_format": "d/m/Y",
  "staff_count": 13,
  "lacps": [
    {
      "id": 240,
      "name": "G1 - Entry"
    }
  ]
}
```

- **Content-Type:** `application/xml`
- **Schema:** `LocationResponse`

#### 403 — Access denied to this resource

#### 500 — Internal server error

---

## GET /location/{id} — Retrieves a location

Returns the details of a single location, based on the ID provided.

### Parameters
- **id** (path · integer(int32) · required): ID of location to retrieve

### Responses

#### 200 — Location response
- **Content-Type:** `application/json`
- **Schema:** `LocationResponse`

```json
{
  "id": 301,
  "created": "2021-08-12T09:26:54+12:00",
  "modified": "2021-09-03T09:26:54+12:00",
  "name": "Head Office",
  "address": " 1008 Tawa Terrace, Tawa, Wellington, New Zealand",
  "phone": "+1 206 555 0123",
  "external_id": "998",
  "postcode": "5021",
  "country": "NZ",
  "timezone": "Pacific/Auckland",
  "date_format": "d/m/Y",
  "staff_count": 13,
  "lacps": [
    {
      "id": 240,
      "name": "G1 - Entry"
    }
  ]
}
```

- **Content-Type:** `application/xml`
- **Schema:** `LocationResponse`

#### 403 — Access denied to this resource

#### 404 — Resource not found.

#### 500 — Internal server error.

---

## PUT /location/{id} — Update a location

Updates a location's details.

### Parameters
- **id** (path · integer(int32) · required): ID of the location to update

### Request Body
Location details:
**Required:** True

- **Content-Type:** `application/json`
- **Schema:** `LocationRequest`

```json
{
  "name": "Head Office",
  "address": " 1008 Tawa Terrace, Tawa, Wellington, New Zealand",
  "phone": "+1 206 555 0123",
  "postcode": "5021",
  "country": "NZ",
  "timezone": "Pacific/Auckland",
  "date_format": "d/m/Y"
}
```

- **Content-Type:** `application/xml`
- **Schema:** `LocationRequest`

### Responses

#### 204 — Updated location

#### 403 — Access denied to this resource

#### 500 — Internal server error

---

## DELETE /location/{id} — Delete a location

Remove a location from the account.

### Parameters
- **id** (path · integer(int32) · required): ID of location to delete

### Responses

#### 204 — Location deleted

#### 403 — Access denied to resource

#### 404 — Resource not found

---

## Tag: Location departments

Departments are used to assign employees to different operational areas of a location. Use the API to retrieve a list of all departments, create new departments and the locations they apply to, update the department names, or remove them from your account.

Learn more about departments in the [OnLocation Help Center](https://onlocation.elevio.help/en/articles/396).

## GET /department — List all departments

Returns a list of departments for the organization. Each department indicates which locations it has been added to.

### Responses

#### 200 — List of departments
- **Content-Type:** `application/json`
- **Schema:** `array of DepartmentResponse`

```json
[
  {
    "id": 1658,
    "created": "2021-08-12T10:54:13+12:00",
    "modified": "2021-08-13T10:54:13+12:00",
    "name": "Administration",
    "locations": [
      {
        "id": 556,
        "name": "Example Name"
      }
    ]
  }
]
```

- **Content-Type:** `application/xml`
- **Schema:** `array of DepartmentResponse`

#### 403 — Access denied to this resource

#### 404 — Resource not found

---

## POST /department — Create a department

Add a new department, details include the name and locations.

### Request Body
Department to save to the database:
**Required:** True

- **Content-Type:** `application/json`
- **Schema:** `DepartmentRequest`

```json
{
  "name": "Department of Examples"
}
```


### Responses

#### 201 — Department created
- **Content-Type:** `application/json`
- **Schema:** `DepartmentResponse`

```json
{
  "id": 1658,
  "created": "2021-08-12T10:54:13+12:00",
  "modified": "2021-08-13T10:54:13+12:00",
  "name": "Administration",
  "locations": [
    {
      "id": 556,
      "name": "Example Name"
    }
  ]
}
```

- **Content-Type:** `application/xml`
- **Schema:** `DepartmentResponse`

#### 403 — Access denied to this resource

#### 409 — Department already exists

#### 500 — Internal server error

---

## GET /department/{id} — Retrieve a department

Retrieves a single department, filtering with the provided ID.

### Parameters
- **id** (path · integer(int32) · required): ID of the department to retrieve

### Responses

#### 200 — Department returned
- **Content-Type:** `application/json`
- **Schema:** `DepartmentResponse`

```json
{
  "id": 1658,
  "created": "2021-08-12T10:54:13+12:00",
  "modified": "2021-08-13T10:54:13+12:00",
  "name": "Administration",
  "locations": [
    {
      "id": 556,
      "name": "Example Name"
    }
  ]
}
```

- **Content-Type:** `application/xml`
- **Schema:** `DepartmentResponse`

#### 403 — Access denied to resource

#### 404 — Resource not found

---

## PUT /department/{id} — Update a department

Updates a department with the submitted data, using the provided ID.

### Parameters
- **id** (path · integer(int32) · required): ID of the department to update

### Request Body
Department to save to the database
**Required:** True

- **Content-Type:** `application/json`
- **Schema:** `DepartmentRequest`

```json
{
  "name": "Department of Examples"
}
```


### Responses

#### 204 — Department updated

#### 403 — Access denied to this resource

#### 500 — Internal server error

---

## DELETE /department/{id} — Delete a department

Removes a department using the provided ID.

### Parameters
- **id** (path · integer(int32) · required): ID of the department to delete

### Responses

#### 204 — Department deleted

#### 403 — Access denied to resource

#### 404 — Resource not found

---

## Tag: Location zones

Zones are used to split your location into specific areas. If you use the Desks and Spaces add-on, you can also create bookable workspaces within each zone. Use the API to retrieve a list of all zones, add new zones, update or delete existing zones, and link zones to access points.

Learn more about zones in the [OnLocation Help Center](https://onlocation.elevio.help/en/articles/296).

## GET /zone — List all zones

Returns a list of all zones

A comma separated list of tag:value search parameters, a colon separator performs an exact match, a percent separator performs a starts-with search and greater/less than search number/date ranges.

### Parameters
- **q** (query · string): Filter by any zone value. (example: `spaces>5`)

### Responses

#### 200 — List of zones
- **Content-Type:** `application/json`
- **Schema:** `array of ZoneResponse`

```json
[
  {
    "id": 1206,
    "created": "2021-11-26T14:35:28+13:00",
    "modified": "2021-12-31T14:35:28+13:00",
    "location_id": 15,
    "zone_group_id": 3,
    "name": "Meeting room 1",
    "reference": "Large meeting room - seats 20",
    "status": "Active",
    "maximum_occupancy": 50,
    "spaces_enabled": true,
    "spaces": 11,
    "spaces_title": "Spaces Reference",
    "zone_group_name": "Floor 1",
    "zone_fullname": "Floor 1 - Meeting room 1"
  }
]
```

- **Content-Type:** `application/xml`
- **Schema:** `array of ZoneResponse`

#### 404 — Resource not found

#### 500 — Internal server error

---

## POST /zone — Create zone

Creates a new zone that can be assigned to a location.

### Request Body
Information to use when creating a new zone record.
**Required:** True

- **Content-Type:** `application/json`
- **Schema:** `ZoneRequest`

```json
{
  "name": "Meeting room 1",
  "location_id": 15,
  "reference": "Large meeting room - seats 20",
  "maximum_occupancy": 50,
  "spaces_enabled": true,
  "spaces": 11,
  "spaces_title": "Spaces Reference"
}
```


### Responses

#### 201 — Zone created
- **Content-Type:** `application/json`
- **Schema:** `ZoneResponse`

```json
{
  "id": 1206,
  "created": "2021-11-26T14:35:28+13:00",
  "modified": "2021-12-31T14:35:28+13:00",
  "location_id": 15,
  "zone_group_id": 3,
  "name": "Meeting room 1",
  "reference": "Large meeting room - seats 20",
  "status": "Active",
  "maximum_occupancy": 50,
  "spaces_enabled": true,
  "spaces": 11,
  "spaces_title": "Spaces Reference",
  "zone_group_name": "Floor 1",
  "zone_fullname": "Floor 1 - Meeting room 1"
}
```

#### 403 — Access denied to this resource

#### 500 — Internal server error

---

## GET /zone/{id} — Retrieve zone

Retrieves a single zone, filtering with the provided ID.

### Parameters
- **id** (path · integer(int32) · required): ID of the zone to retrieve

### Responses

#### 200
- **Content-Type:** `application/json`
- **Schema:** `ZoneResponse`

```json
{
  "id": 1206,
  "created": "2021-11-26T14:35:28+13:00",
  "modified": "2021-12-31T14:35:28+13:00",
  "location_id": 15,
  "zone_group_id": 3,
  "name": "Meeting room 1",
  "reference": "Large meeting room - seats 20",
  "status": "Active",
  "maximum_occupancy": 50,
  "spaces_enabled": true,
  "spaces": 11,
  "spaces_title": "Spaces Reference",
  "zone_group_name": "Floor 1",
  "zone_fullname": "Floor 1 - Meeting room 1"
}
```

- **Content-Type:** `application/xml`
- **Schema:** `ZoneResponse`

#### 403 — Access denied to resource

#### 404 — Resource not found

#### 500 — Internal server error

---

## PUT /zone/{id} — Update zone

Updates the details of a zone.

### Parameters
- **id** (path · integer(int32) · required): ID of the zone to update

### Request Body
Information used to create the zone record
**Required:** True

- **Content-Type:** `application/json`
- **Schema:** `ZoneRequest`

```json
{
  "name": "Meeting room 1",
  "location_id": 15,
  "reference": "Large meeting room - seats 20",
  "maximum_occupancy": 50,
  "spaces_enabled": true,
  "spaces": 11,
  "spaces_title": "Spaces Reference"
}
```


### Responses

#### 200 — Updated resource
- **Content-Type:** `application/json`
- **Schema:** `ZoneResponse`

```json
{
  "id": 1206,
  "created": "2021-11-26T14:35:28+13:00",
  "modified": "2021-12-31T14:35:28+13:00",
  "location_id": 15,
  "zone_group_id": 3,
  "name": "Meeting room 1",
  "reference": "Large meeting room - seats 20",
  "status": "Active",
  "maximum_occupancy": 50,
  "spaces_enabled": true,
  "spaces": 11,
  "spaces_title": "Spaces Reference",
  "zone_group_name": "Floor 1",
  "zone_fullname": "Floor 1 - Meeting room 1"
}
```

- **Content-Type:** `application/xml`
- **Schema:** `ZoneResponse`

#### 403 — Access denied to this resource

#### 500 — Internal server error

---

## DELETE /zone/{id} — Delete zone

Deletes a zone using the provided ID.

### Parameters
- **id** (path · integer(int32) · required): ID of the zone to delete

### Responses

#### 204 — Zone deleted

#### 403 — Access denied to resource

#### 404 — Resource not found

#### 500 — Internal server error

---

## PUT /zone/{id}/archive — Archive zone

Archives a zone. An archived zone cannot be activated again.

### Parameters
- **id** (path · integer(int32) · required): ID of the zone to update

### Responses

#### 200 — Updated resource

#### 403 — Access denied to this resource

#### 500 — Internal server error

---

## PUT /zone/{zoneId}/link/{accessPointId} — Link zone

Links a zone to an access point. Access points are places in your location that people use to sign in or out.

### Parameters
- **zoneId** (path · integer(int32) · required): ID of the zone to link
- **accessPointId** (path · integer(int32) · required): ID of the access point to link

### Responses

#### 204 — Updated resource

#### 403 — Access denied to this resource

#### 500 — Internal server error

---

## PUT /zone/{zoneId}/unlink/{accessPointId} — Unlink zone

Removes the link between a zone and an access point.

### Parameters
- **zoneId** (path · integer(int32) · required): ID of the zone to unlink
- **accessPointId** (path · integer(int32) · required): ID of the access point to unlink

### Responses

#### 204 — Updated resource

#### 403 — Access denied to this resource

#### 500 — Internal server error

---

## Tag: Location zone groups

Zone groups are used to group your location's zones together. For example, a zone group may be a building and each floor is a zone. Use the API to retrieve a list of all zone groups, add new zone groups, and update or delete existing zone groups.

Learn more about zone groups in the [OnLocation Help Center](https://onlocation.elevio.help/en/articles/296).

## GET /zonegroup — List all zone groups

Returns a list of all zone groups.

A comma separated list of tag:value search parameters, a colon separator performs an exact match, a percent separator performs a starts-with search and greater/less than search number/date ranges.

### Responses

#### 200 — List of zone groups
- **Content-Type:** `application/json`
- **Schema:** `array of ZoneGroupResponse`

```json
[
  {
    "id": 1206,
    "created": "2021-11-26T14:35:28+13:00",
    "modifed": "2021-12-31T14:35:28+13:00",
    "org_id": 81,
    "location_id": 22,
    "name": "Main Building"
  }
]
```

- **Content-Type:** `application/xml`
- **Schema:** `array of ZoneGroupResponse`

#### 403 — Access denied to resource

#### 404 — Resource not found

#### 500 — Internal server error

---

## POST /zonegroup — Create a zone group

Creates a new zone group.

### Request Body
Information to use when creating a new zone group record.
**Required:** True

- **Content-Type:** `application/json`
- **Schema:** `ZoneGroupRequest`

```json
{
  "name": "Main Building",
  "location_id": 22
}
```


### Responses

#### 201 — Certification created
- **Content-Type:** `application/json`
- **Schema:** `ZoneGroupResponse`

```json
{
  "id": 1206,
  "created": "2021-11-26T14:35:28+13:00",
  "modifed": "2021-12-31T14:35:28+13:00",
  "org_id": 81,
  "location_id": 22,
  "name": "Main Building"
}
```

#### 403 — Access denied to this resource

#### 500 — Internal server error

---

## GET /zonegroup/{id} — Retrieve a zone group

Retrieves a single zone group, filtering with the provided ID.

### Parameters
- **id** (path · integer(int32) · required): ID of the zone group to retrieve

### Responses

#### 200
- **Content-Type:** `application/json`
- **Schema:** `ZoneGroupResponse`

```json
{
  "id": 1206,
  "created": "2021-11-26T14:35:28+13:00",
  "modifed": "2021-12-31T14:35:28+13:00",
  "org_id": 81,
  "location_id": 22,
  "name": "Main Building"
}
```

- **Content-Type:** `application/xml`
- **Schema:** `ZoneGroupResponse`

#### 403 — Access denied to resource

#### 404 — Resource not found

#### 500 — Internal server error

---

## PUT /zonegroup/{id} — Update a zone group

Updates the details of a zone group.

### Parameters
- **id** (path · integer(int32) · required): ID of the zone group to update

### Request Body
Information used to create the zone group record
**Required:** True

- **Content-Type:** `application/json`
- **Schema:** `ZoneGroupRequest`

```json
{
  "name": "Main Building",
  "location_id": 22
}
```


### Responses

#### 200 — Updated resource
- **Content-Type:** `application/json`
- **Schema:** `ZoneGroupResponse`

```json
{
  "id": 1206,
  "created": "2021-11-26T14:35:28+13:00",
  "modifed": "2021-12-31T14:35:28+13:00",
  "org_id": 81,
  "location_id": 22,
  "name": "Main Building"
}
```

- **Content-Type:** `application/xml`
- **Schema:** `ZoneGroupResponse`

#### 403 — Access denied to this resource

#### 500 — Internal server error

---

## DELETE /zonegroup/{id} — Delete a zone group

Deletes a zone group using the provided ID.

### Parameters
- **id** (path · integer(int32) · required): ID of the zone group to delete

### Responses

#### 204 — Zone group deleted

#### 403 — Access denied to resource

#### 404 — Resource not found

#### 500 — Internal server error

---

## Tag: Notifications

OnLocation sends email, SMS, and OnLocation Mobile push notifications. Use the notification API to retrieve a list of all notifications and provide a notification ID to retrieve a single notification.

## GET /notification — List all notifications

Returns a list of all notification records.

Filter by any notification element. A comma separated list of tag:value search parameters, a colon separator performs an exact match, a percent separator performs a starts-with search and greater/less than search number/date ranges.

### Parameters
- **q** (query · string): Filter by notification value (example: `method:sms,location_id:7`)

### Responses

#### 200 — List of notification records
- **Content-Type:** `application/json`
- **Schema:** `array of NotificationResponse`

```json
[
  {
    "id": 192,
    "created": "2021-11-25T15:01:31+13:00",
    "modified": "2021-11-25T15:01:33+13:00",
    "location_id": 301,
    "visitor_id": 139,
    "for_staff_id": 82,
    "to_staff_id": 431,
    "method": "sms",
    "type": "signin",
    "recipient": "64 212143775",
    "messageid": "18f007ae1767d54b6654bae703d472808634a123@yoursite.whosonlocation.com",
    "to_staff_name": "Great Scott",
    "for_staff_name": "Great Scott",
    "status": [
      {
        "id": 293,
        "created": "2021-11-29T11:10:44+13:00",
        "modified": "2021-11-30T11:10:44+13:00",
        "location_id": 301,
        "created_staff_id": 823431,
        "staff_id": 823431,
        "name": "Frequent Visitor name",
        "email": "frequent.visitor@whosonlocation.com",
        "from": "Another Organisation",
        "phone": "+1 206 555 0123",
        "mobile": "+1 206 555 0123",
        "title": "Mr",
        "assistance": false,
        "type": "frequent",
        "groups": [
          [
            197,
            198
          ]
        ]
      }
    ]
  }
]
```

- **Content-Type:** `application/xml`
- **Schema:** `array of NotificationResponse`

#### 403 — Access denied to this resource

#### 404 — Resource not found

---

## GET /notification/{id} — Retrieve a notification

Retrieves a single notification, filtering with the provided ID.

### Parameters
- **id** (path · integer(int32) · required): ID of the notification to retrieve

### Responses

#### 200 — Notification returned
- **Content-Type:** `application/json`
- **Schema:** `NotificationResponse`

```json
{
  "id": 192,
  "created": "2021-11-25T15:01:31+13:00",
  "modified": "2021-11-25T15:01:33+13:00",
  "location_id": 301,
  "visitor_id": 139,
  "for_staff_id": 82,
  "to_staff_id": 431,
  "method": "sms",
  "type": "signin",
  "recipient": "64 212143775",
  "messageid": "18f007ae1767d54b6654bae703d472808634a123@yoursite.whosonlocation.com",
  "to_staff_name": "Great Scott",
  "for_staff_name": "Great Scott",
  "status": [
    {
      "id": 293,
      "created": "2021-11-29T11:10:44+13:00",
      "modified": "2021-11-30T11:10:44+13:00",
      "location_id": 301,
      "created_staff_id": 823431,
      "staff_id": 823431,
      "name": "Frequent Visitor name",
      "email": "frequent.visitor@whosonlocation.com",
      "from": "Another Organisation",
      "phone": "+1 206 555 0123",
      "mobile": "+1 206 555 0123",
      "title": "Mr",
      "assistance": false,
      "type": "frequent",
      "groups": [
        [
          197,
          198
        ]
      ]
    }
  ]
}
```

- **Content-Type:** `application/xml`
- **Schema:** `NotificationResponse`

#### 403 — Access denied to resource

#### 404 — Resource not found

---

## Tag: Visitor events

A visitor event is a record listing the details of a visit, including the visitor's name, where they visited, contact details, basic question answers, and sign in/out mode and time.

Use the visitor event API to retrieve your visitor event list, add a new visitor event, or update or delete a visitor event.

## GET /visitor/event — List all visitor events

Returns a list of visitor sign in and out events.

A comma separated list of tag:value search parameters, a colon separator performs an exact match, a percent separator performs a starts-with search and greater/less than search number/date ranges.

### Parameters
- **q** (query · string): Filter by any visitor event value. (example: `location_id:100`)
- **order** (query · string): Order by any element returned, prefix with a - to reverse order. (example: `location_id`)
- **limit** (query · integer(date-string)): Limits the amount of records to return. (example: `10`)
- **If-Modified-Since** (header · string(int32)): A UTC timestamp (yyyy-mm-ddThh:mm:ss) . Only entities created or modified since this timestamp will be returned e.g. 2009-11-12T00:00:00

### Responses

#### 200 — List of visitor sign in and out events
- **Content-Type:** `application/json`
- **Schema:** `array of VisitorEventResponse`

```json
[
  {
    "id": 138,
    "created": "2021-11-25T14:47:34+13:00",
    "modified": "2021-11-25T15:01:08+13:00",
    "location_id": 301,
    "created_staff_id": 349,
    "visiting_staff_id": 823,
    "signed_in": "2021-11-25T14:47:36+13:00",
    "signed_out": "2021-11-25T15:01:08+13:00",
    "mode_in": "Kiosk",
    "mode_out": "Kiosk",
    "signed_in_by": 823,
    "signed_out_by": 823,
    "name": "Elayne Trakand",
    "email": "example-email@whosonlocation.com",
    "from": "Andor",
    "phone": "+1 206 555 0123",
    "mobile": "+1 206 555 0123",
    "title": "Ms",
    "assistance": false,
    "expected": false,
    "printed": true,
    "pass": true,
    "purpose": "Just visiting",
    "carpark_question": false,
    "carpark_registration": "MRU366",
    "carpark_number": "5",
    "cardnumber": "cn001",
    "id_verification_checked": true,
    "id_verification_type": "licence",
    "accessdenied": false,
    "id_verification_reference": "Reference verification",
    "visiting_staff_name": "Staff Member",
    "location_name": "Head Office",
    "lacp_in_name": "Reception",
    "lacp_out_name": "Reception",
    "signed_in_by_name": "Person in reception",
    "signed_out_by_name": "Person in reception",
    "onsite": "13 minutes 32 seconds",
    "type": "visit",
    "lacp_in_id": 239,
    "lacp_out_id": 239
  }
]
```

- **Content-Type:** `application/xml`
- **Schema:** `array of VisitorEventResponse`

#### 403 — Access denied to this resource

#### 404 — Resource not found

#### 500 — Internal server error

---

## POST /visitor/event — Create a visitor event

Creates a new visitor sign in and out event.

### Request Body
**Required:** True

- **Content-Type:** `application/json`
- **Schema:** `VisitorEventPostRequest`

```json
{
  "name": "Elayne Trakand",
  "type": "visit",
  "location_id": 301,
  "visiting_staff_id": 534,
  "signed_in": "2021-11-25T12:01:09.380Z",
  "signed_out": "2021-11-25T13:01:09.380Z",
  "email": "example.email@example.org",
  "from": "Andor",
  "phone": "+1 206 555 0123",
  "mobile": "+1 206 555 0123",
  "title": "Ms",
  "assistance": false,
  "purpose": "visiting",
  "carpark_registration": "MRU366",
  "carpark_number": "5",
  "cardnumber": "cn001",
  "id_verification_reference": true,
  "id_verification_type": "licence",
  "lacp_in_id": 239,
  "lacp_out_id": 239
}
```


### Responses

#### 201 — Event created
- **Content-Type:** `application/json`
- **Schema:** `VisitorEventResponse`

```json
{
  "id": 138,
  "created": "2021-11-25T14:47:34+13:00",
  "modified": "2021-11-25T15:01:08+13:00",
  "location_id": 301,
  "created_staff_id": 349,
  "visiting_staff_id": 823,
  "signed_in": "2021-11-25T14:47:36+13:00",
  "signed_out": "2021-11-25T15:01:08+13:00",
  "mode_in": "Kiosk",
  "mode_out": "Kiosk",
  "signed_in_by": 823,
  "signed_out_by": 823,
  "name": "Elayne Trakand",
  "email": "example-email@whosonlocation.com",
  "from": "Andor",
  "phone": "+1 206 555 0123",
  "mobile": "+1 206 555 0123",
  "title": "Ms",
  "assistance": false,
  "expected": false,
  "printed": true,
  "pass": true,
  "purpose": "Just visiting",
  "carpark_question": false,
  "carpark_registration": "MRU366",
  "carpark_number": "5",
  "cardnumber": "cn001",
  "id_verification_checked": true,
  "id_verification_type": "licence",
  "accessdenied": false,
  "id_verification_reference": "Reference verification",
  "visiting_staff_name": "Staff Member",
  "location_name": "Head Office",
  "lacp_in_name": "Reception",
  "lacp_out_name": "Reception",
  "signed_in_by_name": "Person in reception",
  "signed_out_by_name": "Person in reception",
  "onsite": "13 minutes 32 seconds",
  "type": "visit",
  "lacp_in_id": 239,
  "lacp_out_id": 239
}
```

- **Content-Type:** `application/xml`
- **Schema:** `VisitorEventResponse`

#### 403 — Access denied to this resource

#### 500 — Internal server error

---

## GET /visitor/event/{id} — Retrieve a visitor event

Retrieves a single visitor sign in and out event, filtering with the provided ID.

### Parameters
- **id** (path · integer(int32) · required): ID of the visitor sign in and out event to retrieve

### Responses

#### 200 — Event returned
- **Content-Type:** `application/json`
- **Schema:** `VisitorEventResponse`

```json
{
  "id": 138,
  "created": "2021-11-25T14:47:34+13:00",
  "modified": "2021-11-25T15:01:08+13:00",
  "location_id": 301,
  "created_staff_id": 349,
  "visiting_staff_id": 823,
  "signed_in": "2021-11-25T14:47:36+13:00",
  "signed_out": "2021-11-25T15:01:08+13:00",
  "mode_in": "Kiosk",
  "mode_out": "Kiosk",
  "signed_in_by": 823,
  "signed_out_by": 823,
  "name": "Elayne Trakand",
  "email": "example-email@whosonlocation.com",
  "from": "Andor",
  "phone": "+1 206 555 0123",
  "mobile": "+1 206 555 0123",
  "title": "Ms",
  "assistance": false,
  "expected": false,
  "printed": true,
  "pass": true,
  "purpose": "Just visiting",
  "carpark_question": false,
  "carpark_registration": "MRU366",
  "carpark_number": "5",
  "cardnumber": "cn001",
  "id_verification_checked": true,
  "id_verification_type": "licence",
  "accessdenied": false,
  "id_verification_reference": "Reference verification",
  "visiting_staff_name": "Staff Member",
  "location_name": "Head Office",
  "lacp_in_name": "Reception",
  "lacp_out_name": "Reception",
  "signed_in_by_name": "Person in reception",
  "signed_out_by_name": "Person in reception",
  "onsite": "13 minutes 32 seconds",
  "type": "visit",
  "lacp_in_id": 239,
  "lacp_out_id": 239
}
```

- **Content-Type:** `application/xml`
- **Schema:** `VisitorEventResponse`

#### 403 — Access denied to resource

#### 404 — Resource not found

#### 500 — Internal server error

---

## PUT /visitor/event/{id} — Update a visitor event

Updates a visitor sign in and out event based on the provided ID.

### Parameters
- **id** (path · integer(int32) · required): ID of the visitor event to update

### Request Body
Information used to create the visitor event record
**Required:** True

- **Content-Type:** `application/json`
- **Schema:** `VisitorEventPutRequest`

```json
{
  "type": "visit",
  "location_id": 301,
  "visiting_staff_id": 534,
  "signed_in": "2021-11-25T12:01:09.380Z",
  "signed_out": "2021-11-25T13:01:09.380Z",
  "mode_id": "Kiosk",
  "mode_out": "Kiosk",
  "signed_in_by": 823,
  "signed_out_by": 823,
  "name": "Elayne Trakand",
  "email": "example.email@whosonlocation.com",
  "from": "Andor",
  "phone": "+1 206 555 0123",
  "mobile": "+1 206 555 0123",
  "title": "Ms",
  "assistance": false,
  "expected": false,
  "pass": true,
  "purpose": "Just visiting",
  "carpark_question": false,
  "carpark_registration": "MRU366",
  "carpark_number": "5",
  "cardnumber": "cn001",
  "id_verification_reference": true,
  "id_verification_type": "licence",
  "accessdenied": false,
  "visiting_staff_name": "Staff Member",
  "signed_in_by_name": "Person in reception",
  "signed_out_by_name": "Person in reception",
  "onsite": "13 minutes 32 seconds",
  "lacp_in_id": 239,
  "lacp_out_id": 239
}
```


### Responses

#### 204 — Updated event

#### 403 — Access denied to this resource

#### 500 — Internal server error

---

## DELETE /visitor/event/{id} — Delete a visitor event

Deletes the specified visitor sign in and out event.

### Parameters
- **id** (path · integer(int32) · required): ID of the visitor sign in/out event to delete

### Responses

#### 204 — Event deleted

#### 403 — Access denied to resource

#### 404 — Resource not found

#### 500 — Internal server error

---

## Tag: Visitor groups

Each visitor group is created by an employee and includes a set of their visitors who are pre-registered to attend the same events. Use the visitor group API to retrieve your visitor group list, add a new visitor group, or update or delete a visitor group.

Learn more about visitor groups in the [OnLocation Help Center](https://onlocation.elevio.help/en/articles/391).

## GET /visitor/group — List all visitor groups

Returns a list of all visitor groups added by employees.

A comma separated list of tag:value search parameters, a colon separator performs an exact match, a percent separator performs a starts-with search and greater/less than search number/date ranges.

### Parameters
- **q** (query · string): Filter by visitor group value. (example: `staff_id:100`)

### Responses

#### 200 — List of registered visitor groups
- **Content-Type:** `application/json`
- **Schema:** `array of VisitorGroupResponse`

```json
[
  {
    "id": 197,
    "created": "2021-11-29T10:58:12+13:00",
    "modified": "2021-11-29T13:58:12+13:00",
    "location_id": 301,
    "created_staff_id": 823,
    "staff_id": 823,
    "name": "Visitor Group 1",
    "description": "Group 1 description",
    "members": 2
  }
]
```

- **Content-Type:** `application/xml`
- **Schema:** `array of VisitorGroupResponse`

#### 403 — Access denied to this resource

#### 404 — Resource not found

#### 500 — Internal server error

---

## POST /visitor/group — Create a visitor group

Create a new visitor group and assign it the the relevant employee and location.

### Request Body
Data used to create a new visitor group:
**Required:** True

- **Content-Type:** `application/json`
- **Schema:** `VisitorGroupRequest`

```json
{
  "name": "Updated visitor group name",
  "description": "Group description",
  "location_id": 301,
  "staff_id": 556
}
```


### Responses

#### 201 — Group created
- **Content-Type:** `application/json`
- **Schema:** `VisitorGroupResponse`

```json
{
  "id": 197,
  "created": "2021-11-29T10:58:12+13:00",
  "modified": "2021-11-29T13:58:12+13:00",
  "location_id": 301,
  "created_staff_id": 823,
  "staff_id": 823,
  "name": "Visitor Group 1",
  "description": "Group 1 description",
  "members": 2
}
```

- **Content-Type:** `application/xml`
- **Schema:** `VisitorGroupResponse`

#### 403 — Access denied to this resource

#### 500 — Internal server error

---

## GET /visitor/group/{id} — Retrieve a visitor group

Retrieves a single visitor group, filtering with the provided ID.

### Parameters
- **id** (path · integer(int32) · required): ID of the registered visitor group to retrieve

### Responses

#### 200 — Group returned
- **Content-Type:** `application/json`
- **Schema:** `VisitorGroupResponse`

```json
{
  "id": 197,
  "created": "2021-11-29T10:58:12+13:00",
  "modified": "2021-11-29T13:58:12+13:00",
  "location_id": 301,
  "created_staff_id": 823,
  "staff_id": 823,
  "name": "Visitor Group 1",
  "description": "Group 1 description",
  "members": 2
}
```

- **Content-Type:** `application/xml`
- **Schema:** `VisitorGroupResponse`

#### 403 — Access denied to resource

#### 404 — Resource not found

#### 500 — Internal server error

---

## PUT /visitor/group/{id} — Update a visitor group

Updates the specified visitor group based on the provided ID.

### Parameters
- **id** (path · integer(int32) · required): ID of the registered visitor group to update

### Request Body
Information used to update the visitor group record:
**Required:** True

- **Content-Type:** `application/json`
- **Schema:** `VisitorGroupRequest`

```json
{
  "name": "Updated visitor group name",
  "description": "Group description",
  "location_id": 301,
  "staff_id": 556
}
```


### Responses

#### 204 — Updated group

#### 403 — Access denied to this resource

#### 500 — Internal server error

---

## DELETE /visitor/group/{id} — Delete a visitor group

Deletes the specified visitor group.

### Parameters
- **id** (path · integer(int32) · required): ID of the registered visitor group to delete

### Responses

#### 204 — Visitor group deleted

#### 403 — Access denied to resource

#### 404 — Resource not found

#### 500 — Internal server error

---

## Tag: Pre-registered visitors

A pre-registered visitor is someone who has been approved by an employee to visit a location. Each visitor is linked to a visit event. Use the pre-registered visitor API to retrieve your pre-registered visitor events list, add a new pre-registered visitor event, or update or delete a pre-registered visitor event.

Learn more about pre-registered visitors in the [OnLocation Help Center](https://onlocation.elevio.help/en/articles/364).

## GET /visitor/register — List all pre-registered visitor events

Returns a list of all pre-registered visitors and groups.

A comma separated list of tag:value search parameters, a colon separator performs an exact match, a percent separator performs a starts-with search and greater/less than search number/date ranges.

### Parameters
- **q** (query · string): Filter by pre-registered visitor value. (example: `location_id:100`)

### Responses

#### 200 — List of pre-registered visitors
- **Content-Type:** `application/json`
- **Schema:** `array of VisitorRegisterResponse`

```json
[
  {
    "id": 1915,
    "created": "2021-11-29T11:11:25+13:00",
    "modified": "2021-11-29T11:11:56+13:00",
    "location_id": 301,
    "created_staff_id": 823431,
    "visiting_staff_id": 823431,
    "event_start": "2021-11-30T15:00:00+13:00",
    "event_end": "2021-11-30T17:00:00+13:00",
    "name": "Visitor Registor Name",
    "visiting_staff_name": "Jane Doe",
    "location_name": "Head Office",
    "visitors": [
      {
        "id": 2957,
        "name": "Robert Jordan",
        "email": "registered.visitor@whosonlocation.com",
        "from": "ACME Corporation",
        "phone": "+1 206 555 0123",
        "mobile": "+1 206 555 0123",
        "title": "Mr",
        "assistance": false,
        "purpose": "Visiting"
      }
    ],
    "type": "preregister"
  }
]
```

- **Content-Type:** `application/xml`
- **Schema:** `array of VisitorRegisterResponse`

#### 403 — Access denied to this resource.

#### 404 — Resource not found

#### 500 — Internal server error

---

## POST /visitor/register — Create a pre-registered visitor event

Add a new pre-registered visitor event

### Request Body
**Required:** True

- **Content-Type:** `application/json`
- **Schema:** `VisitorRegisterRequest`

```json
{
  "name": "Robert Jordan",
  "type": "preregister",
  "location_id": 301,
  "staff_id": 264,
  "visiting_staff_id": 264,
  "event_start": "2021-11-30T15:00:00+13:00",
  "event_end": "2021-11-30T17:00:00+13:00",
  "visitors": [
    {
      "name": "Robert Jordan",
      "from": "ACME Corporation",
      "email": "example@example.org",
      "phone": "+1 206 555 0123",
      "mobile": "+1 206 555 0123",
      "title": "Mr",
      "assistance": false,
      "purpose": "visiting"
    }
  ]
}
```


### Responses

#### 201 — Pre-registered visitor event created
- **Content-Type:** `application/json`
- **Schema:** `VisitorRegisterRequest`

```json
{
  "name": "Robert Jordan",
  "type": "preregister",
  "location_id": 301,
  "staff_id": 264,
  "visiting_staff_id": 264,
  "event_start": "2021-11-30T15:00:00+13:00",
  "event_end": "2021-11-30T17:00:00+13:00",
  "visitors": [
    {
      "name": "Robert Jordan",
      "from": "ACME Corporation",
      "email": "example@example.org",
      "phone": "+1 206 555 0123",
      "mobile": "+1 206 555 0123",
      "title": "Mr",
      "assistance": false,
      "purpose": "visiting"
    }
  ]
}
```

#### 403 — Access denied to this resource

#### 500 — Internal server error

---

## GET /visitor/register/{id} — Retrieve a pre-registered visitor event

Retrieves a single pre-registered visitor, filtering with the provided ID

### Parameters
- **id** (path · integer(int32) · required)

### Responses

#### 200 — A single pre-registered visitor record
- **Content-Type:** `application/json`
- **Schema:** `VisitorRegisterResponse`

```json
{
  "id": 1915,
  "created": "2021-11-29T11:11:25+13:00",
  "modified": "2021-11-29T11:11:56+13:00",
  "location_id": 301,
  "created_staff_id": 823431,
  "visiting_staff_id": 823431,
  "event_start": "2021-11-30T15:00:00+13:00",
  "event_end": "2021-11-30T17:00:00+13:00",
  "name": "Visitor Registor Name",
  "visiting_staff_name": "Jane Doe",
  "location_name": "Head Office",
  "visitors": [
    {
      "id": 2957,
      "name": "Robert Jordan",
      "email": "registered.visitor@whosonlocation.com",
      "from": "ACME Corporation",
      "phone": "+1 206 555 0123",
      "mobile": "+1 206 555 0123",
      "title": "Mr",
      "assistance": false,
      "purpose": "Visiting"
    }
  ],
  "type": "preregister"
}
```

- **Content-Type:** `application/xml`
- **Schema:** `VisitorRegisterResponse`

#### 403 — Access denied to resource

#### 404 — Resource not found

#### 500 — Internal server error

---

## PUT /visitor/register/{id} — Update a pre-registered visitor event

Updates the specified pre-registered visitor event

### Parameters
- **id** (path · integer(int32) · required): ID of the pre-registered visitor event to update

### Request Body
Information used to create the pre-registered visitor event record
**Required:** True

- **Content-Type:** `application/json`
- **Schema:** `VisitorRegisterRequest`

```json
{
  "name": "Robert Jordan",
  "type": "preregister",
  "location_id": 301,
  "staff_id": 264,
  "visiting_staff_id": 264,
  "event_start": "2021-11-30T15:00:00+13:00",
  "event_end": "2021-11-30T17:00:00+13:00",
  "visitors": [
    {
      "name": "Robert Jordan",
      "from": "ACME Corporation",
      "email": "example@example.org",
      "phone": "+1 206 555 0123",
      "mobile": "+1 206 555 0123",
      "title": "Mr",
      "assistance": false,
      "purpose": "visiting"
    }
  ]
}
```


### Responses

#### 204 — Updated pre-registered visitor event

#### 403 — Access denied to this resource

#### 500 — Internal server error

---

## DELETE /visitor/register/{id} — Delete a pre-registered visitor event

Removes the specified pre-registered visitor event.

### Parameters
- **id** (path · integer(int32) · required)

### Responses

#### 204 — Pre-registered visitor event deleted

#### 403 — Access denied to resource

#### 404 — Resource not found

#### 500 — Internal server error

---

## Tag: Saved visitors

Each employee can maintain a saved visitor list in OnLocation. Use the saved visitor API to retrieve all saved visitors, add a new saved visitor, or update or delete a saved visitor.

Learn more about saved visitors in the [OnLocation Help Center](https://onlocation.elevio.help/en/articles/363).

## GET /visitor/person — List all saved visitors

Returns a list of all saved visitors added by employees.

A comma separated list of tag:value search parameters, a colon separator performs an exact match, a percent separator performs a starts-with search and greater/less than search number/date ranges.

### Responses

#### 200 — List of registered visitors
- **Content-Type:** `application/json`
- **Schema:** `array of VisitorPersonResponse`

```json
[
  {
    "id": 293,
    "created": "2021-11-29T11:10:44+13:00",
    "modified": "2021-11-30T11:10:44+13:00",
    "location_id": 301,
    "created_staff_id": 823431,
    "staff_id": 823431,
    "name": "Frequent Visitor name",
    "email": "frequent.visitor@whosonlocation.com",
    "from": "Another Organisation",
    "phone": "+1 206 555 0123",
    "mobile": "+1 206 555 0123",
    "title": "Mr",
    "assistance": false,
    "type": "frequent",
    "groups": [
      [
        197,
        198
      ]
    ]
  }
]
```

- **Content-Type:** `application/xml`
- **Schema:** `array of VisitorPersonResponse`

#### 403 — Access denied to this resource

#### 404 — Resource not found

#### 500 — Internal server error

---

## GET /visitor/person/{id} — Retrieve a saved visitor

Retrieves a single saved visitor, filtering with the provided ID.

### Parameters
- **id** (path · integer(int32) · required): ID of the registered visitor to retrieve

### Responses

#### 200 — A visitor's details
- **Content-Type:** `application/json`
- **Schema:** `VisitorPersonResponse`

```json
{
  "id": 293,
  "created": "2021-11-29T11:10:44+13:00",
  "modified": "2021-11-30T11:10:44+13:00",
  "location_id": 301,
  "created_staff_id": 823431,
  "staff_id": 823431,
  "name": "Frequent Visitor name",
  "email": "frequent.visitor@whosonlocation.com",
  "from": "Another Organisation",
  "phone": "+1 206 555 0123",
  "mobile": "+1 206 555 0123",
  "title": "Mr",
  "assistance": false,
  "type": "frequent",
  "groups": [
    [
      197,
      198
    ]
  ]
}
```

- **Content-Type:** `application/xml`
- **Schema:** `VisitorPersonResponse`

#### 403 — Access denied to resource

#### 404 — Resource not found

#### 500 — Internal server error

---

## DELETE /visitor/person/{id} — Delete a saved visitor

Deletes a saved visitor using with the provided ID.

### Parameters
- **id** (path · integer(int32) · required): ID of the registered visitor to delete

### Responses

#### 204 — Saved visitor deleted

#### 403 — Access denied to resource

#### 404 — Resource not found

#### 500 — Internal server error

---

## Tag: Qualifications (deprecated)

The qualifications endpoints have been deprecated as Qualifications Management has been renamed Certifications Management. Use the [certifications](/openapi/wol/tag/Certifications/) endpoints to access this data.

## GET /qualification — List all qualifications

Returns a list of all qualifications.

A comma separated list of tag:value search parameters, a colon separator performs an exact match, a percent separator performs a starts-with search and greater/less than search number/date ranges.

### Parameters
- **q** (query · string): Filter by any qualification value. (example: `holders>5`)

### Responses

#### 200 — List of qualifications
- **Content-Type:** `application/json`
- **Schema:** `array of QualificationResponse`

```json
[
  {
    "id": 1206,
    "created": "2021-11-26T14:35:28+13:00",
    "modified": "2021-12-31T14:35:28+13:00",
    "created_by": 35,
    "name": "Qualification Name",
    "category": "external",
    "description": "This describes the certification",
    "holders": 2,
    "status": "Active"
  }
]
```

- **Content-Type:** `application/xml`
- **Schema:** `array of QualificationResponse`

#### 403 — Access denied to this resource

#### 404 — Resource not found

#### 500 — Internal server error

---

## POST /qualification — Create a qualification

Creates a new qualification that can be assigned to employees or contractors.

### Request Body
Information to use when creating a new qualification record:
**Required:** True

- **Content-Type:** `application/json`
- **Schema:** `CertificationRequest`

```json
{
  "name": "Updated Request",
  "category": "external",
  "certification_type_id": 81,
  "description": "This describes the certification"
}
```


### Responses

#### 201 — Resource created
- **Content-Type:** `application/json`
- **Schema:** `QualificationResponse`

```json
{
  "id": 1206,
  "created": "2021-11-26T14:35:28+13:00",
  "modified": "2021-12-31T14:35:28+13:00",
  "created_by": 35,
  "name": "Qualification Name",
  "category": "external",
  "description": "This describes the certification",
  "holders": 2,
  "status": "Active"
}
```

#### 403 — Access denied to this resource

#### 409 — Certification already exists with name

#### 500 — Internal server error

---

## GET /qualification/{id} — Retrieve a qualification

Retrieves a single qualification, filtering with the provided ID.

### Parameters
- **id** (path · integer(int32) · required): ID of the qualification to retrieve

### Responses

#### 200 — Qualification returned
- **Content-Type:** `application/json`
- **Schema:** `QualificationResponse`

```json
{
  "id": 1206,
  "created": "2021-11-26T14:35:28+13:00",
  "modified": "2021-12-31T14:35:28+13:00",
  "created_by": 35,
  "name": "Qualification Name",
  "category": "external",
  "description": "This describes the certification",
  "holders": 2,
  "status": "Active"
}
```

- **Content-Type:** `application/xml`
- **Schema:** `QualificationResponse`

#### 403 — Access denied to resource

#### 404 — Resource not found

#### 500 — Internal server error

---

## PUT /qualification/{id} — Update a qualification

Updates the details of a qualification.

### Parameters
- **id** (path · integer(int32) · required): ID of the qualification to update

### Request Body
Qualification information to use when updating a qualification record:
**Required:** True

- **Content-Type:** `application/json`
- **Schema:** `CertificationRequest`

```json
{
  "name": "Updated Request",
  "category": "external",
  "certification_type_id": 81,
  "description": "This describes the certification"
}
```


### Responses

#### 200 — Updated qualification
- **Content-Type:** `application/json`
- **Schema:** `CertificationResponse`

```json
{
  "id": 1206,
  "created": "2021-11-26T14:35:28+13:00",
  "modified": "2021-12-31T14:35:28+13:00",
  "created_by": 35,
  "name": "Certification Name",
  "category": "internal",
  "certification_type_id": 81,
  "description": "This describes the certification",
  "holders": 2,
  "status": "Active",
  "staff_audience": "locations",
  "contractor_audience": "groups",
  "staff_departments": [
    {
      "id": 556,
      "name": "Example Name"
    }
  ],
  "staff_locations": [
    {
      "id": 556,
      "name": "Example Name"
    }
  ],
  "staff_roles": [
    {
      "id": 556,
      "name": "Example Name"
    }
  ],
  "sp_groups": [
    {
      "id": 556,
      "name": "Example Name"
    }
  ],
  "sp_locations": [
    {
      "id": 556,
      "name": "Example Name"
    }
  ],
  "sp_roles": [
    {
      "id": 556,
      "name": "Example Name"
    }
  ]
}
```

- **Content-Type:** `application/xml`
- **Schema:** `CertificationResponse`

#### 403 — Access denied to this resource

#### 409 — Certification already exists with that name

#### 500 — Internal server error

---

## DELETE /qualification/{id} — Delete a qualification

Deletes a qualification using the provided ID.

### Parameters
- **id** (path · integer(int32) · required): ID of the qualification to delete

### Responses

#### 204 — Qualification deleted

#### 403 — Access denied to resource

#### 404 — Resource not found

#### 500 — Internal server error

---

## Tag: Qualification holders (deprecated)

The qualifications endpoints have been deprecated as Qualifications Management has been renamed Certifications Management. Use the [certification holders](/openapi/wol/tag/Certification-holders/) endpoints to access this data.

## GET /qualification/{id}/holder — List all qualification holders

Returns a list of the employees or contractors that hold the specified qualification.

A comma separated list of tag:value search parameters, a colon separator performs an exact match, a percent separator performs a starts-with search and greater/less than search number/date ranges.

### Parameters
- **id** (path · integer(int32) · required): ID of the qualification to retrieve holders for (example: `5`)

### Responses

#### 200 — List of employees or contractors that hold the qualification
- **Content-Type:** `application/json`
- **Schema:** `array of CertificationHolderResponse`

```json
[
  {
    "id": 218,
    "created": "2021-11-26T14:37:12+13:00",
    "modified": "2021-11-30T14:37:12+13:00",
    "certification_id": 1205,
    "record_id": 823431,
    "record_type": "staff",
    "validfrom": "2021-11-27",
    "validto": "2022-11-27",
    "certification_number": "WFL900",
    "holder_name": "Robert Jordan",
    "holder_email": "example-email@whosonlocation.com",
    "certification_name": "Window Fitting Licence",
    "type": "Licence",
    "documents": 1
  }
]
```

- **Content-Type:** `application/xml`
- **Schema:** `array of CertificationHolderResponse`

#### 403 — Access denied to this resource

#### 404 — Resource not found

#### 500 — Internal server error

---

## POST /qualification/{id}/holder — Create a qualification holder

Adds a new qualification holder association.

### Parameters
- **id** (path · integer(int32) · required): ID of the qualification to create a holder association for

### Request Body
Information use to create the qualification holder association:
**Required:** True

- **Content-Type:** `application/json`
- **Schema:** `CertificationHolderRequest`

```json
{
  "record_id": 823431,
  "record_type": "staff",
  "validfrom": "2021-11-27",
  "validto": "2022-11-27",
  "certification_number": "WFL900"
}
```


### Responses

#### 201 — Qualification holder created
- **Content-Type:** `application/json`
- **Schema:** `CertificationHolderResponse`

```json
{
  "id": 218,
  "created": "2021-11-26T14:37:12+13:00",
  "modified": "2021-11-30T14:37:12+13:00",
  "certification_id": 1205,
  "record_id": 823431,
  "record_type": "staff",
  "validfrom": "2021-11-27",
  "validto": "2022-11-27",
  "certification_number": "WFL900",
  "holder_name": "Robert Jordan",
  "holder_email": "example-email@whosonlocation.com",
  "certification_name": "Window Fitting Licence",
  "type": "Licence",
  "documents": 1
}
```

#### 403 — Access denied to this resource

#### 500 — Internal server error

---

## GET /qualification/{id}/holder/{hid} — Retrieve a qualification holder

Retrieves the details of a specific qualification holder.

### Parameters
- **id** (path · integer(int32) · required): ID of the qualification to retrieve holders for
- **hid** (path · integer(int32) · required): ID of the qualification holder to retrieve

### Responses

#### 200 — List of employees or contractors that hold the qualification
- **Content-Type:** `application/json`
- **Schema:** `CertificationHolderResponse`

```json
{
  "id": 218,
  "created": "2021-11-26T14:37:12+13:00",
  "modified": "2021-11-30T14:37:12+13:00",
  "certification_id": 1205,
  "record_id": 823431,
  "record_type": "staff",
  "validfrom": "2021-11-27",
  "validto": "2022-11-27",
  "certification_number": "WFL900",
  "holder_name": "Robert Jordan",
  "holder_email": "example-email@whosonlocation.com",
  "certification_name": "Window Fitting Licence",
  "type": "Licence",
  "documents": 1
}
```

- **Content-Type:** `application/xml`
- **Schema:** `CertificationHolderResponse`

#### 403 — Access denied to this resource

#### 404 — Resource not found

#### 500 — Internal server error

---

## PUT /qualification/{id}/holder/{hid} — Update a qualification holder

Updates a qualification holder's details.

### Parameters
- **id** (path · integer(int32) · required): ID of the qualification
- **hid** (path · integer(int32) · required): ID of the qualification holder association to update

### Request Body
Information use to create the qualification holder association
**Required:** True

- **Content-Type:** `application/json`
- **Schema:** `CertificationHolderRequest`

```json
{
  "record_id": 823431,
  "record_type": "staff",
  "validfrom": "2021-11-27",
  "validto": "2022-11-27",
  "certification_number": "WFL900"
}
```


### Responses

#### 200 — Updated resource
- **Content-Type:** `application/json`
- **Schema:** `CertificationHolderResponse`

```json
{
  "id": 218,
  "created": "2021-11-26T14:37:12+13:00",
  "modified": "2021-11-30T14:37:12+13:00",
  "certification_id": 1205,
  "record_id": 823431,
  "record_type": "staff",
  "validfrom": "2021-11-27",
  "validto": "2022-11-27",
  "certification_number": "WFL900",
  "holder_name": "Robert Jordan",
  "holder_email": "example-email@whosonlocation.com",
  "certification_name": "Window Fitting Licence",
  "type": "Licence",
  "documents": 1
}
```

- **Content-Type:** `application/xml`
- **Schema:** `CertificationHolderResponse`

#### 403 — Access denied to this resource

#### 500 — Internal server error

---

## DELETE /qualification/{id}/holder/{hid} — Delete a qualification holder

Deletes the specified qualification holder association record.

### Parameters
- **id** (path · integer(int32) · required): ID of the qualification
- **hid** (path · integer(int32) · required): ID of the qualification holder association to delete

### Responses

#### 204 — Qualification holder deleted

#### 403 — Access denied to resource

#### 404 — Resource not found

#### 500 — Internal server error

---

## GET /qualification/{id}/holder/{hid}/document — List all qualification documents

Retrieves a list of documents for a specified qualification holder and specified qualification.

### Parameters
- **id** (path · integer(int32) · required): ID of the qualification
- **hid** (path · integer(int32) · required): ID of the qualification holder

### Responses

#### 200 — List of qualification holder documents
- **Content-Type:** `application/json`
- **Schema:** `array of DocumentResponse`

```json
[
  {
    "id": 70,
    "modified": "2021-11-29T08:12:24+13:00",
    "name": "file-example",
    "type": "pdf",
    "size": 469513,
    "link": "https://your.site.com/storage/file-example.pdf"
  }
]
```

- **Content-Type:** `application/xml`
- **Schema:** `array of DocumentResponse`

#### 403 — Access denied to resource

#### 404 — Resource not found

#### 500 — Internal server error

---

## Tag: Qualification holder documents (deprecated)

The qualifications endpoints have been deprecated as Qualifications Management has been renamed Certifications Management. Use the [certification holder documents](/openapi/wol/tag/Certification-holder-documents/) endpoints to access this data.

## POST /qualification/{id}/holder/{hid}/document — Add a qualification document

Adds a new qualification holder document.

### Parameters
- **id** (path · integer(int32) · required): ID of the qualification
- **hid** (path · integer(int32) · required): ID of the qualification holder

### Request Body
**Required:** True

- **Content-Type:** `multipart/form-data`
- **Schema:** `object`

### Responses

#### 200 — Document added

#### 403 — Access denied to this resource

#### 500 — Internal server error

---

## GET /qualification/{id}/holder/{hid}/document/{did} — Retrieve a qualification document

Retrieves a qualification holder document, filtering with the provided ID.

### Parameters
- **id** (path · integer(int32) · required): ID of the qualification
- **hid** (path · integer(int32) · required): ID of the qualification holder
- **did** (path · integer(int32) · required): ID of the document to retrieve

### Responses

#### 200 — The specified document to be retrieved
- **Content-Type:** `application/json`
- **Schema:** `DocumentResponse`

```json
{
  "id": 70,
  "modified": "2021-11-29T08:12:24+13:00",
  "name": "file-example",
  "type": "pdf",
  "size": 469513,
  "link": "https://your.site.com/storage/file-example.pdf"
}
```

- **Content-Type:** `application/xml`
- **Schema:** `DocumentResponse`

#### 403 — Access denied to resource

#### 404 — Resource not found

#### 500 — Internal server error

---

## DELETE /qualification/{id}/holder/{hid}/document/{did} — Delete a qualification document

Deletes the specified qualification holder document.

### Parameters
- **id** (path · integer(int32) · required): ID of the qualification
- **hid** (path · integer(int32) · required): ID of the qualification holder
- **did** (path · integer(int32) · required): ID of the document to delete

### Responses

#### 204 — Document deleted

#### 403 — Access denied to resource

#### 404 — Resource not found

#### 500 — Internal server error

---

## Tag: Qualification types (deprecated)

The qualifications endpoints have been deprecated as Qualifications Management has been renamed Certifications Management. Use the [certification types](/openapi/wol/tag/Certification-types/) endpoints to access this data.

## GET /qualification/types — List all qualification types

Retrieves a list of all qualification types.

A comma separated list of tag:value search parameters, a colon separator performs an exact match, a percent separator performs a starts-with search and greater/less than search number/date ranges.

### Responses

#### 200 — List of qualification types
- **Content-Type:** `application/json`
- **Schema:** `array of CertificationTypeResponse`

```json
[
  {
    "id": 79,
    "created": "2021-11-26T14:33:41+13:00",
    "modified": "2022-11-26T14:33:41+13:00",
    "name": "Certificate"
  }
]
```

- **Content-Type:** `application/xml`
- **Schema:** `array of CertificationTypeResponse`

#### 403 — Access denied to this resource

#### 404 — Resource not found

#### 500 — Internal server error

---

## POST /qualification/types — Create a qualification type

Adds a new qualification type to assign to a qualification.

### Request Body
Type of qualification to add:
**Required:** True

- **Content-Type:** `application/json`
- **Schema:** `CertificationTypeRequest`

```json
{
  "name": "Diploma"
}
```


### Responses

#### 201 — Qualification type created
- **Content-Type:** `application/json`
- **Schema:** `CertificationTypeResponse`

```json
{
  "id": 79,
  "created": "2021-11-26T14:33:41+13:00",
  "modified": "2022-11-26T14:33:41+13:00",
  "name": "Certificate"
}
```

- **Content-Type:** `application/xml`
- **Schema:** `CertificationTypeResponse`

#### 403 — Access denied to this resource

#### 409 — Type already exists

#### 500 — Internal server error

---

## GET /qualification/types/{id} — Retrieve a qualification type

Retrieves a single qualification type, filtering with the provided ID.

### Parameters
- **id** (path · integer(int32) · required): ID of the qualification type to retrieve

### Responses

#### 200 — Qualification type response
- **Content-Type:** `application/json`
- **Schema:** `CertificationTypeResponse`

```json
{
  "id": 79,
  "created": "2021-11-26T14:33:41+13:00",
  "modified": "2022-11-26T14:33:41+13:00",
  "name": "Certificate"
}
```

- **Content-Type:** `application/xml`
- **Schema:** `CertificationTypeResponse`

#### 403 — Access denied to resource

#### 404 — Resource not found

#### 500 — Internal server error

---

## PUT /qualification/types/{id} — Update a qualification type

Updates a single qualification type.

### Parameters
- **id** (path · integer(int32) · required): ID of the qualification type to update

### Request Body
Information used to create the qualification type record
**Required:** True

- **Content-Type:** `application/json`
- **Schema:** `CertificationTypeRequest`

```json
{
  "name": "Diploma"
}
```


### Responses

#### 200 — Updated qualification type
- **Content-Type:** `application/json`
- **Schema:** `CertificationTypeResponse`

```json
{
  "id": 79,
  "created": "2021-11-26T14:33:41+13:00",
  "modified": "2022-11-26T14:33:41+13:00",
  "name": "Certificate"
}
```

- **Content-Type:** `application/xml`
- **Schema:** `CertificationTypeResponse`

#### 403 — Access denied to this resource

#### 500 — Internal server error

---

## DELETE /qualification/types/{id} — Delete a qualification type

Deletes the specified qualification type.

### Parameters
- **id** (path · integer(int32) · required): ID of the QualificationType to delete

### Responses

#### 204 — Qualification type deleted

#### 403 — Access denied to resource

#### 404 — Resource not found

#### 500 — Internal server error

---

## Tag: Communities

## GET /community — List of all communities

Returns a list of all communities

A comma separated list of tag:value search parameters, a colon separator performs an exact match, a percent separator performs a starts-with search and a greater/less than separator searches number/date ranges.

### Parameters
- **q** (query · string): Filter by any community value, (example: `name%Student`)

### Responses

#### 200 — List of communities
- **Content-Type:** `application/json`
- **Schema:** `array of CommunityResponse`

```json
[
  {
    "id": 1206,
    "created": "2021-11-26T14:35:28+13:00",
    "modified": "2021-12-31T14:35:28+13:00",
    "name": "Students",
    "active": true,
    "is_evac_enabled": true,
    "evac_primary_filter": "name"
  }
]
```

- **Content-Type:** `application/xml`
- **Schema:** `array of CommunityResponse`

#### 403 — Access denied to resource

#### 404 — Resource not found

#### 500 — Internal server error

---

## POST /community — Create a community

Creates a new community.

### Request Body
Information to use when creating a new community record.
**Required:** True

- **Content-Type:** `application/json`
- **Schema:** `CommunityRequest`

```json
{
  "name": "Updated Request",
  "active": true,
  "is_evac_enabled": true,
  "evac_primary_filter": "name"
}
```


### Responses

#### 201 — Community created
- **Content-Type:** `application/json`
- **Schema:** `CommunityResponse`

```json
{
  "id": 1206,
  "created": "2021-11-26T14:35:28+13:00",
  "modified": "2021-12-31T14:35:28+13:00",
  "name": "Students",
  "active": true,
  "is_evac_enabled": true,
  "evac_primary_filter": "name"
}
```

#### 403 — Access denied to this resource

#### 500 — Internal server error

---

## GET /community/{id} — Retrieve a community

Retrieves a single community, filtering with the provided ID.

### Parameters
- **id** (path · integer(int32) · required): ID of the community to retrieve

### Responses

#### 200
- **Content-Type:** `application/json`
- **Schema:** `CommunityResponse`

```json
{
  "id": 1206,
  "created": "2021-11-26T14:35:28+13:00",
  "modified": "2021-12-31T14:35:28+13:00",
  "name": "Students",
  "active": true,
  "is_evac_enabled": true,
  "evac_primary_filter": "name"
}
```

- **Content-Type:** `application/xml`
- **Schema:** `CommunityResponse`

#### 403 — Access denied to resource

#### 404 — Resource not found

#### 500 — Internal server error

---

## PUT /community/{id} — Update a community

Updates a community.

### Request Body
Information to use when updating a new community record.
**Required:** True

- **Content-Type:** `application/json`
- **Schema:** `CommunityRequest`

```json
{
  "name": "Updated Request",
  "active": true,
  "is_evac_enabled": true,
  "evac_primary_filter": "name"
}
```


### Responses

#### 201 — Community updated
- **Content-Type:** `application/json`
- **Schema:** `CommunityResponse`

```json
{
  "id": 1206,
  "created": "2021-11-26T14:35:28+13:00",
  "modified": "2021-12-31T14:35:28+13:00",
  "name": "Students",
  "active": true,
  "is_evac_enabled": true,
  "evac_primary_filter": "name"
}
```

#### 403 — Access denied to this resource

#### 500 — Internal server error

---

## Tag: Community field list

## GET /community/{community_id}/field — List all community fields

Retrieves a list of community fields

### Parameters
- **q** (query · string): Filter by any property (example: `id:1234`)

### Responses

#### 200 — List of community fields
- **Content-Type:** `application/json`
- **Schema:** `array of CommunityFieldResponse`

```json
[
  {
    "id": "cf_8aae3b",
    "title": "Basic Information",
    "type": "text",
    "is_mandatory": true,
    "is_unique": true,
    "is_hidden": true
  }
]
```

- **Content-Type:** `application/xml`
- **Schema:** `array of CommunityFieldResponse`

#### 403 — Access denied to this resource

#### 404 — Community not found

#### 500 — Internal server error

---

## Tag: Community field tabs

## GET /community/{community_id}/tab — List all community field tabs

Retrieves a list of community field tabs and their order

### Parameters
- **q** (query · string): Community ID to filter by. (example: `community/1/tab`)

### Responses

#### 200 — List of community field tabs
- **Content-Type:** `application/json`
- **Schema:** `array of CommunityTabResponse`

```json
[
  {
    "title": "Basic Information",
    "order": 1,
    "fields": {
      "id": "cf_8aae3b",
      "order": 1
    }
  }
]
```

- **Content-Type:** `application/xml`
- **Schema:** `array of CommunityTabResponse`

#### 403 — Access denied to this resource

#### 404 — Community not found

#### 500 — Internal server error

---

## Tag: Community member movements

## GET /community/{community_id}/movement — List all community-member movements

Retrieves a list of community-member movement records.

### Parameters
- **q** (query · integer(int32)): Filters the result. See [Using the API](/developer-portal/using-the-api/#filtering) for more information. (example: `community_id:123456`)
- **order** (query · string): Order by any element returned, prefix with a - to reverse order. (example: `id`)
- **limit** (query · integer(int32)): Limits the amount of records to return. (example: `10`)
- **If-Modified-Since** (header · string(int32)): A UTC timestamp (yyyy-mm-ddThh:mm:ss). Only entities created or modified since this timestamp will be returned e.g. 2009-11-12T00:00:00

### Responses

#### 200 — List of community-member movement records
- **Content-Type:** `application/json`
- **Schema:** `array of CommunityMemberMovementResponse`

```json
[
  {
    "id": 4938,
    "created": "2021-10-28T12:57:14+13:00",
    "modified": "2021-11-15T12:57:14+13:00",
    "location_id": 2538,
    "community_id": 30142,
    "member_id": 16348,
    "signed_in": "2021-10-28T12:57:14+13:00",
    "signed_out": "2021-10-28T16:57:14+13:00",
    "mode_in": "Sign In/Out Manager",
    "mode_out": "Sign In/Out Manager",
    "zone_id": 32,
    "zone_other": "office",
    "signed_in_by": 823493,
    "signed_out_by": 823493,
    "carpark_question": true,
    "carpark_registration": "DRU706",
    "carpark_number": "SP003",
    "purpose": "Repairs",
    "assistance": false,
    "expected": true,
    "accessdenied": false,
    "accessdenied_reason": "trigger",
    "loneworker": true,
    "interzone": true,
    "from_zone": 87,
    "selected_language": "english",
    "lacp_in_name": "Reception",
    "lacp_out_name": "Reception",
    "zone_group_name": "Maintenance",
    "photo_url": "your.site/storage/contractorphoto.jpg",
    "signout_photo_url": "your.site/storage/contractorphoto.jpg"
  }
]
```

- **Content-Type:** `application/xml`
- **Schema:** `array of CommunityMemberMovementResponse`

#### 403 — Access denied to this resource

#### 404 — Resource not found

#### 500 — Internal server error

---

## PUT /community/{community_id}/movement — Create or update a community-member movement

Creates or updates an community-member movement record.

### Request Body
Data required to create or update an community member movement:
**Required:** True

- **Content-Type:** `application/json`
- **Schema:** `CommunityMemberMovementRequest`

```json
{
  "onsite_status": "onsite",
  "location_id": 301,
  "member_id": 54,
  "lacp_id": 38,
  "zone_id": 3443,
  "zone_other": "",
  "carpark_question": false,
  "carpark_registration": "MRD346",
  "carpark_number": "0014",
  "purpose": "Water station repairs",
  "assistance": false,
  "expected": 60
}
```


### Responses

#### 200 — Updated movement
- **Content-Type:** `application/json`
- **Schema:** `CommunityMemberMovementResponse`

```json
{
  "id": 4938,
  "created": "2021-10-28T12:57:14+13:00",
  "modified": "2021-11-15T12:57:14+13:00",
  "location_id": 2538,
  "community_id": 30142,
  "member_id": 16348,
  "signed_in": "2021-10-28T12:57:14+13:00",
  "signed_out": "2021-10-28T16:57:14+13:00",
  "mode_in": "Sign In/Out Manager",
  "mode_out": "Sign In/Out Manager",
  "zone_id": 32,
  "zone_other": "office",
  "signed_in_by": 823493,
  "signed_out_by": 823493,
  "carpark_question": true,
  "carpark_registration": "DRU706",
  "carpark_number": "SP003",
  "purpose": "Repairs",
  "assistance": false,
  "expected": true,
  "accessdenied": false,
  "accessdenied_reason": "trigger",
  "loneworker": true,
  "interzone": true,
  "from_zone": 87,
  "selected_language": "english",
  "lacp_in_name": "Reception",
  "lacp_out_name": "Reception",
  "zone_group_name": "Maintenance",
  "photo_url": "your.site/storage/contractorphoto.jpg",
  "signout_photo_url": "your.site/storage/contractorphoto.jpg"
}
```

- **Content-Type:** `application/xml`
- **Schema:** `CommunityMemberMovementResponse`

#### 403 — Access denied to this resource

#### 500 — Internal server error

---

## GET /community/{community_id}/movement/{id} — Retrieve a community member movement x

Retrieves a single community member movement, filtering with the provided ID.

### Parameters
- **id** (path · integer(int32) · required): ID of the community member movement to retrieve

### Responses

#### 200
- **Content-Type:** `application/json`
- **Schema:** `CommunityMemberMovementResponse`

```json
{
  "id": 4938,
  "created": "2021-10-28T12:57:14+13:00",
  "modified": "2021-11-15T12:57:14+13:00",
  "location_id": 2538,
  "community_id": 30142,
  "member_id": 16348,
  "signed_in": "2021-10-28T12:57:14+13:00",
  "signed_out": "2021-10-28T16:57:14+13:00",
  "mode_in": "Sign In/Out Manager",
  "mode_out": "Sign In/Out Manager",
  "zone_id": 32,
  "zone_other": "office",
  "signed_in_by": 823493,
  "signed_out_by": 823493,
  "carpark_question": true,
  "carpark_registration": "DRU706",
  "carpark_number": "SP003",
  "purpose": "Repairs",
  "assistance": false,
  "expected": true,
  "accessdenied": false,
  "accessdenied_reason": "trigger",
  "loneworker": true,
  "interzone": true,
  "from_zone": 87,
  "selected_language": "english",
  "lacp_in_name": "Reception",
  "lacp_out_name": "Reception",
  "zone_group_name": "Maintenance",
  "photo_url": "your.site/storage/contractorphoto.jpg",
  "signout_photo_url": "your.site/storage/contractorphoto.jpg"
}
```

- **Content-Type:** `application/xml`
- **Schema:** `CommunityMemberMovementResponse`

#### 403 — Access denied to resource

#### 404 — Resource not found

#### 500 — Internal server error

---

## Tag: CommunityMembers

## GET /community/{community_id}/member — List of all community members

Returns a list of all community members

A comma separated list of tag:value search parameters, a colon separator performs an exact match, a percent separator performs a starts-with search and a greater/less than separator searches number/date ranges.

### Parameters
- **q** (query · string): Filter by any community member value, (example: `year:3`)

### Responses

#### 200 — List of community members
- **Content-Type:** `application/json`
- **Schema:** `array of CommunityMemberResponse`

```json
[
  {
    "id": 1206,
    "created": "2021-11-26T14:35:28+13:00",
    "modified": "2021-12-31T14:35:28+13:00",
    "external_id": "C123456789",
    "location_id": 35,
    "community_id": 77,
    "name": "John Doe",
    "title": "Mr",
    "email": "example@example.org",
    "mobile": "0212345566",
    "customfields": [
      {
        "id": 556,
        "name": "Example Name"
      }
    ]
  }
]
```

- **Content-Type:** `application/xml`
- **Schema:** `array of CommunityMemberResponse`

#### 403 — Access denied to resource

#### 404 — Resource not found

#### 500 — Internal server error

---

## POST /community/{community_id}/member — Create a community member

Creates a new community member.

### Request Body
Information to use when creating a new community member record.
**Required:** True

- **Content-Type:** `application/json`
- **Schema:** `CommunityMemberRequest`

```json
{
  "location_id": 35,
  "community_id": 77,
  "external_id": "C123456789",
  "name": "John Doe",
  "title": "Mr",
  "email": "example@example.org",
  "mobile": "0212345566"
}
```


### Responses

#### 201 — Community created
- **Content-Type:** `application/json`
- **Schema:** `CommunityMemberResponse`

```json
{
  "id": 1206,
  "created": "2021-11-26T14:35:28+13:00",
  "modified": "2021-12-31T14:35:28+13:00",
  "external_id": "C123456789",
  "location_id": 35,
  "community_id": 77,
  "name": "John Doe",
  "title": "Mr",
  "email": "example@example.org",
  "mobile": "0212345566",
  "customfields": [
    {
      "id": 556,
      "name": "Example Name"
    }
  ]
}
```

#### 403 — Access denied to this resource

#### 500 — Internal server error

---

## GET /community/{community_id}/member/{id} — Retrieve a community member

Retrieves a single community member, filtering with the provided ID.

### Parameters
- **id** (path · integer(int32) · required): ID of the community member to retrieve

### Responses

#### 200
- **Content-Type:** `application/json`
- **Schema:** `CommunityMemberResponse`

```json
{
  "id": 1206,
  "created": "2021-11-26T14:35:28+13:00",
  "modified": "2021-12-31T14:35:28+13:00",
  "external_id": "C123456789",
  "location_id": 35,
  "community_id": 77,
  "name": "John Doe",
  "title": "Mr",
  "email": "example@example.org",
  "mobile": "0212345566",
  "customfields": [
    {
      "id": 556,
      "name": "Example Name"
    }
  ]
}
```

- **Content-Type:** `application/xml`
- **Schema:** `CommunityMemberResponse`

#### 403 — Access denied to resource

#### 404 — Resource not found

#### 500 — Internal server error

---

## PUT /community/{community_id}/member/{id} — Update a community member

Updates the details of a community member.

### Parameters
- **id** (path · integer(int32) · required): ID of the community member to update

### Request Body
Information used to create the community member record
**Required:** True

- **Content-Type:** `application/json`
- **Schema:** `CommunityMemberUpdateRequest`

```json
{
  "name": "Harry Dresden",
  "external_id": "C123456789",
  "year": 12,
  "title": "Mr",
  "email": "emailaddress@mailinator.com",
  "mobile": "0212345566"
}
```


### Responses

#### 200 — Updated resource
- **Content-Type:** `application/json`
- **Schema:** `CommunityMemberResponse`

```json
{
  "id": 1206,
  "created": "2021-11-26T14:35:28+13:00",
  "modified": "2021-12-31T14:35:28+13:00",
  "external_id": "C123456789",
  "location_id": 35,
  "community_id": 77,
  "name": "John Doe",
  "title": "Mr",
  "email": "example@example.org",
  "mobile": "0212345566",
  "customfields": [
    {
      "id": 556,
      "name": "Example Name"
    }
  ]
}
```

- **Content-Type:** `application/xml`
- **Schema:** `CommunityMemberResponse`

#### 403 — Access denied to this resource

#### 500 — Internal server error

---

## DELETE /community/{community_id}/member/{id} — Delete a community member

Deletes a community member using the provided ID.

### Parameters
- **id** (path · integer(int32) · required): ID of the community member to delete

### Responses

#### 204 — Community member deleted

#### 403 — Access denied to resource

#### 404 — Resource not found

#### 500 — Internal server error

---

# Schema Definitions (90)

## AssetRequest

Asset request body data

| Field | Type | Required | Description | Example |
|-------|------|----------|-------------|---------|
| `type_id` | integer(int32) |  | Type ID of the asset | `1` |
| `name` | string |  | Name of the asset | `"Laptop 001"` |
| `tag` | string |  | Tag of the asset | `"TAG001"` |
| `reference` | string |  | Reference of the asset | `"REF001"` |
| `department_id` | integer(int32) |  | Department ID of the asset | `1` |
| `notes` | string |  | Notes for the asset | `"Notes about the asset"` |
| `geo_lat` | string |  | Geolocation latitude of the asset | `"12.34"` |
| `geo_lon` | string |  | Geolocation longitude of the asset | `"56.78"` |
| `owner_type` | string: 'org', 'sp' |  |  | `"org"` |
| `owner_staff_id` | integer(int32) |  | Asset owner staff ID, should only be set if owner_type is 'org' | `1583` |
| `owner_sp_id` | integer(int32) |  | Asset owner contractor ID, should only be set if owner_type is 'sp' | `2700` |
| `location_id` | integer(int32) |  | Location ID of the asset | `1` |
---

## AssetResponse

Asset Response schema

| Field | Type | Required | Description | Example |
|-------|------|----------|-------------|---------|
| `id` | integer(int32) |  | Asset ID | `1` |
| `type_id` | integer(int32) |  | Type ID of the asset | `1` |
| `name` | string |  | Name of the asset | `"Laptop 001"` |
| `tag` | string |  | Tag of the asset | `"TAG001"` |
| `reference` | string |  | Reference of the asset | `"REF001"` |
| `department_id` | integer(int32) |  | Department ID of the asset | `1` |
| `notes` | string |  | Notes for the asset | `"Notes about the asset"` |
| `geo_lat` | string |  | Geolocation latitude of the asset | `"12.34"` |
| `geo_lon` | string |  | Geolocation longitude of the asset | `"56.78"` |
| `owner_type` | string: 'org', 'sp' |  |  | `"org"` |
| `owner_staff_id` | integer(int32) |  | Asset owner staff ID | `1` |
| `owner_sp_id` | integer(int32) |  | Asset owner contractor ID | `1` |
| `location_id` | integer(int32) |  | Location ID of the asset | `1` |
| `status` | string: 'in', 'out' |  |  | `"in"` |
---

## AssetgroupRequest

Asset Group request body data

| Field | Type | Required | Description | Example |
|-------|------|----------|-------------|---------|
| `name` | string |  | Name of asset group | `"Laptops Group"` |
| `class` | string: 'transfer', 'fixed' |  |  | `"fixed"` |
| `description` | string |  | Description of asset group | `"This group includes the assets that belong to this location"` |
---

## AssetgroupResponse

Asset Group response schema

| Field | Type | Required | Description | Example |
|-------|------|----------|-------------|---------|
| `id` | integer(int32) |  |  | `90008` |
| `name` | string |  | Name of asset group | `"Laptops Group"` |
| `class` | string: 'transfer', 'fixed' |  |  | `"fixed"` |
| `description` | string |  | Description of asset group | `"This group includes the assets that belong to this location"` |
---

## AssetmovementRequest

Asset movement request body data

| Field | Type | Required | Description | Example |
|-------|------|----------|-------------|---------|
| `assigned_type` | string: 'staff', 'spm', 'visitor' |  | Type of the entity to whom the asset is being assigned | `"staff"` |
| `assigned_staff_id` | integer(int32) |  | ID of the staff to whom the asset is being assigned, should only be set if assigned_type is 'staff' | `1` |
| `assigned_spm_id` | integer(int32) |  | ID of the contractor member to whom the asset is being assigned, should only be set if assigned_type is 'spm' | `1` |
| `assigned_visitor_id` | integer(int32) |  | ID of the visitor to whom the asset is being assigned, should only be set if assigned_type is 'visitor' | `1` |
| `dueback` | string(date-time) |  | Dueback date for the asset | `"2023-05-01 13:10:00"` |
---

## AssetmovementResponse

Asset movement response body data

| Field | Type | Required | Description | Example |
|-------|------|----------|-------------|---------|
| `asset_id` | integer(int32) |  | Asset ID | `1` |
| `assigned_type` | string: 'staff', 'spm', 'visitor' |  | Type of the entity to whom the asset is being assigned | `"staff"` |
| `assigned_staff_id` | integer(int32) |  | ID of the staff to whom the asset is being assigned | `1` |
| `assigned_spm_id` | integer(int32) |  | ID of the contractor member to whom the asset is being assigned | `1` |
| `assigned_visitor_id` | integer(int32) |  | ID of the visitor to whom the asset is being assigned | `1` |
| `signed_out` | string(date-time) |  | Datetime when the asset is signed out | `"2023-04-18 13:15:00"` |
| `signed_in` | string(date-time) |  | Datetime when the asset is signed in | `"2023-04-18 13:15:00"` |
| `dueback` | string(date-time) |  | Dueback date for the asset | `"2023-05-01 13:15:07"` |
| `notes` | string |  | Notes for the asset movement | `"Notes about the asset movement"` |
| `issued_out` | string(int32) |  | ID of the employee by whom the asset is issued out | `"10008"` |
| `issued_in` | string(int32) |  | ID of the employee by whom the asset is issued in | `"20008"` |
---

## AssettypeRequest

Asset Type request body data

| Field | Type | Required | Description | Example |
|-------|------|----------|-------------|---------|
| `name` | string |  | Name of the asset type | `"Macbooks"` |
| `group_id` | integer(int32) |  | Group ID of the asset type. | `"10008"` |
| `description` | string |  | Description of the asset type | `"A portable device type"` |
---

## AssettypeResponse

Asset Type response schema

| Field | Type | Required | Description | Example |
|-------|------|----------|-------------|---------|
| `id` | integer(int32) |  | ID of the asset type | `"10008"` |
| `name` | string |  | Name of the asset type | `"Laptop"` |
| `group_id` | integer |  | ID of the asset group that this asset type belongs to | `"20000"` |
| `description` | string |  | Description of the asset type | `"A portable device type"` |
---

## AuditResponse

| Field | Type | Required | Description | Example |
|-------|------|----------|-------------|---------|
| `id` | integer(int32) |  |  | `823431` |
| `audit_timestamp` | string(date-time) |  |  | `"2021-08-12T10:54:13+12:00"` |
| `audit_event` | string |  | Type of audit log: INSERT, UPDATE or DELETE | `"UPDATE"` |
| `audit_table` | string |  | Table name of the record change | `"staff"` |
| `user_name` | string |  | Email address of the logged in user user which initiated the record change | `"john@example.com"` |
| `user_type` | string |  | User type who initiated the record change. Options are api, kiosk, staff, support | `"staff"` |
| `record_id` | integer(int32) |  | The unique identifier of the record that was changed | `823431` |
| `data_changed` | object |  | An array of columns with the changes that were made during the record change | `[{"column_name": "name", "change_from": "John Smith", "change_to": "John B Smith` |
| `data_old` | object |  | An array of columns from the original record before modification | `{"id": 588, "from": "Smith Corp", "name": "John Smith", "created": "2022-05-06 0` |
| `data_new` | object |  | An array of columns from the original record after modification | `{"id": 588, "from": "Smith Corporation", "name": "John B Smith", "created": "202` |
---

## BasicIdNamePair

| Field | Type | Required | Description | Example |
|-------|------|----------|-------------|---------|
| `id` | integer(int32) |  |  | `556` |
| `name` | string |  |  | `"Example Name"` |
---

## CertificationHolderRequest

| Field | Type | Required | Description | Example |
|-------|------|----------|-------------|---------|
| `record_id` | integer(int32) |  | This is the ID of the staff or contractor member being given the qualification | `823431` |
| `record_type` | string: 'staff', 'sp' |  |  | `"staff"` |
| `validfrom` | string(date) |  |  | `"2021-11-27"` |
| `validto` | string(date) |  |  | `"2022-11-27"` |
| `certification_number` | string |  |  | `"WFL900"` |
---

## CertificationHolderResponse

| Field | Type | Required | Description | Example |
|-------|------|----------|-------------|---------|
| `id` | integer(int32) |  |  | `218` |
| `created` | string(date-time) |  |  | `"2021-11-26T14:37:12+13:00"` |
| `modified` | string(date-time) |  |  | `"2021-11-30T14:37:12+13:00"` |
| `certification_id` | integer(int32) |  |  | `1205` |
| `record_id` | integer(int32) |  |  | `823431` |
| `record_type` | string: 'staff', 'sp' |  |  | `"staff"` |
| `validfrom` | string(date) |  |  | `"2021-11-27"` |
| `validto` | string(date) |  |  | `"2022-11-27"` |
| `certification_number` | string |  |  | `"WFL900"` |
| `holder_name` | string |  |  | `"Robert Jordan"` |
| `holder_email` | string |  |  | `"example-email@whosonlocation.com"` |
| `certification_name` | string |  |  | `"Window Fitting Licence"` |
| `type` | string |  |  | `"Licence"` |
| `documents` | integer(int32) |  |  | `1` |
---

## CertificationRequest

| Field | Type | Required | Description | Example |
|-------|------|----------|-------------|---------|
| `name` | string |  |  | `"Updated Request"` |
| `category` | string |  |  | `"external"` |
| `certification_type_id` | integer(int32) |  |  | `81` |
| `description` | string |  |  | `"This describes the certification"` |
---

## CertificationResponse

| Field | Type | Required | Description | Example |
|-------|------|----------|-------------|---------|
| `id` | integer(int32) |  |  | `1206` |
| `created` | string(date-time) |  |  | `"2021-11-26T14:35:28+13:00"` |
| `modified` | string(date-time) |  |  | `"2021-12-31T14:35:28+13:00"` |
| `created_by` | integer(int32) |  |  | `35` |
| `name` | string |  |  | `"Certification Name"` |
| `category` | string |  |  | `"internal"` |
| `certification_type_id` | integer(int32) |  |  | `81` |
| `description` | string |  |  | `"This describes the certification"` |
| `holders` | integer(int32) |  |  | `2` |
| `status` | string: 'Active', 'Inactive' |  |  | `"Active"` |
| `staff_audience` | string: 'locations', 'departments', 'roles', 'all' |  |  | `"locations"` |
| `contractor_audience` | string: 'locations', 'groups', 'roles', 'all' |  |  | `"groups"` |
| `staff_departments` | array of BasicIdNamePair |  |  | `[{"id": 556, "name": "Example Name"}]` |
| `staff_locations` | array of BasicIdNamePair |  |  | `[{"id": 556, "name": "Example Name"}]` |
| `staff_roles` | array of BasicIdNamePair |  |  | `[{"id": 556, "name": "Example Name"}]` |
| `sp_groups` | array of BasicIdNamePair |  |  | `[{"id": 556, "name": "Example Name"}]` |
| `sp_locations` | array of BasicIdNamePair |  |  | `[{"id": 556, "name": "Example Name"}]` |
| `sp_roles` | array of BasicIdNamePair |  |  | `[{"id": 556, "name": "Example Name"}]` |
---

## CertificationTypeRequest

| Field | Type | Required | Description | Example |
|-------|------|----------|-------------|---------|
| `name` | string |  |  | `"Diploma"` |
---

## CertificationTypeResponse

| Field | Type | Required | Description | Example |
|-------|------|----------|-------------|---------|
| `id` | integer(int32) |  |  | `79` |
| `created` | string(date-time) |  |  | `"2021-11-26T14:33:41+13:00"` |
| `modified` | string(date-time) |  |  | `"2022-11-26T14:33:41+13:00"` |
| `name` | string |  |  | `"Certificate"` |
---

## CommunityFieldResponse

| Field | Type | Required | Description | Example |
|-------|------|----------|-------------|---------|
| `id` | string |  |  | `"cf_8aae3b"` |
| `title` | string |  |  | `"Basic Information"` |
| `type` | string |  |  | `"text"` |
| `is_mandatory` | boolean(bool) |  |  | `true` |
| `is_unique` | boolean(bool) |  |  | `true` |
| `is_hidden` | boolean(bool) |  |  | `true` |
---

## CommunityMemberMovementRequest

| Field | Type | Required | Description | Example |
|-------|------|----------|-------------|---------|
| `onsite_status` | string | Yes | Describes whether the movement is 'onsite' or 'offsite' | `"onsite"` |
| `location_id` | integer(int32) | Yes |  | `301` |
| `member_id` | integer(int32) | Yes |  | `54` |
| `lacp_id` | integer(int32) |  | Location access point ID. Not mandatory. | `38` |
| `zone_id` | integer(int32) |  | ID of the zone being signed into. Not mandatory. | `3443` |
| `zone_other` | string |  | Zone note for this movement. Not mandatory | `""` |
| `carpark_question` | boolean |  | Can be set as either 1/0 or true/false. Not mandatory. | `false` |
| `carpark_registration` | string |  | Vehicle registration. Not mandatory. | `"MRD346"` |
| `carpark_number` | string |  | Carpark number. Not mandatory. | `"0014"` |
| `purpose` | string |  | Reason for visit. Not mandatory. | `"Water station repairs"` |
| `assistance` | boolean |  | Whether the member requires assistance. Not mandatory. | `false` |
| `expected` | integer(int32) |  | Expected time on-site in minutes. Not mandatory | `60` |
---

## CommunityMemberMovementResponse

| Field | Type | Required | Description | Example |
|-------|------|----------|-------------|---------|
| `id` | integer(int32) |  |  | `4938` |
| `created` | string(date-time) |  |  | `"2021-10-28T12:57:14+13:00"` |
| `modified` | string(date-time) |  |  | `"2021-11-15T12:57:14+13:00"` |
| `location_id` | integer(int32) |  |  | `2538` |
| `community_id` | integer(int32) |  |  | `30142` |
| `member_id` | integer(int32) |  |  | `16348` |
| `signed_in` | string(date-time) |  |  | `"2021-10-28T12:57:14+13:00"` |
| `signed_out` | string(date-time) |  |  | `"2021-10-28T16:57:14+13:00"` |
| `mode_in` | string: 'Auto', 'Manual', 'Kiosk', 'Touchless', 'Sign In/Out Manager' |  |  | `"Sign In/Out Manager"` |
| `mode_out` | string: 'Auto', 'Manual', 'Kiosk', 'Touchless', 'Sign In/Out Manager' |  |  | `"Sign In/Out Manager"` |
| `zone_id` | integer(in32) |  |  | `32` |
| `zone_other` | string |  |  | `"office"` |
| `signed_in_by` | integer(int32) |  |  | `823493` |
| `signed_out_by` | integer(int32) |  |  | `823493` |
| `carpark_question` | boolean(bool) |  |  | `true` |
| `carpark_registration` | string |  |  | `"DRU706"` |
| `carpark_number` | string |  |  | `"SP003"` |
| `purpose` | string |  |  | `"Repairs"` |
| `assistance` | boolean |  |  | `false` |
| `expected` | boolean |  | Expected time on-site in minutes. | `true` |
| `accessdenied` | boolean |  |  | `false` |
| `accessdenied_reason` | string |  |  | `"trigger"` |
| `loneworker` | boolean |  |  | `true` |
| `interzone` | boolean |  |  | `true` |
| `from_zone` | integer(int) |  |  | `87` |
| `selected_language` | string |  |  | `"english"` |
| `lacp_in_name` | string |  |  | `"Reception"` |
| `lacp_out_name` | string |  |  | `"Reception"` |
| `zone_group_name` | string |  |  | `"Maintenance"` |
| `photo_url` | string |  |  | `"your.site/storage/contractorphoto.jpg"` |
| `signout_photo_url` | string |  |  | `"your.site/storage/contractorphoto.jpg"` |
---

## CommunityMemberRequest

| Field | Type | Required | Description | Example |
|-------|------|----------|-------------|---------|
| `location_id` | integer(int32) |  |  | `35` |
| `community_id` | integer(int32) |  |  | `77` |
| `external_id` | string |  |  | `"C123456789"` |
| `name` | string |  |  | `"John Doe"` |
| `title` | string |  |  | `"Mr"` |
| `email` | string |  |  | `"example@example.org"` |
| `mobile` | string |  |  | `"0212345566"` |
---

## CommunityMemberResponse

| Field | Type | Required | Description | Example |
|-------|------|----------|-------------|---------|
| `id` | integer(int32) |  |  | `1206` |
| `created` | string(date-time) |  |  | `"2021-11-26T14:35:28+13:00"` |
| `modified` | string(date-time) |  |  | `"2021-12-31T14:35:28+13:00"` |
| `external_id` | string |  |  | `"C123456789"` |
| `location_id` | integer(int32) |  |  | `35` |
| `community_id` | integer(int32) |  |  | `77` |
| `name` | string |  |  | `"John Doe"` |
| `title` | string |  |  | `"Mr"` |
| `email` | string |  |  | `"example@example.org"` |
| `mobile` | string |  |  | `"0212345566"` |
| `customfields` | array of BasicIdNamePair |  |  | `[{"id": 556, "name": "Example Name"}]` |
---

## CommunityMemberUpdateRequest

| Field | Type | Required | Description | Example |
|-------|------|----------|-------------|---------|
| `name` | string |  |  | `"Harry Dresden"` |
| `external_id` | string |  |  | `"C123456789"` |
| `year` | integer(int32) |  |  | `12` |
| `title` | string |  |  | `"Mr"` |
| `email` | string |  |  | `"emailaddress@mailinator.com"` |
| `mobile` | string |  |  | `"0212345566"` |
---

## CommunityRequest

| Field | Type | Required | Description | Example |
|-------|------|----------|-------------|---------|
| `name` | string |  |  | `"Updated Request"` |
| `active` | boolean(bool) |  |  | `true` |
| `is_evac_enabled` | boolean(bool) |  |  | `true` |
| `evac_primary_filter` | string |  |  | `"name"` |
---

## CommunityResponse

| Field | Type | Required | Description | Example |
|-------|------|----------|-------------|---------|
| `id` | integer(int32) |  |  | `1206` |
| `created` | string(date-time) |  |  | `"2021-11-26T14:35:28+13:00"` |
| `modified` | string(date-time) |  |  | `"2021-12-31T14:35:28+13:00"` |
| `name` | string |  |  | `"Students"` |
| `active` | boolean |  |  | `true` |
| `is_evac_enabled` | boolean |  |  | `true` |
| `evac_primary_filter` | boolean(string) |  |  | `"name"` |
---

## CommunityTabField

| Field | Type | Required | Description | Example |
|-------|------|----------|-------------|---------|
| `id` | string |  |  | `"cf_8aae3b"` |
| `order` | integer(int32) |  |  | `1` |
---

## CommunityTabResponse

| Field | Type | Required | Description | Example |
|-------|------|----------|-------------|---------|
| `title` | string |  |  | `"Basic Information"` |
| `order` | integer(int32) |  |  | `1` |
| `fields` | array of CommunityTabField |  |  | `{"id": "cf_8aae3b", "order": 1}` |
---

## ContractorInsuranceRequest

| Field | Type | Required | Description | Example |
|-------|------|----------|-------------|---------|
| `org_id` | integer(int32) |  |  | `4587` |
| `type` | string |  |  | `"other"` |
| `name` | string |  |  | `"Policy Underwriter"` |
| `reference` | string |  |  | `"Ref002"` |
| `value` | string |  |  | `"6000"` |
| `start` | string(date) |  |  | `"2021-11-30"` |
| `expires` | string(date) |  |  | `"2022-11-29"` |
---

## ContractorInsuranceResponse

| Field | Type | Required | Description | Example |
|-------|------|----------|-------------|---------|
| `id` | integer(int32) |  |  | `140` |
| `created` | string(date-time) |  |  | `"2021-11-29T08:10:09+13:00"` |
| `modified` | string(date-time) |  |  | `"2022-11-29T08:10:09+13:00"` |
| `org_id` | integer(int32) |  |  | `4587` |
| `org_name` | string |  |  | `"Example Organisation Name"` |
| `type` | string |  |  | `"other"` |
| `name` | string |  |  | `"Policy Underwriter"` |
| `reference` | string |  |  | `"Ref002"` |
| `value` | string |  |  | `"6000"` |
| `start` | string(date) |  |  | `"2021-11-30"` |
| `expires` | string(date) |  |  | `"2022-11-29"` |
| `status` | string |  |  | `"Active"` |
| `tags` | array of object |  |  | `[["asset", "insurance", "other"]]` |
| `documents` | array of DocumentResponse |  |  | `[{"id": 70, "modified": "2021-11-29T08:12:24+13:00", "name": "file-example", "ty` |
---

## ContractorLocation

| Field | Type | Required | Description | Example |
|-------|------|----------|-------------|---------|
| `id` | integer(int32) |  |  | `301` |
| `name` | string |  |  | `"Sub Office"` |
| `start` | string(date) |  |  | `"2021-12-01"` |
| `expires` | string(date) |  |  | `"2021-12-01"` |
---

## ContractorMemberOrganisation

| Field | Type | Required | Description | Example |
|-------|------|----------|-------------|---------|
| `id` | integer(int32) |  |  | `4587` |
| `name` | string |  |  | `"Head Office"` |
| `roles` | array of BasicIdNamePair |  |  | `[{"id": 556, "name": "Example Name"}]` |
| `locations` | array of ContractorLocation |  |  | `[{"id": 301, "name": "Sub Office", "start": "2021-12-01", "expires": "2021-12-01` |
| `locations_same_as_org` | boolean(bool) |  |  | `true` |
---

## ContractorMemberRequest

| Field | Type | Required | Description | Example |
|-------|------|----------|-------------|---------|
| `name` | string | Yes |  | `"New Member"` |
| `title` | string |  |  | `"Contractor"` |
| `email` | string |  |  | `"example@example.org"` |
| `altemail` | string |  |  | `"example-alt@example.org"` |
| `phone` | string |  |  | `"+1 206 555 0123"` |
| `ice` | string |  |  | `"+1 206 555 0123"` |
| `cur_location` | integer(int32) |  |  | `301` |
| `extension` | string |  |  | `"047"` |
| `mobile` | string |  |  | `"+1 206 555 0123"` |
| `valid_from` | string(date) |  |  | `"2021-09-03"` |
| `valid_to` | string(date) |  |  | `"2026-09-02"` |
| `onsite_status` | string |  |  | `"onsite"` |
| `service_provider_id` | string |  |  | `"12345"` |
| `cf` | object |  | This will return an object of custom fields set for the contractor. The key of the custom field corresponds to the ID of the custom fields when using the Customfield endpoint | `{"123": "custom-field value"}` |
---

## ContractorMemberResponse

| Field | Type | Required | Description | Example |
|-------|------|----------|-------------|---------|
| `id` | integer(int32) |  |  | `16348` |
| `created` | string(date-time) |  |  | `"2021-09-03T15:17:57+12:00"` |
| `modified` | string(date-time) |  |  | `"2021-11-29T08:39:55+13:00"` |
| `name` | string |  |  | `"First Contractor"` |
| `setup_by` | integer(int32) |  |  | `823431` |
| `title` | string |  |  | `"Contractor"` |
| `service_provider_id` | string |  |  | `"contract_047"` |
| `email` | string |  |  | `"contractor-email@whosonlocation.com"` |
| `altemail` | string |  |  | `"alternative-address@whosonlocation.com"` |
| `phone` | string |  |  | `"+1 206 555 0123"` |
| `mobile` | string |  |  | `"+1 206 555 0123"` |
| `status` | string |  |  | `"active"` |
| `valid_from` | string(date) |  |  | `"2021-09-03"` |
| `valid_to` | string(date) |  |  | `"2026-09-02"` |
| `extension` | string |  |  | `"047"` |
| `ice` | string |  |  | `"+1 206 555 0123"` |
| `onsite_status` | string |  |  | `"onsite"` |
| `cur_location_id` | integer(int32) |  |  | `301` |
| `cur_sp_org_id` | integer(int32) |  |  | `4587` |
| `cur_location` | string |  |  | `"Head Office"` |
| `sp_orgs` | array of ContractorMemberOrganisation |  |  | `[{"id": 4587, "name": "Head Office", "roles": [{"id": 556, "name": "Example Name` |
| `logs` | array of object |  |  | `[null]` |
| `tokens` | array of object |  |  | `[null]` |
| `customfields` | object |  | This will return an array of custom fields set for contractor members. The ID of the custom field corresponds to the ID of the custom fields when using the Customfield endpoint | `{"123": "custom-field value"}` |
---

## ContractorMembershipLocation

| Field | Type | Required | Description | Example |
|-------|------|----------|-------------|---------|
| `id` | integer(int32) |  |  | `1024` |
| `start` | string(date) |  | Describes when the contractor's membership for this location starts. Only needs to be provided if an expiration date is provided. | `"2021-11-27"` |
| `expires` | string(date) |  | Describes when the contractor's membership for this location expires. If this is left out it is assumed that the membership does not expire. | `"2021-11-27"` |
---

## ContractorMembershipRequest

| Field | Type | Required | Description | Example |
|-------|------|----------|-------------|---------|
| `org_id` | integer(int32) |  | ID of a contractor organisation | `405` |
| `member_id` | integer(int32) |  |  | `43` |
| `roles` | array of object |  |  | `[[44, 6, 73]]` |
| `locations` | array of ContractorMembershipLocation |  |  | `[{"id": 1024, "start": "2021-11-27", "expires": "2021-11-27"}]` |
---

## ContractorMembershipResponse

| Field | Type | Required | Description | Example |
|-------|------|----------|-------------|---------|
| `id` | string(date-time) |  |  | `13863` |
| `created` | string(date-time) |  |  | `"2021-09-03T15:17:59+12:00"` |
| `org_id` | integer(int32) |  |  | `4587` |
| `org_name` | string |  |  | `"Contractor Organisation"` |
| `member_id` | integer(int32) |  |  | `16348` |
| `member_name` | string |  |  | `"Contractor Name"` |
| `roles` | array of BasicIdNamePair |  |  | `[{"id": 556, "name": "Example Name"}]` |
| `locations` | array of ContractorLocation |  |  | `[{"id": 301, "name": "Sub Office", "start": "2021-12-01", "expires": "2021-12-01` |
| `locations_same_as_org` | boolean |  |  | `true` |
---

## ContractorMovementRequest

| Field | Type | Required | Description | Example |
|-------|------|----------|-------------|---------|
| `onsite_status` | string | Yes | Describes whether the movement is 'onsite' or 'offsite', if set to onsite the 'scanned_out' field is unset | `"onsite"` |
| `location_id` | integer(int32) | Yes |  | `301` |
| `sp_member_id` | integer(int32) | Yes |  | `54` |
| `sp_org_id` | integer(int32) | Yes |  | `504` |
| `print_kiosk_id` | integer(int32) |  | ID of the kiosk which has shared badge pass printing enabled. Not mandatory. | `301` |
| `lacp_id` | integer(int32) |  | Location access point ID. Not mandatory. | `38` |
| `zone_id` | integer(int32) |  | ID of the zone being signed into. Not mandatory. | `3` |
| `zone_other` | string |  | Zone note for this movement. Not mandatory | `""` |
| `scanned_in` | string |  | Scanned in note. Only set when onsite_status = 'onsite'. Not mandatory. | `""` |
| `scanned_out` | string |  | Scanned out note. Only set when onsite_status = 'offsite', gets unset when onsite_status = 'onsite'. Not mandatory. | `""` |
| `visiting_staff_id` | integer(int32) |  | ID of staff member being visited. Not mandatory. | `8` |
| `cardnumber` | string |  | Sign in card number. Not mandatory. | `"cp013"` |
| `carpark_question` | boolean |  | Can be set as either 1/0 or true/false. Not mandatory. | `false` |
| `carpark_registration` | string |  | Vehicle registration. Not mandatory. | `"MRD346"` |
| `carpark_number` | string |  | Carpark number. Not mandatory. | `"0014"` |
| `pass` | integer(int32) |  | Not mandatory | `1054` |
| `other_pass` | string |  | Any other pass that might need to be submitted. Not mandatory. | `"Contractor pass"` |
| `sp_purpose` | string |  | Reason for contractor visit. Not mandatory. | `"Water station repairs"` |
| `assistance` | boolean |  | Whether or not a contractor requires assistance. Not mandatory. | `false` |
| `expected` | integer(int32) |  | Expected time on-site. Not mandatory | `60` |
| `loneworker` | object |  | Describes whether or not the contractor is working alone. Not mandatory. | `` |
| `so_staff_id` | integer(int32) |  | Staff safety officer ID. Not mandatory | `40` |
| `so_spm_id` | integer(int32) |  | Contractor manager safety officer ID. Not mandatory | `89` |
---

## ContractorMovementResponse

| Field | Type | Required | Description | Example |
|-------|------|----------|-------------|---------|
| `id` | integer(int32) |  |  | `4938` |
| `created` | string(date-time) |  |  | `"2021-10-28T12:57:14+13:00"` |
| `modified` | string(date-time) |  |  | `"2021-11-15T12:57:14+13:00"` |
| `location_id` | integer(int32) |  |  | `301` |
| `sp_member_id` | integer(int32) |  |  | `16348` |
| `sp_org_id` | integer(int32) |  |  | `4587` |
| `signed_in` | string(date-time) |  |  | `"2021-10-28T12:57:14+13:00"` |
| `signed_out` | string(date-time) |  |  | `"2021-10-28T16:57:14+13:00"` |
| `mode_in` | string: 'Auto', 'Manual', 'Kiosk', 'Touchless', 'Token Scan' |  |  | `"Sign In/Out Manager"` |
| `mode_out` | string: 'Auto', 'Manual', 'Kiosk', 'Touchless', 'Token Scan' |  |  | `"Sign In/Out Manager"` |
| `zone_id` | integer(in32) |  |  | `32` |
| `zone_other` | string |  |  | `"zone"` |
| `signed_in_by` | integer(int32) |  |  | `823493` |
| `signed_out_by` | integer(int32) |  |  | `823493` |
| `scanned_in` | string |  | Scanned in note. | `""` |
| `scanned_out` | string |  | Scanned out note. | `""` |
| `visiting_staff_id` | integer(int32) |  |  | `823431` |
| `cardnumber` | string |  |  | `"c001"` |
| `carpark_question` | boolean(bool) |  |  | `true` |
| `carpark_registration` | string |  |  | `"DRU706"` |
| `carpark_number` | string |  |  | `"SP003"` |
| `pass` | integer(int) |  |  | `20` |
| `other_pass` | string |  |  | `"opass047"` |
| `sp_purpose` | string |  |  | `"Repairs"` |
| `assistance` | boolean |  |  | `false` |
| `expected` | boolean |  |  | `true` |
| `accessdenied` | boolean |  |  | `false` |
| `accessdenied_reason` | string |  |  | `"No reason required"` |
| `loneworker` | boolean |  |  | `true` |
| `so_staff_id` | integer(int32) |  |  | `455` |
| `so_spm_id` | integer(int32) |  |  | `458` |
| `interzone` | boolean |  |  | `true` |
| `breathtest` | string |  |  | `false` |
| `from_zone` | integer(int) |  |  | `87` |
| `selected_language` | string |  |  | `"english"` |
| `lacp_in_name` | string |  |  | `"Reception"` |
| `lacp_out_name` | string |  |  | `"Reception"` |
| `zone_group_name` | string |  |  | `"Maintenance"` |
| `signed_in_by_kiosk` | boolean |  |  | `false` |
| `signed_out_by_kiosk` | boolean |  |  | `false` |
| `photo_url` | string |  |  | `"your.site/storage/contractorphoto.pdf"` |
| `signout_photo_url` | string |  |  | `"your.site/storage/contractorphoto.pdf"` |
| `signed_in_lat` | string |  |  | `"Latitude-value of coordinates for sign-in (Decimal Degrees)"` |
| `signed_in_lon` | string |  |  | `"Longitude-value of coordinates for sign-in (Decimal Degrees)"` |
| `signed_in_accuracy` | string |  |  | `"Precision for sign-in coordinates"` |
| `signed_out_lat` | string |  |  | `"Latitude-value of coordinates for sign-out (Decimal Degrees)"` |
| `signed_out_lon` | string |  |  | `"Longitude-value of coordinates for sign-out (Decimal Degrees)"` |
| `signed_out_accuracy` | string |  |  | `"Precision for sign-out coordinates"` |
---

## ContractorOrganisationRequest

| Field | Type | Required | Description | Example |
|-------|------|----------|-------------|---------|
| `name` | string |  |  | `"Contractor Organisation Name"` |
| `type` | string: 'Charitable organization', 'Discretionary Trading Trust', 'Government Agency', 'Family Partnership', 'Fixed Unit Trust' |  |  | `""` |
| `tradingas` | string |  |  | `"Contractor Organisation Name"` |
| `legalid` | string |  |  | `"lid005"` |
| `legalid_type` | string |  |  | `"legal ID type"` |
| `address` | string |  |  | `"2 Water Street, New York, NY, USA"` |
| `country` | string: 'AF', 'AL', 'DZ', 'AS', 'AD' |  |  | `"NZ"` |
| `email` | string |  |  | `"sp.organisation@whosonlocation.com"` |
| `phone` | string |  |  | `"+1 206 555 0123"` |
| `organization_owner` | integer(int32) |  |  | `823431` |
| `status` | string: 'pending', 'active', 'on-hold', 'inactive' |  |  | `"active"` |
| `locations` | array of ContractorLocation |  |  | `[{"id": 301, "name": "Sub Office", "start": "2021-12-01", "expires": "2021-12-01` |
| `cf` | object |  | This will return an object of custom fields set for contractor-organisations. The key of the custom field corresponds to the ID of the custom fields when using the Customfield endpoint | `{"123": "custom-field value"}` |
---

## ContractorOrganisationResponse

| Field | Type | Required | Description | Example |
|-------|------|----------|-------------|---------|
| `id` | integer(int32) |  |  | `4587` |
| `created` | string(date-time) |  |  | `"2021-09-03T15:16:44+12:00"` |
| `modified` | string(date-time) |  |  | `"2021-11-29T09:51:00+13:00"` |
| `name` | string |  |  | `"Contractor Organisation Name"` |
| `setup_by` | integer(int32) |  |  | `653` |
| `type` | string |  |  | `"Charitable organization"` |
| `tradingas` | string |  |  | `"Contractor Organisation Name"` |
| `legalid` | string |  |  | `"lid005"` |
| `legalid_type` | string |  |  | `"legal ID type"` |
| `address` | string |  |  | `"2 Water Street, New York, NY, USA"` |
| `country` | string |  |  | `"NZ"` |
| `status` | string |  |  | `"active"` |
| `email` | string |  |  | `"sp.organisation@whosonlocation.com"` |
| `phone` | string |  |  | `"+1 206 555 0123"` |
| `locations` | array of ContractorLocation |  |  | `[{"id": 301, "name": "Sub Office", "start": "2021-12-01", "expires": "2021-12-01` |
| `policies` | integer(int32) |  |  | `2` |
| `members` | array of BasicIdNamePair |  |  | `[{"id": 556, "name": "Example Name"}]` |
| `organization_owner` | integer(int32) |  |  | `823431` |
| `logs` | array of object |  | NOTE: At the time of creating this documentation I was not able to figure out what format these logs are returned in | `[null]` |
| `customfields` | object |  | This will return an array of custom fields set for contractor organisations. The ID of the custom field corresponds to the ID of the custom fields when using the Customfield endpoint | `{"123": "custom-field value"}` |
---

## ContractorRoleResponse

| Field | Type | Required | Description | Example |
|-------|------|----------|-------------|---------|
| `role` | array of BasicIdNamePair |  |  | `[{"id": 556, "name": "Example Name"}]` |
---

## CustomQuestionnaireQuestionResponse

| Field | Type | Required | Description | Example |
|-------|------|----------|-------------|---------|
| `id` | integer(int32) |  |  | `976` |
| `created` | string(date-time) |  |  | `"2021-11-25T12:51:26+13:00"` |
| `modified` | string(date-time) |  |  | `"2021-11-30T12:51:26+13:00"` |
| `questionnaire_id` | integer(int32) |  |  | `498` |
| `question` | string |  |  | `"Example Waiver Question"` |
| `type` | string |  |  | `"waiver"` |
| `compulsory` | boolean(bool) |  |  | `true` |
| `answershare` | boolean(bool) |  |  | `true` |
| `order` | integer(int32) |  |  | `9999` |
| `parent_id` | integer(int32) |  |  | `974` |
| `parent_key` | integer(int32) |  |  | `0` |
| `version` | integer(int32) |  |  | `2` |
| `setup_by` | string(int32) |  |  | `855` |
| `settings_options` | array of object |  |  | `[["I acknowledge", "I do not acknowledge"]]` |
| `settings_options_unique` | array of object |  |  | `[[1, 2]]` |
| `settings_waiver` | string |  |  | `"Example waiver"` |
| `settings_video` | string |  |  | `"https://www.youtube.com/watch?v=dQw4w9WgXcQ"` |
| `settings_image` | SettingsImageResponse |  |  | `{"title": "Waiver Image", "thumb": "https://example.url/storage/cache/waverimage` |
| `settings_signature` | boolean(bool) |  |  | `true` |
| `settings_copy` | boolean(bool) |  |  | `true` |
| `settings_copy_host` | boolean(bool) |  |  | `true` |
| `settings_send_recipients` | array of object |  |  | `[[554, 2754]]` |
| `settings_email_non_staff` | string |  |  | `"example-email@whosonlocation.com"` |
---

## CustomQuestionnaireResponse

| Field | Type | Required | Description | Example |
|-------|------|----------|-------------|---------|
| `id` | integer(int32) |  |  | `498` |
| `created` | string(date-time) |  |  | `"2021-11-25T12:45:45+13:00"` |
| `modified` | string(date-time) |  |  | `"2021-11-25T12:51:48+13:00"` |
| `active` | boolean |  |  | `true` |
| `name` | string |  |  | `"Health Check"` |
| `record_type` | string |  |  | `"visitor"` |
| `setup_by` | integer(int32) |  |  | `823` |
| `frequency` | string |  |  | `"every"` |
| `frequency_days` | integer(int32) |  |  | `1` |
| `location_id` | integer(int32) |  |  | `301` |
---

## CustomQuestionnaireSubmissionAnswerResponse

| Field | Type | Required | Description | Example |
|-------|------|----------|-------------|---------|
| `id` | integer(int32) |  |  | `3155` |
| `created` | string(date-time) |  |  | `"2021-11-25T15:01:29+13:00"` |
| `modified` | string(date-time) |  |  | `"2021-12-25T15:01:29+13:00"` |
| `submission_id` | integer(int32) |  |  | `1741` |
| `question_id` | integer(int32) |  |  | `982` |
| `answer` | string |  |  | `"I acknowledge"` |
| `signature_url` | string |  |  | `"https://example.url/storage/signature3152png_zr75U796wybjTZ.png"` |
| `answer_multi` | array of string |  |  | `[["First floor", "Second floor"]]` |
---

## CustomQuestionnaireSubmissionResponse

| Field | Type | Required | Description | Example |
|-------|------|----------|-------------|---------|
| `id` | integer(int32) |  |  | `1740` |
| `created` | string(date-time) |  |  | `"2021-11-25T14:47:34+13:00"` |
| `modified` | string(date-time) |  |  | `"2021-12-11T14:47:34+13:00"` |
| `questionnaire_id` | integer(int32) |  |  | `498` |
| `sp_member_id` | integer(int32) |  |  | `55` |
| `visitor_id` | integer(int32) |  |  | `138` |
| `staff_id` | string(int32) |  |  | `7` |
| `movement_id` | integer(int32) |  |  | `18` |
| `source` | string |  |  | `"kiosk"` |
| `onsite_status` | string |  |  | `"onsite"` |
---

## CustomfieldElement

| Field | Type | Required | Description | Example |
|-------|------|----------|-------------|---------|
| `id` | integer(int32) |  |  | `741` |
| `created` | string(date-time) |  |  | `"2021-11-25T12:17:27+13:00"` |
| `modified` | string(date-time) |  |  | `"2021-11-27T12:17:27+13:00"` |
| `cf_tab_id` | integer(int32) |  |  | `89` |
| `is_visible` | boolean(bool) |  |  | `true` |
| `is_mandatory` | boolean(bool) |  |  | `true` |
| `is_unique` | boolean(bool) |  |  | `true` |
| `title` | string |  |  | `"Checkbox field"` |
| `config` | array of object |  |  | `[null]` |
| `created_by` | integer(int32) |  |  | `823431` |
| `modified_by` | integer(int32) |  |  | `823431` |
| `element_type` | string |  |  | `"checkbox"` |
---

## CustomfieldTab

| Field | Type | Required | Description | Example |
|-------|------|----------|-------------|---------|
| `id` | integer(int32) |  |  | `88` |
| `created` | string(date-time) |  |  | `"2021-11-25T12:14:32+13:00"` |
| `modified` | string(date-time) |  |  | `"2021-11-25T12:23:34+13:00"` |
| `title` | string |  |  | `"Profile Information"` |
| `sort` | integer(int32) |  |  | `2` |
| `created_by` | integer(int32) |  |  | `823` |
| `updated_by` | integer(int32) |  |  | `431` |
---

## DepartmentRequest

| Field | Type | Required | Description | Example |
|-------|------|----------|-------------|---------|
| `name` | string |  |  | `"Department of Examples"` |
---

## DepartmentResponse

Describes the format/shape of department response objects

| Field | Type | Required | Description | Example |
|-------|------|----------|-------------|---------|
| `id` | integer(int32) |  |  | `1658` |
| `created` | string(date-time) |  |  | `"2021-08-12T10:54:13+12:00"` |
| `modified` | string(date-time) |  |  | `"2021-08-13T10:54:13+12:00"` |
| `name` | string | Yes |  | `"Administration"` |
| `locations` | array of BasicIdNamePair |  |  | `[{"id": 556, "name": "Example Name"}]` |
---

## DocumentResponse

| Field | Type | Required | Description | Example |
|-------|------|----------|-------------|---------|
| `id` | integer(int32) |  |  | `70` |
| `modified` | string(date-time) |  |  | `"2021-11-29T08:12:24+13:00"` |
| `name` | string |  |  | `"file-example"` |
| `type` | string |  |  | `"pdf"` |
| `size` | integer(int64) |  |  | `469513` |
| `link` | string |  |  | `"https://your.site.com/storage/file-example.pdf"` |
---

## InductionHolderPostRequest

| Field | Type | Required | Description | Example |
|-------|------|----------|-------------|---------|
| `staff_id` | integer(int32) |  |  | `55` |
| `sp_member_id` | integer(int32) |  |  | `3001` |
| `completed` | string(date-time) |  |  | `"2021-11-25T22:01:09.385Z"` |
---

## InductionHolderPutRequest

| Field | Type | Required | Description | Example |
|-------|------|----------|-------------|---------|
| `completed` | string(date-time) |  |  | `"2021-11-25T22:01:09.385Z"` |
| `staff_id` | string(integer) |  |  | `"123"` |
| `sp_member_id` | string(integer) |  |  | `"123"` |
---

## InductionHolderResponse

| Field | Type | Required | Description | Example |
|-------|------|----------|-------------|---------|
| `id` | integer(int32) |  |  | `6856` |
| `completed` | string(date-time) |  |  | `"2021-11-26T00:00:00+13:00"` |
| `staff_id` | integer(int32) |  |  | `35` |
| `sp_member_id` | integer(int32) |  |  | `642` |
| `learner_name` | string |  |  | `"Robert Jordan"` |
| `learner_email` | string |  |  | `"example.email@whosonlocation.com"` |
| `learner_title` | string |  |  | `"Mr"` |
| `learner_from` | string |  |  | `"OnLocation"` |
| `status` | string |  |  | `"passed"` |
| `attempts` | integer(int32) |  |  | `1` |
| `attempts_left` | integer(int32) |  |  | `0` |
| `renew` | string(date) |  |  | `"2022-11-26"` |
---

## InductionRequest

| Field | Type | Required | Description | Example |
|-------|------|----------|-------------|---------|
| `name` | string |  |  | `"Employee induction course"` |
| `type` | string |  |  | `"internal/external"` |
| `description` | string |  |  | `"Induction course description"` |
| `status` | string |  |  | `"inactive"` |
| `audience_staff` | string |  |  | `"all/location/department/role/none"` |
| `audience_sp` | string |  |  | `"all/location/group/role/none"` |
| `renew` | integer(int32) |  |  | `12` |
| `audience_staff_member` | array of object |  |  | `[[281, 533]]` |
| `audience_sp_member` | array of object |  |  | `[[281, 533]]` |
---

## InductionResponse

| Field | Type | Required | Description | Example |
|-------|------|----------|-------------|---------|
| `id` | integer(int32) |  |  | `184` |
| `created` | string(date-time) |  |  | `"2021-11-03T13:19:14+13:00"` |
| `modified` | string(date-time) |  |  | `"2021-11-10T12:22:08+13:00"` |
| `setup_by` | integer(int32) |  |  | `773` |
| `name` | string |  |  | `"Employee induction course"` |
| `type` | string |  |  | `"online"` |
| `description` | string |  |  | `"Induction course description"` |
| `status` | string |  |  | `"inactive"` |
| `audience_staff` | string: 'all', 'location', 'department', 'role', 'none' |  |  | `"all"` |
| `audience_sp` | string: 'all', 'location', 'group', 'role', 'none' |  |  | `"all"` |
| `renew` | integer(int32) |  |  | `12` |
| `attempts` | integer(int32) |  |  | `1` |
| `inductees` | integer(int32) |  |  | `3` |
| `audience_staff_member` | array of BasicIdNamePair |  |  | `[{"id": 556, "name": "Example Name"}]` |
| `audience_sp_member` | array of BasicIdNamePair |  |  | `[{"id": 556, "name": "Example Name"}]` |
---

## LocationAccessPoint

| Field | Type | Required | Description | Example |
|-------|------|----------|-------------|---------|
| `id` | integer(int32) |  |  | `240` |
| `name` | string |  |  | `"G1 - Entry"` |
---

## LocationRequest

| Field | Type | Required | Description | Example |
|-------|------|----------|-------------|---------|
| `name` | string |  |  | `"Head Office"` |
| `address` | string |  |  | `" 1008 Tawa Terrace, Tawa, Wellington, New Zealand"` |
| `phone` | string |  |  | `"+1 206 555 0123"` |
| `postcode` | string |  |  | `"5021"` |
| `country` | string |  |  | `"NZ"` |
| `timezone` | string |  |  | `"Pacific/Auckland"` |
| `date_format` | string |  |  | `"d/m/Y"` |
---

## LocationResponse

Definition of a location inside an organisation

| Field | Type | Required | Description | Example |
|-------|------|----------|-------------|---------|
| `id` | integer(int32) |  |  | `301` |
| `created` | string(date-time) |  |  | `"2021-08-12T09:26:54+12:00"` |
| `modified` | string(date-time) |  |  | `"2021-09-03T09:26:54+12:00"` |
| `name` | string |  |  | `"Head Office"` |
| `address` | string |  |  | `" 1008 Tawa Terrace, Tawa, Wellington, New Zealand"` |
| `phone` | string |  |  | `"+1 206 555 0123"` |
| `external_id` | string |  |  | `"998"` |
| `postcode` | string |  |  | `"5021"` |
| `country` | string |  |  | `"NZ"` |
| `timezone` | string |  |  | `"Pacific/Auckland"` |
| `date_format` | string |  |  | `"d/m/Y"` |
| `staff_count` | integer(int32) |  |  | `13` |
| `lacps` | array of LocationAccessPoint |  | Location access points | `[{"id": 240, "name": "G1 - Entry"}]` |
---

## NotificationResponse

| Field | Type | Required | Description | Example |
|-------|------|----------|-------------|---------|
| `id` | integer(int32) |  |  | `192` |
| `created` | string(date-time) |  |  | `"2021-11-25T15:01:31+13:00"` |
| `modified` | string(date-time) |  |  | `"2021-11-25T15:01:33+13:00"` |
| `location_id` | integer(int32) |  |  | `301` |
| `visitor_id` | integer(int32) |  |  | `139` |
| `for_staff_id` | integer(int32) |  |  | `82` |
| `to_staff_id` | integer(int32) |  |  | `431` |
| `method` | string |  |  | `"sms"` |
| `type` | string |  |  | `"signin"` |
| `recipient` | string |  |  | `"64 212143775"` |
| `messageid` | string |  |  | `"18f007ae1767d54b6654bae703d472808634a123@yoursite.whosonlocation.com"` |
| `to_staff_name` | string |  |  | `"Great Scott"` |
| `for_staff_name` | string |  |  | `"Great Scott"` |
| `status` | array of VisitorPersonResponse |  |  | `[{"id": 293, "created": "2021-11-29T11:10:44+13:00", "modified": "2021-11-30T11:` |
---

## QualificationRequest

| Field | Type | Required | Description | Example |
|-------|------|----------|-------------|---------|
| `name` | string |  |  | `"Updated Request"` |
| `category` | string: 'external', 'internal' |  |  | `"external"` |
| `description` | string |  |  | `"This describes the qualification"` |
---

## QualificationResponse

| Field | Type | Required | Description | Example |
|-------|------|----------|-------------|---------|
| `id` | integer(int32) |  |  | `1206` |
| `created` | string(date-time) |  |  | `"2021-11-26T14:35:28+13:00"` |
| `modified` | string(date-time) |  |  | `"2021-12-31T14:35:28+13:00"` |
| `created_by` | integer(int32) |  |  | `35` |
| `name` | string |  |  | `"Qualification Name"` |
| `category` | string: 'external', 'internal' |  |  | `"external"` |
| `description` | string |  |  | `"This describes the certification"` |
| `holders` | integer(int32) |  |  | `2` |
| `status` | string: 'Active', 'Inactive' |  |  | `"Active"` |
---

## SettingsImageResponse

| Field | Type | Required | Description | Example |
|-------|------|----------|-------------|---------|
| `title` | string |  |  | `"Waiver Image"` |
| `thumb` | string |  |  | `"https://example.url/storage/cache/waverimage_qoiph7panWllx3_MTIwMCwxMjAw.png"` |
| `full` | string |  |  | `"https://example.url/storage/cache/waverimage_qoiph7panWllx3_MTIwMCwxMjAw.png"` |
---

## SpCategoryRequest

Contractor organization category request schema

| Field | Type | Required | Description | Example |
|-------|------|----------|-------------|---------|
| `name` | string |  | Name of the category | `"Transport Services"` |
---

## SpCategoryResponse

Contractor organization category response schema

| Field | Type | Required | Description | Example |
|-------|------|----------|-------------|---------|
| `id` | integer(int32) |  | Category ID | `1` |
| `name` | string |  | Name of the category | `"Transport Services"` |
| `created` | string(date-time) |  | Created time of the category | `"2023-05-12 09:30:00"` |
---

## SpGroupPutRequest

Contractor organization group request schema

| Field | Type | Required | Description | Example |
|-------|------|----------|-------------|---------|
| `name` | string |  | Name of the contractor organization group | `"Drivers"` |
---

## SpGroupRequest

Contractor organization group request schema

| Field | Type | Required | Description | Example |
|-------|------|----------|-------------|---------|
| `category_id` | integer(int32) |  | Contractor organization category ID | `1` |
| `name` | string |  | Name of the contractor organization group | `"Drivers"` |
---

## SpGroupResponse

Contractor organization group response schema

| Field | Type | Required | Description | Example |
|-------|------|----------|-------------|---------|
| `id` | integer(int32) |  | Contractor organization group ID | `1` |
| `name` | string |  | Name of the contractor organization group | `"Drivers"` |
| `category_id` | integer(int32) |  | Contractor organization category ID that the group belongs to | `1` |
| `created` | string(date-time) |  | Created time of the contractor organization group | `"2023-05-12 09:30:00"` |
---

## SpInsuranceTypeResponse

Contractor insurance type response schema

| Field | Type | Required | Description | Example |
|-------|------|----------|-------------|---------|
| `id` | integer(int32) |  | Insurance type ID | `1` |
| `name` | string |  | Name of the insurance type | `"Public Liability"` |
| `created` | string(date-time) |  | Date and time the insurance type was created | `"2025-01-08T06:04:00+00:00"` |
| `modified` | string(date-time) |  | Date and time the insurance type was last modified | `"2025-06-01T10:00:00+00:00"` |
---

## SpOrgGroupMembershipResponse

Contractor organization group membership item

| Field | Type | Required | Description | Example |
|-------|------|----------|-------------|---------|
| `id` | integer(int32) |  | Contractor organization group ID | `4` |
| `name` | string |  | Name of the contractor organization group | `"Electrical"` |
| `category_id` | integer(int32) |  | Category ID the group belongs to | `1` |
---

## StaffGlobalRoamingLocation

| Field | Type | Required | Description | Example |
|-------|------|----------|-------------|---------|
| `id` | integer(int32) |  | The ID of the roaming location | `1` |
| `name` | string |  | The name of the roaming location | `"Head Office"` |
---

## StaffListResponse

| Field | Type | Required | Description | Example |
|-------|------|----------|-------------|---------|
| `id` | integer(int32) |  |  | `823562` |
| `created` | string(date-time) |  |  | `"2021-10-06T15:35:50+13:00"` |
| `modified` | string(date-time) |  |  | `"2021-10-06T16:35:50+13:00"` |
| `account_status` | string |  |  | `"new"` |
| `last_login` | string(date-time) |  |  | `"2021-11-15T16:35:50+13:00"` |
| `onsite_status` | string |  |  | `"offsite"` |
| `name` | string |  |  | `"Staff Name"` |
| `title` | string |  |  | `"Mr"` |
| `altname` | string |  |  | `"Daffy"` |
| `email` | string |  |  | `"john.doe@example.org"` |
| `phone` | string |  |  | `"+1 206 555 0123"` |
| `mobile` | string |  |  | `"+1 206 555 0123"` |
| `ice` | string |  |  | `"+1 206 555 0123"` |
| `setup_method` | string |  |  | `"manual"` |
| `employee_id` | string |  |  | `"1005"` |
| `location` | string |  |  | `"Head Office"` |
| `cur_location` | string |  |  | `"Head Office"` |
| `department` | string |  |  | `"Administration"` |
| `roles` | array of object |  |  | `[["Safety Office", "Fire Warden"]]` |
| `role_types` | array of object |  |  | `[["Non-Host", "Safety Operator"]]` |
| `tokens` | array of StaffTokens |  |  | `[{"type": "", "number": 0, "issued": "", "expiry": ""}]` |
| `remote` | integer(int32) |  |  | `1` |
| `customfields` | object |  | This will return an array of custom fields set for employees. The ID of the custom field corresponds to the ID of the custom fields when using the Customfield endpoint | `{"123": "custom-field value"}` |
| `globalroaming` | integer: 0, 1 |  | This determines whether the employee has global roaming permission. | `1` |
---

## StaffMovementRequest

| Field | Type | Required | Description | Example |
|-------|------|----------|-------------|---------|
| `onsite_status` | string: 'onsite', 'offsite' | Yes |  | `"onsite"` |
| `location_id` | integer(int32) | Yes |  | `38` |
| `location` | string |  |  | `"Head Office"` |
| `staff_id` | integer(int32) | Yes |  | `201` |
| `lacp_id` | integer(int32) |  |  | `201` |
| `zone_id` | integer(int32) |  |  | `201` |
| `remote` | boolean |  |  | `false` |
| `changed_at` | string(date-time) |  |  | `"2021-11-25 22:01:09"` |
| `print_kiosk_id` | integer(int32) |  | ID of the kiosk which has shared badge pass printing enabled. Not mandatory. | `301` |
---

## StaffMovementResponse

| Field | Type | Required | Description | Example |
|-------|------|----------|-------------|---------|
| `id` | integer(int32) |  |  | `167503` |
| `created` | string(date-time) |  |  | `"2021-09-09T12:52:31+12:00"` |
| `modified` | string(date-time) |  |  | `"2021-09-09T13:52:31+12:00"` |
| `location_id` | integer(int32) |  |  | `301` |
| `staff_id` | integer(int32) |  |  | `823431` |
| `signed_in` | string(date-time) |  |  | `"2021-09-09T12:52:31+12:00"` |
| `signed_out` | string(date-time) |  |  | `"2021-09-09T14:52:31+12:00"` |
| `mode_in` | string: 'Auto', 'Manual', 'Kiosk', 'Touchless', 'Token Scan' |  |  | `"Sign In/Out Manager"` |
| `mode_out` | string: 'Auto', 'Manual', 'Kiosk', 'Touchless', 'Token Scan' |  |  | `"Sign In/Out Manager"` |
| `signed_in_by` | integer(int32) |  |  | `823431` |
| `signed_out_by` | integer(int32) |  |  | `823431` |
| `scanned_in` | string |  | Scanned in note | `""` |
| `scanned_out` | string |  | Scanned out note. | `""` |
| `expected` | boolean |  |  | `true` |
| `assistance` | boolean |  |  | `false` |
| `accessdenied` | boolean |  |  | `false` |
| `loneworker` | boolean |  |  | `true` |
| `so_staff_id` | integer(int32) |  |  | `584` |
| `zone_id` | integer(int32) |  |  | `8` |
| `interzone` | boolean |  |  | `true` |
| `remote` | boolean |  |  | `false` |
| `name` | string |  |  | `"Harold Brunning"` |
| `title` | string |  |  | `"Mr"` |
| `email` | string |  |  | `"staff.movement@whosonlocation.com"` |
| `mobile` | string |  |  | `"+1 206 555 0123"` |
| `phone` | string |  |  | `"+1 206 555 0123"` |
| `lacp_in_name` | string |  |  | `"Ground floor entry"` |
| `lacp_out_name` | string |  |  | `"Ground floor entry"` |
| `signed_in_lat` | string |  |  | `"Latitude-value of coordinates for sign-in (Decimal Degrees)"` |
| `signed_in_lon` | string |  |  | `"Longitude-value of coordinates for sign-in (Decimal Degrees)"` |
| `signed_in_accuracy` | string |  |  | `"Precision for sign-in coordinates"` |
| `signed_out_lat` | string |  |  | `"Latitude-value of coordinates for sign-out (Decimal Degrees)"` |
| `signed_out_lon` | string |  |  | `"Longitude-value of coordinates for sign-out (Decimal Degrees)"` |
| `signed_out_accuracy` | string |  |  | `"Precision for sign-out coordinates"` |
---

## StaffRequest

| Field | Type | Required | Description | Example |
|-------|------|----------|-------------|---------|
| `name` | string | Yes |  | `"Staff Name"` |
| `title` | string |  |  | `"Mr"` |
| `altname` | string |  |  | `"Daffy"` |
| `email` | string |  |  | `"example.staff@whosonlocation.com"` |
| `phone` | string |  |  | `"+1 206 555 0123"` |
| `mobile` | string |  |  | `"+1 206 555 0123"` |
| `ice` | string |  |  | `"+1 206 555 0123"` |
| `employee_id` | integer(int32) |  |  | `301` |
| `onsite_status` | string |  |  | `"offsite"` |
| `location` | string | Yes |  | `"Head Office"` |
| `roletype` | array of object |  |  | `[["Non-Host", "Safety Operator"]]` |
| `customfields` | array of object |  | This field is deprecated and will be removed in the future. Please use the `cf` field instead. | `[["Deprecated, please use the cf-parameter instead"]]` |
| `cf` | object |  | This will return an object of custom fields set for employees. The key of the custom field corresponds to the ID of the custom fields when using the Customfield endpoint | `{"123": "custom-field value"}` |
| `globalroaming` | integer: 0, 1 |  | This determines whether the employee has global roaming permission. | `1` |
| `globalroaming_locations` | array of object |  | The IDs of the global roaming locations to which the employee has access. This field is effective when `globalroaming` is set to `true` or the `globalroaming` parameter is ommitted and the employee already has global roaming permission. | `[[1500, 3000, 2600]]` |
---

## StaffResponse

*(Composed schema: allOf)*

| Field | Type | Required | Description | Example |
|-------|------|----------|-------------|---------|
| `id` | integer(int32) |  |  | `823562` |
| `created` | string(date-time) |  |  | `"2021-10-06T15:35:50+13:00"` |
| `modified` | string(date-time) |  |  | `"2021-10-06T16:35:50+13:00"` |
| `account_status` | string |  |  | `"new"` |
| `last_login` | string(date-time) |  |  | `"2021-11-15T16:35:50+13:00"` |
| `onsite_status` | string |  |  | `"offsite"` |
| `name` | string |  |  | `"Staff Name"` |
| `title` | string |  |  | `"Mr"` |
| `altname` | string |  |  | `"Daffy"` |
| `email` | string |  |  | `"john.doe@example.org"` |
| `phone` | string |  |  | `"+1 206 555 0123"` |
| `mobile` | string |  |  | `"+1 206 555 0123"` |
| `ice` | string |  |  | `"+1 206 555 0123"` |
| `setup_method` | string |  |  | `"manual"` |
| `employee_id` | string |  |  | `"1005"` |
| `location` | string |  |  | `"Head Office"` |
| `cur_location` | string |  |  | `"Head Office"` |
| `department` | string |  |  | `"Administration"` |
| `roles` | array of object |  |  | `[["Safety Office", "Fire Warden"]]` |
| `role_types` | array of object |  |  | `[["Non-Host", "Safety Operator"]]` |
| `tokens` | array of StaffTokens |  |  | `[{"type": "", "number": 0, "issued": "", "expiry": ""}]` |
| `remote` | integer(int32) |  |  | `1` |
| `customfields` | object |  | This will return an array of custom fields set for employees. The ID of the custom field corresponds to the ID of the custom fields when using the Customfield endpoint | `{"123": "custom-field value"}` |
| `globalroaming` | integer: 0, 1 |  | This determines whether the employee has global roaming permission. | `1` |
---

## StaffRoleTypeResponse

Employee role type. When no location_id filter is provided, includes a 'locations' array showing which locations the role is assigned to. When filtered by location_id, returns a flat list where id is the role ID and created/modified are from the location_role assignment record.

| Field | Type | Required | Description | Example |
|-------|------|----------|-------------|---------|
| `id` | integer(int32) |  | Role ID. | `5` |
| `name` | string |  | Name of the role | `"Account Manager"` |
| `created` | string(date-time) |  | Date and time the role was created | `"2025-01-08T06:04:00+00:00"` |
| `modified` | string(date-time) |  | Date and time the role was last modified | `"2025-06-01T10:00:00+00:00"` |
| `locations` | array of object |  | Locations this role is assigned to. Only returned when no location_id filter is applied. | `[{"id": 1, "name": "Head Office"}]` |
---

## StaffTokens

| Field | Type | Required | Description | Example |
|-------|------|----------|-------------|---------|
| `type` | string |  |  | `""` |
| `number` | integer(int64) |  |  | `0` |
| `issued` | string(date-time) |  |  | `""` |
| `expiry` | string(date-time) |  |  | `""` |
---

## VisitorEventPostRequest

| Field | Type | Required | Description | Example |
|-------|------|----------|-------------|---------|
| `name` | string |  |  | `"Elayne Trakand"` |
| `type` | string: 'visit' |  |  | `"visit"` |
| `location_id` | integer(int32) |  |  | `301` |
| `visiting_staff_id` | integer(int32) |  |  | `534` |
| `signed_in` | string(date-time) |  |  | `"2021-11-25T12:01:09.380Z"` |
| `signed_out` | string(date-time) |  |  | `"2021-11-25T13:01:09.380Z"` |
| `email` | string |  |  | `"example.email@example.org"` |
| `from` | string |  |  | `"Andor"` |
| `phone` | string |  |  | `"+1 206 555 0123"` |
| `mobile` | string |  |  | `"+1 206 555 0123"` |
| `title` | string |  |  | `"Ms"` |
| `assistance` | object |  |  | `false` |
| `purpose` | string |  |  | `"visiting"` |
| `carpark_registration` | string |  |  | `"MRU366"` |
| `carpark_number` | string |  |  | `"5"` |
| `cardnumber` | string |  |  | `"cn001"` |
| `id_verification_reference` | string(boolean) |  |  | `true` |
| `id_verification_type` | string |  |  | `"licence"` |
| `lacp_in_id` | integer(int32) |  |  | `239` |
| `lacp_out_id` | integer(int32) |  |  | `239` |
---

## VisitorEventPutRequest

| Field | Type | Required | Description | Example |
|-------|------|----------|-------------|---------|
| `type` | string |  |  | `"visit"` |
| `location_id` | integer(int32) |  |  | `301` |
| `visiting_staff_id` | integer(int32) |  |  | `534` |
| `signed_in` | string(date-time) |  |  | `"2021-11-25T12:01:09.380Z"` |
| `signed_out` | string(date-time) |  |  | `"2021-11-25T13:01:09.380Z"` |
| `mode_id` | string |  |  | `"Kiosk"` |
| `mode_out` | string |  |  | `"Kiosk"` |
| `signed_in_by` | integer(int32) |  |  | `823` |
| `signed_out_by` | integer(int32) |  |  | `823` |
| `name` | string |  |  | `"Elayne Trakand"` |
| `email` | string |  |  | `"example.email@whosonlocation.com"` |
| `from` | string |  |  | `"Andor"` |
| `phone` | string |  |  | `"+1 206 555 0123"` |
| `mobile` | string |  |  | `"+1 206 555 0123"` |
| `title` | string |  |  | `"Ms"` |
| `assistance` | object |  |  | `false` |
| `expected` | boolean |  |  | `false` |
| `pass` | boolean |  |  | `true` |
| `purpose` | string |  |  | `"Just visiting"` |
| `carpark_question` | boolean |  |  | `false` |
| `carpark_registration` | string |  |  | `"MRU366"` |
| `carpark_number` | string |  |  | `"5"` |
| `cardnumber` | string |  |  | `"cn001"` |
| `id_verification_reference` | string(boolean) |  |  | `true` |
| `id_verification_type` | string |  |  | `"licence"` |
| `accessdenied` | boolean |  |  | `false` |
| `visiting_staff_name` | string |  |  | `"Staff Member"` |
| `signed_in_by_name` | string |  |  | `"Person in reception"` |
| `signed_out_by_name` | string |  |  | `"Person in reception"` |
| `onsite` | string |  |  | `"13 minutes 32 seconds"` |
| `lacp_in_id` | integer(int32) |  |  | `239` |
| `lacp_out_id` | integer(int32) |  |  | `239` |
---

## VisitorEventResponse

| Field | Type | Required | Description | Example |
|-------|------|----------|-------------|---------|
| `id` | integer(int32) |  |  | `138` |
| `created` | string(date-time) |  |  | `"2021-11-25T14:47:34+13:00"` |
| `modified` | string(date-time) |  |  | `"2021-11-25T15:01:08+13:00"` |
| `location_id` | integer(int32) |  |  | `301` |
| `created_staff_id` | integer(int32) |  |  | `349` |
| `visiting_staff_id` | integer(int32) |  |  | `823` |
| `signed_in` | string(date-time) |  |  | `"2021-11-25T14:47:36+13:00"` |
| `signed_out` | string(date-time) |  |  | `"2021-11-25T15:01:08+13:00"` |
| `mode_in` | string |  |  | `"Kiosk"` |
| `mode_out` | string |  |  | `"Kiosk"` |
| `signed_in_by` | integer(int32) |  |  | `823` |
| `signed_out_by` | integer(int32) |  |  | `823` |
| `name` | string |  |  | `"Elayne Trakand"` |
| `email` | string |  |  | `"example-email@whosonlocation.com"` |
| `from` | string |  |  | `"Andor"` |
| `phone` | string |  |  | `"+1 206 555 0123"` |
| `mobile` | string |  |  | `"+1 206 555 0123"` |
| `title` | string |  |  | `"Ms"` |
| `assistance` | boolean |  |  | `false` |
| `expected` | boolean |  |  | `false` |
| `printed` | boolean |  |  | `true` |
| `pass` | boolean |  |  | `true` |
| `purpose` | string |  |  | `"Just visiting"` |
| `carpark_question` | boolean |  |  | `false` |
| `carpark_registration` | string |  |  | `"MRU366"` |
| `carpark_number` | string |  |  | `"5"` |
| `cardnumber` | string |  |  | `"cn001"` |
| `id_verification_checked` | boolean |  |  | `true` |
| `id_verification_type` | boolean(string) |  |  | `"licence"` |
| `accessdenied` | boolean |  |  | `false` |
| `id_verification_reference` | string |  |  | `"Reference verification"` |
| `visiting_staff_name` | string |  |  | `"Staff Member"` |
| `location_name` | string |  |  | `"Head Office"` |
| `lacp_in_name` | string |  |  | `"Reception"` |
| `lacp_out_name` | string |  |  | `"Reception"` |
| `signed_in_by_name` | string |  |  | `"Person in reception"` |
| `signed_out_by_name` | string |  |  | `"Person in reception"` |
| `onsite` | string |  |  | `"13 minutes 32 seconds"` |
| `type` | string |  |  | `"visit"` |
| `lacp_in_id` | integer(int32) |  |  | `239` |
| `lacp_out_id` | integer(int32) |  |  | `239` |
---

## VisitorGroupRequest

| Field | Type | Required | Description | Example |
|-------|------|----------|-------------|---------|
| `name` | integer(int32) | Yes |  | `"Updated visitor group name"` |
| `description` | string |  |  | `"Group description"` |
| `location_id` | integer(int32) | Yes |  | `301` |
| `staff_id` | integer(int32) | Yes |  | `556` |
---

## VisitorGroupResponse

| Field | Type | Required | Description | Example |
|-------|------|----------|-------------|---------|
| `id` | integer(int32) |  |  | `197` |
| `created` | string(date-time) |  |  | `"2021-11-29T10:58:12+13:00"` |
| `modified` | string(date-time) |  |  | `"2021-11-29T13:58:12+13:00"` |
| `location_id` | integer(int32) |  |  | `301` |
| `created_staff_id` | integer(int32) |  |  | `823` |
| `staff_id` | integer(int32) |  |  | `823` |
| `name` | string |  |  | `"Visitor Group 1"` |
| `description` | string |  |  | `"Group 1 description"` |
| `members` | integer(int32) |  |  | `2` |
---

## VisitorPersonResponse

| Field | Type | Required | Description | Example |
|-------|------|----------|-------------|---------|
| `id` | integer(int32) |  |  | `293` |
| `created` | string(date-time) |  |  | `"2021-11-29T11:10:44+13:00"` |
| `modified` | string(date-time) |  |  | `"2021-11-30T11:10:44+13:00"` |
| `location_id` | integer(int32) |  |  | `301` |
| `created_staff_id` | integer(int32) |  |  | `823431` |
| `staff_id` | integer(int32) |  |  | `823431` |
| `name` | string |  |  | `"Frequent Visitor name"` |
| `email` | string |  |  | `"frequent.visitor@whosonlocation.com"` |
| `from` | string |  |  | `"Another Organisation"` |
| `phone` | string |  |  | `"+1 206 555 0123"` |
| `mobile` | string |  |  | `"+1 206 555 0123"` |
| `title` | string |  |  | `"Mr"` |
| `assistance` | boolean(bool) |  |  | `false` |
| `type` | string |  |  | `"frequent"` |
| `groups` | array of object |  | Group IDs | `[[197, 198]]` |
---

## VisitorRegisterPersonRequest

| Field | Type | Required | Description | Example |
|-------|------|----------|-------------|---------|
| `name` | string |  |  | `"Robert Jordan"` |
| `from` | string |  |  | `"ACME Corporation"` |
| `email` | string |  |  | `"example@example.org"` |
| `phone` | string |  |  | `"+1 206 555 0123"` |
| `mobile` | string |  |  | `"+1 206 555 0123"` |
| `title` | string |  |  | `"Mr"` |
| `assistance` | boolean |  | Whether or not a contractor requires assistance. | `false` |
| `purpose` | string |  |  | `"visiting"` |
---

## VisitorRegisterPersonResponse

| Field | Type | Required | Description | Example |
|-------|------|----------|-------------|---------|
| `id` | integer(int32) |  |  | `2957` |
| `name` | string |  |  | `"Robert Jordan"` |
| `email` | string |  |  | `"registered.visitor@whosonlocation.com"` |
| `from` | string |  |  | `"ACME Corporation"` |
| `phone` | string |  |  | `"+1 206 555 0123"` |
| `mobile` | string |  |  | `"+1 206 555 0123"` |
| `title` | string |  |  | `"Mr"` |
| `assistance` | object |  |  | `false` |
| `purpose` | string |  |  | `"Visiting"` |
---

## VisitorRegisterRequest

| Field | Type | Required | Description | Example |
|-------|------|----------|-------------|---------|
| `name` | string |  |  | `"Robert Jordan"` |
| `type` | string: 'preregister' |  |  | `"preregister"` |
| `location_id` | string |  |  | `301` |
| `staff_id` | string |  |  | `264` |
| `visiting_staff_id` | string |  |  | `264` |
| `event_start` | string |  |  | `"2021-11-30T15:00:00+13:00"` |
| `event_end` | string |  |  | `"2021-11-30T17:00:00+13:00"` |
| `visitors` | array of VisitorRegisterPersonRequest |  |  | `[{"name": "Robert Jordan", "from": "ACME Corporation", "email": "example@example` |
---

## VisitorRegisterResponse

| Field | Type | Required | Description | Example |
|-------|------|----------|-------------|---------|
| `id` | integer(int32) |  |  | `1915` |
| `created` | string(date-time) |  |  | `"2021-11-29T11:11:25+13:00"` |
| `modified` | string(date-time) |  |  | `"2021-11-29T11:11:56+13:00"` |
| `location_id` | integer(int32) |  |  | `301` |
| `created_staff_id` | integer(int32) |  |  | `823431` |
| `visiting_staff_id` | integer(int32) |  |  | `823431` |
| `event_start` | string(date-time) |  |  | `"2021-11-30T15:00:00+13:00"` |
| `event_end` | string(date-time) |  |  | `"2021-11-30T17:00:00+13:00"` |
| `name` | string |  |  | `"Visitor Registor Name"` |
| `visiting_staff_name` | string |  |  | `"Jane Doe"` |
| `location_name` | string |  |  | `"Head Office"` |
| `visitors` | array of VisitorRegisterPersonResponse |  |  | `[{"id": 2957, "name": "Robert Jordan", "email": "registered.visitor@whosonlocati` |
| `type` | string |  |  | `"preregister"` |
---

## ZoneGroupRequest

| Field | Type | Required | Description | Example |
|-------|------|----------|-------------|---------|
| `name` | string |  |  | `"Main Building"` |
| `location_id` | integer(int32) |  |  | `22` |
---

## ZoneGroupResponse

| Field | Type | Required | Description | Example |
|-------|------|----------|-------------|---------|
| `id` | integer(int32) |  |  | `1206` |
| `created` | string(date-time) |  |  | `"2021-11-26T14:35:28+13:00"` |
| `modifed` | string(date-time) |  |  | `"2021-12-31T14:35:28+13:00"` |
| `org_id` | integer(int32) |  |  | `81` |
| `location_id` | integer(int32) |  |  | `22` |
| `name` | string |  |  | `"Main Building"` |
---

## ZoneRequest

| Field | Type | Required | Description | Example |
|-------|------|----------|-------------|---------|
| `name` | string |  |  | `"Meeting room 1"` |
| `location_id` | integer(int32) |  |  | `15` |
| `reference` | string |  |  | `"Large meeting room - seats 20"` |
| `maximum_occupancy` | integer(int32) |  |  | `50` |
| `spaces_enabled` | boolean(bool) |  |  | `true` |
| `spaces` | integer(int32) |  | This is required if spaces are enabled | `11` |
| `spaces_title` | string |  | This is required if spaces are enabled | `"Spaces Reference"` |
---

## ZoneResponse

| Field | Type | Required | Description | Example |
|-------|------|----------|-------------|---------|
| `id` | integer(int32) |  |  | `1206` |
| `created` | string(date-time) |  |  | `"2021-11-26T14:35:28+13:00"` |
| `modified` | string(date-time) |  |  | `"2021-12-31T14:35:28+13:00"` |
| `location_id` | integer(int32) |  |  | `15` |
| `zone_group_id` | integer(int32) |  |  | `3` |
| `name` | string |  |  | `"Meeting room 1"` |
| `reference` | string |  |  | `"Large meeting room - seats 20"` |
| `status` | string: 'Active', 'Inactive' |  |  | `"Active"` |
| `maximum_occupancy` | integer(int32) |  |  | `50` |
| `spaces_enabled` | boolean(bool) |  |  | `true` |
| `spaces` | integer(int32) |  |  | `11` |
| `spaces_title` | string |  |  | `"Spaces Reference"` |
| `zone_group_name` | string |  |  | `"Floor 1"` |
| `zone_fullname` | string |  |  | `"Floor 1 - Meeting room 1"` |
---

