# Gallagher Command Centre Cardholder REST API Skill

> Generated from `ref/Command Centre REST API_ Cardholders _ API Reference.html`.
> Use this reference when implementing a bridge/sync between MRI OnLocation and Gallagher.

---

# Bridge Quick Reference

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

---

# Introduction

This document describes how you can use the Command Centre REST API to view and manage cardholders and their cards, mobile credentials, access groups, personal data, roles, competencies, personal data, and lockers.

It has companion documents describing the PIV , Alarms and Events , and Access items APIs.

It assumes some familiarity with HTTP and REST interfaces. Before you start developing against this interface, you should also read the Authentication section of the accompanying Alarms and Events API documentation to learn how to format your queries, and Command Centre's Configuration Client online help to learn how to ready the server to receive them (search for 'REST API').

Each section contains a list of use cases intended as quick solutions for simple tasks or a how-to to get you started toward your particular goal. However they also serve as a good introduction to the API if you step through them using a web browser, starting at /api on your Command Centre server. To do that, you will need a browser plugin that lets you set an Authorization HTTP header, and a JSON formatter to prettify the results documents. Search the Configuration client's online help for 'test REST API' for guidance. You will also need to prepare Command Centre, as above.

### Licensing

All of the API calls described here are available with the RESTCardholders licence, with the following exceptions and notes:

- Visitor management calls need both RESTCardholders and VisitorManagement.
- Managing cardholders' connections to competency items is enabled by the RESTCardholders licence but creating, modifying, and deleting competency items themselves requires the RESTConfiguration licence.
- Managing access group memberships is enabled by the RESTCardholders licence but managing access group items, card types, personal data definitions, and roles will require the RESTConfiguration licence. These features are coming soon.
- Lockers and locker banks are also available with the RESTStatus licence, and a subset of their fields (enough to allow overriding them) are available with the RESTOverrides licence.

The server will return a 403 if you attempt an operation for which the server is not licensed.

### Versions

The body of this document clearly indicates when recent features arrived in the API so that readers with older versions of Command Centre know not to expect them.

Changes after 9.30 are listed in the parent page .

#### Cardholder API changes on the roadmap

- You will be able to create, delete, and change the the basic configuration of roles, personal data fields (PDFs), and card types.
- POTENTIALLY BREAKING CHANGE: the API route that returns the value of a date PDF presents it in a format specific to the server's locale. 21/11/2025 12:00:00 am , for example. A future version will change to an internet standard.
- POTENTIALLY BREAKING CHANGE: searching for cardholders using pdf_xxx="" or pdf_xxx= currently returns no cardholders. That is neither useful nor desired. Instead, a future version of Command Centre will return cardholders with a blank value for that PDF.
- POTENTIALLY BREAKING CHANGE: the API route that lets you modify a cardholder by PATCH also works if you send it a POST. It should not, because POSTs create things, not modify them. The fix will break clients that rely on the cardholder POST acting like the PATCH. No clients should be doing that since the POST was not documented.
- POTENTIALLY BREAKING CHANGE: in future, PATCH methods will return "200 Success" instead of "204 No Content" and may contain the object you modified plus a message from the API giving you feedback on your request. Please be aware that all 200-level response codes mean success, not just the ones old versions have been sending you.
- A cardholder will contain its visits and escort.
- Personal data fields (PDFs) will include "revealable" access levels.
- Current versions of the API only show a PDF on a cardholder if he or she has a value for that PDF. That makes it difficult to find out which PDFs a cardholder could have, so a future version will allow you to ask for all of them with fields=allPdfs .
- You will be able to request the competencies that your operator has the permission to assign. That is determined by privileges, a competency setting, and an operator group setting. Current versions let you request the competencies that your operator has the permission to view, which is a larger set determined by privileges alone, and not suitable for interactive REST clients.

#### Cardholder API changes in 9.30

- POTENTIALLY BREAKING CHANGE: for efficiency, 9.30 orders search results differently from 9.20 when your query does not include a sort parameter. If you care about the order of your items, tell the server how you want them sorted.
- POTENTIALLY BREAKING CHANGE: GovPass cards may have a new credentialClass and GovPass card states will have new names, depending on the card type and the site's licences.
- You can create, modify, and delete access groups .

#### Cardholder API changes in 9.20

- You can filter by cardholders' direct divisions. The current division search is recursive: it returns all cardholders that have any of the filter's divisions anywhere in their ancestry. The new division search is shallow: it only returns cardholders that are direct members of the filter's divisions.

#### Cardholder API changes in 9.10

- You can create, modify, and delete competencies.
- You can request a locker's status and send an override to quarantine it. The use cases section shows how.

#### Cardholder API changes in 9.00

- You can search for cardholders by the access zone they are in.
- GovPass cards have two new fields, visitorContractor and ownedBySite .

#### Cardholder API changes in 8.90

- Write-only access to card PINs.
- You can set a reason when re-issuing or removing a card which will appear on the resulting event. The default is the card's previous state.
- You can now set a visitor's state (signing in, etc.). Visits will return that, if you ask.
- Bug fix: removing an operator group from a cardholder by PATCH was not possible in the early versions of 8.70 and 8.80. It was fixed in 8.70.2113, 8.80.1116, and all versions of 8.90.

#### Cardholder API changes in 8.80

- BREAKING CHANGE: Date PDFs no longer come out of the API with a time component or time zone designator, because date PDFs don't hold a time or time zone. Previously they came out as midnight UTC, which implied more accuracy than date PDFs hold.
- Redactions .
- An optional PDF field operatorAccess contains the level of access your operator has to cardholders' values for the PDF.
- Card type regex and regexDescription fields.
- Email and mobile PDFs return their 'Default Notifications' flag on request. This actually happened in 8.50 but did not make it into the "changes" list.

#### Cardholder API changes in 8.70

- BREAKING CHANGE: Missing image PDFs will no longer have "not captured" as their value. Instead, they will have no value at all.
- A cardholder's operator group memberships (added in 8.50) now contain an href that you can use in an HTTP DELETE as another way of removing a cardholder from an operator group.
- The server returns 1000 items by default instead of 100.
- Image PDFs tell you whether they are the cardholder's profile picture.
- Image PDFs have a new optional field contentType that returns their MIME type. This deprecates the old imageFormat field, which is non-standard.
- Asking for a particular PDF on a cardholder (by putting pdf_XXX into the fields query parameter) will cause it to appear even if the cardholder has no value for that PDF. Previous versions only showed if it had a non-blank value.
- Attempting to give a cardholder a competency they already have will fail instead of allowing it and raising an alarm. Note that a cardholder having two links to the same competency leads to undefined behaviour.

#### Cardholder API changes in 8.60

- You can search for cardholders by division or description.
- 8.60 rejects unsupported HTTP verbs instead of treating them like a GET. For example, if you send a POST to /api/competencies 8.50 will return a list of competencies but 8.60 will return a 400-level error.
- In build 8.60.1684 or later, attempting to give a cardholder a competency they already have will fail instead of allowing it and raising an alarm. Note that a cardholder having two links to the same competency leads to undefined behaviour.

#### API changes in 8.50

- The server property that turns off client certificate checking changed.
- You can manage operators by setting their operator group memberships and logon credentials.
- You can view operator groups .
- Lockers and locker banks report the hardware controller where they reside.
- Cardholders report their 'disable cipher pad' setting (vision impairment).
- Card types will show their division on request.
- Cardholders can have default floors and passenger type flags per elevator group. Thyssen-Krupp systems can prepare an elevator car when Command Centre grants a cardholder access through lobby turnstiles.
- You can list receptions and see enough of a division's visitor management configuration to create and manage visits.
- Image Personal Data Field definitions will show their width, height, and format (JPG, PNG, or BMP) on request.
- Email and mobile PDFs return their 'Default Notifications' flag on request.

#### API changes in 8.40

- Access groups have 22 new fields showing the privileges and access they grant their members. These new fields are in the default set for the detail view .
- Access groups now return the alarm zones over which they have privilege.
- Items on remote servers now show their origin server in a new serverDisplayName field.
- Cards contain the date and time of their last print or encode, and their issue level at the time.

#### API changes in 8.30

- Cardholder change tracking lets you synchronise users into an external system by informing it of changes as they occur.
- Cardholder group membership hrefs now contain a long string where they used to contain a small integer. Integrations that cache membership hrefs a) should not and b) will need to refresh.
- A cardholder's card object now contains a Boolean field called trace indicating whether its trace flag is set. A card with tracing on generates an event every time it is used.

#### API changes in 8.20

- A new field update_location on a cardholder gives a link to a POST that changes his or her current location (access zone).
- Lockers and locker banks are now visible with the RESTStatus licence, including whether a locker is available or allocated. You still need RESTCardholders to see which cardholder/s a locker is assigned to.
- Lockers moved from the locker banks controller into their own. That changed their hrefs.
- Bugfix: you can now change a cardholder's division.
- A cardholder's relationships block now contains the role holder's two name fields. The name field, present since 7.90, is simply these two fields joined with a space, and is therefore ambiguous when a cardholder only has one name.

#### API changes in 8.10.1112

- You can send an override to a locker to open it.
- You can use the fields query parameter to select the fields you receive from locker bank summary and details GETs. The use cases section shows how.

#### API changes in 8.10

All changes contain the text "8.10".

- A card types API that returns the card types your operator is able to use when assigning cards to cardholders.
- A PDF API that returns the personal data field definitions your operator is able to see.
- You can change the notifications flag on a cardholder's PDF.
- Certificates and biometric data are available on PIV cards.

### Efficiency tips

- When searching for a cardholder (or any other object) by name or personal data, if you have the exact string, put it in quotes "..." . It is still a case-insensitive search but it attempts to match the entire string. Without the quotes Command Centre will perform a substring search, which is slower and may return more results than you need. In one test, a substring search of half a million cardholders took more than ten seconds, but with quotes it took a tenth of a second. That is a 100x speedup. If the server believes you have omitted the quotes in error, it will include a polite reminder in its results.
- When downloading a significant number of items, sort by ID.
- Set your page size as large as your client can handle. The default was 100 before 8.70; using the top parameter to take it to 1000 or more gives much better throughput.

To give you an idea of the effect of setting sort and top , here are some timings for a download of 200,000 cardholders from CC 8.40:

SortingPaginationTimeDefault (by name)Default (100)28 minutes`sort=id`Default (100)5 minutesDefault (by name)`top=1000`3.5 minutes`sort=id``top=1000`1 minute

- In version 8.00 or later, use the fields parameter to add fields to the results of a search, saving you GETting the details page. For example, to see the names, personal data fields, and access groups of all your cardholders: GET /api/cardholders?sort=id&top=10000 &fields=firstname,lastname,personalDataFields,accessGroups It adds to the payload size, though, so only do this if you are certain that the search will not return too many cardholders you are not interested in.
- If you are writing an integration that synchronises users from a source of truth to Command Centre, see the 'Synchronising a user directory' use case in the Cardholders section.
- If you must get an object's details page, and you are on v8.00 or later, using the fields parameter will still save work. Be explicit with what you want: do not use fields=defaults unless you need every field that 'defaults' gives you and you accept that we may add fields to the defaults in future.
- When creating cardholders, assign their cards, groups, and other attributes in the POST. This is significantly quicker than sending a handful of PATCHes chasing after your POST, and if you are interrupted, you will not end up with a partially-configured cardholder.
- When modifying a cardholder, do all your operations in one PATCH. This is much quicker than doing it in several smaller PATCHes.

### Dates and times

When you send dates and times to Command Centre, you must use an ISO-8601 calendar date, time, and time zone designator, with hyphens separating the date fields and colons separating the time fields. This looks like YYYY-MM-DDTHH:MM:SSZ , where T is an actual T, and Z is a timezone designator such as 'Z' for UTC or '-0800' for Pacific Standard Time. Specify up to seven decimals on the seconds if you wish, but be aware that some Command Centre fields will not use them. You must supply the year and month but omit the other fields as you like. Command Centre will assume sensible defaults, such as the top of the minute when you omit seconds, midnight when you omit the time, or first of the month when you omit the day.

Do not omit the timezone designator. If you do, Command Centre will make an assumption.

### Write-locked cardholders

If an operator has unsaved changes on a cardholder object when your integration attempts to update it, your PATCH will fail with a 409 error. The message will tell you the operator and workstation that is holding the lock; we suggest your application logs that and tries its update again later.

##### Request Content-Types: application/json

##### Response Content-Types: application/json

##### Schemes: https

##### Version: 1.0.0

# Authentication

### API key

Clients authenticate by including a pre-shared API key in the Authorization header of each request. Command Centre generates an API key when you create an endpoint for your clients to connect to. Search the Configuration Client online help for 'REST API' for how do do that.

In current versions of Command Centre, the API key will be in the format XXXX-XXXX-XXXX-XXXX-XXXX-XXXX-XXXX-XXXX with each X an uppercase hexadecimal digit. Future versions of Command Centre may use other formats of API key, so treat it as an opaque string. Tempting as it may be, do not interpret is as a GUID.

There are two ways you can pass the API key to the server: following an authorisation method of GGL-API-KEY , or (in 9.0 or later) in the style of HTTP Basic authentication: prefixed with a colon, Base64-encoded, and following an authorisation method of Basic .

Examples:

Authorization: GGL-API-KEY C774-B01F-D695-AA4B-215F-A158-AC22-ADEB

Authorization: Basic OkM3NzQtQjAxRi1ENjk1LUFBNEItMjE1Ri1BMTU4LUFDMjItQURFQg==

Early versions of Command Centre allowed you to omit the 'GGL-API-KEY' from the first form. Future versions of Command Centre and all versions of the API gateway will reject you with a 401 if you do that. The API gateway is especially fussy about case and punctuation.

#### HTTP Basic authentication

The two example headers above are equivalent: prepending a colon to the API key beginning with C774 then encoding it into Base64 produces the 56 characters beginning with OkM3 .

According to the HTTP Basic specification, the colon is a separator between a username and a password. In our case, the API key is the password and the username is unnecessary. If you place a username before the colon, before Base64-encoding it, Command Centre will ignore it and authenticate the connection based on the API key alone.

#### Pinning a client TLS certificate

Depending on Command Centre's site configuration, its REST API may also require a client certificate with each request. In versions up to and including 8.40 this was controlled by a flag labelled 'Do not require pinned client certficates' on the 'Web Services' tab of the server properties. In 8.50 that flag's label changed to 'Enable REST Clients with no client certificate', and its behaviour changed slightly when turned on.

If off, for all versions of CC, an incoming request's certificate has to match the thumbprint on its API key's REST Client item, and REST Client items with a blank thumbprint field do not work at all. This is how it ships, and it is the recommended way of running a production server.

If 'Do not require pinned client certificates' was turned on in versions up to and including 8.40, the server did not check any client certificates. If that flag is turned on in 8.50, now relabelled 'Enable REST Clients with no client certificate', it still does not check the client certificate if the thumbprint field of the matching REST Client item is blank, but if the Client item has a thumbprint, the server will reject connections with the wrong certificate.

See the Configuration Client help for instructions on where to enter REST Client thumbprints.

#### Source IP filtering

Also note that if IP filtering is enabled on the REST Client item in Command Centre, the API will only accept connections from the IP address ranges configurated into that client item.

#### Symptoms of authentication failures

If a connection attempt fails, the server will return a 401 and raise an event containing its reason for refusing the request. If that happens too often, the server will stop reporting each offence and will instead create a summary alarm at a much lower rate. The details of the alarm tell you how many failed attempts there have been since the start of the flood. The server will stay in this mode of reduced reporting until several minutes pass without a failed connection attempt.

The current failure limit is ten errors inside one minute. After that you will receive one "a large volume of requests has been denied" alarm every minute while the failures continue until five minutes passes without a failure.

The most common queries we receive from our integrators relate to their certificate handling. If your client's HTTP client library complains about certificates the first thing to check is Command Centre's alarm list. If there are 'invalid client certificate' alarms there, your client is not sending the certificate CC is expecting. If there are no alarms then the client is most likely rejecting the server's certificate.

- **type**: apiKey

- **name**: Authorization

- **in**: header

- **x-external**: eventsApi.yaml#/securityDefinitions/API key

# Documentation suite

Microsoft Edge users please note: early versions did not render the table of contents correctly in the orange column on the left, making navigation difficult. Chrome, Firefox, and recent versions of Edge work well.

The API's reference documentation divides into:

[Alarms, events, bulk status monitoring, and generic items](https://gallaghersecurity.github.io/cc-rest-docs/ref/events.html)

The alarms and events APIs let you download, monitor, and create events, and download, monitor, and manage alarms.

This section also covers API calls that support divisions and items as they relate to events, as well as bulk status monitoring. It does not cover the deep configuration of items: see Status and Overrides.

[Cardholders and related items](https://gallaghersecurity.github.io/cc-rest-docs/ref/cardholders.html) The cardholder parts of the API let you manage your users, their personal data and credentials (cards), and their links to associated items such as access groups, roles, operator groups, competencies, and lockers. [Status, overrides, and configuration of non-cardholder items](https://gallaghersecurity.github.io/cc-rest-docs/ref/rest.html)

These functions let you monitor and override the types of Command Centre items that have their own status and typically appear on site plans, including access zones, alarm zones, doors, fence zones, inputs, outputs, macros, elevator groups, interlock groups, and schedules.

Despite its name this section does not cover mass-monitoring item status .

[PIV cards](https://gallaghersecurity.github.io/cc-rest-docs/ref/piv.html) This supplement describes how to work with PIV and PIV-I cards. It is separate from the main cardholder documentation in the interest of brevity.

π

# Forward compatibility (HATEOAS)

This is a self-referencing REST API that follows the principles of HATEOAS. Other than the initial GET to /api when it first connects, your source code should not contain any URLs, as they are subject to change. You should append the query parameters this document describes for operations such as filtering and searching, but everying in the path should come from the results of /api or pages linked from it.

/api only shows licensed API calls.

Be prepared to append query parameters to URLs that already have their own: do not assume that you can simply add a question mark and your parameters.

# Operator privileges

First, some background on operators, operator groups, and divisions.

To determine your REST client's privileges, the server starts by searching the list of REST Client items in its configuration for the one with the API key in the Authorization header of your request. Assuming it finds one, it takes the cardholder from that REST Client item.

To be any use at all that cardholder must be a member of at least one operator group. Being in an operator group makes a cardholder an operator .

After the server has your operator, it needs to check whether that operator has access to the items or events in the request. To explain that, it is necessary to cover divisions.

Aside from some special items such as day categories, every item in Command Centre is in a division . Divisions are hierarchical, with all divisions (but the root) being a child of another. Multi-server installations have one division tree per server.

Every event and alarm has a division. It is usually the division of the source item. Card events or 'forced door' alarms, for example, typically use a door as a source item; when an operator modifies an item, the event recording that change uses that operator's workstation as the source item; the alarm that the server generates when you send a bad API key uses the server item as the source.

An event's division is not always its source's division. 'Card activated' events have the server as a source item, but take on the cardholder's division so that operators who can see the cardholder can also see when the server activates their cards.

To link privileges to items and events, an operator group contains a list of privileges and a list of divisions. Its operators enjoy those privileges on all the items, events, and alarms that are in those divisions.

Having a privilege on a division also grants an operator that privilege on that division's descendants. Therefore an operator with a privilege on the root division has that privilege on all that server's items and events.

A common misconception is that the division an operator is in, or the division an operator group is in, have a bearing on the operator's privileges. They do not. It is all about the divisions in the operator group's 'Operator privileges' list.

### If your privileges do not seem to be working

First, to protect Command Centre from accident and malice, you should strive to grant your REST clients the fewest and lowest-level privileges you can. Do not give them 'advanced user'. Reaching a point where you do not receive the results you want is a good thing: it means you have screwed things down a little too tightly.

If changing privileges in Command Centre does not seem to make any difference , remember to push the 'Refresh operator privileges' button in the properties window of your REST Client item.

Here are some general rules that may help if you are still not receiving the results you expect.

- Receiving a 403 from a GET means the server may not have a license for the retrieval you are attempting or your operator does not have the privilege to view that object. If it is a licensing problem, the body of the response will describe it.
- Receiving a 403 from a PATCH, POST, or DELETE means your operator does not have the necessary privilege to perform that operation. You need one of the privileges beginning with 'Edit', 'Modify', or 'Create'.
- Receiving a 404 when trying to get one item or event means it either does not exist or your operator does not have the privilege to view it.
- Receiving a 200 and an empty result set from a search means you are licensed for that search but your operator could not see any items or events that matched. One possible cause is that your operator is not licensed to see any items or events of that kind. Make sure that one of the operator groups that your operator is in has a 'View' privilege such as 'View events' or 'View cardholders' - whatever is appropriate. If it does, and you have hit the button mentioned above, then check that the divisions on the operator group contain the items you expect. If it is events you are searching for, check the source item on those events either by using a REST operator with 'View events' on the root division or by looking at them in one of the thick clients.

# Text encoding

Command Centre's REST API encodes all payloads using UTF-8, and expects clients to do the same. It does not escape special characters in response bodies except where required to embed them in JSON.

Specifically, it does not sanitise HTML, XML, or SQL. Your clients should expect to receive strings exactly as they were sent, even if they were sent by a hostile client. Write your clients to resist injection attacks.

# Field specifiers

You can specify the fields you want in the results of a query. This lets you:

- save calls by adding more fields to search results, which are terse by default
- save bandwidth by putting fewer fields on detail pages, which are verbose by default, and
- retrieve more data by adding fields to detail pages that are not normally there, such as an item's status, short name, and notes.

You do this using the query parameter fields to the search, detail, and updates pages. It takes a comma-separated list of field names. They are case-sensitive: copy them carefully from this document. The special field name defaults adds in the fields the server would have sent had you not set the fields parameter at all.

Example GETs:

- /api/cardholders?name=Smith&fields=defaults,cards,competencies will list all cardholders with 'Smith' in the first or last name, showing the fields you normally get on a search page plus their cards and competencies.
- /api/access_groups/123?fields=children,cardholders will show just the child groups of access group 123 and a link to retrieve its cardholders - nothing else.
- /api/outputs?fields=name,shortname,id will show the name, short name, and ID of your outputs.
- /api/access_zones?fields=name,doors will show the name and attached doors of your access zones.
- /api/alarms?fields=time,message,details will return the time, message text, and details text of your alarms. Not only does that save a lot of data by removing default fields, but it adds the details field which does not normally come out of an alarm search.

Each controller has different optional fields - see their sections under Operations.

# Cardholders

A cardholder is a user account.

These methods give you read and write access to the cardholder data in the Command Centre database. You can download cardholders, create them, search them by name or Personal Data Field (PDF) value, and work on them as you would in the interactive clients.

The first use case below introduces the main entry point. It is a paginated search interface that gives you basic data for any number of cardholders. You won't receive many fields by default, but you can ask for more using the fields query parameter.

A field called 'href' serves both as a unique identifier for the cardholder and the location of what we call his or her details page. You can submit an HTTP GET to that href to retrieve the entire cardholder record, or an HTTP PATCH to update it, or an HTTP DELETE to remove it. Be careful: there is no coming back from deleting a cardholder.

### Use cases

#### Searching for cardholders by name

- GET /api .
- Follow the link at features.cardholders.cardholders.href ↪ , appending a search term to narrow the results and the 'fields' parameter to add the fields you need.
- Process the results, following the next link until there isn't one.

#### Downloading all cardholders

- GET /api
- Follow the link at features.cardholders.cardholders.href ↪ . This is effectively a cardholder search with no filters. You should add sort and top query parameters to sort by ID and increase the number of cardholders per page: see the efficiency tips!
- Process the bundle of cardholders in the result.
- Follow the link at next.href if there is one, and repeat.

#### Synchronising a user directory

This is a common use case, described in the section on Cardholder changes . It shows how to:

- Get a bookmark at the head of the queue of cardholder changes.
- Do a one-off sync of all cardholders.
- Loop to stay up to date, starting with the bookmark.

That is how Gallagher's cardholder-synchronising integrations work.

#### Searching for cardholders by Personal Data Field value

Say you want to find a cardholder with a particular employee ID, and Command Centre is holding that in a personal data field called "employee_ID".

First, reconsider, and look at the 'Synchronising a user directory' use case above. If you are going to be doing this for many cardholders, GETting cardholders in bulk and filtering out the interesting ones client-side is more efficient than a large number of unique PDF searches. You don't have to get all cardholders; you could get all cardholders in one division, or all cardholders with a non-blank value for your PDF (shown below), or a combination. It will be quicker than a sequence of searches that return one cardholder each.

Before you can start a cardholder search, you must find the PDF's identifier.

- GET /api .
- v8.10 or later: follow the link at features.personalDataFields.personalDataFields.href ↪ , appending the query name="employee_id" . This will return every PDF in the system with that name. There will be more than one, in rare cases.
- Earlier versions: follow the link at features.items.items.href ↪ , appending the query name="employee_id" (after a ? or & , of course). This will return every item in the system with that name. Pick the one with a type name of 'Personal Data Field'. Or you could append type=33 to the item search: you will only be shown PDFs.
- Note the ID of the item. It will be a short alphanumeric.

Case does not matter when searching by name but remember the quotes: Command Centre will perform a substring match if you omit them, which is vastly slower and (for the example above) return you the PDFs 'previous_employee_ID' and 'employee_ID_allocated_flag', if you have PDFs with those names.

Now you can use that PDF ID in a cardholder search.

- Recall the JSON object you received from GET /api .
- Follow the link at features.cardholders.cardholders.href ↪ , adding a query separator and pdf_<your_pdf_id>=<your_pdf_value> , without the angle brackets.

The results will only include cardholders who have <your_pdf_value> as a substring of the Personal Data Field with ID <your_pdf_id> . Again, the search is case-insensitive and you should bookend <your_pdf_value> with quotes if you want to anchor it at each end. In our example we would GET /api/cardholders?pdf_345="EID8888" .

Use a percent sign % for <your_pdf_value> if you want to see all cardholders who have a non-blank value for that PDF.

The server will reply with a default set of fields for your cardholder. If they are not what you'd like, you can use the fields query parameter to change them.

See the cardholder GET for more.

#### Creating a cardholder

- GET /api .
- Use the link at features.accessGroups.accessGroups.href to find the hrefs of access groups you wish to add your new cardholder to.
- Do the same for the competencies, relationships ( roles ), lockers, PDF definitions, and cards/credentials ( cardTypes ) that your new cardholder needs, using other URLs in the 'features' section of /api . In v8.10, the card types that your operator has the privilege to assign are at a new URL given in the field features.cardTypes.assign.href ↪ .
- Compose a JSON body using this example and this detail , then POST it to the href at features.cardholders.cardholders.href ↪ on the /api page.

#### Modifying a cardholder

- Find your cardholder using one of the processes above.
- If you need the current values before you update them, use the fields parameter to add fields (such as accessGroups, cards, or competencies) to the search results.
- PATCH the cardholder's href with a document describing the additions, deletions, and modifications you wish to make to the cardholder and his or her associations.

In its simplest form, your document could be a collection of key/value pairs much the same as you receive from a GET to the same href. More complex forms allow adding, modifying, and removing cards, competencies, group membership, PDFs, and relationships. Three examples follow.

#### Removing a cardholder from groups

- Find your cardholder using one of the processes above.
- Follow the link to their details page or add fields=accessGroups to the search. That will show all their access group memberships.
- PATCH the cardholder with the hrefs of the memberships you want to delete in an array called remove inside a block called accessGroups .

The access groups section describes what to do when you want to start with a group and remove several cardholders from it.

#### Removing all group memberships, competencies, relationships, cards, or lockers

This shows how you could remove a cardholder from all his or her access groups, but you could just as simply apply the process to other fields that come back as arrays, such as competencies, relationships, operator groups, cards, and lockers.

- GET your cardholder's details page, or search with accessGroups in the field list. Either way, you will receive all their access group memberships.
- Take the array at accessGroups , rename it to remove , and put it in object called accessGroups at the root level of a JSON object .
- PATCH the cardholder with that JSON object. That will remove the cardholder from all the groups that came back in the GET.

#### Assigning a new card to a cardholder

- GET /api .
- Follow the link at features.cardTypes.cardTypes.href ↪ to find the href for the new card's type.
- If you want to create a card in a state other than the default, get the state from the same page.
- Find the href for the cardholder using one of the search methods above.
- Optional: follow the link at edit.href on the cardholder page. If there is a link at update.href , your REST client has permission to modify the cardholder.
- PATCH the cardholder's href according to the cardholder patch schema .

#### Deleting a cardholder's PDF value

- Find the URL of your cardholder using one of the processes above.
- PATCH your cardholder with this in the body: { "@name_of_PDF": null }

#### Deleting a cardholder

- Find your cardholder using one of the processes above.
- Send an HTTP DELETE to the cardholder's href.

#### Updating a cardholder's location

- Find the href of your target access zone using the link at features.cardholders.updateLocationAccessZones ↪ in the results of GET /api .
- Find your cardholder using one of the processes above. If you are using search , add updateLocation to the fields parameter to save you having to GET their details page later.
- Send an HTTP POST to that cardholder's updateLocation.href link.

#### Finding which access zones and doors a cardholder can access

- GET your cardholder's details page, or search cardholders with accessGroups in the field list. Either way, you will receive all their access group memberships.
- Iterate through all the access groups looking into their access blocks for access zones and schedules. Recurse up the access group hierarchy by following each group's parent.href .

Now you have the cardholder's access zones. If you want doors:

- For each access zone, GET its href and look in its doors block.

#### Finding recently-created cardholders

Informing you when Command Centre creates cardholders is a function of the Cardholder changes call, described there.

However for that to work you need to make API calls before and after the cardholders are created. If they already exist, a workaround is to get a page of cardholders sorted by database ID descending using sort=-id&top=10 . Change the ten to the number of cardholders you'd like, obviously. Because Command Centre allocates database IDs to new items in increasing order, the first page you get will be the most-recently created cardholders. This is not suitable for use in a production system! Use the cardholder changes call instead.

### Field names in query parameters

The cardholder search and details operations' fields parameter and the change tracking operation's fields and filter parameters take a list of field names. Other sections of this document describe how those parameters affect the API calls, but their format is the same so this section will cover it once.

You can form the name of a field by joining the components of its JSON path with dots. Fields at the root level of the cardholder object, such as firstName and @emailAddress (a PDF), have just one component. Fields that are one level down, such as accessGroups.status , have two.

Treat the string matches as case sensitive: use lastName rather than lastname .

The string must not contain any spaces. Just alphanumerics, underscores, commas, and dots.

To serve as examples, this is the list you can choose from in 8.30:

href , id , firstName , lastName , shortName , description , authorised , lastSuccessfulAccessTime , lastSuccessfulAccessZone , division , personalDataFields , cards , accessGroups , competencies , notes , notifications , relationships , lockers , cards.href , cards.number , cards.from , cards.until , cards.cardSerialNumber , cards.type , cards.status , cards.invitation , cards.issueLevel , cards.pivData , cards.pivData.chuid , cards.pivData.pivStatus , cards.pivData.lastCheckTime , cards.pivData.chuid.hash , cards.pivData.chuid.fascn , cards.pivData.chuid.duns , cards.pivData.chuid.orgIdentifier , cards.invitation.href , cards.invitation.singleFactorOnly , cards.invitation.email , cards.invitation.mobile , notifications.enabled , notifications.from , notifications.until , accessGroups.href , accessGroups.accessGroup , accessGroups.from , accessGroups.until , accessGroups.status , relationships.href , relationships.cardholder , relationships.role , lockers.href , lockers.locker , lockers.from , lockers.until

Later versions of Command Centre added many more. See the details operation for the complete list of cardholder fields.

A special value default applies a default set of fields that varies with the call you're making.

personalDataFields is also special. It will give you the personalDataDefinitions block plus all the PDF values at the root level with their names preceded by '@'-signs.

---

# Operations (63)

## GET /api/cardholders — Search cardholders

This call returns cardholders matching your search criteria.

The result will contain no more than 100 or 1000 cardholders depending on your version; you should follow the next link, if it is present, to collect the next batch.

When you have loaded all the cardholders there will be no next link.

If your result set is empty it means your operator does not have the privilege to view any cardholders. Perhaps there are none in the divisions in which your operator has privileges, or your operator has no privileges at all.

Adding or modifying cardholders between calls to this API will not affect the pagination of its results if you sort by ID.

Do not code this URL into your application. Take it from the 'href' field in the features.cardholders.cardholders section of /api .

### Parameters

- **sort** (in query · string id , name , -id , -name): Changes the sort field between database ID and name. If you prefix id or name with a minus sign (ASCII 45), the sort order is reversed. There are two very strong reasons to sort by ID: Sorting by name carries a risk of missing or duplicating objects if your result set spans multiple pages and another operator is editing the database while your REST client is enumerating them. This is known as "page drift." Sorting by ID does not carry that risk. Following a next link is dramatically quicker when sorting by ID. We strongly recommend sorting by ID. In case you were still in doubt, we will do that by default in a future version of Command Centre. The server silently ignores anything except the options listed here.
- **top** (in query · integer x ≥ 1): Sets maximum number of cardholders to return per page. Older versions of Command Centre returned 100. That is acceptable for a GUI application that will only display the first page of cardholders, but for integrations that intend to proceed through the entire database it causes a lot of chatter. Version 8.70 will return 1000 items per request by default. 1000 is about where a graph of performance versus page size begins to level out. You may see some improvement by taking it even higher.
- **name** (in query · string): Limits the results to cardholders with a name that matches this string. By default, it is a substring search against the first name or the last name or the concatenation 'lastName, firstName'; surround the parameter with double quotes "..." for an exact search. Without quotes, a percent sign % inside your search string will anchor the search at both ends (so it will no longer be a substring search) and the % will match any substring. For example, boothroyd,% will only match cardholders whose last name is Boothroyd. The search is always case-insensitive. Results are undefined if you do a substring search for the empty string ( name= ). You will receive no cardholders if you search for those with no name ( name="" ), as all items must have a name. Because a plus sign + represents a space in a query string, replace each plus sign in your search string with %2d . The search parameters form a logical conjunction. They are ANDed together. Therefore if you search for name=Mary&pdf_1315=nanny you will only get back cardholders with 'Mary' in their name and 'nanny' in the PDF with ID 1315.
- **pdf_{id}** (in query · string): Limits the results to cardholders with a value for the Personal Data Field with this ID that matches the parameter. It is a substring match by default; surround it with double quotes "..." for an exact match. Tests showed an exact match to be 100x quicker than a substring search on a large database. Without quotes, _ will match any single character and % will match any substring. Having either in your term will anchor the string at both ends so it will not be a substring search. A lone % will return any cardholder who has this PDF set to a non-null value. Searching for a blank value using pdf_xxx="" or pdf_xxx= currently matches no cardholders, which is not useful. Do not rely on that behaviour since we will change it in a future version of Command Centre to return cardholders with no value for that PDF. Because a plus sign + represents a space in a query string, turn plus signs in your string into %2d . The search is always case-insensitive. Search parameters form a logical conjunction. They are ANDed together. Therefore the search pdf_1315=nanny&pdf_1315=paratrooper will only return cardholders whose PDF 1315 contains the strings 'nanny' and 'paratrooper'.
- **division** (in query · string[]): Limits the returned items to those that are in these divisions. That includes all the items in those divisions' child divisions, because Command Centre treats items as though they are also in their division's parent, and its parent, and so on up to the root division. Separate the IDs of the divisions you are interested with commas. Item IDs are short alphanumeric strings, not URLs. Results are undefined if you provide an ID that is not in the form of a division ID. Search parameters are ANDed together.
- **directDivision** (in query · string[]): Restricts items to those whose division is in this list. Unlike division= , it does not follow ancestry. That means it will limit the search to items that are directly assigned to the divisions you list, whereas division (without the direct ) will also include items that are assigned to their descendants. List the IDs of the divisions you are interested in separated by commas. Item IDs are short alphanumeric strings, not URLs. Because this is the first query parameter we added that contains a capital letter, this will be the first time you have had to think about case sensitivity. directDivision works, directdivision does not. Results are undefined if you provide an ID that is not in the form of a division ID. Search parameters are ANDed together. Added in 9.20.
- **description** (in query · string): Limits the returned items to those with a description that matches this string. By default it is a substring match; surround it with double quotes "..." for an exact match. A _ will match any single character, and a % will match any substring. With or without quotes, having either of these wildcards in the string will anchor it at both ends as though you had surrounded it with " . The search is always case-insensitive. Results are undefined if you search for the empty string ( description= or description="" ). Search parameters are ANDed together.
- **accessZone** (in query · string[]): Limits the results to cardholders who are in one of the access zones with the given IDs. Do not put quotes around the IDs, and separate them with commas. To get everyone who is in any access zone use accessZone=* . It will return all cardholders who have badged at least once and who are not currently 'outside the system'. A cardholder is 'outside' if they badge through a door that has no access zone configured for that direction of travel, or if an operator manually moves them outside.
- **fields** (in query · string[] defaults): Specifies the fields you want in the search results. The values you can use here are the same as you can for the details page . Using it you can return everything on the search page that you would find on the details page with the exception of the edit and updates links. Separate values with commas. Use the special value defaults to return the fields you would have received had you not given the parameter at all. Obviously only do that if you have more to add. Use the special value personalDataFields to return the 'personalDataDefinitions' block as well as the PDF values at the root level of the cardholder object. Use the special value pdf_XXX (where XXX is the ID of a PDF definition) to include just that PDF. If you do not have the PDF's ID, and don't mind the performance hit of retrieving all the PDFs, using personalDataDefinitions may be simpler. Treat the string matches as case sensitive: use 'lastName' rather than 'lastname'. In v8.00 you will receive the href and internal ID even if you do not ask for them. In 8.10 you will not. If you are going to send the fields parameter and need the href or ID, include them.
- **pos** (in query · integer): Reserved for internal use. Do not add it to your queries.
- **skip** (in query · integer): Reserved for internal use. Do not add it to your queries.

### Responses

- **200 OK**: Success. See the note in the description about privileges if your result set is empty.

- **400 Bad Request**: The server could not make sense of your search terms.

- **403 Forbidden**: The site does not have the RESTCardholders licence.

---

## POST /api/cardholders — Create a cardholder

This creates a new cardholder, including his or her cards, group memberships, personal data, competencies, and roles.

Do not code this URL into your application. Take it from the 'href' field in the features.cardholders.cardholders section of /api .

The POST expects a document in the same format as the the cardholder detail . Many fields are optional, of course, and some (like the last successful access time) do not make sense when creating a cardholder. See the cardholder POST example for details.

You will achieve better performance if you combine all you want to achieve into one POST, rather than creating the cardholder bare with a POST then adding cards, groups, PDFs, etc., with PATCHes later.

When successful it returns a location header containing the address of the new cardholder.

Note that you can only create one cardholder per POST.

### Request body

[Cardholder POST example](https://gallaghersecurity.github.io/cc-rest-docs/ref/cardholders.html#/definitions/Cardholder%20POST%20example)

### Responses

- **201 Created**: Success.

- **400 Bad Request**: The body of the POST did not describe a valid cardholder. If you see 'Data has not been entered for this Personal Data Field' in the response body, you have attempted to create a cardholder in an access group that has a required Personal Data Field, but not supplied a value for that PDF. If you see 'Invalid cardholder', the server could not parse the JSON in the body of your POST. Remember to quote all strings, especially those than contain @ symbols.

- **403 Forbidden**: The operator does not have a privilege that allows creating cardholders, or you attempted to set a field for which the operator has no privilege (probably 'notes'), or the server has reached its licensed limit of cardholders.

### Examples

##### Request Example

```json
{
  "firstName": "Algernon",
  "lastName": "Boothroyd",
  "shortName": "Q",
  "description": "Quartermaster",
  "authorised": true,
  "division": {
    "href": "https://localhost:8904/api/divisions/5387"
  },
  "@email": "user@sample.com",
  "@headshot": "/9j/4A...==",
  "personalDataDefinitions": [
    {
      "@email": {
        "notifications": true
      }
    }
  ],
  "cards": [
    {
      "type": {
        "href": "https://localhost:8904/api/card_types/600"
      },
      "pin": "153624"
    },
    {
      "type": {
        "href": "https://localhost:8904/api/card_types/654"
      },
      "number": "Nick's mobile",
      "invitation": {
        "email": "nick@example.com",
        "mobile": "02123456789"
      }
    }
  ],
  "accessGroups": [
    {
      "accessGroup": {
        "href": "https://localhost:8904/api/access_groups/352"
      },
      "from": "2019-01-01"
    }
  ],
  "operatorGroups": [
    {
      "operatorgroup": {
        "href": "https://localhost:8904/api/operator_groups/523"
      }
    }
  ],
  "competencies": [
    {
      "competency": {
        "href": "https://localhost:8904/api/competencies/2354"
      },
      "enabled": false,
      "enablement": "2019-01-01"
    }
  ],
  "notes": "",
  "notifications": {
    "enabled": true,
    "from": "2017-10-10T14:59:00Z",
    "until": "2017-10-17T14:59:00Z"
  },
  "relationships": [
    {
      "role": {
        "href": "https://localhost:8904/api/roles/5396"
      },
      "cardholder": {
        "href": "https://localhost:8904/api/cardholders/5398"
      }
    }
  ],
  "lockers": [
    {
      "locker": {
        "href": "https://localhost:8904/api/lockers/3456"
      }
    },
    {
      "locker": {
        "href": "https://localhost:8904/api/lockers/3457"
      }
    }
  ],
  "elevatorGroups": [
    {
      "elevatorGroup": {
        "href": "https://localhost:8904/api/elevator_groups/635"
      },
      "accessZone": {
        "href": "https://localhost:8904/api/access_zones/637"
      }
    },
    {
      "elevatorGroup": {
        "href": "https://localhost:8904/api/elevator_groups/639"
      },
      "enableCodeBlueFeatures": true
    }
  ]
}
```

---

## GET /api/cardholders/{id} — Get details of a cardholder

This retrieves one cardholder from Command Centre. It does not return all the fields available (there are too many) so, much like the cardholder search, it accepts a fields parameter where you specify the fields you need.

Do not use a cardholder's ID to build the URL yourself: follow the href in the cardholder search to get here.

### Parameters

- **fields** (in query · string[] defaults): Specifies the fields you want in the results. The values you can list are the same as the field names in the detail results . Use it to return fewer fields than normal. Separate values with commas. Treat the string matches as case sensitive: use 'lastName' rather than 'lastname'. Added to the cardholders controller in 8.00. In that version you will receive the href, internal ID, and updates link even if you do not ask for them. In 8.10 you will not. If you are going to send the fields parameter and need those fields, include them.
- **id** (in path · string): The ID of the cardholder.

### Responses

- **200 OK**: Success.

- **404 Not Found**: That is not the URL of a cardholder, or the operator does not have the privilege to view that cardholder.

---

## PATCH /api/cardholders/{id} — Update a cardholder

This is the call you use to update a cardholder, including:

- changing general properties such as names, division, and description,
- changing PDF values, and
- adding or updating cards, access group membership, roles and relationships, lockers, and competencies.

When changing a cardholder's division at the same time as PDFs or competencies, the extra privilege checks required for PDFs and competency changes use the origin division, not the destination division. So if you are moving a cardholder from a division in which your operator has no access to PDFs and competencies into one in which it does, first PATCH the cardholder into the new division then PATCH it with the other changes.

You can find this URL in the href in the cardholder search or any of the other calls that return cardholder hrefs. Do not build it yourself .

Note that the REST API does not implement the full suite of Command Centre privileges. In particular, the following privileges do not have the same effect on an operator's ability to modify a cardholder that they do in the administrative clients:

- Disable Card. This privilege has no effect on the 7.90 REST API. You need Edit Cardholders to disable cards.
- Add or Edit Cardholder Notes. You also need Edit Cardholders to change notes on an existing cardholder via the API. In the administrative clients, you do not.
- Manage Locker Assignments. You also need Edit Cardholders to assign and un-assign lockers on a cardholder via the API. In the administrative clients, you do not.

The 'De-authorise Cardholder' privilege is implemented. It allows an operator to set a cardholder's 'authorised' field to false (denying all their future access requests) without the Edit Cardholders privilege.

In short, if your application intends to do more to cardholders than de-authorise them, you will need an operator with Edit Cardholders.

The PATCH is best illustrated by example.

### Parameters

- **id** (in path · string): The ID of the cardholder.

### Request body

[Cardholder PATCH example](https://gallaghersecurity.github.io/cc-rest-docs/ref/cardholders.html#/definitions/Cardholder%20PATCH%20example)

### Responses

- **200 OK**: Success. Future versions will add feedback from the server about your PATCH.

- **204 No Content**: Success, with no feedback.

- **400 Bad Request**: The parameters are invalid, or other errors prevented the update. If you receive 'No fields have been defined for update', check that your submission body is valid JSON.

- **403 Forbidden**: The site does not have a RESTCardholders licence, or you attempted to set a PDF for which you have no privilege.

- **409 Conflict**: The cardholder is locked for editing by another operator. The body of the response will tell you which operator is holding the lock.

- **4xx**: The operator does not have the privilege to modify that cardholder, or you attempted to set a field for which you have no privilege (such as 'notes' or 'operatorPassword', both of which require special privileges). In versions prior to 8.80 your operator needed 'Edit cardholders' to de-authorise a cardholder. In 8.80 and later, 'De-authorise cardholder' on its own is enough.

### Examples

##### Request Example

```json
{
  "authorised": true,
  "@employeeId": "THX1139",
  "personalDataDefinitions": [
    {
      "@email": {
        "notifications": true
      }
    },
    {
      "@cellphone": {
        "notifications": false
      }
    }
  ],
  "cards": {
    "add": [
      {
        "type": {
          "href": "https://localhost:8904/api/card_types/354"
        },
        "pin": "153624"
      },
      {
        "type": {
          "href": "https://localhost:8904/api/card_types/600"
        },
        "number": "Jock's iPhone 8",
        "status": {
          "value": "Pending sign-off"
        },
        "invitation": {
          "email": "jock@example.com"
        }
      }
    ],
    "update": [
      {
        "href": "https://localhost:8904/api/cardholders/325/cards/97b6a24ard6d4500a9d",
        "issueLevel": 2,
        "until": "",
        "status": {
          "value": "Stolen"
        },
        "pin": "153624"
      }
    ],
    "remove": [
      {
        "href": "https://localhost:8904/api/cardholders/325/cards/77e8affe7c7e4b56",
        "status": {
          "value": "Lost"
        }
      }
    ]
  },
  "accessGroups": {
    "add": [
      {
        "accessGroup": {
          "href": "https://localhost:8904/api/access_groups/352"
        }
      },
      {
        "accessGroup": {
          "href": "https://localhost:8904/api/access_groups/124"
        },
        "until": "2019-12-31"
      }
    ],
    "update": [
      {
        "href": "https://localhost:8904/api/cardholders/325/access_groups/10ad21",
        "until": ""
      }
    ],
    "remove": [
      {
        "href": "https://localhost:8904/api/cardholders/325/access_groups/10ed27"
      }
    ]
  },
  "competencies": {
    "add": [
      {
        "competency": {
          "href": "https://localhost:8904/api/competencies/2354"
        },
        "enabled": false,
        "enablement": "2021-01-01T08:00+13"
      }
    ],
    "update": [
      {
        "href": "https://localhost:8904/api/cardholders/325/competencies/2dc3",
        "enabled": true
      }
    ]
  },
  "relationships": {
    "add": [
      {
        "role": {
          "href": "https://localhost:8904/api/roles/5396"
        },
        "cardholder": {
          "href": "https://localhost:8904/api/cardholders/5398"
        }
      }
    ],
    "update": [
      {
        "href": "https://localhost:8904/api/cardholders/325/roles/1799lah1170",
        "cardholder": {
          "href": "https://localhost:8904/api/cardholders/10135"
        }
      }
    ]
  },
  "lockers": {
    "add": [
      {
        "locker": {
          "href": "https://localhost:8904/api/lockers/1200",
          "from": "2019-01-01"
        }
      }
    ],
    "update": [
      {
        "href": "https://localhost:8904/api/cardholders/325/lockers/wxyz1234",
        "until": "2020-02-29"
      }
    ],
    "remove": [
      {
        "href": "https://localhost:8904/api/cardholders/325/lockers/abcd4321"
      }
    ]
  },
  "operatorGroups": {
    "add": [
      {
        "operatorGroup": {
          "href": "https://localhost:8904/api/operator_groups/532"
        }
      },
      {
        "operatorGroup": {
          "href": "https://localhost:8904/api/operator_groups/535"
        }
      }
    ],
    "remove": [
      {
        "href": "https://localhost:8904/api/cardholders/325/operator_groups/EBDRSD"
      }
    ]
  },
  "elevatorGroups": {
    "add": [
      {
        "elevatorGroup": {
          "href": "https://localhost:8904/api/elevator_groups/635"
        },
        "accessZone": {
          "href": "https://localhost:8904/api/access_zones/637"
        }
      }
    ],
    "update": [
      {
        "href": "https://localhost:8904/api/cardholders/325/elevator_groups/1268613268",
        "enableCodeBlueFeatures": true,
        "enableVipFeatures": false
      }
    ],
    "remove": [
      {
        "href": "https://localhost:8904/api/cardholders/325/elevator_groups/3498734"
      }
    ]
  }
}
```

---

## DELETE /api/cardholders/{id} — Remove a cardholder

This call removes a cardholder from Command Centre. You can find the URL in the href in the cardholder search or any of the other calls that return cardholder hrefs. Do not build it yourself .

### Parameters

- **id** (in path · string): The ID of the cardholder.

### Responses

- **200 OK**: Success.

- **204 No Content**: Success.

- **400 Bad Request**: Deleting the cardholder failed. This happens when the cardholder is a critical part of another construct (a personalised notification, for example).

- **403 Forbidden**: The operator does not have permissions to delete that cardholder or the server is not licensed for cardholder operations. In versions up to and including 9.20 403 also means that there is no such cardholder.

- **404 Not Found**: New in 9.30. There is no such cardholder.

- **409 Conflict**: The cardholder is locked for editing by another operator. The body of the response will tell you which operator is holding the lock.

---

## DELETE /api/cardholders/{id}/access_groups/{membership_id} — Remove an access group membership

This call removes a cardholder's membership in an access group. Note that a cardholder may have more than one membership in a group.

You can find this URL in the cardholder object . Do not build it yourself .

### Parameters

- **id** (in path · string): The identifier of the cardholder.
- **membership_id** (in path · string): The identifier of the cardholder's membership in the access group.

### Responses

- **204 No Content**: Success.

- **403 Forbidden**: The operator does not have a privilege that allows editing that cardholder.

- **404 Not Found**: That is not the href of an access group membership.

- **409 Conflict**: The cardholder is locked for editing by another operator. The body of the response will tell you which operator is holding the lock.

- **4xx**: The operator does not have a privilege that allows editing that cardholder's access group memberships plus 'Modify access control' on the group.

---

## DELETE /api/cardholders/{id}/cards/{card_id} — Remove a card from a cardholder

This call removes a card from a cardholder.

You can find this URL in the cardholder object . Do not build it yourself .

### Parameters

- **id** (in path · string): The identifier of the cardholder.
- **card_id** (in path · string): An opaque identifier for the card.

### Responses

- **200 OK**: Success.

- **204 No Content**: Success.

- **403 Forbidden**: The operator does not have a privilege that allows editing that cardholder.

- **404 Not Found**: That card is not on that cardholder.

- **409 Conflict**: The cardholder is locked for editing by another operator. The body of the response will tell you which operator is holding the lock.

---

## DELETE /api/cardholders/{id}/competencies/{link_id} — Remove a competency from a cardholder

This call removes a competency from a cardholder.

You can find this URL in the cardholder object . Do not build it yourself .

### Parameters

- **id** (in path · string): The identifier of the cardholder.
- **link_id** (in path · string): An opaque identifier for the link between the cardholder and the competency.

### Responses

- **200 OK**: Success.

- **204 No Content**: Success.

- **403 Forbidden**: The operator does not have a privilege that allows editing that cardholder.

- **404 Not Found**: That is not the href of a cardholder's competency.

---

## POST /api/cardholders/{id}/competencies/{link_id}/credit — Change competency credit

This call increases or decreases a cardholder's competency credit. It is an indivisible operation.

It is reserved for the Pre-pay Car Parking feature.

### Parameters

- **id** (in path · string): An opaque identifier for the cardholder.
- **link_id** (in path · string): An opaque identifier for the link between the cardholder and the competency.
- **add** (in query · integer): The amount to adjust the competency credit. This can be positive or negative.

### Responses

- **200 OK**: Success. Future versions will return feedback from the server.

- **204 No Content**: Success.

- **403 Forbidden**: The operator does not have a privilege that allows editing that cardholder.

- **404 Not Found**: The parameters are invalid.

---

## DELETE /api/cardholders/{id}/elevator_groups/{assignment_id} — Remove an elevator group from a cardholder

This call removes a default floor assignment and passenger types from a cardholder for one elevator group.

You will find this URL in the elevatorGroups block of a cardholder object . Do not build it yourself .

### Parameters

- **id** (in path · string): An opaque identifier for the cardholder.
- **assignment_id** (in path · string): An opaque identifier for the link between the cardholder and the elevator group.

### Responses

- **204 No Content**: Success.

- **403 Forbidden**: Your operator does not have the necessary privilege to change this cardholder's elevator groups.

- **404 Not Found**: That is not the URL of a cardholder's elevator group. You can take an href from the 'elevatorGroups' block of a cardholder detail. The message in the results document will tell you more about the problem.

---

## DELETE /api/cardholders/{id}/lockers/{assignment_id} — Remove a locker assignment

This call removes a locker assignment from a cardholder. If the cardholder has no other assignments for this locker after this operation he or she will not be able to open it.

You will find this URL in the lockers block of a cardholder object . In the interest of forward compability, do not build it yourself .

### Parameters

- **id** (in path · string): An opaque identifier for the cardholder.
- **assignment_id** (in path · string): An opaque identifier for the locker assignment.

### Responses

- **204 No Content**: Success.

- **404 Not Found**: That is not the URL of a cardholder's locker assignment. You can take an href from the 'lockers' block of a cardholder detail, or the 'assignments' block of a locker or locker bank detail. The message in the results document will tell you more about the problem.

- **4xx**: You do not have privileges for the operation.

---

## DELETE /api/cardholders/{id}/operator_groups/{membership_id} — Remove an operator group membership

This call removes a cardholder's membership in an operator group. Operator group memberships do not have start and end dates, so a cardholder can only have one membership in a given operator group.

You can find this URL in the cardholder object . In the interest of forward compability, do not build it yourself .

### Parameters

- **id** (in path · string): The identifier of the cardholder.
- **membership_id** (in path · string): The identifier of the cardholder's membership in the operator group.

### Responses

- **204 No Content**: Success.

- **403 Forbidden**: The operator does not have a privilege that allows editing that cardholder's operator group memberships ('Modify operator group membership').

- **404 Not Found**: That is not the href of an operator group membership.

- **409 Conflict**: The cardholder is locked for editing by another operator. The body of the response will tell you which operator is holding the lock.

---

## DELETE /api/cardholders/{id}/roles/{relationship_id} — Remove a relationship

This call severs a relationship between two cardholders.

You can find the URL in the relationships block in the cardholder object of the cardholder who has the relationship, not the cardholder who holds the role. For example if you have a 'supervisor' role you would find the URL to delete by looking up the supervised cardholder, not the supervisor.

In the interest of forward compability, do not build the URL yourself .

### Parameters

- **id** (in path · string): The identifier of the cardholder.
- **relationship_id** (in path · string): An opaque identifier for the link between the cardholder and the role.

### Responses

- **204 No Content**: Success.

- **403 Forbidden**: The operator does not have a privilege that allows editing that cardholder.

- **404 Not Found**: That is not the URL of a relationship. Perhaps it is deleted already.

---

## POST /api/cardholders/{id}/update_location — Change a cardholder's location

This call updates a cardholder's location (moves them) to a target access zone. Added in 8.20.

Do not code this URL into your application. Take it from the 'updateLocation.href' field in a cardholder response .

The POST expects a document which contains an href to the target access zone. The recommended way of getting access zone hrefs is through an access zones call added in 8.20 that returns you the access zones to which you are allowed to move cardholders, according to your operator privileges.

You can also get access zone hrefs from the items controller .

Note that to change a cardholder's location your REST operator will need the "Manage Cardholder Location" privilege in the division of the target access zone and "View Cardholder" on the cardholder itself.

It is not possible to put a cardholder into "nowhere", also known as "outside the system". The only place you can move them is a new access zone.

### Parameters

- **id** (in path · string): An opaque identifier for the cardholder.

### Request body

[Cardholder Update Location POST example](https://gallaghersecurity.github.io/cc-rest-docs/ref/cardholders.html#/definitions/Cardholder%20Update%20Location%20POST%20example)

### Responses

- **200 OK**: Success. Future versions will return feedback from the server.

- **204 No Content**: Success.

- **400 Bad Request**: The cardholder ID or access zone href is invalid.

- **403 Forbidden**: The operator does not have a privilege ('Manage Cardholder Location') that allows moving this cardholder to the target zone.

### Examples

##### Request Example

```json
{
  "accessZone": {
    "href": "https://localhost:8904/api/access_zones/412"
  }
}
```

---

## GET /api/cardholders/{id}/edit — Find out which fields you can edit

This tells you which fields your REST operator can edit based on its privileges.

Gallagher uses this call for rapidly-evolving internal applications. As such, its results are tuned to those applications and are subject to change. Rather than relying on this call, we suggest that you simply give your REST operator the privileges it needs to access to everything it needs.

Note that this method returns 400 if there are two PDFs in the system with the same name. CC insists on unique names for items when you create them but you can end up with duplicates when you form a multiserver cluster out of standalone installations. You should move on that, because having two PDFs with the same name will bewilder your operational staff.

### Parameters

- **id** (in path · string): An opaque identifier for the cardholder.

### Responses

- **200 OK**: Success.

- **4xx**: The parameter is invalid or you do not have privileges for the operation.

---

## GET /api/cardholders/changes — Get changes

This returns cardholder changes matching your search criteria and a link for the next batch.

The first time you call this it will return a link back to this call with a parameter marking the head of the change list. There will not be any changes in the result set for your first call . When you later GET the link the server sent you it will return the changes that occurred since, if there were any, and a new link.

There will be no more than 1000 changes, or as many as you asked for using the top query parameter.

When you are up to date with all the cardholder changes, the results array will be empty. When it comes time to check again, don't just re-use the same URL: get a new one from the next block. It can change even when there are no results.

You can monitor changes to card data using this call, including serial numbers (MIFARE UIDs). However you cannot monitor changes in the lastSuccessfulAccessTime or lastSuccessfulAccessZone fields because they are not attributes of a cardholder object: they are derivatives of his or her activity. If you want to monitor a person's movements we advise subscribing to events.

This is a polled interface: it will return immediately, whether or not there are results. Therefore you should wait for a time between calls if the results array was empty.

Do not code this URL into your application. Take it from the href field in the features.cardholders.changes section of /api .

### Efficiency tips when collecting cardholder changes

- Filter for the kinds of changes you are after using the filter parameter. If you are only interested in people's access group memberships, for example, add filter=accessGroups to your GET, and you will not be troubled with all the other kinds of changes that cardholders go through.
- The default set of fields is large and expensive to compute. Ease the load on the server, the network, and your client by asking for a smaller response. For example if you are only interested in the current state of a cardholder's group memberships and do not care who made the change, or when, or what state the cardholder was in before, use fields=item,cardholder.accessGroups . You need item so you can tell which cardholder changed. If you store your own identifier in a PDF you could drop item and add cardholder.pdf_XXX , where 'XXX' is the ID of your PDF.
- Sleep for as long as you can between calls.
- If you are only interested in changes to cardholders in certain divisions, only give your REST operator access to those divisions. It will not see changes outside them.

### Parameters

- **top** (in query · integer 1 ≤ x ≤ 1000): Sets the maximum number of changes to return per page.
- **pos** (in query · integer x ≥ 0): Reserved for internal use. You may see it in URLs you receive from the server, but you must never add it yourself.
- **filter** (in query · string[] href , id , firstName , lastName , shortName , description , authorised , lastSuccessfulAccessZone , division , notes , personalDataFields , operatorLoginEnabled , operatorUsername , operatorPassword , operatorPasswordExpired , windowsLoginEnabled , windowsUsername , oidcLoginEnabled , oidcUserId , cards , accessGroups , competencies , notifications , relationships , lockers defaults): Limits the search results to the changes that affected these fields, and limits the oldValues and newValues blocks to these fields. You can specify practically any of the fields in the cardholder detail . You can also go into more detail; for example you can monitor a cardholder's cards' validity dates using filter=cards.from,cards.until . filter reduces the number of results; if you want to choose the blocks you receive in each result, use fields . If you do not supply a filter parameter it will use the same fields you get in a cardholder details page. That is nearly everything the API has for a cardholder, but omits some seldom-used or expensive features such as card tracing and the large PIV fields. If you want to monitor them you must list them here. For example, filter=defaults,cards.trace will add the card trace flag to the usual filter. personalDataFields will filter for PDF changes, and will give you the personalDataDefinitions block plus the PDF values that changed. You cannot filter for changes to a particular PDF: you will need to do that in your client. Being able to monitor operator settings arrived in 8.50 and operator group memberships in 8.60. If card changes are all you are interested in, and you do not want to hear about all the other changes that cardholders can experience, use filter=cards .
- **fields** (in query · string[] href , operator , operator.href , operator.name , time , type , item , oldValues , newValues , cardholder , cardholder.* defaults): Limits the blocks in the results. fields affects the blocks in each result; if you want to reduce the number of results, use filter . You can have finer-grained control of fields inside those blocks by listing their JSON paths. For example, to see what the operator and affected cardholder's names are now, you could use fields=operator.name,cardholder.firstName,cardholder.lastName . The values you can list for the cardholder block are very similar to these field names . Prefix each with cardholder. (since in this API they are all inside a block called cardholder ). Separate values with commas and treat the strings as case sensitive. If you do not send this parameter the API will return all cardholder changes to a default set of fields. Use personalDataFields for changes to PDF values.
- **deadline** (in query · integer x ≥ 0 50): Sets the number of seconds after which the server will abort the query and return a 500. If that happens, you should try again later when the server (particularly the database server) is not so busy. Using this can be dangerous. When the server aborts a query it discards all the work it did up to that point. If you send the same query again later, with the same deadline, you may end up stuck in a loop, never making progress, and effectively DoSing your server. If your server is struggling, try reducing the size of each result set using top . Added in 8.80.

### Responses

- **200 OK**: Success.

- **500 Internal Server Error**: The server was too busy to complete the request before the deadline.

- **4xx**: The site does not have the RESTCardholders licence.

---

## GET /api/access_groups — Search access groups

This returns access groups matching your search criteria.

The result will contain no more than 100 or 1000 groups depending on your version; you should follow the next link, if it is present, to collect the next batch.

When you have loaded all the access groups there will be no next link.

If your result set is empty it means your operator does not have the privilege to view any access groups. Perhaps there are none in the divisions in which your operator has 'View access groups' or 'Edit access groups', or your operator has no privileges at all.

This request does not return the group's cardholders. That would make the results unwieldy. Instead, it provides a separate link.

Adding, deleting, or modifying access groups between calls to this API will not affect the pagination of its results if you sort by ID.

You can find the URL for this call in the features.accessGroups.accessGroups.href field of /api . In the interest of forward compability, do not build it yourself .

### Parameters

- **sort** (in query · string id , name , -id , -name): Changes the sort field between database ID and name. If you prefix id or name with a minus sign (ASCII 45), the sort order is reversed. There are two very strong reasons to sort by ID: Sorting by name carries a risk of missing or duplicating objects if your result set spans multiple pages and another operator is editing the database while your REST client is enumerating them. This is known as "page drift." Sorting by ID does not carry that risk. Following a next link is dramatically quicker when sorting by ID. We strongly recommend sorting by ID. In case you were still in doubt, we will do that by default in a future version of Command Centre. The server silently ignores anything except the options listed here.
- **top** (in query · integer x ≥ 1): Sets the maximum number of access groups to return per page. The default depends on your server version; you should set it appropriately for your application.
- **name** (in query · string): Limits the returned items to those with a name that matches this string. Without surrounding quotes or a percent sign or underscore, it is a substring match; surround the parameter with double quotes "..." for an exact match. Without quotes, a percent sign % will match any substring and an underscore will match any single character. The search is always case-insensitive. Results are undefined if you do a substring search for the empty string ( name= ). You will receive no items if you search for those with no name ( name="" ), as all items must have a name. Search parameters are ANDed together.
- **division** (in query · string[]): Limits the returned items to those that are in these divisions. That includes all the items in those divisions' child divisions, because Command Centre treats items as though they are also in their division's parent, and its parent, and so on up to the root division. Separate the IDs of the divisions you are interested with commas. Item IDs are short alphanumeric strings, not URLs. Results are undefined if you provide an ID that is not in the form of a division ID. Search parameters are ANDed together.
- **directDivision** (in query · string[]): Restricts items to those whose division is in this list. Unlike division= , it does not follow ancestry. That means it will limit the search to items that are directly assigned to the divisions you list, whereas division (without the direct ) will also include items that are assigned to their descendants. List the IDs of the divisions you are interested in separated by commas. Item IDs are short alphanumeric strings, not URLs. Because this is the first query parameter we added that contains a capital letter, this will be the first time you have had to think about case sensitivity. directDivision works, directdivision does not. Results are undefined if you provide an ID that is not in the form of a division ID. Search parameters are ANDed together. Added in 9.20.
- **description** (in query · string): Limits the returned items to those with a description that matches this string. By default it is a substring match; surround it with double quotes "..." for an exact match. A _ will match any single character, and a % will match any substring. With or without quotes, having either of these wildcards in the string will anchor it at both ends as though you had surrounded it with " . The search is always case-insensitive. Results are undefined if you search for the empty string ( description= or description="" ). Search parameters are ANDed together.
- **fields** (in query · string[] href , id , name , description , parent , division , children , notes , personalDataDefinitions , cardholders , access , saltoAccess , alarmZones , ... defaults): Sets the list of fields to return in the search results. The values you can list are the same as the field names in the details page . Using it you can return everything on the search page that you would find on the details page. Separate values with commas. Use the special value defaults to return the fields you would have received had you not given the parameter at all. Add more after a comma. Treat the string matches as case sensitive. In v8.00 you will receive the href and internal ID even if you didn't ask for them. In 8.10 you will not. If you are going to send the fields parameter and need the href or ID, include them.
- **pos** (in query · integer): Reserved for internal use. Do not add it to your queries.
- **skip** (in query · integer): Reserved for internal use. Do not add it to your queries.

### Responses

- **200 OK**: Success. See the note in the description about privileges if your result set is empty.

- **403 Forbidden**: The site does not have the RESTCardholders licence.

---

## POST /api/access_groups — Create an access group

Creates a new access group, setting its basic fields, parent, PDFs, and some membership defaults.

Do not code this URL into your application. Take it from the href field in the features.accessGroups.accessGroups.href field of GET
                  /api .

The POST expects a document in the same format as the access group detail . All fields except division are optional.

You will achieve better performance if you combine all you want to achieve into one POST, rather than creating the access group bare with a POST then refining it with PATCHes later.

When successful it returns a location header containing the address of the new access group.

Note that you can only create one access group per POST.

This call requires the RESTConfiguration licence and a server running 9.30 or later.

### Request body

[Access group POST example](https://gallaghersecurity.github.io/cc-rest-docs/ref/cardholders.html#/definitions/Access%20group%20POST%20example)

### Responses

- **201 Created**: Success.

- **400 Bad Request**: The body of the POST did not describe a valid access group. If you see 'Invalid Access Group JSON object', the server could not parse the JSON in the body of your POST. Remember to quote all strings, especially those than contain @ symbols.

- **403 Forbidden**: The operator does not have a privilege that allows creating access groups, or the server does not have the 'RESTConfiguration' licence.

### Examples

##### Request Example

```json
{
  "name": "R&D special projects group.",
  "description": "Deep underground.",
  "division": {
    "href": "https://localhost:8904/api/divisions/352"
  },
  "notes": "",
  "parent": {
    "href": "https://localhost:8904/api/access_groups/100"
  },
  "membershipAutoRemoveExpired": true,
  "membershipFromDefault": "2025-01-01T00:00:00Z",
  "membershipUntilDefault": "2025-01-01T00:00:00Z",
  "personalDataDefinitions": [
    {
      "href": "https://localhost:8904/api/personal_data_fields/5516"
    },
    {
      "href": "https://localhost:8904/api/personal_data_fields/9370"
    }
  ]
}
```

---

## GET /api/access_groups/{id} — Get details of an access group

In addition to the group's vitals and a link to the membership document in the access group search results, this call lists the group's child groups.

Note that you can obtain the same results by adding a fields query parameter to a search .

You can find the URL for this call in the access group search results and in a cardholder's accessGroups array. In the interest of forward compability, do not build it yourself .

### Parameters

- **id** (in path · string): The identifier of the access group.
- **fields** (in query · string[] href , id , name , description , parent , division , children , notes , personalDataDefinitions , access , saltoAccess , alarmZones , ... defaults): Sets the list of fields to return. The values you can list are the same as the field names in the detail results . Use it to return less data than normal. Separate values with commas. Treat the string matches as case sensitive. In v8.00 you will receive the href and internal ID even if you didn't ask for them. In 8.10 you will not. If you are going to send the fields parameter and need the href or ID, include them.

### Responses

- **200 OK**: Success.

- **404 Not Found**: That is not the URL of an access group, or the operator does not have a privilege that allows viewing it, such as 'Modify Access Control' or 'View Access Groups'.

---

## PATCH /api/access_groups/{id} — Update an access group

This is the call you use to modify fields on an access group.

The PATCH expects a document in the same format as the the access group detail but with fewer fields, given in this POST example . Note that you cannot change everything on an access group that the API shows you, such as its membership, access, and permissions. You can change its basic fields, PDFs, and membership defaults.

You can find the URL for this call in the access group search results and in a cardholder's accessGroups array. In the interest of forward compability, do not build it yourself .

This call requires the RESTConfiguration licence and a server running 9.30 or later.

### Parameters

- **id** (in path · string): An opaque identifier.

### Request body

[Access group PATCH example](https://gallaghersecurity.github.io/cc-rest-docs/ref/cardholders.html#/definitions/Access%20group%20PATCH%20example)

### Responses

- **200 OK**: Success. Future versions will return feedback from the server about your PATCH.

- **204 No Content**: Success.

- **400 Bad Request**: The body of the PATCH did not describe a valid access group. See the body of the response for help on what went wrong. It may be that you tried to use the name of another access group: no two items of the same type can have the same name. Or you may have tried to set the division to one that is not visible to you, or you may have attempted to create a loop in the access group hierarchy.

- **403 Forbidden**: The operator has a privilege that allows viewing the item but not modifying it, or you tried to set the division to one you cannot configure, or the server is missing the necessary licence. You need the 'Edit Access Groups' privilege on the item you are changing, which means you need on it on the item's current division. You also need it on the new division, if you are changing that. This is also the response when the server does not have the 'RESTConfiguration' licence.

- **404 Not Found**: That is not the URL of an access group or your operator does not have the privilege to view it. This probably means you have built the URL yourself instead of taking it from the results of a GET .

- **409 Conflict**: The item is locked for editing by another operator. The body of the response will tell you which operator is holding the lock.

### Examples

##### Request Example

```json
{
  "personalDataDefinitions": {
    "add": [
      {
        "href": "https://localhost:8904/api/personal_data_fields/5516"
      }
    ],
    "remove": [
      {
        "href": "https://localhost:8904/api/personal_data_fields/9370"
      }
    ]
  }
}
```

---

## DELETE /api/access_groups/{id} — Remove an access group

This call removes an access group from Command Centre.

You can find the URL for this call in the access group search results and in a cardholder's accessGroups array. In the interest of forward compability, do not build it yourself .

This call requires the RESTConfiguration licence and a server running 9.30 or later.

### Parameters

- **id** (in path · string): An opaque identifier.

### Responses

- **200 OK**: Success.

- **204 No Content**: Success.

- **400 Bad Request**: Deleting the access group failed. This happens when it still has members.

- **403 Forbidden**: The operator has the permission to view the item but not delete it, or the server does not have the 'RESTConfiguration' licence.

- **404 Not Found**: That is not the URL of an access group or the operator is not privileged to view it.

---

## GET /api/access_groups/{id}/cardholders — Get membership of an access group

This lists all cardholders who are direct members of a particular group. It does not paginate the results, so there is no next link. That can make for a large document, so it omits the group's child groups and their cardholder members. It is not recursive, in other words.

There may be more than one entry per cardholder, because any one cardholder can have many memberships to a group, each with different from and until date-times.

If your operator does not have the privilege to view a cardholder item you will receive its name but not its href (since following it would 404).

You can find the URL in the cardholders block of an access group's search results or detail pages. In the interest of forward compability, do not build it yourself .

### Parameters

- **id** (in path · string): The identifier of the access group.

### Responses

- **200 OK**: Success.

type Access group membership

- **404 Not Found**: The ID is invalid, or it is valid but you do not have privileges for the access group. Check the body of the result for a description of the problem.

---

## GET /api/card_types — Search card types

This returns the card types your operator is privileged to view. They may be different card types from those you are allowed to assign to cardholders, and since that is the only thing that this API does with card types, you should probably be using that function instead.

### Responses

- **200 OK**: Success.

---

## POST /api/card_types — Create a card type [coming]

Creates a new card type, setting practically everything except the PIV fields.

Do not code this URL into your application. Take it from the href field in the features.cardTypes.cardTypes.href field of GET /api .

The POST expects a document in the same format as the card type detail .

When successful it returns a location header containing the address of the new card type.

Note that you can only create one item per POST.

This call requires the RESTConfiguration licence.

### Request body

[Card type](https://gallaghersecurity.github.io/cc-rest-docs/ref/cardholders.html#/definitions/Card%20type)

### Responses

- **201 Created**: Success.

- **400 Bad Request**: The body of the POST did not describe a valid card type. If you see 'Invalid Access Group JSON object', the server could not parse the JSON in the body of your POST. Remember to quote all strings, especially those than contain @ symbols.

- **403 Forbidden**: The operator does not have a privilege that allows creating card types, or the server does not have the 'RESTConfiguration' licence.

### Examples

##### Request Example

```json
{
  "id": "600",
  "name": "Red DESFire visitor badge",
  "division": {
    "href": "https://localhost:8904/api/divisions/2"
  },
  "notes": "Disabled after 7d inactivity, 6-char PIN",
  "facilityCode": "A12345",
  "credentialClass": "card",
  "minimumNumber": "1",
  "maximumNumber": "16777215",
  "regex": "^[A-Za-z0-9]+$",
  "regexDescription": "Only alphanumeric characters"
}
```

---

## GET /api/card_types/assign — Search usable card types

This returns the card types you are privileged to assign to a cardholder.

Since a site usually has only a few card types, and each is small, the search, sorting, field selection, and pagination parameters are probably of little use to you. But they work.

If your result set is empty it means your operator does not have the privilege to view any card types. Your operator needs a privilege that allows assigning cards and credentials to cardholders such as 'Add / Edit Card or Credential' or 'Create / Edit / Create and Edit Cardholders'.

You can find this URL in features.cardTypes.assign in /api . It was new in 8.10. In the interest of forward compability, do not build it yourself .

### Parameters

- **sort** (in query · string id , name , -id , -name): Changes the sort field between database ID and name. If you prefix id or name with a minus sign (ASCII 45), the sort order is reversed. There are two very strong reasons to sort by ID: Sorting by name carries a risk of missing or duplicating objects if your result set spans multiple pages and another operator is editing the database while your REST client is enumerating them. This is known as "page drift." Sorting by ID does not carry that risk. Following a next link is dramatically quicker when sorting by ID. We strongly recommend sorting by ID. In case you were still in doubt, we will do that by default in a future version of Command Centre. The server silently ignores anything except the options listed here.
- **top** (in query · integer x ≥ 1): Limits the results to no more than this many items per page. Older versions of Command Centre returned 100 items per page in the absence of this parameter. That is acceptable for GUI applications that will only display the first page, but for integrations that intend to proceed through the entire database it causes a lot of chatter. 8.70 and later versions will default to 1000 items per request. This is about where a graph of throughput versus page size begins to level out.
- **name** (in query · string): Limits the returned items to those with a name that matches this string. Without surrounding quotes or a percent sign or underscore, it is a substring match; surround the parameter with double quotes "..." for an exact match. Without quotes, a percent sign % will match any substring and an underscore will match any single character. The search is always case-insensitive. Results are undefined if you do a substring search for the empty string ( name= ). You will receive no items if you search for those with no name ( name="" ), as all items must have a name. Search parameters are ANDed together.
- **division** (in query · string[]): Limits the returned items to those that are in these divisions. That includes all the items in those divisions' child divisions, because Command Centre treats items as though they are also in their division's parent, and its parent, and so on up to the root division. Separate the IDs of the divisions you are interested with commas. Item IDs are short alphanumeric strings, not URLs. Results are undefined if you provide an ID that is not in the form of a division ID. Search parameters are ANDed together.
- **directDivision** (in query · string[]): Restricts items to those whose division is in this list. Unlike division= , it does not follow ancestry. That means it will limit the search to items that are directly assigned to the divisions you list, whereas division (without the direct ) will also include items that are assigned to their descendants. List the IDs of the divisions you are interested in separated by commas. Item IDs are short alphanumeric strings, not URLs. Because this is the first query parameter we added that contains a capital letter, this will be the first time you have had to think about case sensitivity. directDivision works, directdivision does not. Results are undefined if you provide an ID that is not in the form of a division ID. Search parameters are ANDed together. Added in 9.20.
- **description** (in query · string): Limits the returned items to those with a description that matches this string. By default it is a substring match; surround it with double quotes "..." for an exact match. A _ will match any single character, and a % will match any substring. With or without quotes, having either of these wildcards in the string will anchor it at both ends as though you had surrounded it with " . The search is always case-insensitive. Results are undefined if you search for the empty string ( description= or description="" ). Search parameters are ANDed together.
- **fields** (in query · string[] href , id , name , division , facilityCode , availableCardStates , credentialClass , minimumNumber , maximumNumber , notes , regex , regexDescription): Specifies which fields to return instead of the default set (which contains nearly every field). Separate values with commas. Treat the string matches as case sensitive: use 'facilityCode' rather than 'facilitycode'.
- **pos** (in query · integer): Reserved for internal use. Do not add it to your queries.
- **skip** (in query · integer): Reserved for internal use. Do not add it to your queries.

### Responses

- **200 OK**: Success.

- **403 Forbidden**: The installation lacks a cardholders licence.

---

## GET /api/card_types/{id} — Get one card type

This returns some basic data for a card type. It exists so that clients following card type hrefs from other controllers do not receive a 404. To find out about card types available for assigning to cardholders, use card_types/assign instead.

### Parameters

- **id** (in path · string): An opaque identifier.

### Responses

- **200 OK**: Success.

---

## PATCH /api/card_types/{id} — Update a card type [coming]

This is the call you use to update a card type. Take the URL from the results of a card type search. In the interest of forward compability, do not build it yourself .

The PATCH expects a document in the same format as the the card type detail but with fewer fields.

This call requires the RESTConfiguration licence.

### Parameters

- **id** (in path · string): An opaque identifier.

### Request body

[Card type](https://gallaghersecurity.github.io/cc-rest-docs/ref/cardholders.html#/definitions/Card%20type)

### Responses

- **200 OK**: Success. Future versions will return feedback from the server about your PATCH.

- **204 No Content**: Success.

- **400 Bad Request**: The body of the PATCH did not describe a valid card type. Card types have many rules, easily enforced in a graphical interface but less so in an API. See the body of the response for help on what went wrong.

- **403 Forbidden**: The operator has a privilege that allows viewing the item but not modifying it, or you tried to set the division to one you cannot configure, or the server is missing the necessary licence. You need the 'Configure Site' privilege on the item you are changing, which means you need on it on the item's current division. You also need it on the new division, if you are changing that. This is also the response when the server does not have the 'RESTConfiguration' licence.

- **404 Not Found**: That is not the URL of a card type or your operator does not have the privilege to view it. This probably means you have built the URL yourself instead of taking it from the results of a GET .

- **409 Conflict**: The item is locked for editing by another operator. The body of the response will tell you which operator is holding the lock.

### Examples

##### Request Example

```json
{
  "id": "600",
  "name": "Red DESFire visitor badge",
  "division": {
    "href": "https://localhost:8904/api/divisions/2"
  },
  "notes": "Disabled after 7d inactivity, 6-char PIN",
  "facilityCode": "A12345",
  "credentialClass": "card",
  "minimumNumber": "1",
  "maximumNumber": "16777215",
  "regex": "^[A-Za-z0-9]+$",
  "regexDescription": "Only alphanumeric characters"
}
```

---

## DELETE /api/card_types/{id} — Remove a card type [coming]

This call removes a card type from Command Centre. In the interest of forward compability, take the URL from the results of a card type search rather than building it yourself .

This call requires the RESTConfiguration licence.

### Parameters

- **id** (in path · string): An opaque identifier.

### Responses

- **200 OK**: Success.

- **204 No Content**: Success.

- **400 Bad Request**: Deleting the card type failed. This happens when the card type is in use. Most often it is because a cardholder still has a card of this type, but card types can also be connected to other items.

- **403 Forbidden**: The operator has the permission to view the item but not delete it, or the server does not have the 'RESTConfiguration' licence.

- **404 Not Found**: That is not the URL of a card type, or the operator is not privileged to view it.

---

## GET /api/competencies — Search competencies

This returns competencies matching your search criteria.

The result will contain no more than 100 or 1000 competencies depending on your version; you should follow the next link (if present) for the next batch.

If your result set is empty it means your operator does not have the privilege to view any competencies. Perhaps there are none in the divisions in which your operator has 'View site' or 'Edit site', or your operator has no privileges at all.

A bug in 7.90 meant that this call did not provide next links for sites that had more than 100 competencies. If this is you, set the 'top' parameter as high as you can (as recommended in the efficiency tips). Command Centre clamps that to a maximum of ten thousand. If that is not enough competencies for you, you are probably already in contact with Gallagher technical support.

Get this URL from features.competencies.competencies.href in /api . In the interest of forward compability, do not build it yourself .

### Parameters

- **sort** (in query · string id , name , -id , -name): Changes the sort field between database ID and name. If you prefix id or name with a minus sign (ASCII 45), the sort order is reversed. There are two very strong reasons to sort by ID: Sorting by name carries a risk of missing or duplicating objects if your result set spans multiple pages and another operator is editing the database while your REST client is enumerating them. This is known as "page drift." Sorting by ID does not carry that risk. Following a next link is dramatically quicker when sorting by ID. We strongly recommend sorting by ID. In case you were still in doubt, we will do that by default in a future version of Command Centre. The server silently ignores anything except the options listed here.
- **top** (in query · integer x ≥ 1): Limits the results to no more than this many items per page. Older versions of Command Centre returned 100 items per page in the absence of this parameter. That is acceptable for GUI applications that will only display the first page, but for integrations that intend to proceed through the entire database it causes a lot of chatter. 8.70 and later versions will default to 1000 items per request. This is about where a graph of throughput versus page size begins to level out.
- **name** (in query · string): Limits the returned items to those with a name that matches this string. Without surrounding quotes or a percent sign or underscore, it is a substring match; surround the parameter with double quotes "..." for an exact match. Without quotes, a percent sign % will match any substring and an underscore will match any single character. The search is always case-insensitive. Results are undefined if you do a substring search for the empty string ( name= ). You will receive no items if you search for those with no name ( name="" ), as all items must have a name. Search parameters are ANDed together.
- **division** (in query · string[]): Limits the returned items to those that are in these divisions. That includes all the items in those divisions' child divisions, because Command Centre treats items as though they are also in their division's parent, and its parent, and so on up to the root division. Separate the IDs of the divisions you are interested with commas. Item IDs are short alphanumeric strings, not URLs. Results are undefined if you provide an ID that is not in the form of a division ID. Search parameters are ANDed together.
- **directDivision** (in query · string[]): Restricts items to those whose division is in this list. Unlike division= , it does not follow ancestry. That means it will limit the search to items that are directly assigned to the divisions you list, whereas division (without the direct ) will also include items that are assigned to their descendants. List the IDs of the divisions you are interested in separated by commas. Item IDs are short alphanumeric strings, not URLs. Because this is the first query parameter we added that contains a capital letter, this will be the first time you have had to think about case sensitivity. directDivision works, directdivision does not. Results are undefined if you provide an ID that is not in the form of a division ID. Search parameters are ANDed together. Added in 9.20.
- **description** (in query · string): Limits the returned items to those with a description that matches this string. By default it is a substring match; surround it with double quotes "..." for an exact match. A _ will match any single character, and a % will match any substring. With or without quotes, having either of these wildcards in the string will anchor it at both ends as though you had surrounded it with " . The search is always case-insensitive. Results are undefined if you search for the empty string ( description= or description="" ). Search parameters are ANDed together.
- **fields** (in query · string[] href , id , name , shortName , description , division , notes , expiryNotify , noticePeriod , defaultExpiry , defaultAccess defaults): Specifies which fields to return in the search results. The values you can list are the same as the field names in the details page . Using it you can return everything on the search page that you would find on the details page. Separate values with commas. Use the special value defaults to return the fields you would have received had you not given the parameter at all. Add more after a comma. Treat the string matches as case sensitive. In v8.00 you will receive the href and internal ID even if you did not ask for them. In 8.10 you will not. If you are going to send the fields parameter and need the href or ID, be explicit.
- **pos** (in query · integer): Reserved for internal use. Do not add it to your queries.
- **skip** (in query · integer): Reserved for internal use. Do not add it to your queries.

### Responses

- **200 OK**: Success. See the note in the description about privileges if your result set is empty.

- **403 Forbidden**: The installation lacks a cardholders licence.

---

## POST /api/competencies — Create a competency

Creates a new competency.

The POST expects a document in the same format as the the competency detail but with far fewer fields. An example is this POST example .

When successful it returns a location header containing the address of the new competency.

Note that you can only create one competency per POST.

Do not code this URL into your application. Take it from the results of GET /api .

New to Command Centre 9.10.

This requires the RESTConfiguration licence.

### Request body

[Competency PATCH and POST example](https://gallaghersecurity.github.io/cc-rest-docs/ref/cardholders.html#/definitions/Competency%20PATCH%20and%20POST%20example)

### Responses

- **201 Created**: Success. Check the response body for feedback about your request.

- **400 Bad Request**: The body of the POST did not describe a valid competency. You may have tried to give the new competency the same name as an existing one. See the body of the response for help.

- **403 Forbidden**: The operator does not have a privilege on the division that allows creating items inside it ('Configure Site'), or the server has reached its licensed limit of competencies, or it does not have the 'RESTConfiguration' licence.

### Examples

##### Request Example

```json
{
  "name": "New competency",
  "shortName": "C4",
  "description": "Translated automatically.",
  "division": {
    "href": "https://localhost:8904/api/divisions/2"
  },
  "notes": "A very long string."
}
```

---

## GET /api/competencies/{id} — Get details of a competency

You get this URL from a cardholder or from a competency search. In the interest of forward compability, do not build it yourself .

The results document gives everything Command Centre has on a competency except the warning messages that appear on a reader when a cardholder needs to take action.

### Parameters

- **id** (in path · string): The alphanumeric identifier of the competency you are after.
- **fields** (in query · string[] href , id , name , shortName , description , division , notes , expiryNotify , noticePeriod , defaultExpiry , defaultAccess defaults): Specifies which fields to return. The values you can list are the same as the field names in the details page . Use it to reduce the size of the result document. Separate values with commas. Treat the string matches as case sensitive.

### Responses

- **200 OK**: Success.

- **403 Forbidden**: The installation lacks a cardholders licence.

- **404 Not Found**: Your REST operator does not have the privilege to view this competency.

---

## PATCH /api/competencies/{id} — Update a competency

This is the call you use to update a division's name, short name, description, notes, or division. In the interest of forward compability, take the URL from the results of a division search rather than building it yourself .

The PATCH expects a document in the same format as the the competency detail but with fewer fields. An example is this PATCH example .

New to Command Centre 9.10.

This call requires the RESTConfiguration licence.

### Parameters

- **id** (in path · string): An opaque identifier.

### Request body

[Competency PATCH and POST example](https://gallaghersecurity.github.io/cc-rest-docs/ref/cardholders.html#/definitions/Competency%20PATCH%20and%20POST%20example)

### Responses

- **200 OK**: Success. Future versions will return feedback from the server about your PATCH.

- **204 No Content**: Success.

- **400 Bad Request**: The body of the PATCH did not describe a valid competency. See the body of the response for help on what went wrong. It may be that you tried to use the name of another competency: no two items of the same type can have the same name. Or you may have tried to set the competency's division to one that is not visible to you.

- **403 Forbidden**: The operator has a privilege that allows viewing the item but not modifying it, or you tried to set the division to one you cannot configure, or the server is missing the necessary licence. You need the 'Configure Site' privilege on the item you are changing, which means you need on it on the item's current division. You also need it on the new division, if you are changing that. This is also the response when the server does not have the 'RESTConfiguration' licence.

- **404 Not Found**: That is not the URL of a competency or your operator does not have the privilege to view it. This probably means you have built the URL yourself instead of taking it from the results of a GET .

- **409 Conflict**: The item is locked for editing by another operator. The body of the response will tell you which operator is holding the lock.

### Examples

##### Request Example

```json
{
  "name": "New competency",
  "shortName": "C4",
  "description": "Translated automatically.",
  "division": {
    "href": "https://localhost:8904/api/divisions/2"
  },
  "notes": "A very long string."
}
```

---

## DELETE /api/competencies/{id} — Remove a competency

This call removes a competency from Command Centre. You get this URL from a cardholder or from a competency search. In the interest of forward compability, do not build it yourself .

New to Command Centre 9.10.

This call requires the RESTConfiguration licence.

### Parameters

- **id** (in path · string): An opaque identifier.

### Responses

- **200 OK**: Success.

- **204 No Content**: Success.

- **400 Bad Request**: Deleting the competency failed. This happens when the item is still being used by another item.

- **403 Forbidden**: The operator has the permission to view the item but not delete it, or the server does not have the 'RESTConfiguration' licence.

- **404 Not Found**: That is not the URL of a competency, or the operator is not privileged to view it.

---

## GET /api/locker_banks — Search locker banks

This returns locker banks matching your search criteria.

The result will contain no more than 100 or 100 objects depending on your version; you should follow the next link, if it is present, to collect more.

If the result set is empty it means there are no locker banks in the divisions in which the operator has a privilege that allows listing them, such as 'View lockers and assignments'.

When you have loaded all the locker banks there will no next link.

Do not code this URL into your application. Take it from the 'href' field in the features.lockerBanks.lockerBanks section of /api .

### Parameters

- **sort** (in query · string id , name , -id , -name): Changes the sort field between database ID and name. If you prefix id or name with a minus sign (ASCII 45), the sort order is reversed. There are two very strong reasons to sort by ID: Sorting by name carries a risk of missing or duplicating objects if your result set spans multiple pages and another operator is editing the database while your REST client is enumerating them. This is known as "page drift." Sorting by ID does not carry that risk. Following a next link is dramatically quicker when sorting by ID. We strongly recommend sorting by ID. In case you were still in doubt, we will do that by default in a future version of Command Centre. The server silently ignores anything except the options listed here.
- **top** (in query · integer x ≥ 1): Sets the maximum number of locker banks to return per page. The default depends on your server version; you should set it appropriately for your application.
- **name** (in query · string): Limits the returned items to those with a name that matches this string. Without surrounding quotes or a percent sign or underscore, it is a substring match; surround the parameter with double quotes "..." for an exact match. Without quotes, a percent sign % will match any substring and an underscore will match any single character. The search is always case-insensitive. Results are undefined if you do a substring search for the empty string ( name= ). You will receive no items if you search for those with no name ( name="" ), as all items must have a name. Search parameters are ANDed together.
- **division** (in query · string[]): Limits the returned items to those that are in these divisions. That includes all the items in those divisions' child divisions, because Command Centre treats items as though they are also in their division's parent, and its parent, and so on up to the root division. Separate the IDs of the divisions you are interested with commas. Item IDs are short alphanumeric strings, not URLs. Results are undefined if you provide an ID that is not in the form of a division ID. Search parameters are ANDed together.
- **directDivision** (in query · string[]): Restricts items to those whose division is in this list. Unlike division= , it does not follow ancestry. That means it will limit the search to items that are directly assigned to the divisions you list, whereas division (without the direct ) will also include items that are assigned to their descendants. List the IDs of the divisions you are interested in separated by commas. Item IDs are short alphanumeric strings, not URLs. Because this is the first query parameter we added that contains a capital letter, this will be the first time you have had to think about case sensitivity. directDivision works, directdivision does not. Results are undefined if you provide an ID that is not in the form of a division ID. Search parameters are ANDed together. Added in 9.20.
- **description** (in query · string): Limits the returned items to those with a description that matches this string. By default it is a substring match; surround it with double quotes "..." for an exact match. A _ will match any single character, and a % will match any substring. With or without quotes, having either of these wildcards in the string will anchor it at both ends as though you had surrounded it with " . The search is always case-insensitive. Results are undefined if you search for the empty string ( description= or description="" ). Search parameters are ANDed together.
- **fields** (in query · string[] href , id , name , shortName , description , division , lockers , notes , lockers.defaults , lockers.id , lockers.href , lockers.name , lockers.shortName , lockers.description , lockers.division , lockers.notes , lockers.commands , lockers.assignments defaults): Sets which fields to return. The values you can list are the same as the field names in the details page . Using it you can return everything on the search page that you would find on the details page. Separate values with commas. If you specify any value for this parameter, the default no longer applies and you will only receive the fields you asked for. Use the special value defaults to return the locker bank fields you would have received had you not given the parameter at all. Then you can add a comma and more fields. In v8.00 you will receive the href and internal ID even if you did not ask for them. In 8.10 you will not. If you are going to send the fields parameter and need the href or ID, be explicit. All the field names beginning with lockers. arrived in v8.10.1112. They affect the fields that appear inside the 'lockers' block. The guideline for using defaults is: if you want to receive less data, specify only the fields you want. If you want to receive more, either list every field you want or simply use lockers.defaults plus your extras. Treat the string matches as case sensitive.
- **pos** (in query · integer): Reserved for internal use. Do not add it to your queries.
- **skip** (in query · integer): Reserved for internal use. Do not add it to your queries.

### Responses

- **200 OK**: Success.

- **403 Forbidden**: The installation lacks a lockers licence, or it is missing RESTCardholders and RESTStatus and (in 8.60) RESTOverrides.

---

## GET /api/locker_banks/{id} — Get details of a locker bank

This returns details for a locker bank. In the interest of forward compatibility follow the href in the locker bank summary to get here rather than building the URL yourself .

### Parameters

- **id** (in path · string): An internal identifier of the locker bank.
- **fields** (in query · string[] href , id , name , shortName , description , division , notes , connectedController , lockers , lockers.defaults , lockers.connectedController , ... defaults): Sets which fields to return. The values you can list are the same as the field names you would get in the details page . Use it to cut back on the size of the response for large locker banks. Separate values with commas. Treat the string matches as case sensitive. In v8.00 you will receive the href and internal ID even if you did not ask for them. In 8.10 you will not. If you are going to send the fields parameter and need the href or ID, be explicit.

### Responses

- **200 OK**: Success.

- **403 Forbidden**: The installation lacks a lockers licence, or it is missing RESTCardholders and RESTStatus and (in 8.60) RESTOverrides.

- **404 Not Found**: Your REST operator does not have the privilege to view that locker bank.

---

## GET /api/lockers/{id} — Get details of a locker

This returns details for a locker. Follow the href in the locker bank summary or locker bank detail to get here rather than building the URL yourself .

Prior to v8.20 this endpoint was at /api/locker_banks/{locker_bank_id}/lockers/{id} .

### Parameters

- **id** (in path · string): An internal identifier of the locker bank.
- **fields** (in query · string[] href , id , name , shortName , description , division , assignments , notes , commands , connectedController defaults): Sets which fields to return. Use it to cut back on the size of the response for a locker with many assignments. Separate values with commas. Treat the string matches as case sensitive.

### Responses

- **200 OK**: Success.

- **403 Forbidden**: The installation lacks a lockers licence, or it is missing both RESTCardholders and RESTStatus and (in 8.60) RESTOverrides.

- **404 Not Found**: Your REST operator does not have the privilege to view that locker bank ('Locker assignments').

---

## POST /api/lockers/{id}/open — Open a locker

Sends an override to open a locker.

You should get this URL from a locker detail or locker bank detail rather than building it yourself .

New in 8.10.1112.

### Parameters

- **id** (in path · string): An opaque identifier.

### Responses

- **200 OK**: Success. Future versions will contain feedback from the server.

- **204 No Content**: Success.

- **403 Forbidden**: The site does not have the RESTOverrides licence (in which case the body of the result will say so), or your operator does not have 'Override - Open Locker' on the locker's division.

- **404 Not Found**: That is not the URL of a locker, or it is the URL of a locker your operator does not have the privilege to see.

---

## POST /api/lockers/{id}/quarantine — Quarantine a locker

Sends an override to quarantine a locker. If the override succeeds, the locker will not be allocatable until its quarantine ends.

The call will fail if the locker is allocated to a cardholder.

You should get this URL from a locker detail or locker bank detail rather than building it yourself .

New in 9.10.

### Parameters

- **id** (in path · string): The ID of the locker to quarantine.

### Responses

- **200 OK**: Success. The response body will contain feedback from the server.

- **204 No Content**: Success.

- **400 Bad Request**: Check the response body for an error message. A common fault is the locker being allocated.

- **403 Forbidden**: The site does not have the RESTOverrides licence (in which case the body of the result will say so), or your operator does not have 'Override' on the locker's division.

- **404 Not Found**: That is not the URL of a locker, or it is the URL of a locker your operator does not have the privilege to see.

---

## POST /api/lockers/{id}/cancel_quarantine — Un-quarantine a locker

Sends an override to remove the quarantine from a locker, making it available for allocation.

You should get this URL from a locker detail or locker bank detail rather than building it yourself .

New in 9.10.

### Parameters

- **id** (in path · string): An opaque identifier.

### Responses

- **200 OK**: Success. Future versions will return feedback from the server.

- **204 No Content**: Success.

- **403 Forbidden**: The site does not have the RESTOverrides licence (in which case the body of the result will say so), or your operator does not have 'Override' on the locker's division.

- **404 Not Found**: That is not the URL of a locker, or it is the URL of a locker your operator does not have the privilege to see.

---

## GET /api/operator_groups — Search operator groups

This returns operator groups matching your search criteria.

The result will contain a batch of groups; you should follow the next link, if it is present, to collect the next batch.

When you have loaded all the operator groups there will be no next link.

If your result set is empty it means either your search terms were too tight or your operator does not have the privilege to view any operator groups. Perhaps there are none in the divisions in which your operator has 'View operators' or 'Edit operators', or your operator does not have those privileges at all.

This request does not return the group's cardholders. That would make the results unwieldy. Instead, it provides a separate link.

Adding, deleting, or modifying operator groups between calls to this API will not affect the pagination of its results if you sort by ID.

You can find the URL for this call in the features.operatorGroups.operatorGroups.href field of /api . In the interest of forward compability, do not build it yourself .

Added in 8.50.

### Parameters

- **sort** (in query · string id , name , -id , -name): Changes the sort field between database ID and name. If you prefix id or name with a minus sign (ASCII 45), the sort order is reversed. There are two very strong reasons to sort by ID: Sorting by name carries a risk of missing or duplicating objects if your result set spans multiple pages and another operator is editing the database while your REST client is enumerating them. This is known as "page drift." Sorting by ID does not carry that risk. Following a next link is dramatically quicker when sorting by ID. We strongly recommend sorting by ID. In case you were still in doubt, we will do that by default in a future version of Command Centre. The server silently ignores anything except the options listed here.
- **top** (in query · integer x ≥ 1): Sets the maximum number of operator groups to return per page. The default depends on your server version; you should set it appropriately for your application.
- **name** (in query · string): Limits the returned items to those with a name that matches this string. Without surrounding quotes or a percent sign or underscore, it is a substring match; surround the parameter with double quotes "..." for an exact match. Without quotes, a percent sign % will match any substring and an underscore will match any single character. The search is always case-insensitive. Results are undefined if you do a substring search for the empty string ( name= ). You will receive no items if you search for those with no name ( name="" ), as all items must have a name. Search parameters are ANDed together.
- **division** (in query · string[]): Limits the returned items to those that are in these divisions. That includes all the items in those divisions' child divisions, because Command Centre treats items as though they are also in their division's parent, and its parent, and so on up to the root division. Separate the IDs of the divisions you are interested with commas. Item IDs are short alphanumeric strings, not URLs. Results are undefined if you provide an ID that is not in the form of a division ID. Search parameters are ANDed together.
- **directDivision** (in query · string[]): Restricts items to those whose division is in this list. Unlike division= , it does not follow ancestry. That means it will limit the search to items that are directly assigned to the divisions you list, whereas division (without the direct ) will also include items that are assigned to their descendants. List the IDs of the divisions you are interested in separated by commas. Item IDs are short alphanumeric strings, not URLs. Because this is the first query parameter we added that contains a capital letter, this will be the first time you have had to think about case sensitivity. directDivision works, directdivision does not. Results are undefined if you provide an ID that is not in the form of a division ID. Search parameters are ANDed together. Added in 9.20.
- **description** (in query · string): Limits the returned items to those with a description that matches this string. By default it is a substring match; surround it with double quotes "..." for an exact match. A _ will match any single character, and a % will match any substring. With or without quotes, having either of these wildcards in the string will anchor it at both ends as though you had surrounded it with " . The search is always case-insensitive. Results are undefined if you search for the empty string ( description= or description="" ). Search parameters are ANDed together.
- **fields** (in query · string[] href , id , name , description , division , notes , cardholders , divisions defaults): Sets which fields to return. The values you can list are the same as the field names in the details page . Using it you can return everything on the search page that you would find on the details page. Separate values with commas. Use the special value defaults to return the fields you would have received had you not given the parameter at all. Add more after a comma. Treat the string matches as case sensitive.
- **pos** (in query · integer): Reserved for internal use. Do not add it to your queries.
- **skip** (in query · integer): Reserved for internal use. Do not add it to your queries.

### Responses

- **200 OK**: Success. See the note in the description about privileges if your result set is empty.

- **403 Forbidden**: The site does not have the RESTCardholders licence.

---

## GET /api/operator_groups/{id} — Get details of an operator group

In addition to the group's vitals and a link to the membership document, this call returns the divisions in which the operator groups grants its privileges.

Note that you can obtain the same results by adding fields=defaults,description,division,divisions,cardholders to a search .

You can find the URL for this call in the operator group search results and in a cardholder's operatorGroups array. In the interest of forward compability, do not build it yourself .

Added in 8.50.

### Parameters

- **id** (in path · string): The identifier of the operator group.
- **fields** (in query · string[] defaults , href , id , name , description , division , notes , cardholders , divisions defaults): Sets which fields to return. The values you can list are the same as the field names in the detail results . Use it to return the notes, which don't appear by default. Separate values with commas. Treat the string matches as case sensitive.

### Responses

- **200 OK**: Success.

- **403 Forbidden**: The site does not have the RESTCardholders licence.

- **404 Not Found**: That is not the URL of an operator group, or it is the URL of an operator group that the operator does not have the privilege to see ('View' or 'Edit Operators').

---

## GET /api/operator_groups/{id}/cardholders — Get membership of an operator group

This lists all cardholders who are members of the group identified by the request URL. It does not paginate the results, so there is no next link.

Operator groups cannot contain other operator groups, so every cardholder benefiting from this operator group comes out of this call.

If your operator does not have the privilege to view a cardholder item you will receive its name but not its href (since following the href would 404).

You can find this call's URL in the cardholders block of an operator group. In the interest of forward compability, do not build it yourself .

Added in 8.50.

There are two hrefs per cardholder. The lower-level href is the identifier for the cardholder, found throughout this API. The higher-level href field only appears if you ask for it using the fields query parameter, and only if the server is 8.70 or later. It refers to the cardholder's membership in the operator group. If you DELETE it, you will remove the cardholder from this operator group (which will demote them from an operator to a regular cardholder if this was their last remaining operator group).

### Parameters

- **id** (in path · string): The identifier of the operator group.
- **fields** (in query · string[] defaults , cardholder , href defaults): Specifies the fields you want in the search results. The only two candidates are cardholder and href . The default is fields=defaults , which is the same as fields=cardholder . Separate values with commas.

### Responses

- **200 OK**: Success.

type Operator group membership

- **4xx**: The ID is invalid, or it is valid but you do not have privileges for the operator group. Check the body of the result for a description of the problem.

---

## GET /api/personal_data_fields — Search PDF definitions

This returns the PDF definitions you are privileged to view. You will need the 'View Personal Data Definitions' privilege, or its 'Edit' equivalent, on a PDF's division in order to see it.

The result will contain no more than 100 or 1000 objects, depending on your version; you should follow the next link, if it is present, to collect more. When you have loaded all the PDF definitions there will no next link.

Do not code this URL into your application. Take it from the href in the features.personalDataFields.personalDataFields section of /api .

### Parameters

- **sort** (in query · string id , name , -id , -name): Changes the sort field between database ID and name. If you prefix id or name with a minus sign (ASCII 45), the sort order is reversed. There are two very strong reasons to sort by ID: Sorting by name carries a risk of missing or duplicating objects if your result set spans multiple pages and another operator is editing the database while your REST client is enumerating them. This is known as "page drift." Sorting by ID does not carry that risk. Following a next link is dramatically quicker when sorting by ID. We strongly recommend sorting by ID. In case you were still in doubt, we will do that by default in a future version of Command Centre. The server silently ignores anything except the options listed here.
- **top** (in query · integer x ≥ 1): Limits the results to no more than this many items per page. Older versions of Command Centre returned 100 items per page in the absence of this parameter. That is acceptable for GUI applications that will only display the first page, but for integrations that intend to proceed through the entire database it causes a lot of chatter. 8.70 and later versions will default to 1000 items per request. This is about where a graph of throughput versus page size begins to level out.
- **name** (in query · string): Limits the returned items to those with a name that matches this string. Without surrounding quotes or a percent sign or underscore, it is a substring match; surround the parameter with double quotes "..." for an exact match. Without quotes, a percent sign % will match any substring and an underscore will match any single character. The search is always case-insensitive. Results are undefined if you do a substring search for the empty string ( name= ). You will receive no items if you search for those with no name ( name="" ), as all items must have a name. Search parameters are ANDed together.
- **division** (in query · string[]): Limits the returned items to those that are in these divisions. That includes all the items in those divisions' child divisions, because Command Centre treats items as though they are also in their division's parent, and its parent, and so on up to the root division. Separate the IDs of the divisions you are interested with commas. Item IDs are short alphanumeric strings, not URLs. Results are undefined if you provide an ID that is not in the form of a division ID. Search parameters are ANDed together.
- **directDivision** (in query · string[]): Restricts items to those whose division is in this list. Unlike division= , it does not follow ancestry. That means it will limit the search to items that are directly assigned to the divisions you list, whereas division (without the direct ) will also include items that are assigned to their descendants. List the IDs of the divisions you are interested in separated by commas. Item IDs are short alphanumeric strings, not URLs. Because this is the first query parameter we added that contains a capital letter, this will be the first time you have had to think about case sensitivity. directDivision works, directdivision does not. Results are undefined if you provide an ID that is not in the form of a division ID. Search parameters are ANDed together. Added in 9.20.
- **description** (in query · string): Limits the returned items to those with a description that matches this string. By default it is a substring match; surround it with double quotes "..." for an exact match. A _ will match any single character, and a % will match any substring. With or without quotes, having either of these wildcards in the string will anchor it at both ends as though you had surrounded it with " . The search is always case-insensitive. Results are undefined if you search for the empty string ( description= or description="" ). Search parameters are ANDed together.
- **fields** (in query · string[] href , id , name , description , division , serverDisplayName , type , default , required , unique , sortPriority , accessGroups , regex , regexDescription , defaultAccess , operatorAccess , notificationDefault defaults): Specifies the fields in the search results. The values you can list are the same in the search and details pages. Using it you can return everything on the search page that you would find on the details page. Separate values with commas. Use the special value defaults to return the fields you would have received had you not given the parameter at all. Add more after a comma. Treat the string matches as case sensitive.
- **pos** (in query · integer): Reserved for internal use. Do not add it to your queries.
- **skip** (in query · integer): Reserved for internal use. Do not add it to your queries.

### Responses

- **200 OK**: Success.

- **403 Forbidden**: The site does not have the RESTCardholders or RESTEvents licence.

---

## POST /api/personal_data_fields — Create [coming]

Creates a new personal data definition.

Do not code this URL into your application. Take it from the href field in the features.personalDataFields.personalDataFields.href field of GET /api .

When successful it returns a location header containing the address of the new PDF.

Note that you can only create one per POST.

This call requires the RESTConfiguration licence.

### Request body

[PDF definition](https://gallaghersecurity.github.io/cc-rest-docs/ref/cardholders.html#/definitions/PDF%20definition)

### Responses

- **201 Created**: Success.

- **400 Bad Request**: The body of the POST did not describe a valid PDF. If you see 'Invalid Personal Data Field JSON object', the server could not parse the JSON in the body of your POST. Remember to quote all strings, especially those than contain @ symbols.

- **403 Forbidden**: The operator does not have a privilege that allows creating PDFs or the server does not have the 'RESTConfiguration' licence.

### Examples

##### Request Example

```json
{
  "name": "email",
  "description": "Corporate mailbox",
  "division": {
    "id": "2",
    "href": "https://localhost:8904/api/divisions/2"
  },
  "type": "email",
  "default": "contact@example.com",
  "required": false,
  "unique": false,
  "defaultAccess": "fullAccess",
  "sortPriority": 50,
  "notificationDefault": false,
  "regex": ".*@.*",
  "regexDescription": "@ least",
  "imageWidth": 600,
  "imageHeight": 800,
  "imageFormat": "jpg",
  "contentType": "image/jpeg",
  "isProfileImage": false
}
```

---

## GET /api/personal_data_fields/{id} — Get details of a PDF definition

This returns details for a PDF definition. Follow the href in the summary to get here, rather than building it yourself .

### Parameters

- **id** (in path · string): An internal identifier for the PDF definition.
- **fields** (in query · string[] href , id , name , description , division , serverDisplayName , type , default , required , unique , sortPriority , accessGroups , regex , regexDescription , defaultAccess , operatorAccess , notificationDefault defaults): Specifies the fields in the search results. The values you can list are the same in the search and details pages. Using it you can return everything on the search page that you would find on the details page. Separate values with commas. Use the special value defaults to return the fields you would have received had you not given the parameter at all. Add more after a comma. Treat the string matches as case sensitive.

### Responses

- **200 OK**: Success.

- **403 Forbidden**: The site does not have the RESTCardholders or RESTEvents licence

- **404 Not Found**: The operator does not have a privilege that allows viewing that PDF's definition ('View' or 'Edit Personal Data Definition'), or the PDF's own access control is hiding it from the operator.

---

## PATCH /api/personal_data_fields/{id} — Update [coming]

This is the call you use to update a PDF. Follow the href in the summary to get here, rather than building it yourself .

The PATCH expects a document in the same format as the the PDF detail .

It requires the RESTConfiguration licence.

### Parameters

- **id** (in path · string): An opaque identifier for the PDF.

### Request body

[PDF definition](https://gallaghersecurity.github.io/cc-rest-docs/ref/cardholders.html#/definitions/PDF%20definition)

### Responses

- **200 OK**: Success. Future versions will return feedback from the server about your PATCH.

- **204 No Content**: Success.

- **400 Bad Request**: The body of the PATCH did not describe a valid PDF. PDFs have many rules, easily enforced in a graphical interface but less so in an API. See the body of the response for help on what went wrong.

- **403 Forbidden**: The operator has a privilege that allows viewing the item but not modifying it, or you tried to set the division to one you cannot configure, or the server is missing the necessary licence. You need the 'Edit Personal Data Definitions' privilege on the item you are changing, which means you need on it on its current division. You also need it on the new division, if you are changing that. This is also the response when the server does not have the 'RESTConfiguration' licence.

- **404 Not Found**: That is not the URL of a PDF or your operator does not have the privilege to view it. This probably means you have built the URL yourself instead of taking it from the results of a GET .

- **409 Conflict**: The item is locked for editing by another operator. The body of the response will tell you which operator is holding the lock.

### Examples

##### Request Example

```json
{
  "name": "email",
  "description": "Corporate mailbox",
  "division": {
    "id": "2",
    "href": "https://localhost:8904/api/divisions/2"
  },
  "type": "email",
  "default": "contact@example.com",
  "required": false,
  "unique": false,
  "defaultAccess": "fullAccess",
  "sortPriority": 50,
  "notificationDefault": false,
  "regex": ".*@.*",
  "regexDescription": "@ least",
  "imageWidth": 600,
  "imageHeight": 800,
  "imageFormat": "jpg",
  "contentType": "image/jpeg",
  "isProfileImage": false
}
```

---

## DELETE /api/personal_data_fields/{id} — Remove [coming]

This call removes a PDF item from Command Centre. Follow the href in the summary to get here, rather than building it yourself .

It requires the RESTConfiguration licence.

### Parameters

- **id** (in path · string): An opaque identifier for the PDF.

### Responses

- **200 OK**: Success.

- **204 No Content**: Success.

- **400 Bad Request**: Deleting failed. This happens when the PDF is in use.

- **403 Forbidden**: The operator has the permission to view the item but not delete it, or the server does not have the 'RESTConfiguration' licence.

- **404 Not Found**: That is not the URL of a PDF, or the operator is not privileged to view it.

---

## GET /api/receptions — Search receptions

This returns the receptions you are privileged to view.

The result will contain no more than 100 or 1000 objects depending on your version; you should follow the next link, if it is present, to collect more, or use the top parameter to get more per page. Receptions are small, so setting a limit of 1000 is sensible.

When you have loaded all the receptions there will no next link.

Do not code this URL into your application. Take it from the href in the features.receptions.receptions section of /api .

### Parameters

- **sort** (in query · string id , name , -id , -name): Changes the sort field between database ID and name. If you prefix id or name with a minus sign (ASCII 45), the sort order is reversed. There are two very strong reasons to sort by ID: Sorting by name carries a risk of missing or duplicating objects if your result set spans multiple pages and another operator is editing the database while your REST client is enumerating them. This is known as "page drift." Sorting by ID does not carry that risk. Following a next link is dramatically quicker when sorting by ID. We strongly recommend sorting by ID. In case you were still in doubt, we will do that by default in a future version of Command Centre. The server silently ignores anything except the options listed here.
- **top** (in query · integer x ≥ 1): Limits the results to no more than this many items per page. Older versions of Command Centre returned 100 items per page in the absence of this parameter. That is acceptable for GUI applications that will only display the first page, but for integrations that intend to proceed through the entire database it causes a lot of chatter. 8.70 and later versions will default to 1000 items per request. This is about where a graph of throughput versus page size begins to level out.
- **name** (in query · string): Limits the returned items to those with a name that matches this string. Without surrounding quotes or a percent sign or underscore, it is a substring match; surround the parameter with double quotes "..." for an exact match. Without quotes, a percent sign % will match any substring and an underscore will match any single character. The search is always case-insensitive. Results are undefined if you do a substring search for the empty string ( name= ). You will receive no items if you search for those with no name ( name="" ), as all items must have a name. Search parameters are ANDed together.
- **division** (in query · string[]): Limits the returned items to those that are in these divisions. That includes all the items in those divisions' child divisions, because Command Centre treats items as though they are also in their division's parent, and its parent, and so on up to the root division. Separate the IDs of the divisions you are interested with commas. Item IDs are short alphanumeric strings, not URLs. Results are undefined if you provide an ID that is not in the form of a division ID. Search parameters are ANDed together.
- **directDivision** (in query · string[]): Restricts items to those whose division is in this list. Unlike division= , it does not follow ancestry. That means it will limit the search to items that are directly assigned to the divisions you list, whereas division (without the direct ) will also include items that are assigned to their descendants. List the IDs of the divisions you are interested in separated by commas. Item IDs are short alphanumeric strings, not URLs. Because this is the first query parameter we added that contains a capital letter, this will be the first time you have had to think about case sensitivity. directDivision works, directdivision does not. Results are undefined if you provide an ID that is not in the form of a division ID. Search parameters are ANDed together. Added in 9.20.
- **description** (in query · string): Limits the returned items to those with a description that matches this string. By default it is a substring match; surround it with double quotes "..." for an exact match. A _ will match any single character, and a % will match any substring. With or without quotes, having either of these wildcards in the string will anchor it at both ends as though you had surrounded it with " . The search is always case-insensitive. Results are undefined if you search for the empty string ( description= or description="" ). Search parameters are ANDed together.
- **fields** (in query · string[] href , name , description , division , serverDisplayName , defaultVisitorType , notes defaults): Specifies the fields in the response. The values you can list are the same in the search and details pages. Using it you can return everything on the search page that you would find on the details page, plus the reception's notes. Separate values with commas. Use the special value defaults to return the fields you would have received had you not given the parameter at all. Add more after a comma. Treat the string matches as case sensitive.
- **pos** (in query · integer): Reserved for internal use. Do not add it to your queries.
- **skip** (in query · integer): Reserved for internal use. Do not add it to your queries.

### Responses

- **200 OK**: Success.

- **403 Forbidden**: The site does not have the RESTCardholders licence.

---

## GET /api/receptions/{id} — Get details of a reception

This returns details for a reception. Follow the href in the summary to get here, rather than building it yourself .

### Parameters

- **id** (in path · string): An internal identifier for the reception.
- **fields** (in query · string[] href , name , description , division , serverDisplayName , defaultVisitorType , notes defaults): Specifies the fields in the response. The values you can list are the same in the search and details pages. Using it you can return everything on the search page that you would find on the details page, plus the reception's notes. Separate values with commas. Use the special value defaults to return the fields you would have received had you not given the parameter at all. Add more after a comma. Treat the string matches as case sensitive.

### Responses

- **200 OK**: Success.

- **403 Forbidden**: The site does not have the RESTCardholders or RESTEvents licence

- **404 Not Found**: The operator does not have a privilege that allows viewing that reception's definition ('View Site', 'Edit Site', 'View Visits', 'Edit Visits', or 'Manage Receptions').

---

## GET /api/cardholders/redactions — Search redactions

This returns all cardholders' redactions. Paginated.

### Parameters

- **cardholder** (in query · string): Limits the results to redactions to cardholders with these IDs. Separate with commas.
- **status** (in query · string pending , inProgress , cancelled , done , failed): Limits the results to redactions with this status. Separate with commas.
- **type** (in query · string normalEvents , cardholder): Limits the results to redactions of this type. Separate with commas.
- **after** (in query · string (date-time)): Limits the results to redactions that were scheduled to occur after this time.
- **before** (in query · string (date-time)): Limits the results to redactions that were scheduled to occur before this time.
- **fields** (in query · string[] before cardholder finishTime href redactionOperator status type when): Specifies the fields you want in the results. Treat the string matches as case sensitive: use lastName rather than lastname .
- **pos** (in query · integer): Reserved for internal use. Do not add it to your queries.

### Responses

- **200 OK**: An array of redactions, and a link to the next page if any.

type object

- **403 Forbidden**: The site does not have the RESTCardholders licence.

---

## POST /api/cardholders/redactions — Schedule a redaction

Schedules a cardholder redaction.

### Request body

[Redaction POST Example](https://gallaghersecurity.github.io/cc-rest-docs/ref/cardholders.html#/definitions/Redaction%20POST%20Example)

### Responses

- **201 Created**: Success.

- **400 Bad Request**: The cardholder does not exist.

- **403 Forbidden**: The operator does not have permissions to redact or to delete a cardholder, or redactions are not enabled on the server, or the server is not licensed for cardholder operations.

### Examples

##### Request Example

```json
{
  "cardholder": {
    "href": "https://localhost:8904/api/cardholders/630"
  },
  "type": "normalEvents",
  "when": "2023-01-01T00:00:00Z",
  "before": "2022-01-01T00:00:00Z"
}
```

---

## DELETE /api/cardholders/redactions — Cancel a redaction

This deletes a redaction record. It must be pending. Take the URL from the results of a redaction search.

Note that the only way to modify a redaction is to delete it and create a new one for the same cardholder. This is due to concerns about a redaction's effect on auditing.

### Responses

- **201 Created**: Success.

- **403 Forbidden**: The operator does not have permissions to redact or to delete a cardholder, or the server is not licensed for cardholder operations.

- **404 Not Found**: No such redaction, or your operator cannot see it.

---

## GET /api/roles — Search

This returns the roles you are privileged to view.

The result will contain no more than 100 or 1000 objects depending on your version; you should follow the next link, if it is present, to collect more, or use the top parameter to get more per page. Roles are small, so a limit of 1000 is sensible.

When you have loaded all the roles there will no next link.

Do not code this URL into your application. Take it from the href in the features.roles.roles section of /api .

### Parameters

- **sort** (in query · string id , name , -id , -name): Changes the sort field between database ID and name. If you prefix id or name with a minus sign (ASCII 45), the sort order is reversed. There are two very strong reasons to sort by ID: Sorting by name carries a risk of missing or duplicating objects if your result set spans multiple pages and another operator is editing the database while your REST client is enumerating them. This is known as "page drift." Sorting by ID does not carry that risk. Following a next link is dramatically quicker when sorting by ID. We strongly recommend sorting by ID. In case you were still in doubt, we will do that by default in a future version of Command Centre. The server silently ignores anything except the options listed here.
- **top** (in query · integer x ≥ 1): Limits the results to no more than this many items per page. Older versions of Command Centre returned 100 items per page in the absence of this parameter. That is acceptable for GUI applications that will only display the first page, but for integrations that intend to proceed through the entire database it causes a lot of chatter. 8.70 and later versions will default to 1000 items per request. This is about where a graph of throughput versus page size begins to level out.
- **name** (in query · string): Limits the returned items to those with a name that matches this string. Without surrounding quotes or a percent sign or underscore, it is a substring match; surround the parameter with double quotes "..." for an exact match. Without quotes, a percent sign % will match any substring and an underscore will match any single character. The search is always case-insensitive. Results are undefined if you do a substring search for the empty string ( name= ). You will receive no items if you search for those with no name ( name="" ), as all items must have a name. Search parameters are ANDed together.
- **division** (in query · string[]): Limits the returned items to those that are in these divisions. That includes all the items in those divisions' child divisions, because Command Centre treats items as though they are also in their division's parent, and its parent, and so on up to the root division. Separate the IDs of the divisions you are interested with commas. Item IDs are short alphanumeric strings, not URLs. Results are undefined if you provide an ID that is not in the form of a division ID. Search parameters are ANDed together.
- **directDivision** (in query · string[]): Restricts items to those whose division is in this list. Unlike division= , it does not follow ancestry. That means it will limit the search to items that are directly assigned to the divisions you list, whereas division (without the direct ) will also include items that are assigned to their descendants. List the IDs of the divisions you are interested in separated by commas. Item IDs are short alphanumeric strings, not URLs. Because this is the first query parameter we added that contains a capital letter, this will be the first time you have had to think about case sensitivity. directDivision works, directdivision does not. Results are undefined if you provide an ID that is not in the form of a division ID. Search parameters are ANDed together. Added in 9.20.
- **description** (in query · string): Limits the returned items to those with a description that matches this string. By default it is a substring match; surround it with double quotes "..." for an exact match. A _ will match any single character, and a % will match any substring. With or without quotes, having either of these wildcards in the string will anchor it at both ends as though you had surrounded it with " . The search is always case-insensitive. Results are undefined if you search for the empty string ( description= or description="" ). Search parameters are ANDed together.
- **pos** (in query · integer): Reserved for internal use. Do not add it to your queries.
- **skip** (in query · integer): Reserved for internal use. Do not add it to your queries.

### Responses

- **200 OK**: Success.

- **403 Forbidden**: The site does not have the RESTCardholders licence.

---

## POST /api/roles — Create [coming]

Creates a new role.

Do not code this URL into your application. Take it from the href field in the features.roles.roles.href field of GET /api .

When successful it returns a location header containing the address of the new role.

Note that you can only create one per POST.

This call requires the RESTConfiguration licence.

### Request body

[Role](https://gallaghersecurity.github.io/cc-rest-docs/ref/cardholders.html#/definitions/Role)

### Responses

- **201 Created**: Success.

- **400 Bad Request**: The body of the POST did not describe a valid role. If you see 'Invalid Role JSON object', the server could not parse the JSON in the body of your POST. Remember to quote all strings, especially those than contain @ symbols.

- **403 Forbidden**: The operator does not have a privilege that allows creating roles, or the server does not have the 'RESTConfiguration' licence.

### Examples

##### Request Example

```json
{
  "name": "Supervisor",
  "description": "aka floor manager",
  "division": {
    "href": "https://localhost:8904/api/divisions/2"
  },
  "enableCompetencyExpiryWarnings": false,
  "enableCardExpiryWarnings": true
}
```

---

## PATCH /api/roles/{id} — Update [coming]

This is the call you use to update a role. Take its URL from a cardholder or a role search rather than building it yourself .

The PATCH expects a document in the same format as the the role detail .

This call requires the RESTConfiguration licence.

### Parameters

- **id** (in path · string): An opaque identifier for the role.

### Request body

[Role](https://gallaghersecurity.github.io/cc-rest-docs/ref/cardholders.html#/definitions/Role)

### Responses

- **200 OK**: Success. Future versions will return feedback from the server about your PATCH.

- **204 No Content**: Success.

- **400 Bad Request**: The body of the PATCH did not describe a valid card type. Card types have many rules, easily enforced in a graphical interface but less so in an API. See the body of the response for help on what went wrong.

- **403 Forbidden**: The operator has a privilege that allows viewing the item but not modifying it, or you tried to set the division to one you cannot configure, or the server is missing the necessary licence. You need the 'Configure Site' privilege on the item you are changing, which means you need on it on the item's current division. You also need it on the new division, if you are changing that. This is also the response when the server does not have the 'RESTConfiguration' licence.

- **404 Not Found**: That is not the URL of a card type or your operator does not have the privilege to view it. This probably means you have built the URL yourself instead of taking it from the results of a GET .

- **409 Conflict**: The item is locked for editing by another operator. The body of the response will tell you which operator is holding the lock.

### Examples

##### Request Example

```json
{
  "name": "Supervisor",
  "description": "aka floor manager",
  "division": {
    "href": "https://localhost:8904/api/divisions/2"
  },
  "enableCompetencyExpiryWarnings": false,
  "enableCardExpiryWarnings": true
}
```

---

## DELETE /api/roles/{id} — Remove [coming]

This call removes a role item from Command Centre. Take its URL from a cardholder or a role search rather than building it yourself .

It requires the RESTConfiguration licence.

### Parameters

- **id** (in path · string): An opaque identifier for the role.

### Responses

- **200 OK**: Success.

- **204 No Content**: Success.

- **400 Bad Request**: Deleting failed. This happens when the role is in use. The usual reason is that cardholders are still connected by the role you are attempting to remove.

- **403 Forbidden**: The operator has the permission to view the item but not delete it, or the server does not have the 'RESTConfiguration' licence.

- **404 Not Found**: That is not the URL of a role, or the operator is not privileged to view it.

---

## GET /api/visits — Search visits

This returns the visits you are privileged to view.

The result will contain no more than 100 or 1000 objects depending on your version; you should follow the next link, if it is present, to collect more, or use the top parameter to get more per page.

When you have loaded all the visits there will no next link.

Do not code this URL into your application. Take it from the href in the features.visits.visits section of /api .

Visits are new to 8.50.

### Parameters

- **sort** (in query · string id , name , -id , -name): Changes the sort field between database ID and name. If you prefix id or name with a minus sign (ASCII 45), the sort order is reversed. There are two very strong reasons to sort by ID: Sorting by name carries a risk of missing or duplicating objects if your result set spans multiple pages and another operator is editing the database while your REST client is enumerating them. This is known as "page drift." Sorting by ID does not carry that risk. Following a next link is dramatically quicker when sorting by ID. We strongly recommend sorting by ID. In case you were still in doubt, we will do that by default in a future version of Command Centre. The server silently ignores anything except the options listed here.
- **top** (in query · integer x ≥ 1): Limits the results to no more than this many items per page. Older versions of Command Centre returned 100 items per page in the absence of this parameter. That is acceptable for GUI applications that will only display the first page, but for integrations that intend to proceed through the entire database it causes a lot of chatter. 8.70 and later versions will default to 1000 items per request. This is about where a graph of throughput versus page size begins to level out.
- **name** (in query · string): Limits the returned items to those with a name that matches this string. Without surrounding quotes or a percent sign or underscore, it is a substring match; surround the parameter with double quotes "..." for an exact match. Without quotes, a percent sign % will match any substring and an underscore will match any single character. The search is always case-insensitive. Results are undefined if you do a substring search for the empty string ( name= ). You will receive no items if you search for those with no name ( name="" ), as all items must have a name. Search parameters are ANDed together.
- **division** (in query · string[]): Limits the returned items to those that are in these divisions. That includes all the items in those divisions' child divisions, because Command Centre treats items as though they are also in their division's parent, and its parent, and so on up to the root division. Separate the IDs of the divisions you are interested with commas. Item IDs are short alphanumeric strings, not URLs. Results are undefined if you provide an ID that is not in the form of a division ID. Search parameters are ANDed together.
- **directDivision** (in query · string[]): Restricts items to those whose division is in this list. Unlike division= , it does not follow ancestry. That means it will limit the search to items that are directly assigned to the divisions you list, whereas division (without the direct ) will also include items that are assigned to their descendants. List the IDs of the divisions you are interested in separated by commas. Item IDs are short alphanumeric strings, not URLs. Because this is the first query parameter we added that contains a capital letter, this will be the first time you have had to think about case sensitivity. directDivision works, directdivision does not. Results are undefined if you provide an ID that is not in the form of a division ID. Search parameters are ANDed together. Added in 9.20.
- **description** (in query · string): Limits the returned items to those with a description that matches this string. By default it is a substring match; surround it with double quotes "..." for an exact match. A _ will match any single character, and a % will match any substring. With or without quotes, having either of these wildcards in the string will anchor it at both ends as though you had surrounded it with " . The search is always case-insensitive. Results are undefined if you search for the empty string ( description= or description="" ). Search parameters are ANDed together.
- **fields** (in query · string[] href , name , description , division , serverDisplayName , notes , reception , visitorType , host , from , until , location , badgeText , visitorAccessGroups , visitors , visitors.cardholder , visitors.href , visitors.invitation , visitors.status , ... defaults): Specifies the fields in the response. The values you can list are the same in the search and details pages. Using it you can return everything on the search page that you would find on the details page, plus the visit's notes. Separate values with commas. Use the special value defaults to return the fields you would have received had you not given the parameter at all. Add more after a comma. Treat the string matches as case sensitive.
- **pos** (in query · integer): Reserved for internal use. Do not add it to your queries.
- **skip** (in query · integer): Reserved for internal use. Do not add it to your queries.

### Responses

- **200 OK**: Success.

- **403 Forbidden**: The site does not have both the RESTCardholders and VisitorManagement licences.

---

## POST /api/visits — Create a visit

This creates a new visit.

In the interest of forward compatibility , do not code its URL into your application. Take it from the href in the features.visits.visits section of /api .

This POST expects a JSON body in the same format as a visit detail .

These fields are required:

- name
- reception
- visitor type (access group)
- host cardholder
- start time ( from )
- end time ( until )

Do not code this URL into your application. Take it from the href in the features.visits.visits section of /api .

Like all the visit endpoints, this one is new to 8.50.

### Request body

[Visit POST example](https://gallaghersecurity.github.io/cc-rest-docs/ref/cardholders.html#/definitions/Visit%20POST%20example)

### Responses

- **201 Created**: Success.

- **400 Bad Request**: The body of the POST did not describe a valid visit. Check the body of the server's response for hints.

- **403 Forbidden**: The site does not have both the RESTCardholders and VisitorManagement licences.

### Examples

##### Request Example

```json
{
  "name": "Visit hosted by Greta Ginger",
  "description": "Initial scoping",
  "reception": {
    "href": "https://localhost:8904/api/receptions/937"
  },
  "visitorType": {
    "href": "https://localhost:8904/api/access_groups/925"
  },
  "host": {
    "href": "https://localhost:8904/api/cardholders/526"
  },
  "from": "1971-03-08T14:35:00Z",
  "until": "2023-03-08T14:35:00Z",
  "location": "Gather in Ginger's office",
  "visitorAccessGroups": [
    {
      "href": "https://localhost:8904/api/access_groups/926"
    },
    {
      "href": "https://localhost:8904/api/access_groups/9260"
    }
  ],
  "visitors": [
    {
      "href": "https://localhost:8904/api/cardholders/940"
    },
    {
      "href": "https://localhost:8904/api/cardholders/9040"
    }
  ]
}
```

---

## GET /api/visits/{id} — Get details of a visit

This returns details for a visit. Follow the href in the summary to get here rather than building the URL yourself .

All visit endpoints are new to 8.50.

### Parameters

- **id** (in path · string): An internal identifier for the visit.
- **fields** (in query · string[] href , name , description , division , serverDisplayName , notes , reception , visitorType , host , from , until , location , badgeText , visitorAccessGroups , visitors , visitors.cardholder , visitors.href , visitors.invitation , visitors.status , ... defaults): Specifies the fields in the response. The values you can list are the same in the search and details pages. Using it you can return everything on the search page that you would find on the details page, plus the visit's notes. Separate values with commas. Use the special value defaults to return the fields you would have received had you not given the parameter at all. Add more after a comma. Treat the string matches as case sensitive.

### Responses

- **200 OK**: Success.

- **403 Forbidden**: The site does not have both the RESTCardholders and VisitorManagement licences.

- **404 Not Found**: That is not the URL of a visit, or it is a visit but the operator does not have the privilege to see ('View Visits' or 'Edit Visits').

---

## PATCH /api/visits/{id} — Modify a visit

Changes an existing visit.

Follow the href in a visit search to get here rather than building the URL yourself .

New to 8.50.

### Parameters

- **id** (in path · string): An opaque identifier for the visit.

### Request body

[Visit PATCH example](https://gallaghersecurity.github.io/cc-rest-docs/ref/cardholders.html#/definitions/Visit%20PATCH%20example)

### Responses

- **200 OK**: Success. Future versions will return feedback from the server about your PATCH.

- **204 No Content**: Success.

- **400 Bad Request**: The body of the PATCH did not describe a valid change to a visit. Check the body of the server's response for hints. If you receive 'No fields have been defined for update', check that your submission body is valid JSON.

- **403 Forbidden**: The site does not have the RESTCardholders and VisitorManagement licences, or the operator does not have the privilege to modify that visit ('Edit Visits'), or you attempted to set a field for which you have no privilege, or the visit has expired. See the descriptions for each field for some rules about their use.

- **404 Not Found**: That is not the URL of a visit, or it is a visit that the operator does not have the privilege to see ('View Visits' or 'Edit Visits').

### Examples

##### Request Example

```json
{
  "name": "Visit hosted by Greta Ginger",
  "description": "Initial scoping",
  "reception": {
    "href": "https://localhost:8904/api/receptions/937"
  },
  "visitorType": {
    "accessgroup": {
      "href": "https://localhost:8904/api/access_groups/925"
    }
  },
  "host": {
    "href": "https://localhost:8904/api/cardholders/526"
  },
  "from": "1971-03-08T14:35:00Z",
  "until": "2023-03-08T14:35:00Z",
  "location": "Gather in Ginger's office",
  "visitorAccessGroups": {
    "add": [
      {
        "href": "https://localhost:8904/api/access_groups/926"
      },
      {
        "href": "https://localhost:8904/api/access_groups/9260"
      }
    ],
    "remove": [
      {
        "href": "https://localhost:8904/api/access_groups/930"
      },
      {
        "href": "https://localhost:8904/api/visits/941/visitor_access_groups/930"
      }
    ]
  },
  "visitors": {
    "add": [
      {
        "href": "https://localhost:8904/api/cardholders/940"
      },
      {
        "href": "https://localhost:8904/api/cardholders/9040"
      }
    ],
    "remove": [
      {
        "href": "https://localhost:8904/api/cardholders/937"
      },
      {
        "href": "https://localhost:8904/api/visits/941/visitors/937"
      }
    ]
  }
}
```

---

## PATCH /api/visits/{id}/visitors/{visitorId} — Modify a visitor's status

Changes a visitor's state for a visit. This is how you mark a visitor as signing in, signed in, on site, off site again, etc.

Doing so will create an event if your visitor was not already in the state you attempted to move them to.

It will not send a notification to the visitor's host or escort. There are other API calls for notifying that a visitor is signing in or on-site .

Note that this URL is different from a normal cardholder href because a cardholder can be a visitor on more than one visit at a time.

Do not code this URL into your application. Take itf from a visit search or visit .

New to 8.90.

### Parameters

- **id** (in path · string): An opaque identifier for the visit.
- **visitorId** (in path · string): An opaque identifier for the visitor.

### Request body

[Visitor PATCH example](https://gallaghersecurity.github.io/cc-rest-docs/ref/cardholders.html#/definitions/Visitor%20PATCH%20example)

### Responses

- **200 OK**: Success. Future versions will return feedback from the server about your PATCH.

- **204 No Content**: Success.

- **400 Bad Request**: The body of the PATCH did not describe a valid change to a visitor's status. Check the body of the server's response for hints.

- **403 Forbidden**: The site does not have the RESTCardholders and VisitorManagement licences, or the operator does not have the privilege to modify that visit ('Edit Visits'), or you attempted to set a field for which you have no privilege, or the visit has expired.

- **404 Not Found**: That is not the URL of a visitor, or it is a visitor on a visit that the operator does not have the privilege to see ('View Visits' or 'Edit Visits').

### Examples

##### Request Example

```json
{
  "status": {
    "value": "signingIn"
  }
}
```

---

## POST /api/visits/{id}/visitors/{visitorId}/notify_signing_in — Notify that a visitor is signing in

Sends an email and / or SMS notification to a visit's host.

The difference between this and notify_onsite is the type of event that Command Centre puts in the audit trail. In all other ways they are identical.

New to 8.90.

### Parameters

- **id** (in path · string): An opaque identifier for the visit.
- **visitorId** (in path · string): An opaque identifier for the visitor.

### Request body

[Visitor notification POST](https://gallaghersecurity.github.io/cc-rest-docs/ref/cardholders.html#/definitions/Visitor%20notification%20POST)

### Responses

- **200 OK**: Success. Future versions will return feedback from the server about your PATCH.

- **204 No Content**: Success.

- **400 Bad Request**: The body of the PATCH did not describe a notification. Check the body of the server's response for hints.

- **403 Forbidden**: The site does not have the RESTCardholders and VisitorManagement licences, or the operator does not have sufficient privileges, or the visit has expired.

- **404 Not Found**: That is not a visitor's notification URL, or it is a notification URL for a visitor on a visit that the operator does not have the privilege to see ('View Visits' or 'Edit Visits').

### Examples

##### Request Example

```json
{
  "emailSubject": "Visitor arriving",
  "emailMessage": "",
  "smsMessage": "Visitor arriving"
}
```

---

## POST /api/visits/{id}/visitors/{visitorId}/notify_onsite — Notify that a visitor is on-site

Sends an email and / or SMS notification to a visit's host.

The difference between this and notify_signing_in is the type of event that Command Centre puts in the audit trail. In all other ways they are identical.

New to 8.90.

### Parameters

- **id** (in path · string): An opaque identifier for the visit.
- **visitorId** (in path · string): An opaque identifier for the visitor.

### Request body

[Visitor notification POST](https://gallaghersecurity.github.io/cc-rest-docs/ref/cardholders.html#/definitions/Visitor%20notification%20POST)

### Responses

- **200 OK**: Success. Future versions will return feedback from the server about your PATCH.

- **204 No Content**: Success.

- **400 Bad Request**: The body of the PATCH did not describe a notification. Check the body of the server's response for hints.

- **403 Forbidden**: The site does not have the RESTCardholders and VisitorManagement licences, or the operator does not have sufficient privileges, or the visit has expired.

- **404 Not Found**: That is not a visitor's notification URL, or it is a notification URL for a visitor on a visit that the operator does not have the privilege to see ('View Visits' or 'Edit Visits').

### Examples

##### Request Example

```json
{
  "emailSubject": "Visitor arriving",
  "emailMessage": "",
  "smsMessage": "Visitor arriving"
}
```

---

# Schema Definitions (54)

## Cardholder search

## Cardholder search:

An array of cardholder summaries, and a next link for more.

results:[Cardholder summary](https://gallaghersecurity.github.io/cc-rest-docs/ref/cardholders.html#/definitions/Cardholder%20summary)

An array of cardholder summaries.

[Cardholder summary](https://gallaghersecurity.github.io/cc-rest-docs/ref/cardholders.html#/definitions/Cardholder%20summary)next:object

The link to the next page. Absent if you have retrieved them all.

href:string(url)

##### Example

```json
{
  "results": [
    {
      "href": "https://localhost:8904/api/cardholders/325",
      "id": "325",
      "firstName": "Algernon",
      "lastName": "Boothroyd",
      "shortName": "Q",
      "description": "Quartermaster",
      "authorised": true
    }
  ],
  "next": {
    "href": "https://localhost:8904/api/cardholders?skip=61320"
  }
}
```

---

## Cardholder summary

## Cardholder summary:

The cardholder search at /api/cardholders returns an array of these. It is a subset of what you get from a cardholder's detail page at /api/cardholders/{id} (linked as the href in this object), to be more suitable for large result sets.

href:string(url)

A link to a cardholder detail object for this cardholder. This is Command Centre's identifier for this cardholder: use it whenever you need to specify a cardholder in REST operations.

id:string

An alphanumeric identifier, unique to the server. No API calls use cardholder IDs.

Deprecated. Use the href instead.

firstName:stringlastName:stringshortName:string (up to 16 chars)

If you supply a string that is too long when creating or updating a cardholder, Command Centre will truncate it.

description:stringauthorised:boolean

This is called 'Cardholder Authorised' in the administrative clients. If false, Command Centre will deny card access decisions for this cardholder.

'authorised' is false by default. If you want a new cardholder to open doors, be sure to set it to true in your POST.

You need the 'Edit cardholders' privilege to set this true. In versions prior to 8.80 your operator also needed that privilege to set it false, but in 8.80 and later 'De-authorise cardholder' is enough on its own.

##### Example

```json
{
  "href": "https://localhost:8904/api/cardholders/325",
  "id": "325",
  "firstName": "Algernon",
  "lastName": "Boothroyd",
  "shortName": "Q",
  "description": "Quartermaster",
  "authorised": true
}
```

---

## Cardholder detail

## Cardholder detail:

/api/cardholders/{id} returns one of these, and you submit parts of one in a POST to create a cardholder.

You may find it contains a block called updates . This is reserved for future development and its behaviour will change in later versions of Command Centre.

[Cardholder summary](https://gallaghersecurity.github.io/cc-rest-docs/ref/cardholders.html#/definitions/Cardholder%20summary)
lastSuccessfulAccessTime:string(date-time)

The date and time of the last successful card event or operator-initiated movement (using a tag board or this API ).

lastSuccessfulAccessZone:object

The last zone the cardholder entered.

If their last movement was to the 'outside' (through a door that has no access zone configured for that direction of travel) this field will be missing but lastSuccessfulAccessTime will be present.

If the cardholder is a visitor and their last movement was to leave the site, this will be their visit's reception, not an access zone. 8.50 and later will present only the name of the reception and withhold the href in that case.

Change is coming.

A future version of Command Centre will add the href field even if the item is a reception. So you can tell the difference, it will add a field called canonicalTypeName which will have one of two values: accesszone or reception .

serverDisplayName:string

If you are running a multi-server installation and this item is homed on a remote server, this field will contain the name of that server. This field is missing from items that are held on the machine that served the API request. Added in 8.40.

This is a read-only field. The server will ignore it if you send it.

division:object

The division containing this cardholder. You must send this when creating a cardholder.

@Student ID:object

An example Personal Data Field value.

All PDFs except images appear at the top level of a cardholder in a field named after the PDF with a leading '@'.

Date PDFs will contain a date-time with the time part set to midnight.

Image PDFs appear as a block containing an href. GETting that href will return the image data with an appropriate content-type header.

Put your PDF values here when creating or modifying a cardholder. Note that to hold a PDF value the cardholder must be a member of an access group that has that PDF attached to it. In this example, being a member of the special projects group has granted our Mr. Boothroyd the student ID and photo PDFs.

To set an image PDF, Base64-encode the image and send it as a string. There is one in the cardholder POST example .

Date PDFs are only accurate to a day, so version 8.75 and later will ignore the time and time zone components if you send them. 8.70 and earlier will first perform a timezone correction then convert it to UTC before truncating the time, which might shift the date back a day if you specified a positive timezone offset. To be sure that does not happen on a server older than 8.75, put T00:00:00Z on the end of your date.

disableCipherPad:boolean

True if this cardholder should not have the numbers on an alarms terminal scrambled when the terminal is configured as a cipher pad. Usually the reason is a vision impairment, making a randomised keypad impracticable.

Deprecated from 9.50 in favour of the disableCipherPad element in the accessibilities array. This field can still be set for backwards compatibility, but only set one of the two: setting them to disagreeing values will earn you a 400.

usercode:string

This field is write-only, so you will not see it in the results of a GET. It appears here because you can send it in the body of a POST or PATCH.

Set it to the empty string "" to clear the cardholder's user code.

If it is not empty it must be a string of at least four digits. It may need to be longer, because the minimum length is set by your site configuration.

You will find that "0000" does not work.

operatorLoginEnabled:boolean

True if this operator can log in to the interactive Command Centre clients. Note that a cardholder also needs a privilege to use the Configuration Client.

operatorUsername:string

The name that the operator uses when logging in to the interactive clients. Absent if blank.

Send the empty string "" to clear it.

operatorPassword:string

The password this user must supply when logging in to the interactive clients, if they are not using Windows integrated authentication.

Command Centre imposes password length and complexity restrictions. If your password does not meet those requirements, the entire update will fail. The body of the 4xx response will tell you why.

Like the user code, this is write-only so you will not see it in the results of a GET.

operatorPasswordExpired:boolean

If true, the interactive clients will request a new password the next time this person logs in.

windowsLoginEnabled:boolean

If true, and they have a Windows username, this user can log in using Windows integrated authentication.

windowsUsername:string

This user's Windows username. For an example of the format that will work in your organisation, look in the 'Windows logon' field of the 'Operator configuration' tab of a cardholder's properties in the Configuration Client.

Send the empty string "" to clear it.

oidcLoginEnabled:boolean

If true, and they have an oidcUserId , the operator can log in to the Command Centre Client using their Entra ID username.

Added in 9.40.

oidcUserId:string

This user's Entra ID username. This should be the operator's Entra ID 'User Principal Name' found in the overview of the user's details in Entra ID Admin Center.

Send the empty string "" to clear it.

Added in 9.40.

personalDataDefinitions:[Cardholder PDF](https://gallaghersecurity.github.io/cc-rest-docs/ref/cardholders.html#/definitions/Cardholder%20PDF)

All the personal data definitions and values for this cardholder. It is an array of objects, each with one key/value pair. The key is the name of the PDF preceded by an '@'; the value is an object containing the definition of the PDF (common for all cardholders) and the value for this cardholder. The value block of an image PDF will contain an href to the image.

Your visibility of a cardholder's PDF value depends entirely on the PDF's access settings in your operator groups, or if it has none of those, its default privilege. The PDF's division is irrelevant. So if you were expecting a PDF value here when none arrived, ensure the cardholder has a value for that PDF (because the server does not send nulls) then check the PDF's access settings in all of your operator groups, then the default visibility on the PDF item itself.

When you send this array in a POST or PATCH, Command Centre will take the notifications flag from here. It can take value from here too, but if you also send a field in the root of the cardholder with the name of the PDF preceded by an '@', that one wins.

To request this block using the 'fields' parameter, use personalDataFields (note 'fields' not 'definitions'). Doing so will not only give you the personalDataDefinitions block, but also the values at the root level (with their names preceded by '@'-signs).

[Cardholder PDF](https://gallaghersecurity.github.io/cc-rest-docs/ref/cardholders.html#/definitions/Cardholder%20PDF)cards:[Cardholder card](https://gallaghersecurity.github.io/cc-rest-docs/ref/cardholders.html#/definitions/Cardholder%20card)

All the cards for this cardholder. Note that even though Command Centre calls these things 'cards', they include other types that do not require a physical card, such as digital IDs and mobile credentials.

[Cardholder card](https://gallaghersecurity.github.io/cc-rest-docs/ref/cardholders.html#/definitions/Cardholder%20card)accessGroups:[Cardholder access group](https://gallaghersecurity.github.io/cc-rest-docs/ref/cardholders.html#/definitions/Cardholder%20access%20group)

All the cardholder's access group memberships. This does not list memberships the cardholder inherits through other groups.

The server property 'Show all cardholder access group memberships' does not affect the API as it affects the configuration and operational clients, so this field only appears if the operator has permission to see access group memberships. That is granted by a privilege such as 'View cardholders'.

[Cardholder access group](https://gallaghersecurity.github.io/cc-rest-docs/ref/cardholders.html#/definitions/Cardholder%20access%20group)operatorGroups:[Cardholder operator group](https://gallaghersecurity.github.io/cc-rest-docs/ref/cardholders.html#/definitions/Cardholder%20operator%20group)

All the cardholder's operator group memberships. Added in 8.50.

[Cardholder operator group](https://gallaghersecurity.github.io/cc-rest-docs/ref/cardholders.html#/definitions/Cardholder%20operator%20group)competencies:[Cardholder competency](https://gallaghersecurity.github.io/cc-rest-docs/ref/cardholders.html#/definitions/Cardholder%20competency)

Every competency a cardholder possesses. There is quite a lot there: see the schema definition for detailed explanation.

[Cardholder competency](https://gallaghersecurity.github.io/cc-rest-docs/ref/cardholders.html#/definitions/Cardholder%20competency)edit:object(url)

Reserved for internal use. It is a link to another call that helps interactive administrative clients. Do not send it when creating or editing a cardholder.

updateLocation:object

Link to POST to when you want to update the location of this cardholder. Added in 8.20.

notes:string

Free-form text.

The 'Edit Cardholder Notes' privilege lets you change cardholder notes however you like, but 'Add Cardholder Notes' only lets you add more to the end. To do that, read the existing notes field, append your text, and send it back in a PATCH. If you try to change the existing text the server will respond with a 4xx.

You will need one of those two privileges to edit notes. 'Create Cardholders' and 'Edit Cardholders' let you set most cardholder fields, but not notes.

Because of the potential size of the notes field, the server does not return it unless you ask for it with fields=notes .

notifications:object

This block shows the cardholder's notification settings. It deserves some explanation.

The enabled bool is correct at the time of your call. If false, CC will not be sending your cardholder any notifications.

The from and until date-times show if and when enabled will change. When the from date-time passes, enabled will flip, the from date-time will take the until value, and until will become null. This continues until from is null.

You can set from with a null until , but not until with a null from . They must both be in the future or null, and from must be before until .

If you want to simply turn a person's notifications on or off without any changes scheduled for the future, set enabled true or false and both from and until null.

relationships:[Cardholder relationship](https://gallaghersecurity.github.io/cc-rest-docs/ref/cardholders.html#/definitions/Cardholder%20relationship)

This is a list of the roles that other cardholders perform for this cardholder.

Since only one cardholder can perform a given role for another, there will be no more elements in this array than there are roles on the site.

The example shows that this cardholder has one supervisor.

[Cardholder relationship](https://gallaghersecurity.github.io/cc-rest-docs/ref/cardholders.html#/definitions/Cardholder%20relationship)lockers:[Cardholder locker](https://gallaghersecurity.github.io/cc-rest-docs/ref/cardholders.html#/definitions/Cardholder%20locker)

All the cardholder's locker assignments.

[Cardholder locker](https://gallaghersecurity.github.io/cc-rest-docs/ref/cardholders.html#/definitions/Cardholder%20locker)elevatorGroups:[Cardholder elevator group](https://gallaghersecurity.github.io/cc-rest-docs/ref/cardholders.html#/definitions/Cardholder%20elevator%20group)

The cardholder's elevator group properties. They may have one per elevator group.

Added in 8.50.

[Cardholder elevator group](https://gallaghersecurity.github.io/cc-rest-docs/ref/cardholders.html#/definitions/Cardholder%20elevator%20group)updates:object

Cardholders include an updates block, as other items do, that allows monitoring one cardholder per TCP session in a long poll. However the Cardholder Changes methods are a far more efficient way of monitoring your cardholders.

redactions:[Cardholder redaction](https://gallaghersecurity.github.io/cc-rest-docs/ref/cardholders.html#/definitions/Cardholder%20redaction)

All the cardholder's scheduled redactions.

Because this is relatively expensive to compute, this field does not appear by default. You must ask for it with fields=redactions or fields=redactions.href,redactions.type,redactions.when , for example.

Added in 8.80.

[Cardholder redaction](https://gallaghersecurity.github.io/cc-rest-docs/ref/cardholders.html#/definitions/Cardholder%20redaction)accessibilities:object[]

The cardholder's accessibility settings in an array of three elements.

Added in 9.50.

objecttype:stringdisableCipherPad,                                       useExtendedAccessTime,                                       pinException

- disableCipherPad means this cardholder will not have the numbers on an alarms terminal scrambled when the terminal is configured as a cipher pad. Usually the reason is a vision impairment, making a randomised keypad impracticable.
- useExtendedAccessTime increases the time a door remains unlocked and the amount of time that can pass before raising a 'door open too long' alarm.
- pinExemption bypasses PIN entry. Users with this accessibility enabled who badge a card at a reader that normally requires PIN entry will not be prompted to enter a PIN.
enabled:boolean

If false, the accessibility is disabled. If true, it depends on from and until .

from:string(date-time)

If set and in the future, the accessibility will not be active until then. If not set or in the past, the accessibility will depend on enabled .

until:string(date-time)

If set and in the past, the accessibility will have been inactive since then. If not set or in the future, the accessibility will depend on enabled.

If you send it, it must be later than both the from date, if sent, and the current time.

##### Example

```json
{
  "href": "https://localhost:8904/api/cardholders/325",
  "id": "325",
  "firstName": "Algernon",
  "lastName": "Boothroyd",
  "shortName": "Q",
  "description": "Quartermaster",
  "authorised": true,
  "lastSuccessfulAccessTime": "2004-11-18T19:21:52Z",
  "lastSuccessfulAccessZone": {
    "href": "https://localhost:8904/api/access_zones/333",
    "name": "Twilight zone"
  },
  "serverDisplayName": "ruatoria.satellite.int",
  "division": {
    "href": "https://localhost:8904/api/divisions/2"
  },
  "@Student ID": "8904640",
  "disableCipherPad": false,
  "usercode": "numeric, and write-only",
  "operatorLoginEnabled": true,
  "operatorUsername": "qmaster",
  "operatorPassword": "write-only",
  "operatorPasswordExpired": false,
  "windowsLoginEnabled": true,
  "windowsUsername": "misix.local\\qmaster",
  "oidcLoginEnabled": true,
  "oidcUserId": "admin_qmaster@misix.gov.uk",
  "personalDataDefinitions": [
    {
      "@Student ID": {
        "href": "https://localhost:8904/api/cardholders/325/personal_data/2356",
        "definition": {
          "href": "https://localhost:8904/api/personal_data_fields/2356",
          "name": "Student ID",
          "id": "2356",
          "type": "string"
        },
        "value": "8904640"
      }
    },
    {
      "@Photo": {
        "href": "https://localhost:8904/api/cardholders/325/personal_data/2369",
        "definition": {
          "href": "https://localhost:8904/api/personal_data_fields/2369",
          "name": "Photo",
          "id": "2369",
          "type": "image"
        },
        "value": {
          "href": "https://localhost:8904/api/cardholders/325/personal_data/2369"
        }
      }
    }
  ],
  "cards": [
    {
      "href": "https://localhost:8904/api/cardholders/325/cards/97b6a24ard6d4500a9",
      "number": "1",
      "cardSerialNumber": "045A5769713E80",
      "issueLevel": 1,
      "status": {
        "value": "Disabled (manually)",
        "type": "inactive"
      },
      "type": {
        "href": "https://localhost:8904/api/card_types/354",
        "name": "Card type no. 1"
      },
      "invitation": {
        "email": "nick@example.com",
        "mobile": "02123456789",
        "singleFactorOnly": "boolean",
        "status": "sent",
        "href": "https://security.gallagher.cloud/api/invitations/abcd1234defg5678"
      },
      "from": "2017-01-01T00:00:00Z",
      "until": "2017-12-31T11:59:59Z",
      "credentialClass": "mobile",
      "trace": false,
      "lastPrintedOrEncodedTime": "2020-08-10T09:20:50Z",
      "lastPrintedOrEncodedIssueLevel": 1,
      "lastUsedTime": "2025-09-24T14:00:00.000Z",
      "pin": "153624",
      "visitorContractor": false,
      "ownedBySite": false,
      "credentialId": "reserved",
      "bleFacilityId": "reserved"
    }
  ],
  "accessGroups": [
    {
      "href": "https://localhost:8904/api/cardholders/325/access_groups/D714D8A89F",
      "accessGroup": {
        "name": "R&D special projects group",
        "href": "https://localhost:8904/api/access_groups/352"
      },
      "status": {
        "value": "Pending",
        "type": "pending"
      },
      "from": "2017-01-01T00:00:00Z",
      "until": "2017-12-31T11:59:59Z"
    }
  ],
  "operatorGroups": [
    {
      "href": "https://localhost:8904/api/cardholders/325/operator_groups/EBDRSD",
      "operatorGroup": {
        "name": "Locker admins",
        "href": "https://localhost:8904/api/operator_groups/532"
      }
    }
  ],
  "competencies": [
    {
      "href": "https://localhost:8904/api/cardholders/325/competencies/2dc3p0",
      "competency": {
        "href": "https://localhost:8904/api/competencies/2354",
        "name": "Hazardous goods handling"
      },
      "status": {
        "value": "Pending",
        "type": "pending"
      },
      "expiryWarning": "2017-03-06T15:45:00Z",
      "expiry": "2017-03-09T15:45:00Z",
      "enablement": "2018-03-09T15:45:00Z",
      "comment": "CPR refresher due March.",
      "limitedCredit": true,
      "credit": 37
    }
  ],
  "edit": {
    "href": "https://localhost:8904/cardholders/325/edit"
  },
  "updateLocation": {
    "href": "https://localhost:8904/api/cardholders/402/update_location"
  },
  "notes": "",
  "notifications": {
    "enabled": true,
    "from": "2017-10-10T14:59:00Z",
    "until": "2017-10-17T14:59:00Z"
  },
  "relationships": [
    {
      "href": "https://localhost:8904/api/cardholders/325/relationships/179lah1170",
      "role": {
        "href": "https://localhost:8904/api/roles/5396",
        "name": "Supervisor"
      },
      "cardholder": {
        "href": "https://localhost:8904/api/cardholders/5398",
        "name": "Miles Messervy",
        "firstName": "Miles",
        "lastName": "Messervy"
      }
    }
  ],
  "lockers": [
    {
      "href": "https://localhost:8904/api/cardholders/325/lockers/t1m4",
      "locker": {
        "name": "Bank A locker 1",
        "shortName": "A1",
        "lockerBank": {
          "href": "https://localhost:8904/api/locker_banks/4567",
          "name": "Bank A"
        },
        "href": "https://localhost:8904/api/lockers/3456"
      },
      "from": "2017-01-01T00:00:00Z",
      "until": "2018-12-31T00:00:00Z"
    }
  ],
  "elevatorGroups": [
    {
      "href": "https://localhost:8904/api/cardholders/325/elevator_groups/567",
      "elevatorGroup": {
        "href": "https://localhost:8904/api/elevator_groups/635",
        "name": "Main building lower floors"
      },
      "accessZone": {
        "href": "https://localhost:8904/api/access_zones/637",
        "name": "Lvl 1 lift lobby"
      },
      "enableCaptureFeatures": true,
      "enableCodeBlueFeatures": false,
      "enableExpressFeatures": true,
      "enableServiceFeatures": false,
      "enableService2Features": true,
      "enableService3Features": true,
      "enableVipFeatures": false
    }
  ],
  "updates": {
    "reserved": "for future use"
  },
  "redactions": [
    {
      "href": "https://localhost:8904/api/cardholders/redactions/625",
      "type": "normalEvents",
      "when": "2023-01-01T00:00:00Z",
      "before": "2022-01-01T00:00:00Z",
      "status": "pending",
      "redactionOperator": {
        "name": "REST Operator",
        "href": "https://localhost:8904/api/items/100"
      }
    }
  ],
  "accessibilities": [
    {
      "type": "useExtendedAccessTime",
      "enabled": true,
      "from": "2026-08-15T21:00:00Z"
    },
    {
      "type": "disableCipherPad",
      "enabled": false
    },
    {
      "type": "pinExemption",
      "enabled": false
    }
  ]
}
```

---

## Cardholder POST example

## Cardholder POST example:

This is an example of a POST you could use to create a cardholder in a specific division and access group, with an access card, another cardholder as a supervisor, a competency, a student ID and a photo held in personal data fields, and two lockers.

There are plenty more fields than shown in this example. For a complete list please see the schema for the detailed cardholder object that you receive from a cardholder href.

firstName:string

You must supply either this or the last name when creating a cardholder.

The maximum length of a cardholder's first name in version 9.50 is 50 characters.

lastName:string

You must supply either this or the first name when creating a cardholder.

The maximum length of a cardholder's last name in version 9.50 is 50 characters.

shortName:stringdescription:stringauthorised:boolean

Remember to set this true, as shown here in the POST, or later in a PATCH. Otherwise your new cardholder will never get through a door.

division:object

Mandatory when creating any cardholder. In this example, we want all students in division 5387.

@email:string

An example PDF value. In this example, access group 352 must include a PDF called 'email' otherwise this will appear to have no effect.

@headshot:string

An example image PDF encoded to Base64. Access group 352 must include this PDF as well. As a quick visual check on your encoding, JPEGs start with /9j/ .

personalDataDefinitions:[Cardholder PDF](https://gallaghersecurity.github.io/cc-rest-docs/ref/cardholders.html#/definitions/Cardholder%20PDF)

This is how you set and unset the notifications flags on PDFs. If you do not do it here when you first give a cardholder a PDF, it will come from the PDF's definition.

[Cardholder PDF](https://gallaghersecurity.github.io/cc-rest-docs/ref/cardholders.html#/definitions/Cardholder%20PDF)cards:[Cardholder card](https://gallaghersecurity.github.io/cc-rest-docs/ref/cardholders.html#/definitions/Cardholder%20card)

This example creates one physical card of type 600 with a PIN and a system-generated card number, and another of type 654 which--judging by the invitation block--must be a mobile credential. Command Centre will send an invitation to Nick at those coordinates.

Card PINs were added to the API in 8.90.

[Cardholder card](https://gallaghersecurity.github.io/cc-rest-docs/ref/cardholders.html#/definitions/Cardholder%20card)accessGroups:[Cardholder access group](https://gallaghersecurity.github.io/cc-rest-docs/ref/cardholders.html#/definitions/Cardholder%20access%20group)

Here you can add the access groups necessary to give your new cardholder the PDFs and access he or she needs. In this example we set the activation date of the access group membership to the first of January 2019.

[Cardholder access group](https://gallaghersecurity.github.io/cc-rest-docs/ref/cardholders.html#/definitions/Cardholder%20access%20group)operatorGroups:[Cardholder operator group](https://gallaghersecurity.github.io/cc-rest-docs/ref/cardholders.html#/definitions/Cardholder%20operator%20group)

Here you can add the operator groups necessary to give your new cardholder the software access he or she needs. Added in 8.50.

[Cardholder operator group](https://gallaghersecurity.github.io/cc-rest-docs/ref/cardholders.html#/definitions/Cardholder%20operator%20group)competencies:[Cardholder competency](https://gallaghersecurity.github.io/cc-rest-docs/ref/cardholders.html#/definitions/Cardholder%20competency)

In this example we are giving our new cardholder a disabled competency, set to enable in January 2019.

[Cardholder competency](https://gallaghersecurity.github.io/cc-rest-docs/ref/cardholders.html#/definitions/Cardholder%20competency)notes:stringnotifications:object

You can set or update any of the three fields in this block.

relationships:[Cardholder relationship](https://gallaghersecurity.github.io/cc-rest-docs/ref/cardholders.html#/definitions/Cardholder%20relationship)

Here you would set the cardholders who will perform roles for this cardholder.

The example shows that this cardholder will have cardholder 5398 performing role 5396.

Remember that you can supply an array of these objects if your cardholder is having more than one role filled.

[Cardholder relationship](https://gallaghersecurity.github.io/cc-rest-docs/ref/cardholders.html#/definitions/Cardholder%20relationship)lockers:[Cardholder locker](https://gallaghersecurity.github.io/cc-rest-docs/ref/cardholders.html#/definitions/Cardholder%20locker)

Here you set all the cardholder's locker assignments. Ensure you are only attempting to give your cardholder a locker according to site policy (one locker per locker bank, for example), otherwise the POST will fail.

The example shows our cardholder receiving two lockers.

[Cardholder locker](https://gallaghersecurity.github.io/cc-rest-docs/ref/cardholders.html#/definitions/Cardholder%20locker)elevatorGroups:[Cardholder elevator group](https://gallaghersecurity.github.io/cc-rest-docs/ref/cardholders.html#/definitions/Cardholder%20elevator%20group)

Here you set all the new cardholder's default elevator floors and passenger types. Passenger type properties are false by default.

The example shows our cardholder receiving a default floor for the first elevator group, with the Code Blue feature enabled in the second group.

Added in 8.50.

[Cardholder elevator group](https://gallaghersecurity.github.io/cc-rest-docs/ref/cardholders.html#/definitions/Cardholder%20elevator%20group)

##### Example

```json
{
  "firstName": "Algernon",
  "lastName": "Boothroyd",
  "shortName": "Q",
  "description": "Quartermaster",
  "authorised": true,
  "division": {
    "href": "https://localhost:8904/api/divisions/5387"
  },
  "@email": "user@sample.com",
  "@headshot": "/9j/4A...==",
  "personalDataDefinitions": [
    {
      "@email": {
        "notifications": true
      }
    }
  ],
  "cards": [
    {
      "type": {
        "href": "https://localhost:8904/api/card_types/600"
      },
      "pin": "153624"
    },
    {
      "type": {
        "href": "https://localhost:8904/api/card_types/654"
      },
      "number": "Nick's mobile",
      "invitation": {
        "email": "nick@example.com",
        "mobile": "02123456789"
      }
    }
  ],
  "accessGroups": [
    {
      "accessGroup": {
        "href": "https://localhost:8904/api/access_groups/352"
      },
      "from": "2019-01-01"
    }
  ],
  "operatorGroups": [
    {
      "operatorgroup": {
        "href": "https://localhost:8904/api/operator_groups/523"
      }
    }
  ],
  "competencies": [
    {
      "competency": {
        "href": "https://localhost:8904/api/competencies/2354"
      },
      "enabled": false,
      "enablement": "2019-01-01"
    }
  ],
  "notes": "",
  "notifications": {
    "enabled": true,
    "from": "2017-10-10T14:59:00Z",
    "until": "2017-10-17T14:59:00Z"
  },
  "relationships": [
    {
      "role": {
        "href": "https://localhost:8904/api/roles/5396"
      },
      "cardholder": {
        "href": "https://localhost:8904/api/cardholders/5398"
      }
    }
  ],
  "lockers": [
    {
      "locker": {
        "href": "https://localhost:8904/api/lockers/3456"
      }
    },
    {
      "locker": {
        "href": "https://localhost:8904/api/lockers/3457"
      }
    }
  ],
  "elevatorGroups": [
    {
      "elevatorGroup": {
        "href": "https://localhost:8904/api/elevator_groups/635"
      },
      "accessZone": {
        "href": "https://localhost:8904/api/access_zones/637"
      }
    },
    {
      "elevatorGroup": {
        "href": "https://localhost:8904/api/elevator_groups/639"
      },
      "enableCodeBlueFeatures": true
    }
  ]
}
```

---

## Cardholder Update Location POST example

## Cardholder Update Location POST example:

This is an example of a POST you could use to move a cardholder to a target access zone.

accessZone:objecthref:string(url)

The href of the access zone into which you want to move your cardholder. This example came from the access zones controller , but you can also use an href from the items controller if you do not have a RESTStatus or (in 8.60) RESTOverrides licence.

In 9.60 and later, use the empty string "" to move a cardholder out of all zones (known as "outside the system").

##### Example

```json
{
  "accessZone": {
    "href": "https://localhost:8904/api/access_zones/412"
  }
}
```

---

## Cardholder PATCH example

## Cardholder PATCH example:

Send one of these in a PATCH to /api/cardholders/{id} to modify the cardholder at that URL, or to add and modify cards, competencies, groups, and relationships.

All the cardholder fields you can supply are described in the cardholder detail .

This example:

- sets 'authorised' true,
- sets a PDF holding an employee ID,
- changes the notification flags on two PDFs,
- adds two new credentials, one card and one mobile,
- changes the issue level with Stolen as the reissue reason and clears the until date on another credential,
- deletes a third credential with Lost as the remove reason,
- adds two access group memberships, one unending and one with an until date,
- clears the until date on another access group membership,
- removes a fourth access group membership,
- adds a competency, inactive, with a future enablement date,
- activates a competency that the cardholder already had,
- adds one relationship,
- changes the cardholder on another,
- adds one locker assignment with a from date,
- sets the until date on another locker assignment,
- removes a third locker assignment,
- adds two operator group memberships,
- removes a third operator group membership,
- adds an elevator group with a default floor,
- modifies another elevator group, turning the 'code blue' feature on and the VIP feature off, and
- removes a third elevator group.

This is also the method you use for changing a cardholder's name, description, notes, user code, etc.

In addition to those fields, this PATCH format accepts five arrays: 'cards', 'accessGroups', 'competencies', 'relationships', (in 8.50) 'operatorGroups', and (in 9.50) 'accessibilities'.

authorised:boolean

You can modify all the fields on a cardholder, provided you have the necessary privileges.

@employeeId:string

Replace PDF values as though they were flat fields on a cardholder. Prefix the name of the PDF with @ , and Base64-encode images. Send 'null' if you want to delete a PDF value.

Remember that a cardholder's record will not return a PDF if he or she is not a member of the access group that grants that PDF. In this example, one or both of access groups 352 and 124 are attached to 'employeeId'.

personalDataDefinitions:[Cardholder PDF](https://gallaghersecurity.github.io/cc-rest-docs/ref/cardholders.html#/definitions/Cardholder%20PDF)

This is how you set and unset the notifications flags on PDFs. Presumably this cardholder wants to receive notifications at the email address stored in the 'email' PDF rather than by SMS.

[Cardholder PDF](https://gallaghersecurity.github.io/cc-rest-docs/ref/cardholders.html#/definitions/Cardholder%20PDF)cards:object

This object can contain three arrays, named 'add', 'update', and 'remove'. Every element you put in those arrays should be in the card schema .

Each element of the 'add' array will need a 'type' member, at the very least. Every card field makes sense here except 'href'. Only existing cards have hrefs. This example adds two cards: one has nothing more than the type, so it will receive blank 'from' and 'until' dates and a computed number and issue level. The other is a mobile credential (it has an 'invitation' block) with a custom initial state.

Each element of the 'update' array should be a card to modify. It will need the href of that card, plus the fields you want to change. Remember you cannot change a card's type. The example changes the issue level and resets the 'until' date.

The only fields that make sense in an element of the 'remove' array are 'href' and 'status'.

You can remove and add cards in the same PATCH. In fact you should do that in preference to making multiple API calls. That is a good way of reissuing a mobile credentials, for example: put the href to the old one in the 'remove' array and a new invitation in the 'add' array. The new credential should have the same card number and the same 'type' and 'invitation' blocks as the credential you're re-issuing.

Do not put the same href in both the 'update' and 'remove' arrays.

In version 8.90 and later you can specify a card state in the value field inside the status block when removing a card or changing its issue level. It becomes the final state of the card if you remove it or the final state of the card with the previous issue level if you re-issue it, so it must be one of the valid states for the card type. The Gallagher clients and the resulting event call this state the 'reason' for the re-issue or removal. By default it will be the same as the card's current state.

accessGroups:object

Like the 'cards' object, this can contain three arrays named 'add', 'update', and 'remove'. Every element you send in those arrays should be in the access group schema .

This operation does not modify access groups in Command Centre; it works on a cardholder's memberships to those groups.

Each element of the 'add' array will need an 'accessGroup' member containing an href identifying the group to which you wish to add the cardholder. The other fields are optional. If you omit 'from', the membership will take effect immediately. If you omit 'until', it will be unending.

Each element of the 'update' array will need an href identifying the membership (not the group!) to update, and one or both of the 'from' and 'until' date-times with new values. You cannot change the group: just the activity period. The example removes the until date, effectively making the group membership unending.

In version 7.90.883 or earlier, 'from' and 'until' should be in UTC with a trailing 'Z'. Releases after 883 understand different timezones here.

Note that updating a cardholder's group membership will change its href, so do not cache it.

The only field that makes sense in an element of the 'remove' array is the href.

Do not put the same href in both the 'update' and 'remove' arrays.

competencies:object

Like the cards and accessGroups objects, this can contain three arrays named 'add', 'update', and 'remove'. Every element should be in the cardholder competency schema .

This operation does not modify Command Centre's competencies: it works on a cardholder's holdings of those competencies.

Each element of the 'add' array will need a 'competency' block containing an href member identifying the competency you wish to grant the cardholder. All other fields are optional. Note that attempting to give a cardholder a competency he or she already has will result in an error or an alarm depending on the server version.

Each element of the 'update' array will need an href identifying the cardholder/competency link to update, and one or more of the 'expiry', 'enabled', 'enablement', 'comment', 'limitedCredit', and 'credit' fields.

The only field that makes sense in an element of the 'remove' array is the href of the link between the cardholder and the competency.

Do not put the same href in both the 'update' and 'remove' arrays.

In this example we are giving the cardholder a disabled competency which will enable in 2021, and activating another competency.

relationships:object

It should be no surprise that this can contain three arrays named 'add', 'update', and 'remove', and that every element should be in the relationship schema .

This operation does not modify Command Centre's roles: it works on the relationships between two cardholders.

Each element of the 'add' array will need a 'role' member containing an href identifying the type of relationship you wish to establish, and a 'cardholder' member containing an href identifying the other party (the one who will perform the role for the cardholder at the URL you are PATCHing). There are no optional fields.

Each element of the 'update' array will need an href identifying the relationship to update, and one or both of the 'role' and 'cardholder' blocks containing the updated values. The example leaves the role but changes the cardholder - a new supervisor, presumably.

The only field that makes sense in an element of the 'remove' array is the href.

Do not put the same href in both the 'update' and 'remove' arrays.

lockers:object

With you well in the habit by now, each element of your three arrays should be in the cardholder locker schema . They will allocate lockers to cardholders, de-allocate them, and adjust validity periods.

Each member of the 'add' array will need a 'locker' member containing an href identifying the locker to allocate. The cardholder you will allocate it to is identified by the request URL, remember.

Each member of the 'update' array will need an href identifying the allocation to update, and one or both of the 'from' and 'until' date-times.

The validity period is all you can change about a locker allocation. If you want to change the locker, delete the old one and add a new. You can do that in the same PATCH, with one element in each of the 'add' and 'remove' arrays.

This example allocates one locker starting in January 2019, sets the end-date of an existing allocation to the end of February 2020, and removes another entirely.

operatorGroups:object

This can contain two arrays named 'add' and 'remove'. Every element you send in those arrays should be in the cardholder operator group schema .

Each element of the 'add' array will need an 'operatorGroup' member containing an href identifying the operator group to which you wish to add the cardholder. Supported in 8.50 and later.

The only field that makes sense in an element of the 'remove' array is the href. Make sure it is the href of the membership, not of the operator group. Supported in 8.90 and later.

There is nothing about an operator group membership that you can change so there is no point to an 'update' array. Operator group memberships are, or are not: there is no update.

elevatorGroups:object

This can contain three arrays named 'add', 'update', and 'remove', each containing an element in the cardholder elevator group schema .

The 'elevatorGroup' block only makes sense in the 'add' array. The 'update' array is for changing the cardholder's default floor and passenger types on an existing elevator group assignment.

The server will ignore all fields in the elevatorGroup and accessZone objects except 'href' if you place them in the body of your PATCH.

Remove the default floor in an update by supplying an accessZone block with the href set to null or "".

Change a passenger type in an update by supplying the new value. The passenger type will be unchanged otherwise.

As with cards and access group memberships etc., the server ignores root-level hrefs in the elements of an 'add' array, requires them in the 'update' array (since they indicate the entries to work on), and ignores everything but them in the 'remove' array.

The example shows our cardholder receiving a default floor for one elevator group and updating the Code Blue and VIP passenger types for another elevator group.

Added in 8.50.

##### Example

```json
{
  "authorised": true,
  "@employeeId": "THX1139",
  "personalDataDefinitions": [
    {
      "@email": {
        "notifications": true
      }
    },
    {
      "@cellphone": {
        "notifications": false
      }
    }
  ],
  "cards": {
    "add": [
      {
        "type": {
          "href": "https://localhost:8904/api/card_types/354"
        },
        "pin": "153624"
      },
      {
        "type": {
          "href": "https://localhost:8904/api/card_types/600"
        },
        "number": "Jock's iPhone 8",
        "status": {
          "value": "Pending sign-off"
        },
        "invitation": {
          "email": "jock@example.com"
        }
      }
    ],
    "update": [
      {
        "href": "https://localhost:8904/api/cardholders/325/cards/97b6a24ard6d4500a9d",
        "issueLevel": 2,
        "until": "",
        "status": {
          "value": "Stolen"
        },
        "pin": "153624"
      }
    ],
    "remove": [
      {
        "href": "https://localhost:8904/api/cardholders/325/cards/77e8affe7c7e4b56",
        "status": {
          "value": "Lost"
        }
      }
    ]
  },
  "accessGroups": {
    "add": [
      {
        "accessGroup": {
          "href": "https://localhost:8904/api/access_groups/352"
        }
      },
      {
        "accessGroup": {
          "href": "https://localhost:8904/api/access_groups/124"
        },
        "until": "2019-12-31"
      }
    ],
    "update": [
      {
        "href": "https://localhost:8904/api/cardholders/325/access_groups/10ad21",
        "until": ""
      }
    ],
    "remove": [
      {
        "href": "https://localhost:8904/api/cardholders/325/access_groups/10ed27"
      }
    ]
  },
  "competencies": {
    "add": [
      {
        "competency": {
          "href": "https://localhost:8904/api/competencies/2354"
        },
        "enabled": false,
        "enablement": "2021-01-01T08:00+13"
      }
    ],
    "update": [
      {
        "href": "https://localhost:8904/api/cardholders/325/competencies/2dc3",
        "enabled": true
      }
    ]
  },
  "relationships": {
    "add": [
      {
        "role": {
          "href": "https://localhost:8904/api/roles/5396"
        },
        "cardholder": {
          "href": "https://localhost:8904/api/cardholders/5398"
        }
      }
    ],
    "update": [
      {
        "href": "https://localhost:8904/api/cardholders/325/roles/1799lah1170",
        "cardholder": {
          "href": "https://localhost:8904/api/cardholders/10135"
        }
      }
    ]
  },
  "lockers": {
    "add": [
      {
        "locker": {
          "href": "https://localhost:8904/api/lockers/1200",
          "from": "2019-01-01"
        }
      }
    ],
    "update": [
      {
        "href": "https://localhost:8904/api/cardholders/325/lockers/wxyz1234",
        "until": "2020-02-29"
      }
    ],
    "remove": [
      {
        "href": "https://localhost:8904/api/cardholders/325/lockers/abcd4321"
      }
    ]
  },
  "operatorGroups": {
    "add": [
      {
        "operatorGroup": {
          "href": "https://localhost:8904/api/operator_groups/532"
        }
      },
      {
        "operatorGroup": {
          "href": "https://localhost:8904/api/operator_groups/535"
        }
      }
    ],
    "remove": [
      {
        "href": "https://localhost:8904/api/cardholders/325/operator_groups/EBDRSD"
      }
    ]
  },
  "elevatorGroups": {
    "add": [
      {
        "elevatorGroup": {
          "href": "https://localhost:8904/api/elevator_groups/635"
        },
        "accessZone": {
          "href": "https://localhost:8904/api/access_zones/637"
        }
      }
    ],
    "update": [
      {
        "href": "https://localhost:8904/api/cardholders/325/elevator_groups/1268613268",
        "enableCodeBlueFeatures": true,
        "enableVipFeatures": false
      }
    ],
    "remove": [
      {
        "href": "https://localhost:8904/api/cardholders/325/elevator_groups/3498734"
      }
    ]
  }
}
```

---

## Cardholder PDF

## Cardholder PDF:

A personal data definition and its value for a cardholder. Each definition is an object containing one property named after the PDF (plus a leading '@'), which in turn contains its value and notifications flag for the containing cardholder and an object containing some of the PDF definition's basic fields.

This is returned as part of a cardholder , and you can send it in a POST or a PATCH to set the PDF's value and notification flag for a cardholder. The server will ignore the definition block if you send it back. It uses the name of the block (minus its leading @ ) to find the PDF to change.

While you can use this structure to change the value of a cardholder's PDF, it may be simpler to put a property in the root of the payload named after the PDF with a leading '@'.

@PDF_name:object

This is the name of the personal data field prefixed with an @ .

href:string(url)

GET this link to receive this cardholder's value for this PDF. If it is an image you will receive the raw image data with an appropriate content-type. A browser will render it correctly.

This field will be absent if the cardholder does not have a value for the PDF.

It is read-only: do not send it in a PATCH or POST.

If the PDF is not an image the content type will be text/plain, encoded (like everything else in this API) as UTF-8. But you should only use it for image PDFs because:

- Values for scalar PDF types (everything except images) are also given to you in the value field in this object, described below.
- The format of date PDFs varies with the Command Centre version. Old servers return it in a format that depends on the server's locale. This was a bug. Newer servers use ISO 8601.
- If you learn this URL while a cardholder has the PDF, then the cardholder loses it, then you GET the URL, results will vary depending on the server version. The correct result is a 404, and if the PDF is an image or the server is recent you will see that. But old servers may return a blank string or even a 500 for non-image PDFs.
definition:object

The definition object is read-only: do not send it in a PATCH or POST.

href:string(url)

This is the href of the PDF's definition, common to all cardholders who hold a value for this PDF.

name:string

The PDF's name, without an '@' prefix.

id:string

Short alphanumeric identifier used elsewhere in the API to filter cardholder searches and add PDF values to alarm and event GETs.

type:stringstring,                                     image,                                     strEnum,                                     numeric,                                     date,                                     address,                                     phone,                                     email,                                     mobilevalue:string

In a GET response for text, numeric, and date PDFs this will be a scalar containing the value, but for image PDFs it will be an object containing a copy of the href field above.

In versions up to and including 8.60, if a cardholder did not have an image PDF captured (in other words they were a member of an access group that included an image PDF, but they did not have a value for it) the API returned the string 'Not captured' in the value field. Versions 8.70 and later will not return the value field at all if there is no image.

For date PDFs the string will comply to ISO 8601, but may or may not contain a time depending on the version of Command Centre. Newer versions omit it because date PDFs only hold a date, not a time. If the result includes a time, it will be midnight UTC.

In a POST or PATCH send text PDFs (including dates) as strings, numeric PDFs as numbers or strings that parse to numbers, and image data Base64-encoded into a string.

notifications:boolean

In a GET response, this will only appear for email and mobile PDF types. In a POST or PATCH, it only makes sense for the same types.

If true, cardholder notifications will go to the telephone number or email address held by this PDF. Cardholders can have their notifications go to as many contacts as they wish.

##### Example

```json
{
  "@cellphone": {
    "href": "https://localhost:8904/api/cardholders/325/personal_data/9998",
    "definition": {
      "href": "https://localhost:8904/api/personal_data_fields/9998",
      "name": "cellphone",
      "id": "9998",
      "type": "mobile"
    },
    "value": "a@b.com",
    "notifications": false
  }
}
```

---

## Cardholder card

## Cardholder card:

A card is an access credential, including physical cards and mobile (Bluetooth and NFC) credentials and digital IDs. The server returns it as part of a cardholder's details , and you supply it when creating or modifying a card or credential on a cardholder.

Every card has a type, a status, and a validity period. Its type determines other fields of relevance, described below.

PIV cards are complex enough to warrant a document of their own .

href:string(url)

DELETE this link to delete a card.

Do not specify it when creating a card.

DELETE is the only verb you can use on this URL. GET will always return a 404 in the current versions of Command Centre.

number:string

For a physical access card, this is its card number. It must be unique across all cards of the same card type. If you leave it blank when creating a card of a type that has a decimal number format, Command Centre will use the next available number.

Card numbers for a mobile credential need not be unique. They are strings, and are not used by Command Centre except for display.

Card numbers for digital IDs are GUIDs.

This field is mandatory for card types with text or PIV number formats. If the card type has a text card number format with a regular expression and you supply a card number that does not match that regex, Command Centre will reject your update.

PIV card numbers must be the same as the card's FASC-N. PIV-I card numbers must not.

One rule is common across all card types: you cannot change the number of an existing card or credential.

cardSerialNumber:string

The serial number (CSN, MIFARE UID) of a physical access card as a hex string without a leading '0x'. The number is encoded in the traditional way, with an even number of numbers and the letters from 'A' through 'F', but the '0x' is missing when it comes out of the API and it must be missing when you send it back.

The example in this example shows a seven-byte serial in the order you can expect in a GET and should use in a PATCH or POST. Those on NXP MIFARE cards start with 04 and end with 80 or 90.

Four-byte serial numbers are in the same format, but shorter: "EBD62D25" , for example.

This may not be not present on very old cards.

This field was read-only until 7.90. It is read-write in 8.00 and later.

issueLevel:integer0 ≤ x ≤ 15

The issue level of a physical access card. If two cards have the same number but different issue levels, only the one with this issue level will gain access.

If you leave it blank when creating a card, Command Centre will pick an appropriate value.

If you increase it when modifying a card, all cards with lower issue levels will stop working.

Do not specify one when creating or updating a mobile credential or digital ID. They do not have issue levels.

status:object

Each card has two status codes: value , and type , covered in separate sections below.

value is human-readable. It comes from the Card State Set configured by the site, and could be adjusted according to the card's activation dates. The REST API and this document do not cover card state sets because in most cases, the default set is sufficient. See the online help for the Command Centre client if you wish to create your own.

The second field, type , comes from a fixed enumeration, and so is better suited than value for integrations. Command Centre derives it from the card's state and activation dates.

value:string

This card's state, taken from the card type's state set. The default card state set contains 'Active', 'Disabled (manually)', 'Lost', 'Stolen', and 'Damaged', or translations of those into the server's language. Your card state sets may differ, as they are customisable. PIV cards have extra values covered in the PIV supplement .

In addition to the values in the card type's state set, it will be 'Not Yet Activated' if the card's activation date is in the future, 'Expired' if its deactivation date has passed, or 'Disabled (by inactivity)' if that is the site's policy.

Because the values of the value field is set by site administrators, you should not use it for programmatically determining whether a card is active. Use type instead. Use value for display.

When creating or updating a card, set value to one of the valid card states from the card state set. Command Centre is not fussy about case. If you omit it when you create a card, Command Centre will use the default for the card state set ('active', for the factory state set).

type:stringpending,                               active,                               expired,                               inactive

This will be 'pending' if the activation date ( from ) is in the future, 'expired' if its deactivation date ( until ) is in the past, or 'inactive' if it is disabled for one of the reasons given in the card state.

Never send type : Command Centre always infers it.

type:object

The name of the card type from the site configuration, and a link to the card type object.

When creating a card, the href must be a card type that makes sense for the other values you placed this object.

You cannot change a card's type once it is created.

href:string(url)name:stringinvitation:object

Command Centre will only return this object for a mobile credential, and you should send it only when creating one. In the interests of security you cannot modify the invitation block of an existing credential. Someone's thumb might be on its way to accept it, after all. If there was something wrong with it you should delete it and start afresh.

Whether you should send mobile or email when creating a mobile credential depends on whether you are using the Gallagher mobile apps or your own.

If you specify either mobile or mail to early-version servers you must also supply the other so that Command Centre can send both an email invitation and a confirmation SMS. It is an error to only specify one on those servers.

Later versions of Command Centre made the SMS verification optional to better support installations that use the Gallagher apps but prefer not to depend on cellular connectivity. You do not need to send a mobile number when creating such a credential.

If you do not give an email address Command Centre cannot send an invitation to the cardholder. It will be up to another application to complete the creation of this credential using the Mobile Connect SDK.

You should not send a mobile number when creating a credential for use by third-party apps that use the Mobile Connect SDK. SMS is not supported there.

For more detail on using the Mobile Connect SDK, including more complete coverage of how an email address and mobile number are used in the provisioning process, see its documentation at gallaghersecurity.github.io .

email:string

The email address to which Command Centre will send or did send an invitation for this credential.

mobile:string

The telephone number to which Command Centre will send or did send an SMS containing a confirmation code for this invitation.

singleFactorOnly:booleanfalse

If you set this true Command Centre will not require a PIN or fingerprint from the cardholder when they enrol. Be aware that without that second authentication factor, zones in modes that require a PIN and readers that always require a second factor will not grant access to the cardholder.

singleFactorOnly will only be in the result of a GET if it is true. If it is false, the field will be missing.

status:stringnotSent,                               sent,                               expired,                               accepted

- sent means Command Centre is waiting for the user to accept the invitation, either in Gallagher Mobile Connect or another app that uses the Mobile Connect SDK.
- accepted means that the credential is ready for use.
- notSent means the credential is only a few seconds old or Command Centre is having trouble contacting the cloud.
- expired means that Command Centre did not receive a response in time.
href:string(url)

Mobile applications use this URL to accept this invitation. See the Mobile Connect SDK documentation for how to do that in your own applications.

Only present if 'status' is 'sent'.

from:string(date-time)

The start of the time period during which this card is active. If this time is in the future, the card is not active.

When it is not set, Command Centre acts as though it is set to a time in the distant past.

When modifying a card, send a null string "" to reset it.

It must be before midnight on the morning of January 1, 2100 local time (so servers with positive timezone offsets have maximums during December 31 2099), and it must be less than or equal to than the until time. A future version of Command Centre will clamp too-high values to December 31 2099 and return a 2xx (success) instead of 400 in that case.

The server will reject timestamps in card updates that are not in an acceptable format. But in requests to create a card, rather than modify an existing card, the server will ignore such strings and use the card type's defaults. This is undesirable behaviour and will change in a future version of Command Centre.

until:string(date-time)

The end of the time period during which this card is active. If this time is in the past, the card is not active.

When it is not set, Command Centre acts as though it is set to a time in the distant future.

When modifying a card, send a null string "" to reset it.

It must be before midnight on the morning of January 1, 2100 local time (so servers with positive timezone offsets have maximums during December 31 2099), and it must be greater than or equal to the from time. A future version of Command Centre will clamp too-high values to December 31 2099 and return a 2xx (success) instead of 400 in that case.

The server will reject timestamps in card updates that are not in an acceptable format. But in requests to create a card, rather than modify an existing card, the server will ignore such strings and use the card type's defaults. This is undesirable behaviour and will change in a future version of Command Centre.

credentialClass:stringcard,                         digitalId,                         govPass,                         mobile,                         piv,                         pivi,                         trackingTag,                         transact

This indicates the type of the card. It comes from an enumeration, and is a reliable way of determining the credential's type.

Added in 8.00.

The experimental applePass was added in 9.10. Its name may change in future versions.

The govPass credential class will change its name in 9.30.

trace:boolean

If set, using this credential will generate an event.

This field is not in the default set so you will not see it unless you ask for it using the fields query parameter.

Added in 8.30.

lastPrintedOrEncodedTime:string(date-time)

The date and time this card was last printed or encoded. It will not come out by default - you need to ask for it with fields=cards.lastPrintedOrEncodedTime .

Added in 8.40.

lastPrintedOrEncodedIssueLevel:integer1 ≤ x ≤ 15

The issue level of this card when it was last printed or encoded, provided it was non-zero. It will not come out by default - you need to ask for it with fields=cards.lastPrintedOrEncodedIssueLevel .

Added in 8.40.

lastUsedTime:string(date-time)

When a Gallagher controller last observed the credential in use.

pin:string

Even though this appears in the example of a cardholder GET, this is a write-only field. You can use it in POSTs and PATCHes but the server will not send it to you.

Being numeric, you might be tempted to send PINs to the server without surrounding quotes. However if you do that, leading zeros will be lost and the resulting behaviour is undefined.

8.90 servers will reject your request if the card's credentialClass is not card , piv , piv-i , or govPass . More recent servers will issue a warning in that case, and will not set the PIN, but will allow the rest of the update.

PINs were added to the API in 8.90.

visitorContractor:boolean

This is a credential property only for GovPass, indicating that the enrolled credential is for a visitor / contractor.

It is a write-once field new to 9.00.

ownedBySite:boolean

This is a credential property only for GovPass, indicating if the site is the owner of the card.

It is a read-only field new to 9.00.

credentialId:object

Reserved for use by Gallagher applications.

bleFacilityId:object

Reserved for use by Gallagher applications.

##### Example

```json
{
  "href": "https://localhost:8904/api/cardholders/325/cards/97b6a24ard6d4500a9",
  "number": "1",
  "cardSerialNumber": "045A5769713E80",
  "issueLevel": 1,
  "status": {
    "value": "Disabled (manually)",
    "type": "inactive"
  },
  "type": {
    "href": "https://localhost:8904/api/card_types/354",
    "name": "Card type no. 1"
  },
  "invitation": {
    "email": "nick@example.com",
    "mobile": "02123456789",
    "singleFactorOnly": "boolean",
    "status": "sent",
    "href": "https://security.gallagher.cloud/api/invitations/abcd1234defg5678"
  },
  "from": "2017-01-01T00:00:00Z",
  "until": "2017-12-31T11:59:59Z",
  "credentialClass": "mobile",
  "trace": false,
  "lastPrintedOrEncodedTime": "2020-08-10T09:20:50Z",
  "lastPrintedOrEncodedIssueLevel": 1,
  "lastUsedTime": "2025-09-24T14:00:00.000Z",
  "pin": "153624",
  "visitorContractor": false,
  "ownedBySite": false,
  "credentialId": "reserved",
  "bleFacilityId": "reserved"
}
```

---

## Cardholder card mobile example

## Cardholder card mobile example:

This is an example of what you would send to create a new mobile credential. The card detail contains everything in a card, and is a bit daunting, so this example shows only the fields you need for a mobile credential.

You can submit it as part of a larger document in a POST to create a cardholder with cards, or as part of a PATCH to create a mobile credential on an existing cardholder.

number:string

The so-called card 'number' on a mobile credential does not need to be numeric.

status:object

Optional. Provide a field value set to one of the valid starting states for the mobile credential. If you omit it, the credential will start in the default state.

type:object

This is the only compulsory field in the POST body. It must contain an href to the card type of the new mobile credential.

from:string(date-time)until:string(date-time)invitation:object

This is a block containing fields that describe how Command Centre should set about registering the mobile device.

If you specify mobile you must also supply email . If you give neither, Command Centre will not send an invitation to the cardholder.

singleFactorOnly defaults to false, which is the recommended setting. See the card detail for more.

email:stringmobile:stringsingleFactorOnly:boolean

##### Example

```json
{
  "number": "Nick's mobile",
  "status": {
    "value": "active"
  },
  "type": {
    "href": "https://localhost:8904/api/card_types/654"
  },
  "from": "2017-01-01T00:00:00Z",
  "until": "2018-01-01T00:00:00Z",
  "invitation": {
    "email": "nick@example.com",
    "mobile": "02123456789",
    "singleFactorOnly": false
  }
}
```

---

## Cardholder card physical example

## Cardholder card physical example:

This is a minimal object for creating a card, containing the only required field: the card type. See the card detail for other fields you can use, such as the card's issue level and from/until dates.

You can submit it as part of a larger document in a POST to create a cardholder with cards, or as part of a PATCH to assign a card to an existing cardholder.

type:object

This is the only compulsory field in the POST body. It must contain an href to the card type of the new card.

##### Example

```json
{
  "type": {
    "href": "https://localhost:8904/api/card_types/600"
  }
}
```

---

## Cardholder access group

## Cardholder access group:

An access group is an object in Command Centre. This section is about a connection between an access group and a cardholder, called a membership . A cardholder can be a member of many groups, and groups can have any number of members.

Less obvious is that a cardholder can have many memberships to the same group. This is useful because a membership has a validity period, expressed with from and until date-times. Outside those moments Command Centre does not regard the cardholder as being a member of the group. If there exists one membership with from in the past or unset and until in the future or unset, the cardholder is a member.

Presence in an access group affects physical access rights and possession of PDFs, among other things.

The membership object can come from the server in a cardholder's details, and you send it to the server when modifying a cardholder's group memberships. All uses are described below.

href:string(url)

DELETE this URL to remove this group membership, and use it in the body of a PATCH to a cardholder to identify memberships you want to modify.

Note that changing an access group membership with a PATCH will change this href. Do not cache it.

DELETE is the only verb you can use on this URL. GET returns a 404.

accessGroup:object

An object containing a link to this group's detail page and (when sent by the the server) its name. You should not send it when modifying a group membership because you cannot change a group membership's group; you can only change its dates. But you must send it when adding a new group membership, of course, because it identifies the cardholder's new group. Just send the href: don't bother with the name.

8.70 and later will not return the link if your operator does not have the privilege to view the access group (given by 'View access groups', for example).

status:object

The two fields in this block are read-only because they are determined by the from and until dates.

value:string

The state of this cardholder's access group membership, in the site's language. In an English locale, the value is the same as the type, but capitalised.

type:stringpending,                               active,                               expired

The state of this cardholder's access group membership.

This will be 'pending' if the activation date is set and in the future, 'expired' if its deactivation date is set and in the past, or 'active'.

from:string(date-time)

The start of the time period during which this group membership is active. If this time is in the future, the card will be inactive.

When sending this to a server of version 7.90.883 or earlier, use UTC with a trailing 'Z'. More recent versions understand timezone offsets.

until:string(date-time)

The end of the time period during which this group membership is active. If this time is in the past, the card will be inactive.

When sending this to a server of version 7.90.883 or earlier, use UTC with a trailing 'Z'. More recent versions understand timezone offsets.

##### Example

```json
{
  "href": "https://localhost:8904/api/cardholders/325/access_groups/D714D8A89F",
  "accessGroup": {
    "name": "R&D special projects group",
    "href": "https://localhost:8904/api/access_groups/352"
  },
  "status": {
    "value": "Pending",
    "type": "pending"
  },
  "from": "2017-01-01T00:00:00Z",
  "until": "2017-12-31T11:59:59Z"
}
```

---

## Cardholder operator group

## Cardholder operator group:

An operator group is an object in Command Centre. The connection between an operator group and a cardholder is a membership . A cardholder can be a member of many different operator groups, and operator groups usually have more than one member, but a cardholder can only have one membership to a given operator group at a time. This is because an operator group membership, unlike an access group membership, does not have start and end dates.

Presence in an operator group affects software access. Your REST operator, for example, must be in an operator group that grants privileges otherwise it will receive nothing but 404s.

Added to the API in 8.50.

href:string(url)

DELETE this URL or use it in the 'operatorGroups' block of a cardholder PATCH to remove this operator group membership.

DELETE is the only verb you can use on this URL. GET will always return a 404.

operatorGroup:object

An object containing a link to this operator group's detail page and its name. It is marked read-only because you do not send it to the server when managing a cardholder's operator groups: you cannot change a group membership's group.

The link will be absent if your operator does not have the privilege to view the operator group ('View operators' or 'Edit operators', for example).

##### Example

```json
{
  "href": "https://localhost:8904/api/cardholders/325/operator_groups/EBDRSD",
  "operatorGroup": {
    "name": "Locker admins",
    "href": "https://localhost:8904/api/operator_groups/532"
  }
}
```

---

## Cardholder competency

## Cardholder competency:

A competency is an object in Command Centre with some basic fields like a name and a notice period. Its purpose is to allow a site to refuse access to cardholders who do not meet a special requirement.

This section describes the link between a cardholder and a competency. The status of that link determines whether they "hold" the competency for the purposes of access control decisions at the door. When we talk about updating, adding, or deleting a competency in the cardholder API, we do not mean the competency object itself, but rather the link a cardholder has to the competency.

The link is a block in the cardholder detail. You can also put it in the results of a cardholder search by putting competencies in the fields parameter.

This particular section describes what you receive from a GET. The Cardholder competency update describes what you should send in a POST or PATCH to set or update a cardholder's link to a competency.

href:string(url)

Use this URL as the target of a DELETE to remove a cardholder's competency, or in the body of a cardholder PATCH to modify it.

DELETE is the only verb you can use on this URL. GET will always return a 404.

competency:object

This contains the competency's name and its href. They are read only because they belong to the competency itself, not the cardholder's link to it.

status:object

This object contains two strings. Both are read-only, so do not specify them when assigning or updating a cardholder's competency. value is taken from the site's language pack, suitable for display. type comes from a fixed enumeration. It will be expiryDue or active when the cardholder carries this competency; anything else means no. A fuller explanation follows.

A competency can be disabled, expired, both, or neither. Whether it is enabled is a flag on the cardholder's holding of the competency. Whether it is expired is derived from an expiry timestamp (accurate to the second): if it is in the past, Command Centre considers the competency expired.

A competency can also have an enablement date. If that date (timestamp) passes while the competency is disabled, Command Centre will enable the competency. It will leave the date on the item for future reference, though it will not affect the competency again.

If the competency is disabled, the status type will be inactive when there is no enable date or it is in the past, and pending when the enable date is in the future (i.e., there is an automatic re-enablement coming).

If the competency is not disabled, the expiry time is important. If it is in the past, type will be expired .

All of those cases are negative. Two remain, when our cardholder is blessed with an enabled and active competency. type will be expiryDue if the expires time is in the future but within the competency's advance notice period, or active if the expires time is beyond the advance notice period or not set at all.

You can see and set the enabled flag, the enable date, and the expiry date via this API.

When creating a cardholder or updating a competency on an existing cardholder, you should set the enabled field one way or the other, and the enablement and expiry dates if you wish. They will determine the contents of this status block.

Here it is in table form. The first two rows are the positive cases, when Command Centre would grant access to a competency-enforced zone.

Enabled flagEnablement dateExpiry date`status.type`true-Far futureactivetrue-Near futureexpiryDuetrue-PastexpiredfalseFuture-pendingfalsePast-inactivefalseUnset-inactiveexpiryWarning:string(date-time)

The time at which the cardholder will (or did) receive a warning about the competency expiring. If this time is set and in the past but the competency has not yet expired and is still enabled, the status type will be expiryDue .

expiry:string(date-time)

The time at which the competency will expire. If this time is set, in the past, and the competency is enabled, status type will be expired .

enablement:string(date-time)

The time at which the competency will be re-enabled. If set and in the future, and the competency is disabled, the status type will be pending .

comment:string

The comment appears in the management clients when viewing the cardholder.

limitedCredit:boolean

If false, Command Centre's 'Pre-pay Car Parking' feature (available under its own licence) will not reduce the current credit.

This field will be in the results if its value is true.

credit:integer

The balance, or amount of credit left on this competency for use by Pre-pay Car Parking. It can be negative.

This field will be in the results if its value is not zero.

##### Example

```json
{
  "href": "https://localhost:8904/api/cardholders/325/competencies/2dc3p0",
  "competency": {
    "href": "https://localhost:8904/api/competencies/2354",
    "name": "Hazardous goods handling"
  },
  "status": {
    "value": "Pending",
    "type": "pending"
  },
  "expiryWarning": "2017-03-06T15:45:00Z",
  "expiry": "2017-03-09T15:45:00Z",
  "enablement": "2018-03-09T15:45:00Z",
  "comment": "CPR refresher due March.",
  "limitedCredit": true,
  "credit": 37
}
```

---

## Cardholder competency update

## Cardholder competency update:

This is a description of the fields you would supply when updating a cardholder's holding of a competency. It is slightly different from what you receive in a cardholder GET: you do not send read-only blocks such the status and the competency, and there is a write-only field enabled that you can supply to change the status.

You cannot use a PATCH to change the competency to which the status, times, comment, and credit apply. If you need to do that, delete the cardholder's existing competency and give them a new one. You can do that in one PATCH.

A cardholder can have only one link to a given competency. Attempting to give a cardholder a competency a second time will either fail or raise a stateful alarm depending on your version of Command Centre.

href:string(url)

Required. This identifies the cardholder competency you are updating.

enabled:boolean

This is a write-only field. You will not receive it from the GET. It changes the competency between enabled (status type expired , expiryDue , or active ) and disabled (status type inactive or pending ). Set it to false in a PATCH if you want to disable the competency now, particularly if you are setting a future enablement date.

expiry:string(date-time)

The time at which you want to expire the competency. See the full description above .

enablement:string(date-time)

The time at which you want to re-enable the competency. You can set this on an enabled competency but it has no effect there, so with this field you would generally also set enabled: false . See the full description above .

comment:stringlimitedCredit:boolean

If you set this false, Command Centre's 'Pre-pay Car Parking' feature (available under its own licence) will not reduce the current credit.

credit:integer

The amount of credit left on this competency for use by Pre-pay Car Parking. It can be negative.

##### Example

```json
{
  "href": "https://localhost:8904/api/cardholders/325/competencies/2dc3p0",
  "enabled": true,
  "expiry": "2017-03-09T15:45:00Z",
  "enablement": "2018-03-09T15:45:00Z",
  "comment": "CPR refresher due March.",
  "limitedCredit": true,
  "credit": 37
}
```

---

## Cardholder relationship

## Cardholder relationship:

A role is an object in Command Centre. They are usually named using nouns such as 'supervisor', 'manager', or 'team leader'.

The operator clients and the REST API allow you to create a link between two cardholders, called a relationship, using a role. The link is directional: we refer to the cardholder who 'has' the role as the child, and the cardholder who performs or 'is' the role as the parent.

A parent can perform a role for any number of child cardholders, but a child can only have one relationship (parent) for each role. Command Centre will reject your submission if you try to create a relationship when one already exists for the same child and role.

For example, a role on your system might be 'supervisor'. A cardholder can be a supervisor for any number of others, but will only have one supervisor.

Loops are possible: two cardholders can supervise each other, for example.

The REST API manages relationships through the child cardholder. You can create them at the same time as creating the child in a POST, or add them later in a PATCH.

There is currently no way to list all a cardholder's children in one request (everyone a particular cardholder is supervising, for example). You would achieve that by iterating through all cardholders, after re-reading the efficiency tips, and checking their relationships - quite easily done in JSONPath.

This section describes the object you receive in a cardholder's detail page and you will send in a POST or PATCH. The child cardholder does not appear in it because he or she is identified by the URL of the request.

href:string(url)

DELETE this link, or put it in relationships.remove.href of a PATCH, to sever the relationship between the two cardholders. Specify it in relationships.update.href of a PATCH to update the relationship.

Do not specify it when creating a new relationship.

DELETE is the only verb you can use on this URL. GET will always return a 404.

role:object

This is the role that the parent identified in the next block performs for the cardholder identified by the request URL.

Once set, this cannot be changed. Command Centre will ignore it if you send it in an update PATCH. If you need to swap a parent from one role to another, send an add and a delete in the same PATCH.

cardholder:object

The href and name of the cardholder that performs this role.

The three name fields are read-only: Command Centre sends them to you in the body of a GET but will ignore them if you send them in the body of a POST or PATCH.

This block and the href in it are unnecessary when deleting a relationship. The href is required when creating one, and optional when updating, but strongly advised since the cardholder is the only thing about a relationship you can change.

firstName and lastName appeared in 8.20.

##### Example

```json
{
  "href": "https://localhost:8904/api/cardholders/325/relationships/179lah1170",
  "role": {
    "href": "https://localhost:8904/api/roles/5396",
    "name": "Supervisor"
  },
  "cardholder": {
    "href": "https://localhost:8904/api/cardholders/5398",
    "name": "Miles Messervy",
    "firstName": "Miles",
    "lastName": "Messervy"
  }
}
```

---

## Cardholder locker

## Cardholder locker:

These appear in an array in a cardholder detail, showing the cardholder's allocated lockers. Each allocation has from and until dates, much like cards and access group memberships, outside of which the allocation is inactive.

A locker can have allocations to more than one cardholder, with separate or overlapping periods. Unlike access groups, however, one cardholder cannot have more than one allocation to the same locker.

href:string(url)

DELETE this to end a cardholder's use of a locker, or use it to identify the allocation you wish to modify in a cardholder PATCH .

DELETE is the only verb you can use on this URL. GET will always return a 404.

locker:object

This href in this object is a link to the allocated locker, and is the identifier to use when allocating the same locker to another cardholder.

The object also contains the name and short name of the locker, and the name and identifying href of its bank.

from:string(date-time)

The start of the time period during which the cardholder has access to this locker.

Send an empty string "" to reset it.

until:string(date-time)

The end of the time period during which the cardholder has access to this locker.

Send an empty string "" to reset it, making the allocation permanent.

##### Example

```json
{
  "href": "https://localhost:8904/api/cardholders/325/lockers/t1m4",
  "locker": {
    "name": "Bank A locker 1",
    "shortName": "A1",
    "lockerBank": {
      "href": "https://localhost:8904/api/locker_banks/4567",
      "name": "Bank A"
    },
    "href": "https://localhost:8904/api/lockers/3456"
  },
  "from": "2017-01-01T00:00:00Z",
  "until": "2018-12-31T00:00:00Z"
}
```

---

## Cardholder elevator group

## Cardholder elevator group:

These appear in an array in a cardholder detail, showing the cardholder's elevator group properties.

A cardholder can have one default floor per elevator group. The elevator system will prepare a car to carry that cardholder to the access zone shown here when the cardholder badges a card at an appropriately configured kiosk.

Elevator system features can be activated by enabling the feature for an elevator group. For example, 'VIP features' gives exclusive access to an elevator car to the passenger.

href:string(url)

The href of this cardholder's elevator group entry. DELETE this to remove it from the cardholder.

elevatorGroup:object

The href and name of the elevator group for which this cardholder has a default floor or passenger type.

accessZone:object

The href and name of the access zone (floor) to which this cardholder is most likely to want to travel after entering the group's main elevator lobby. This property will be missing if the cardholder does not have a default floor for this elevator group.

enableCaptureFeatures:boolean

Cardholders can select and recall specific elevators to specific floors using a kiosk. Once captured, the elevator car can be placed on independent service to give users control of the car to clean the interior or perform maintenance.

enableCodeBlueFeatures:boolean

A special elevator mode which is commonly found in hospitals. It allows an elevator to be summoned to any floor for use in an emergency situation.

enableExpressFeatures:boolean

Allows the cardholder to program selected elevators to cycle continuously between two floors for a pre-determined duration. For example, this feature can help hotels transport food efficiently from their kitchen to a ballroom on another floor. You can also prevent other guests from boarding to provide your banquet guests with VIP treatment.

enableServiceFeatures:boolean

Service personnel can use this function to call an empty elevator and ride it nonstop to their destination floor. The user simply registers a call via a card swipe or PIN entry that is pre-programmed to grant access.

enableService2Features:boolean

Service personnel can use this function to call an empty elevator and ride it nonstop to their destination floor. The user simply registers a call via a card swipe or PIN entry that is pre-programmed to grant access.

enableService3Features:boolean

Service personnel can use this function to call an empty elevator and ride it nonstop to their destination floor. The user simply registers a call via a card swipe or PIN entry that is pre-programmed to grant access.

enableVipFeatures:boolean

VIP operation allows cardholders to swipe a card or enter a PIN to isolate the elevator and provide uninterrupted access to their designated floor.

##### Example

```json
{
  "href": "https://localhost:8904/api/cardholders/325/elevator_groups/567",
  "elevatorGroup": {
    "href": "https://localhost:8904/api/elevator_groups/635",
    "name": "Main building lower floors"
  },
  "accessZone": {
    "href": "https://localhost:8904/api/access_zones/637",
    "name": "Lvl 1 lift lobby"
  },
  "enableCaptureFeatures": true,
  "enableCodeBlueFeatures": false,
  "enableExpressFeatures": true,
  "enableServiceFeatures": false,
  "enableService2Features": true,
  "enableService3Features": true,
  "enableVipFeatures": false
}
```

---

## Cardholder changes

## Cardholder changes:

An array of cardholder changes, described in the next section, and a next link for more.

results:[Cardholder change](https://gallaghersecurity.github.io/cc-rest-docs/ref/cardholders.html#/definitions/Cardholder%20change)

An array of cardholder changes.

[Cardholder change](https://gallaghersecurity.github.io/cc-rest-docs/ref/cardholders.html#/definitions/Cardholder%20change)next:object

The link to the next page of changes. This will always be present, because (unlike items) changes never run out.

Because the next link is a pointer to the head of a queue of changes, and new changes are being added to that queue which will not suit your filter or privileges, it will change even when there are no results.

Therefore you should always use this link for your next query. Do not be tempted to re-use a URL after results comes back empty, thinking you merely need to ask the same question again. Doing that will cause the server unnecessary work, skipping over changes that did not pass your filter or privilege checks on the previous call.

href:string(url)

##### Example

```json
{
  "results": [
    {
      "href": "https://localhost:8904/api/cardholders/changes/f4e67a",
      "time": "2020-01-14T03:14:33Z",
      "type": "update",
      "item": {
        "href": "https://localhost:8904/api/cardholders/525"
      },
      "operator": {
        "name": "System Operator",
        "href": "https://localhost:8904/api/items/1"
      },
      "oldValues": {
        "firstName": "Craig",
        "competencies": [
          {
            "enablement": "",
            "href": "https://localhost:8904/api/cardholders/525/competencies/3910e4"
          }
        ]
      },
      "newValues": {
        "firstName": "Gavin",
        "competencies": [
          {
            "enablement": "2020-02-29T00:00:00Z",
            "href": "https://localhost:8904/api/cardholders/525/competencies/3910e4"
          }
        ],
        "cards": [
          {
            "number": "2",
            "cardSerialNumber": "",
            "issueLevel": 1,
            "from": "",
            "until": "",
            "href": "https://localhost:8904/api/cardholders/525/cards/285f779af1ef49abbba"
          }
        ],
        "accessGroups": [
          {
            "accessGroup": {
              "name": "Access Group 1",
              "href": "https://localhost:8904/api/access_groups/499"
            },
            "from": "",
            "until": "2020-01-15T04:39:00Z",
            "href": "https://localhost:8904/api/cardholders/525/access_groups/f9cb328b4"
          }
        ]
      },
      "cardholder": {
        "firstName": "Gavin",
        "cards": [
          {
            "number": "2",
            "cardSerialNumber": "",
            "issueLevel": 1,
            "from": "",
            "until": "",
            "href": "https://localhost:8904/api/cardholders/525/cards/285f779af1ef49abbba"
          }
        ]
      }
    }
  ],
  "next": {
    "href": "https://localhost:8904/api/cardholders/changes?pos=SWEp9"
  }
}
```

---

## Cardholder change

## Cardholder change:

/api/cardholders/changes returns an array of these. Each contains a description of a change made to a cardholder.

In this example a cardholder has had his name changed from Craig to Gavin, has had an enablement date set on a competency, and has had a card and access group membership added.

### Notes

- Changes to an access group membership will have a different href in the oldValues and newValues blocks, because modifying a group membership changes its ID.
- The API will not notify changes to a competency's credit integer or limitedCredit boolean. These fields are part of a separate Command Centre feature and are not supported by the changes API.
- The API will report a spurious change to a PDF value when a cardholder rejoins an access group he or she was previously a member of, provided that access group carried a PDF with no default and the cardholder did not have a value for it.
- This API will not notify changes to lastSuccessfulAccessTime or lastSuccessfulAccessZone .
href:string(url)

Command Centre's identifier for this change. This has no use in the API: you cannot use it as a URL, but you may like to use it to track the changes you have seen.

time:string(date-time)

The time that this change occurred.

type:stringadd,                         update,                         remove

'add' if this change added a cardholder, 'update' if it modified a cardholder, or 'remove' if it deleted a cardholder.

item:object

A block containing the href of the changed cardholder. You can GET this URL to find the cardholder's current state, or you could add the cardholder block to the change using fields=cardholder in the query. Note that you can add individual cardholder fields such as PDFs using fields=defaults,cardholder.personalDataFields .

operator:object

A block containing the href and current name of the operator who made this change.

Because building this block requires more work from the server it is not in the default field set. If you need it you must ask for it using the fields parameter: fields=defaults,operator .

oldValues:object

A block containing the values of the changed fields before the change, if Command Centre still has them, in the same format as a cardholder detail . If a value is blank, it means that the value was null before the change or the server no longer has it.

Because this requires extra effort from the server, you may like to omit this block using something like fields=time,type,item,operator unless you are particularly interested in historical data.

newValues:object

A similar block containing the values of the changed fields after the change, if the server has them.

Generally, this block is less useful than the current state of the cardholder, described next. It also requires extra effort from the server, so you may like to omit this block using fields .

cardholder:object

A block containing the current fields on the cardholder, provided the cardholder has not been deleted. This is in the same format as a cardholder detail .

Because building this requires more work from the server it is not in the default result set. If you need it you must ask for it using the fields parameter. While the server has a default field set for cardholders, your query will be more efficient if you ask for just the fields you need: fields=defaults,cardholder.firstName,cardholder.lastName,cardholder.cards , etc. Note how you must prefix each field with cardholder since they are all inside a block with that name.

##### Example

```json
{
  "href": "https://localhost:8904/api/cardholders/changes/f4e67a",
  "time": "2020-01-14T03:14:33Z",
  "type": "update",
  "item": {
    "href": "https://localhost:8904/api/cardholders/525"
  },
  "operator": {
    "name": "System Operator",
    "href": "https://localhost:8904/api/items/1"
  },
  "oldValues": {
    "firstName": "Craig",
    "competencies": [
      {
        "enablement": "",
        "href": "https://localhost:8904/api/cardholders/525/competencies/3910e4"
      }
    ]
  },
  "newValues": {
    "firstName": "Gavin",
    "competencies": [
      {
        "enablement": "2020-02-29T00:00:00Z",
        "href": "https://localhost:8904/api/cardholders/525/competencies/3910e4"
      }
    ],
    "cards": [
      {
        "number": "2",
        "cardSerialNumber": "",
        "issueLevel": 1,
        "from": "",
        "until": "",
        "href": "https://localhost:8904/api/cardholders/525/cards/285f779af1ef49abbba"
      }
    ],
    "accessGroups": [
      {
        "accessGroup": {
          "name": "Access Group 1",
          "href": "https://localhost:8904/api/access_groups/499"
        },
        "from": "",
        "until": "2020-01-15T04:39:00Z",
        "href": "https://localhost:8904/api/cardholders/525/access_groups/f9cb328b4"
      }
    ]
  },
  "cardholder": {
    "firstName": "Gavin",
    "cards": [
      {
        "number": "2",
        "cardSerialNumber": "",
        "issueLevel": 1,
        "from": "",
        "until": "",
        "href": "https://localhost:8904/api/cardholders/525/cards/285f779af1ef49abbba"
      }
    ]
  }
}
```

---

## Cardholder redaction

## Cardholder redaction:

These appear in an array in a cardholder object. Each describes a redaction scheduled for the cardholder.

A cardholder can have multiple event redactions pending, because they can operate on events from different periods, but since a cardholder redaction removes the cardholder item there can be only one.

href:string(url)

DELETE this URL to cancel this redaction.

DELETE is the only verb you can use on this URL. GET will always return a 404.

type:stringnormalEvents,                         cardholder

Whether this redaction is for cardholder events or cardholder information.

when:string(date-time)

When redaction is meant to happen. This should be in the future. If it is in the past, the service returns 400-Bad Request Invalid Start Time.

Optional. If it is absent, it means to do it asap.

before:string(date-time)

For event redactions, do not redact any events after this time. No effect on cardholder information redactions.

Optional.

status:stringpending,                         inProgress,                         cancelled,                         done,                         failed

The status of this redaction.

redactionOperator:object

A block containing the href and current name of the operator who scheduled the redaction.

##### Example

```json
{
  "href": "https://localhost:8904/api/cardholders/redactions/625",
  "type": "normalEvents",
  "when": "2023-01-01T00:00:00Z",
  "before": "2022-01-01T00:00:00Z",
  "status": "pending",
  "redactionOperator": {
    "name": "REST Operator",
    "href": "https://localhost:8904/api/items/100"
  }
}
```

---

## Access group search

## Access group search:

An array of access group summaries, described in the next section, and a next link for more.

results:[Access group summary](https://gallaghersecurity.github.io/cc-rest-docs/ref/cardholders.html#/definitions/Access%20group%20summary)

An array of access group summaries.

[Access group summary](https://gallaghersecurity.github.io/cc-rest-docs/ref/cardholders.html#/definitions/Access%20group%20summary)next:object

The link to the next page. Absent if you have retrieved them all.

href:string(url)

##### Example

```json
{
  "results": [
    {
      "id": "352",
      "href": "https://localhost:8904/api/access_groups/352",
      "name": "R&D special projects group.",
      "description": "Deep underground.",
      "parent": {
        "href": "https://localhost:8904/api/access_groups/100",
        "name": "All R&D"
      },
      "division": {
        "id": "2",
        "href": "https://localhost:8904/api/divisions/2"
      },
      "cardholders": {
        "href": "https://localhost:8904/api/access_groups/352/cardholders"
      },
      "serverDisplayName": "ruatoria.satellite.int"
    }
  ],
  "next": {
    "href": "https://localhost:8904/api/access_groups?skip=61320"
  }
}
```

---

## Access group summary

## Access group summary:

The access group search at /api/access_groups returns an array of these, and /api/access_groups/{id} (linked as the href in this object) returns one with more fields.

id:string

An alphanumeric identifier for this access group. No API calls use access group IDs.

href:string(url)

A link to an access group detail object for this access group.

name:stringdescription:stringparent:object

A link to the group's parent, and its name.

division:object

The division that contains this access group.

cardholders:object

Following this link lists the group's direct memberships .

In v8.00 you will receive this field along with the ID and href in an access group's details page whether or not you specified it in the fields parameter, but if you send the fields parameter to 8.10 you will only get what you asked for.

serverDisplayName:string

If you are running a multi-server installation and this item is homed on a remote server, this field will contain the name of that server. This field is missing from items that are held on the machine that served the API request. Added in 8.40.

This is a read-only field. The server will ignore it if you send it.

##### Example

```json
{
  "id": "352",
  "href": "https://localhost:8904/api/access_groups/352",
  "name": "R&D special projects group.",
  "description": "Deep underground.",
  "parent": {
    "href": "https://localhost:8904/api/access_groups/100",
    "name": "All R&D"
  },
  "division": {
    "id": "2",
    "href": "https://localhost:8904/api/divisions/2"
  },
  "cardholders": {
    "href": "https://localhost:8904/api/access_groups/352/cardholders"
  },
  "serverDisplayName": "ruatoria.satellite.int"
}
```

---

## Access group detail

## Access group detail:

/api/access_groups/{id} returns one of these. In addition to the basic details, it lists the child groups, privileges, and access zones and Salto items to which the group grants access.

There are brief descriptions of those fields below. If they fall short the Configuration Client's online documentation is the authority, in particular the section 'Setting up Access Groups'.

[Access group summary](https://gallaghersecurity.github.io/cc-rest-docs/ref/cardholders.html#/definitions/Access%20group%20summary)
children:object[]

Names and links for the groups that claim this one as a parent. This array does not include the childrens' children.

Notice of breaking change . This field being present and empty when the group has no children is a break from the API's principle of omitting empty fields, and is therefore a bug. A future version of Command Centre will not return the children array if the group has no children.

objecthref:string(url)

The href of the child access group.

name:string

The name of the child access group.

personalDataDefinitions:object[]

The names and hrefs of the PDFs that this access group gives to its members.

objectname:string

Read-only.

href:string(url)visitor:boolean

If true, members of an access group with the 'escortVisitors' privilege can escort members of this group through a door, provided both groups have access to the entry zone.

A group with 'visitor' cannot also have 'escortVisitors', 'lockUnlockAccessZones', or 'firstCardUnlock', because visitors are not allowed to do those things.

New to 8.40.

escortVisitors:boolean

If true, members of this group can escort members of a group with the 'visitor' privilege through a door, provided both groups have access to the entry zone.

A group cannot have both 'escortVisitors' and 'visitor'.

New to 8.40.

lockUnlockAccessZones:boolean

If true, members of this group can use a reader or terminal to change the access mode of this group's access zones. They can do this by logging on, if the reader has a screen and keypad, or by double-badging.

A group cannot have both 'lockUnlockAccessZones' and 'visitor'.

New to 8.40.

enterDuringLockdown:boolean

If true, members of this group are not subject to lockdown restrictions when requesting to enter its access zones.

New to 8.40.

firstCardUnlock:boolean

If true, members of this group will change an access zone from secure to free when entering it, unlocking all its doors.

A group cannot have both 'firstCardUnlock' and 'visitor'.

New to 8.40.

overrideAperioPrivacy:boolean

Some Aperio locks have a 'privacy mode' button that room occupants can push when they do not want anyone else coming in. If this field is true, Aperio locks will ignore that button when members of this group attempt to open them.

This field will not appear if your site is not licensed for Aperio.

New to 8.40.

aperioOfflineAccess:boolean

Aperio locks normally refuse to open when offline. If this field is true, Aperio locks that support it will make an exception for members of this group.

Obviously the lock needs to be online long enough to synchronise this setting and the members of the group before setting it will have an effect.

This field will not appear if your site is not licensed for Aperio.

New to 8.40.

disarmAlarmZones:boolean

If true, members of this group can use a reader or terminal to disarm the group's access zones' alarm zones, either by logging on or double-badging.

New to 8.40.

armAlarmZones:boolean

If true, members of this group can use a reader or terminal to arm the group's access zones' alarm zones.

New to 8.40.

hvLfFenceZones:boolean

If true, members of this group can use a reader or terminal to change the group's fence zones between 'high voltage' and 'low feel', which will in turn change the exuberance of their energisers. A group's fence zones are those that use the same alarm zones as the group's access zones.

New to 8.40.

viewAlarms:boolean

If true, members of this group can view alarms and inputs on remote arming terminals ("RATs") and HBUS terminals.

New to 8.40.

shunt:boolean

If true, members of this group can shunt (isolate) items using RATs and HBUS terminals.

New to 8.40.

lockOutFenceZones:boolean

If true, members of this group can lock out (de-energise, make safe) fence zones using RATs and HBUS terminals. Like all other access group privileges, this only works on the fence zones to which this group has access.

New to 8.40.

cancelFenceZoneLockout:boolean

Normally, only the cardholder who locked out a fence zone can cancel the lockout and re-energise the fence. With this privilege, members of this group can cancel any lockout on the group's fence zones.

New to 8.40.

ackAll:boolean

If true, members of this group can acknowledge alarms at a RAT or HBUS terminal.

An access group cannot have both this privilege and 'ackBelowHigh'.

New to 8.40.

ackBelowHigh:boolean

If true, members of this group can acknowledge alarms at a RAT or HBUS terminal, provided the alarms are not at high, very high, or critical priority.

An access group cannot have both this privilege and 'ackAll'.

New to 8.40.

selectAlarmZone:boolean

If true, members of this group can choose from a list of the group's alarm zones when performing overrides at RATs and HBUS terminals, rather than having it chosen for them by site configuration.

A group can only have this privilege if it also has 'disarmAlarmZones' or 'armAlarmZones'. Without one of those there is no point in being able to select an alarm zone.

New to 8.40.

armWhileAlarm:boolean

Normally, a cardholder cannot arm an alarm zone if it has open, unshunted, inputs. With this privilege, members of the group can force-arm the alarm zone from a RAT or HBUS terminal. What happens then depends on an alarm zone setting.

A group with this privilege will also have 'armAlarmZones' and will not have 'armWhileActiveAlarm'.

New to 8.40.

armWhileActiveAlarm:boolean

Normally, a cardholder cannot arm an alarm zone when it has active alarms. Members of a group with this privilege can do so from an HBUS terminal, provided they also meet other criteria (detailed in the Configuration Client documentation).

A group with this privilege will also have have 'armAlarmZones' and will not have 'armWhileAlarm'.

New to 8.40.

isolateAlarmZones:boolean

Members of a group with this privilege have the option of isolating open inputs from a RAT or HBUS terminal when they are preventing an alarm zone from arming. Like all these privileges, it only works for the alarm zones on the group's access zones.

To have this privilege, a group must also have 'armAlarmZones'.

New to 8.40.

access:object[]

Names and hrefs of the access zones to which this access group gives access, and the schedules that govern it.

Your operator needs 'View Schedules' to see schedule hrefs, and 'View Site', 'Edit Site', or 'Override' to see access zone hrefs.

New to 8.40.

objectaccessZone:objecthref:string(url)name:string

Read-only.

schedule:objecthref:string(url)name:string

Read-only.

saltoAccess:object[]

Types, names, and hrefs of the Salto doors and door groups ("Salto Access Zones") to which this access group gives access, and the schedules that govern it.

Watch those definitions: a 'Salto Access Zone' is a group of Salto doors, while Command Centre's definition of an access zone is a space into which a cardholder moves after passing through a door.

Therefore if an access group gives access to a Salto Access Zone, it is giving access through any number of Salto doors. If you don't have access to the Salto system itself you can see the Salto zone/door hierarchy in the Command Centre Configuration client.

New to 8.40.

object

Each element in the array contains three blocks: the Salto item type, the item itself, and the controlling schedule.

saltoItemType:object

This block tells you whether the Salto item is a Salto door or a Salto 'access zone'.

value:stringsaltoAccessZone,                                             saltoDoorsaltoItem:object

The name and href of the Salto zone or Salto door to which this access group gives access.

alarmZones:object[]

Names and hrefs of the alarm zones to which members of this access group have the 20-odd management privileges listed above.

Each connection between the access group and the alarm zone is in its own block called alarmZone to leave room for the addition of more fields later.

Added in 8.40.

objectalarmZone:objecthref:string(url)name:string

Read-only.

##### Example

```json
{
  "id": "352",
  "href": "https://localhost:8904/api/access_groups/352",
  "name": "R&D special projects group.",
  "description": "Deep underground.",
  "parent": {
    "href": "https://localhost:8904/api/access_groups/100",
    "name": "All R&D"
  },
  "division": {
    "id": "2",
    "href": "https://localhost:8904/api/divisions/2"
  },
  "cardholders": {
    "href": "https://localhost:8904/api/access_groups/352/cardholders"
  },
  "serverDisplayName": "ruatoria.satellite.int",
  "children": [
    {
      "href": "https://localhost:8904/api/access_groups/5122",
      "name": "R&D super-special projects"
    },
    {
      "href": "https://localhost:8904/api/access_groups/3420",
      "name": "R&D social committee"
    }
  ],
  "personalDataDefinitions": [
    {
      "name": "email",
      "href": "https://localhost:8904/api/personal_data_fields/5516"
    },
    {
      "name": "cellphone",
      "href": "https://localhost:8904/api/personal_data_fields/9998"
    }
  ],
  "visitor": false,
  "escortVisitors": false,
  "lockUnlockAccessZones": false,
  "enterDuringLockdown": false,
  "firstCardUnlock": false,
  "overrideAperioPrivacy": false,
  "aperioOfflineAccess": false,
  "disarmAlarmZones": false,
  "armAlarmZones": false,
  "hvLfFenceZones": false,
  "viewAlarms": false,
  "shunt": false,
  "lockOutFenceZones": false,
  "cancelFenceZoneLockout": false,
  "ackAll": false,
  "ackBelowHigh": false,
  "selectAlarmZone": false,
  "armWhileAlarm": false,
  "armWhileActiveAlarm": false,
  "isolateAlarmZones": false,
  "access": [
    {
      "accessZone": {
        "href": "https://localhost:8904/api/access_zones/333",
        "name": "Twilight zone"
      },
      "schedule": {
        "href": "https://localhost:8904/api/schedules/5",
        "name": "Default Cardholder Access Granted"
      }
    },
    {
      "accessZone": {
        "href": "https://localhost:8904/api/access_zones/412",
        "name": "Server room"
      },
      "schedule": {
        "href": "https://localhost:8904/api/schedules/557",
        "name": "8am-5pm weekdays"
      }
    }
  ],
  "saltoAccess": [
    {
      "saltoItemType": {
        "value": "saltoAccessZone"
      },
      "saltoItem": {
        "href": "https://localhost:8904/api/items/570",
        "name": "Salto BLE CV19"
      },
      "schedule": {
        "href": "https://localhost:8904/api/schedules/5",
        "name": "Default Cardholder Access Granted"
      }
    },
    {
      "saltoItemType": {
        "value": "saltoDoor"
      },
      "saltoItem": {
        "href": "https://localhost:8904/api/items/579",
        "name": "Salto CU5000"
      },
      "schedule": {
        "href": "https://localhost:8904/api/schedules/557",
        "name": "8am-5pm weekdays"
      }
    }
  ],
  "alarmZones": [
    {
      "alarmZone": {
        "href": "https://localhost:8904/api/alarm_zones/328",
        "name": "Roswell building 2 lobby alarms"
      }
    },
    {
      "alarmZone": {
        "href": "https://localhost:8904/api/alarm_zones/10138",
        "name": "Roswell building 3 lobby alarms"
      }
    }
  ]
}
```

---

## Access group POST example

## Access group POST example:

DRAFT DOCUMENTATION

This is an example of a POST you could use to create an access group, containing examples of all the settable fields.

name:string

All items have a name.

description:stringdivision:object

Mandatory when creating any access group. In this example, we want the access group in division 352.

notes:stringparent:object

A link to the group's parent.

This field is optional in a POST because unlike divisions, which must have a parent, an access group can stand alone. But should you wish it to be a member of another, link it here.

An access group may be a member of only one other: multiple inheritance is not possible.

Use the blank string "" to clear a group's parent in a PATCH.

membershipAutoRemoveExpired:boolean

If true, memberships will be removed when their until time passes. If false, they will remain on the cardholder, inactive.

membershipFromDefault:string(date-time)

When a cardholder is granted membership of this access group without specifying a 'from' time, this default will apply. If left blank, such a membership will not have a 'from' time (meaning it will be effective immediately, provided its 'until' time is blank or in the future).

To clear it, send the blank string "" .

membershipUntilDefault:string(date-time)

When a cardholder is granted membership of this access group without specifying an 'until' time, this default will apply. If blank, such a membership will not have an 'until' time (meaning it will be effective until removed).

To clear it, send the blank string "" .

personalDataDefinitions:object[]

This is where you give PDFs to cardholders. All the members of this access group will be able to have values for the PDFs you list in this array.

Each element should contain the href of a PDF definition. You can take those from the PDFs controller or other API routes, including the personalDataDefinitions block of another access group.

object

##### Example

```json
{
  "name": "R&D special projects group.",
  "description": "Deep underground.",
  "division": {
    "href": "https://localhost:8904/api/divisions/352"
  },
  "notes": "",
  "parent": {
    "href": "https://localhost:8904/api/access_groups/100"
  },
  "membershipAutoRemoveExpired": true,
  "membershipFromDefault": "2025-01-01T00:00:00Z",
  "membershipUntilDefault": "2025-01-01T00:00:00Z",
  "personalDataDefinitions": [
    {
      "href": "https://localhost:8904/api/personal_data_fields/5516"
    },
    {
      "href": "https://localhost:8904/api/personal_data_fields/9370"
    }
  ]
}
```

---

## Access group PATCH example

## Access group PATCH example:

DRAFT DOCUMENTATION

The body you would send to an access group PATCH has the same schema as an access group POST apart from the personalDataDefinitions block.

personalDataDefinitions:object

This object can contain two arrays named add and remove . Every element you put in those arrays should contain a string called href giving the href of a PDF that you want to add to or remove from the access group.

In this example we are adding PDF 5516 and removing 9370.

##### Example

```json
{
  "personalDataDefinitions": {
    "add": [
      {
        "href": "https://localhost:8904/api/personal_data_fields/5516"
      }
    ],
    "remove": [
      {
        "href": "https://localhost:8904/api/personal_data_fields/9370"
      }
    ]
  }
}
```

---

## Access group membership

## Access group membership:

Returned in an array by /api/access_groups/{id}/cardholders , containing cardholders who are direct members of a particular group. The array does not contain the group's child groups, or their cardholder members.

Each item contains a cardholder and (possibly) two date-times. The group membership is active if and only if the current time is between 'from' and 'until'. If 'from' is absent, assume the distant past. If 'until' is absent, assume the far future.

Use the href in the cardholder block to change the 'from' and 'until', or even the group, using cardholder patch .

Each also contains an href at the top level: DELETE that to remove the membership.

href:string(url)

DELETE this URL to remove the membership. Do not specify this when creating or modifying a cardholder.

DELETE is the only verb you can use on this URL. GET will always return a 404.

cardholder:object

The name and href of the member cardholder.

from:string(time-stamp)until:string(time-stamp)

##### Example

```json
{
  "href": "https://localhost:8904/api/cardholders/325/access_groups/D714D8A894724F",
  "cardholder": {
    "name": "Boothroyd, Algernon",
    "href": "https://localhost:8904/api/cardholders/325"
  },
  "from": "2017-01-01T00:00:00Z",
  "until": "2017-12-31T11:59:59Z"
}
```

---

## Card type search

## Card type search:

An array of card types, and a next link for more. Sites generally have only a handful of card types, so when retrieving them you should set your 'top' parameter high enough that you do not need the 'next' link.

results:[Card type](https://gallaghersecurity.github.io/cc-rest-docs/ref/cardholders.html#/definitions/Card%20type)

An array of card types.

[Card type](https://gallaghersecurity.github.io/cc-rest-docs/ref/cardholders.html#/definitions/Card%20type)next:object

The link to the next page. Absent if you have retrieved them all.

href:string(url)

##### Example

```json
{
  "results": [
    {
      "href": "https://localhost:8904/api/card_types/600",
      "id": "600",
      "name": "Red DESFire visitor badge",
      "division": {
        "href": "https://localhost:8904/api/divisions/2"
      },
      "notes": "Disabled after 7d inactivity, 6-char PIN",
      "facilityCode": "A12345",
      "availableCardStates": [
        "Active",
        "Disabled (manually)",
        "Lost",
        "Stolen",
        "Damaged"
      ],
      "credentialClass": "card",
      "minimumNumber": "1",
      "maximumNumber": "16777215",
      "serverDisplayName": "ruatoria.satellite.int",
      "regex": "^[A-Za-z0-9]+$",
      "regexDescription": "Only alphanumeric characters"
    }
  ],
  "next": {
    "href": "https://localhost:8904/api/card_types/assign?skip=1000"
  }
}
```

---

## Card type

## Card type:

This object describes a single Card Type. "Credential type" would be a better name, as it includes mobile credentials.

href:string(url)

This is the identifier to use when assigning a card to a cardholder in a cardholder PATCH or POST .

This is also the URL for the card type's detail page. GETting it will return you a 404 if you do not have 'View site' or 'Configure site' on the card type's division.

It is server-generated, and therefore read-only.

id:string

The API does not use this field. Nor should you.

name:stringdivision:object

The division that contains this card type. Required when creating a card type.

New to 8.50.

notes:string

Free text.

Because of its potential size, the server does not return the notes field by default. You need to ask for it with fields=notes .

facilityCode:string

A facility code is a letter (A-P) followed by up to five digits. It is encoded onto cards so that they only work at sites with the correct facility code.

PIV cards, PIV-I cards, and mobile credentials do not have a facility code.

Most credential classes require a facility code on creation. But it cannot be changed once set, so this field is ignored in a PATCH.

availableCardStates:string[]

All credential types have a set of card states.

If you need this, ask for it using the fields parameter.

This field is read-only: it is derived from the card type's card state set (also known as a workflow). If you send it in a POST or a PATCH, the server will ignore it.

stringActive,                           Disabled (manually),                           Lost,                           Stolen,                           DamagedcredentialClass:stringpiv,                         pivi,                         card,                         mobile,                         digitalId,                         govPass,                         trackingTag,                         transact

Required when creating a new card type but ignored when modifying one since a credential's type cannot be changed once set.

minimumNumber:string

For card types with integer card numbers, this is the minimum. Must be non-negative.

maximumNumber:string

For card types with integer card numbers, this is the maximum. Must be non-negative.

serverDisplayName:string

If you are running a multi-server installation and this item is homed on a remote server, this field will contain the name of that server. This field is missing from items that are held on the machine that served the API request. Added in 8.40.

This is a read-only field. The server will ignore it if you send it.

regex:string

This is the regular expression that a text card number must match before Command Centre will accept it.

regexDescription:string

Regular expressions often need explaining to your users.

##### Example

```json
{
  "href": "https://localhost:8904/api/card_types/600",
  "id": "600",
  "name": "Red DESFire visitor badge",
  "division": {
    "href": "https://localhost:8904/api/divisions/2"
  },
  "notes": "Disabled after 7d inactivity, 6-char PIN",
  "facilityCode": "A12345",
  "availableCardStates": [
    "Active",
    "Disabled (manually)",
    "Lost",
    "Stolen",
    "Damaged"
  ],
  "credentialClass": "card",
  "minimumNumber": "1",
  "maximumNumber": "16777215",
  "serverDisplayName": "ruatoria.satellite.int",
  "regex": "^[A-Za-z0-9]+$",
  "regexDescription": "Only alphanumeric characters"
}
```

---

## Locker bank search

## Locker bank search:

An array of locker bank summaries, and a next link for more.

results:[Locker bank summary](https://gallaghersecurity.github.io/cc-rest-docs/ref/cardholders.html#/definitions/Locker%20bank%20summary)

An array of locker summaries.

[Locker bank summary](https://gallaghersecurity.github.io/cc-rest-docs/ref/cardholders.html#/definitions/Locker%20bank%20summary)next:object

The link to the next page of results. Absent if you have retrieved them all.

href:string(url)

##### Example

```json
{
  "results": [
    {
      "name": "Lobby",
      "href": "https://localhost:8904/api/locker_banks/4566",
      "description": "Behind reception",
      "division": {
        "id": "2",
        "href": "https://localhost:8904/api/divisions/2"
      }
    },
    {
      "name": "Bank A",
      "href": "https://localhost:8904/api/locker_banks/4567",
      "description": "Level 4 east A",
      "division": {
        "id": "2",
        "href": "https://localhost:8904/api/divisions/2"
      }
    }
  ],
  "next": {
    "href": "https://localhost:8904/api/locker_banks?skip=2"
  }
}
```

---

## Locker bank summary

## Locker bank summary:

The locker bank search at /api/locker_banks returns an array of these. It is a subset of what you get from a locker bank's detail page at /api/locker_banks/{id} (linked as the href in this object).

Like the cardholder and access group summary pages in this API, this contains only the basic information about a locker bank. It is the result of a search and could return many items, and we did not want the size getting out of hand.

Also like the other summary pages in this API, you can add all the fields you want using the fields query parameter to the API routes that return it.

The most important field is the href to the detail page, covered next.

href:string(url)

A link to the detail page for this locker bank.

name:stringshortName:string

The bank's short name.

description:stringdivision:object

The division containing this locker bank.

##### Example

```json
{
  "href": "https://localhost:8904/api/locker_banks/4566",
  "name": "Lobby, building 28",
  "shortName": "Lobby",
  "description": "Behind reception",
  "division": {
    "id": "2",
    "href": "https://localhost:8904/api/divisions/2"
  }
}
```

---

## Locker bank detail

## Locker bank detail:

/api/locker_banks/{id} returns one of these. It contains an array of all the lockers in the bank, each of which contains an array of the cardholders assigned to that locker.

This example shows two lockers on the bank, called 'L1' and 'L2'. L1 has two cardholders who can open it: one for two years, the other for a week in April 2018.

href:string(url)

A self-reference.

name:stringshortName:stringdescription:stringdivision:object

The division containing this locker bank.

notes:stringconnectedController:object

This block describes this item's hardware controller.

Retrieving it takes a little more time than the other fields so only ask for it if you need it.

Added in 8.50.

name:stringhref:string(url)

This is the REST API's identifier for the hardware controller. It is only an identifier, not a usable URL, because there is no interface for hardware controllers. GETting the URL will return a 404.

id:string

An alphanumeric identifier, unique to the server. This is the ID to use in the source parameter of event filters .

lockers:object[]

An array of locker objects, each containing the details of the locker and the cardholders who can open it.

objectname:stringshortName:string

A T20 will display this instead of the long name, if it has both.

description:stringhref:string(url)

This is the href of the locker. Use it when creating new assignments in a cardholder PATCH or POST. Trying to GET it will always yield a 404.

connectedController:object

This block describes this item's hardware controller. Retrieving it takes a little more time than the other fields so only ask for it if you need it.

Added in 8.50.

assignments:object[]

A locker can have many assignments. Each contains a cardholder and up to two dates, between which the cardholder can open the locker. If either of the dates is missing, the assignment is unbounded in that direction.

objecthref:string(url)

This is the href of a cardholder's assignment to a locker. Put it in a cardholder PATCH to update or delete it.

You can also revoke a cardholder's access to this locker by sending a DELETE to this href.

If the site does not have the RESTCardholders licence or your operator does not have the privilege to view the cardholder, this link will not work in a DELETE or a cardholder PATCH.

cardholder:object

The name and href of the cardholder assigned to this locker. It will be missing if the site does not have the RESTCardholders licence or your operator does not have the privilege to view that cardholder.

from:string(date-time)

If missing, the locker assignment has no start time, meaning the cardholder can open the locker provided the 'until' time is in the future.

until:string(date-time)

If missing, the locker assignment has no end time, meaning the cardholder can open the locker provided the 'from' time is in the past.

commands:object

Overrideable items return one of these blocks if your operator has the right privilege and the locker is fit to be overridden. It is a list of commands, each represented by a block containing an href that accepts a POST that will send an override to the locker, changing its state.

open:objecthref:string(url)

POST to this URL to send an open override to the locker. The server will ignore the POST body.

quarantine:objecthref:string(url)

POST to this URL to send a quarantine override to the locker. Do not send a body with the POST since some server versions use the same POST for quarantineUntil , which takes a time from the body.

quarantineUntil:objecthref:string(url)

POST to this URL to send an override to the locker that will quarantine it until a timestamp you send in the body .

cancelQuarantine:objecthref:string(url)

POST to this URL to send a quarantine cancel override to the locker. The server will ignore the POST body.

updates:object

This is not a default field: you must ask for it by adding lockers.updates to the fields query parameter. Since it can only monitor one locker at a time we discourage its use in favour of bulk status monitoring .

##### Example

```json
{
  "name": "Lobby",
  "href": "https://localhost:8904/api/locker_banks/4566",
  "description": "Behind reception",
  "division": {
    "id": "2",
    "href": "https://localhost:8904/api/divisions/2"
  },
  "lockers": [
    {
      "name": "Lobby locker 1",
      "shortName": "L1",
      "description": "Wheelchair-suitable",
      "href": "https://localhost:8904/api/lockers/3456",
      "assignments": [
        {
          "href": "https://localhost:8904/api/cardholders/325/lockers/abe3456e",
          "cardholder": {
            "name": "Boothroyd, Algernon",
            "href": "https://localhost:8904/api/cardholders/325"
          },
          "from": "2018-01-01T00:00:00Z",
          "until": "2020-01-01T00:00:00Z"
        },
        {
          "href": "https://localhost:8904/api/cardholders/10135/lockers/deb9456f",
          "cardholder": {
            "name": "Messervy, Miles",
            "href": "https://localhost:8904/api/cardholders/10135"
          },
          "from": "2018-04-01T05:00:00Z",
          "until": "2018-04-07T00:00:00Z"
        }
      ],
      "commands": {
        "open": {
          "href": "https://localhost:8904/api/lockers/3456/open"
        },
        "quarantine": {
          "href": "https://localhost:8904/api/lockers/3456/quarantine"
        },
        "quarantineUntil": {
          "href": "https://localhost:8904/api/lockers/3456/quarantine"
        },
        "cancelQuarantine": {
          "href": "https://localhost:8904/api/lockers/3456/cancel_quarantine"
        }
      }
    },
    {
      "name": "Lobby locker 2",
      "shortName": "L2",
      "description": "Faulty USB charging port",
      "href": "https://localhost:8904/api/lockers/3457",
      "assignments": [
        {
          "cardholder": {
            "name": "R",
            "href": "https://localhost:8904/api/cardholders/10136"
          },
          "from": "1999-11-08T00:00:00Z",
          "until": "2002-11-20T00:00:00Z"
        }
      ],
      "commands": {
        "open": {
          "href": "https://localhost:8904/api/lockers/3457/open"
        },
        "quarantine": {
          "href": "https://localhost:8904/api/lockers/3457/quarantine"
        },
        "quarantineUntil": {
          "href": "https://localhost:8904/api/lockers/3457/quarantine"
        },
        "cancelQuarantine": {
          "href": "https://localhost:8904/api/lockers/3457/cancel_quarantine"
        }
      }
    }
  ]
}
```

---

## Locker detail

## Locker detail:

/api/lockers/{id} returns one of these. It contains some basic data about a locker, its assignments (the cardholders who can open it), and a link to override it open.

href:string(url)

A self-reference.

name:stringshortName:stringdescription:stringdivision:object

The division containing this locker.

notes:string

The notes field is not in the default result set. You must ask for it using fields .

connectedController:object

This block describes this item's hardware controller.

Retrieving it takes a little more time than the other fields so only ask for it if you need it.

Added in 8.50.

name:stringhref:string(url)

This is the REST API's identifier for the hardware controller. It is only an identifier, not a usable URL, because there is no interface for hardware controllers. GETting the URL will return a 404.

id:string

An alphanumeric identifier, unique to the server. This is the ID to use in the source parameter of event filters .

assignments:object[]

A locker can have many assignments. Each contains a cardholder and up to two dates, between which the cardholder can open the locker. If either of the dates is missing, the assignment is unbounded in that direction.

objecthref:string(url)

This is the href of a cardholder's assignment to a locker. Put it in a cardholder PATCH to update or delete it.

You can also revoke a cardholder's access to this locker by sending a DELETE to this href.

If the site does not have the RESTCardholders licence or your operator does not have the privilege to view the cardholder, this link will not work in a DELETE or a cardholder PATCH.

cardholder:object

The name and href of the cardholder assigned to this locker. It will be missing if the site does not have the RESTCardholders licence or your operator does not have the privilege to view that cardholder.

from:string(date-time)

If missing, the locker assignment has no start time, meaning the cardholder can open the locker while the 'until' time is in the future.

until:string(date-time)

If missing, the locker assignment has no end time, meaning the cardholder can open the locker after the 'from' time.

commands:object

Overrideable items return one of these blocks if your operator has the right privilege and the locker is fit to be overridden. It is a list of commands, each represented by a block containing an href that accepts a POST that will send an override to the locker, changing its state.

open:objecthref:string(url)

POST to this URL to send an open override to the locker. The server will ignore the POST body.

quarantine:objecthref:string(url)

POST to this URL to send a quarantine override to the locker. Do not send a body with the POST since some server versions use the same POST for quarantineUntil , which takes a time from the body.

quarantineUntil:objecthref:string(url)

POST to this URL to send an override to the locker that will quarantine it until a timestamp you send in the body .

cancelQuarantine:objecthref:string(url)

POST to this URL to send a quarantine cancel override to the locker. The server will ignore the POST body.

updates:object

Follow the URL in the href inside this block to receive the locker's current status, then follow the next link in the results to long poll for changes to that status.

updates was added to the lockers controller in 9.10 for consistency with other controllers, but we discourage its use in favour of bulk status monitoring .

Although a locker's status includes 'free' or 'allocated', allocating or un-allocating a locker does not cause the API to report a change of status. Opening, closing, quarantining, and unquarantining a locker do.

href:string(url)

##### Example

```json
{
  "href": "https://localhost:8904/api/lockers/3456",
  "name": "Lobby locker 1",
  "shortName": "L1",
  "description": "Wheelchair-suitable",
  "division": {
    "id": "2",
    "href": "https://localhost:8904/api/divisions/2"
  },
  "notes": "string",
  "connectedController": {
    "name": "Third floor C6000",
    "href": "https://localhost:8904/api/items/508",
    "id": "634"
  },
  "assignments": [
    {
      "href": "https://localhost:8904/api/cardholders/325/lockers/abe3456e",
      "cardholder": {
        "name": "Boothroyd, Algernon",
        "href": "https://localhost:8904/api/cardholders/325"
      },
      "from": "2018-01-01T00:00:00Z",
      "until": "2020-01-01T00:00:00Z"
    },
    {
      "href": "https://localhost:8904/api/cardholders/10135/lockers/deb9456f",
      "cardholder": {
        "name": "Messervy, Miles",
        "href": "https://localhost:8904/api/cardholders/10135"
      },
      "from": "2018-04-01T05:00:00Z",
      "until": "2018-04-07T00:00:00Z"
    }
  ],
  "commands": {
    "open": {
      "href": "https://localhost:8904/api/lockers/3456/open"
    },
    "quarantine": {
      "href": "https://localhost:8904/api/lockers/3456/quarantine"
    },
    "quarantineUntil": {
      "href": "https://localhost:8904/api/lockers/3456/quarantine"
    },
    "cancelQuarantine": {
      "href": "https://localhost:8904/api/lockers/3456/cancel_quarantine"
    }
  },
  "updates": {
    "href": "https://localhost:8904/api/lockers/3456/updates"
  }
}
```

---

## Competency PATCH and POST example: object

## Competency PATCH and POST example: object

This is an example of a PATCH you could use to update a competency, and a POST you could use to create one.

No fields are mandatory in a PATCH, but when using a POST to create a competency you must supply a division.

name:string

The new item's name. If you supply a name and another item of the same type already exists with that name, the call will fail. If you leave it blank in a POST, Command Centre will pick a name for you.

shortName:string (up to 16 chars)

If you supply a string that is too long, Command Centre will truncate it.

description:string

The new item's description.

division:object

The division to contain this competency.

notes:string

A string, able to me much longer than description , suitable for holding notes about the item.

##### Example

```json
{
  "name": "New competency",
  "shortName": "C4",
  "description": "Translated automatically.",
  "division": {
    "href": "https://localhost:8904/api/divisions/2"
  },
  "notes": "A very long string."
}
```

---

## Competency search

## Competency search:

An array of competency summaries, and a next link for more.

results:[Competency summary](https://gallaghersecurity.github.io/cc-rest-docs/ref/cardholders.html#/definitions/Competency%20summary)

An array of competency summaries.

[Competency summary](https://gallaghersecurity.github.io/cc-rest-docs/ref/cardholders.html#/definitions/Competency%20summary)next:object

The link to the next page. Absent if you have retrieved them all.

href:string(url)

##### Example

```json
{
  "results": [
    {
      "href": "https://localhost:8904/api/competencies/2354",
      "id": "2354",
      "name": "Hazardous goods handling",
      "description": "Required for access to chem sheds.",
      "serverDisplayName": "ruatoria.satellite.net",
      "notes": ""
    }
  ],
  "next": {
    "href": "https://localhost:8904/api/competencies?skip=1000"
  }
}
```

---

## Competency summary

## Competency summary:

The competency search at /api/competencies returns an array of these. The object contains some of what you get from a competency's detail page at /api/competencies/{id} , linked as the href in this object.

href:string(url)

This is the identifier to use when assigning a competency to a cardholder in a cardholder PATCH or POST .

id:stringname:stringdescription:stringserverDisplayName:stringnotes:string

##### Example

```json
{
  "href": "https://localhost:8904/api/competencies/2354",
  "id": "2354",
  "name": "Hazardous goods handling",
  "description": "Required for access to chem sheds.",
  "serverDisplayName": "ruatoria.satellite.net",
  "notes": ""
}
```

---

## Competency detail

## Competency detail:

/api/competencies/{id} returns one of these. It contains the same fields you see when you view or edit a competency in the Configuration client.

href:string(url)id:stringname:stringdescription:stringserverDisplayName:stringdivision:object

The division containing this competency.

notes:stringshortName:stringexpiryNotify:boolean

Set if notifications should go out before a cardholder loses this competency. How long before is in the 'noticePeriod' block. Who the notification goes to, in addition to the cardholder holding the competency, depends on the cardholder's relationships and what notifications are set on the relationship's role.

noticePeriod:object

How long before expiry Command Centre should send its notification and display 'expiry due' messages on display devices such as a T20 and through this API.

A zero-length notice period means there will be no warning notification.

units:stringdays,                               weeks,                               months,                               yearsnumber:integer0 ≤ x ≤ 9990defaultExpiry:object

In this block, 'expiryType' and 'expiryValue' tell you the expiry time Command Centre will use if the operator does not specify one when assigning this competency to a cardholder. It can be unset (meaning the assignment will be unending), a fixed date, or a time period.

In this example, cardholders will hold the 'Hazardous goods handling' competency for six months after an operator first gives it to them. In practice, the operator should enter the closest end date of the qualifications that allow them to handle hazardous goods. Safety and first aid training, presumably.

expiryType:stringnone,                               durationdays,                               durationweeks,                               durationmonths,                               durationyears,                               datenone

If 'none', there is no default expiry.

expiryValue:object

This field will be missing if 'expiryType' is 'none', a string containing a date-time if 'expiryType' is 'date', or an unquoted integer between zero and 999 otherwise.

Zero is a valid value. Because durations are always rounded up to shortly before midnight, an expiryValue of zero means that the competency will be active for the rest of the day on which the operator grants it.

defaultAccess:stringnoAccess,                         readOnly,                         fullAccessfullAccess

This is the access that operators will have to cardholders' assignments of this competency if the operator is not a member of an operator group that overrides it. Check the operator group's 'Competency' tab in the Configuration Client.

##### Example

```json
{
  "href": "https://localhost:8904/api/competencies/2354",
  "id": "2354",
  "name": "Hazardous goods handling",
  "description": "Required for access to chem sheds.",
  "serverDisplayName": "ruatoria.satellite.net",
  "division": {
    "id": "2",
    "href": "https://localhost:8904/api/divisions/2"
  },
  "notes": "",
  "shortName": "",
  "expiryNotify": false,
  "noticePeriod": {
    "units": "weeks",
    "number": 2
  },
  "defaultExpiry": {
    "expiryType": "durationmonths",
    "expiryValue": 6
  },
  "defaultAccess": "fullAccess"
}
```

---

## Operator group search

## Operator group search:

An array of operator group summaries, described in the next section, and a next link for more.

results:[Operator group summary](https://gallaghersecurity.github.io/cc-rest-docs/ref/cardholders.html#/definitions/Operator%20group%20summary)

An array of operator group summaries.

[Operator group summary](https://gallaghersecurity.github.io/cc-rest-docs/ref/cardholders.html#/definitions/Operator%20group%20summary)next:object

The link to the next page. Absent if you have retrieved them all.

href:string(url)

##### Example

```json
{
  "results": [
    {
      "href": "https://localhost:8904/api/operator_groups/523",
      "name": "Locker admins.",
      "serverDisplayName": "ruatoria.satellite.int"
    }
  ],
  "next": {
    "href": "https://localhost:8904/api/operator_groups?skip=61320"
  }
}
```

---

## Operator group summary

## Operator group summary:

The operator group search at /api/operator_groups returns an array of these, and /api/operator_groups/{id} (linked as the href in this object) returns one with more fields.

href:string(url)

A link to an operator group detail object for this operator group.

name:stringserverDisplayName:string

If you are running a multi-server installation and this item is homed on a remote server, this field will contain the name of that server. This field is missing from items that are held on the machine that served the API request. Added in 8.40.

This is a read-only field. The server will ignore it if you send it.

##### Example

```json
{
  "href": "https://localhost:8904/api/operator_groups/523",
  "name": "Locker admins.",
  "serverDisplayName": "ruatoria.satellite.int"
}
```

---

## Operator group detail

## Operator group detail:

/api/operator_groups/{id} returns one of these. In addition to the basic items details such as division and description, it lists the divisions in which it grants privileges to its members.

[Operator group summary](https://gallaghersecurity.github.io/cc-rest-docs/ref/cardholders.html#/definitions/Operator%20group%20summary)
description:stringdivision:object

The division that contains this operator group. This has no bearing on the divisions in which this operator group grants privileges; that is divisions .

cardholders:object

Following this link lists the group's cardholder members .

divisions:array

An array containing the divisions in which this operator group grants its privileges.

##### Example

```json
{
  "href": "https://localhost:8904/api/operator_groups/523",
  "name": "Locker admins.",
  "serverDisplayName": "ruatoria.satellite.int",
  "description": "For managing locker assignments.",
  "division": {
    "id": "2",
    "href": "https://localhost:8904/api/divisions/2"
  },
  "cardholders": {
    "href": "https://localhost:8904/api/operator_groups/523/cardholders"
  },
  "divisions": [
    {
      "division": {
        "name": "Staff",
        "href": "https://localhost:8904/api/divisions/647"
      }
    },
    {
      "division": {
        "name": "Contractors",
        "href": "https://localhost:8904/api/divisions/649"
      }
    }
  ]
}
```

---

## Operator group membership

## Operator group membership:

Returned in an array by /api/operator_groups/{id}/cardholders , containing cardholders who are members of a particular operator group.

Each item contains the cardholder's name and (if your operator has the privilege to view that cardholder) an href to the cardholder record. 8.70 added an href that you can use to delete the operator group membership.

PATCH the href in the cardholder block if you want to change anything about that cardholder. The operator groups they are in, for example.

href:string(url)

DELETE this URL to remove the cardholder from this operator group.

DELETE is the only verb you can use on this URL. GET will always return a 404.

It is not a default field - if you want it, you need to request it using the fields query parameter.

Added in 8.70.

cardholder:object

The name and href of the member cardholder.

##### Example

```json
{
  "href": "https://localhost:8904/api/cardholders/325/operator_groups/EBDRSD",
  "cardholder": {
    "name": "Boothroyd, Algernon",
    "href": "https://localhost:8904/api/cardholders/325"
  }
}
```

---

## PDF definition search

## PDF definition search:

An array of PDF definition summaries, and a next link for more.

results:[PDF definition](https://gallaghersecurity.github.io/cc-rest-docs/ref/cardholders.html#/definitions/PDF%20definition)

An array of PDF definition summaries.

[PDF definition](https://gallaghersecurity.github.io/cc-rest-docs/ref/cardholders.html#/definitions/PDF%20definition)next:object

The link to the next page of results. Absent if you have retrieved them all.

href:string(url)

##### Example

```json
{
  "results": [
    {
      "name": "email",
      "id": "5516",
      "href": "https://localhost:8904/api/personal_data_fields/5516"
    },
    {
      "name": "cellphone",
      "id": "9998",
      "href": "https://localhost:8904/api/personal_data_fields/9998",
      "serverDisplayName": "ruatoria.satellite.int"
    }
  ],
  "next": {
    "href": "https://localhost:8904/api/personal_data_fields?pos=900&sort=id"
  }
}
```

---

## PDF definition

## PDF definition:

/api/personal_data_fields returns an array of these. By default it gives you just the basics about a PDF: its ID, href, name, and (if it is remote) the name of its home server. By using the fields parameter you can add more.

id:string

An alphanumeric identifier, unique to the server. Use it to filter cardholder searches and to add your external ID to the results of an event search.

name:stringserverDisplayName:string

If you are running a multi-server installation and this item is homed on a remote server, this field will contain the name of that server. This field is missing from items that are held on the machine that served the API request. Added in 8.40.

This is a read-only field. The server will ignore it if you send it.

description:stringdivision:object

The division containing this PDF definition. Required when creating one.

type:stringstring,                         image,                         strEnum,                         numeric,                         date,                         address,                         phone,                         email,                         mobile

The type of PDF: string, image, email address, etc.

Required when creating a new PDF definition, but ignored when PATCHing one since a PDF's type is fixed once set.

default:string

This is the value that new cardholders will receive, if not supplied by the creating client. Required for 'required' PDFs.

This field will be missing if there is no default value.

To clear the default, send the blank string "" .

required:boolean

If true, every cardholder with this PDF must have a value for it. No blanks allowed.

unique:boolean

If true, every cardholder with a value for this PDF must have a different value.

After 8.70 this will not show for date and image PDFs, because it can never be true for them.

defaultAccess:stringnoAccess,                         readOnly,                         fullAccessfullAccess

This is the access that operators will have to cardholders' values of this PDF if the operator is not a member of an operator group that overrides it. Check the operator group's 'Personal data' tab in the Configuration Client.

operatorAccess:stringnoAccess,                         readOnly,                         fullAccess

This is the access that your operator has to cardholders' values of this PDF including the permissions granted by this cardholder's operator groups. You will only get this field if you ask for it with the fields parameter.

New in 8.80.

sortPriority:integer

This is called 'sort order' in the Configuration Client. Interactive clients use this number to order the list of PDFs on a cardholder. It has no effect on access control or this API.

accessGroups:array

This array contains a block for each access group that gives this PDF to its members. Each block contains the group's name, and if the operator has read access to the group, its href.

notificationDefault:boolean

This value is copied to the 'notification' flag on a cardholder's value for this PDF when they first gain membership of one of this PDF's access groups. It will only appear for PDF types that can receive notifications (email addresses and mobile numbers), and only if you ask for it with the fields parameter. The server will ignore it if you send it in a POST or PATCH to a PDF that is not email or mobile.

New in 8.50.

regex:string

This is the regular expression that a PDF value must match before Command Centre will accept it on a cardholder.

To remove the requirement that PDF values match a regular expression, send the empty string "" .

regexDescription:string

Regular expressions often need explaining.

imageWidth:integer

The maximum width of an image stored in this PDF, in pixels, for image PDF types. You will only get this field if you ask for it with the fields parameter.

You can set this on a new PDF definition but not change it on an existing definition.

New in 8.50.

imageHeight:integer

The maximum height of an image stored in this PDF, in pixels, for image PDF types. You will only get this field if you ask for it with the fields parameter.

You can set this on a new PDF definition but not change it on an existing definition.

New in 8.50.

imageFormat:stringbmp,                         jpg,                         png

Whether this image PDF stores BMPs, JPEGs, or PNGs. You will only get this field if you ask for it with the fields parameter.

New in 8.50. Deprecated in 8.70 by contentType , which is more standard.

contentType:stringimage/bmp,                         image/jpeg,                         image/png

Whether this image PDF stores BMPs, JPEGs, or PNGs. You will only get this field if you ask for it with the fields parameter.

You can set this on a new PDF definition but not change it on an existing definition.

New in 8.70.

isProfileImage:boolean

True if and only if this PDF holds images and it is set as a profile image.

Unlike many other properties of an image PDF, this can be changed at will.

New in 8.70.

##### Example

```json
{
  "id": "string",
  "name": "email",
  "serverDisplayName": "ruatoria.satellite.int",
  "description": "Corporate mailbox",
  "division": {
    "id": "2",
    "href": "https://localhost:8904/api/divisions/2"
  },
  "type": "email",
  "default": "contact@example.com",
  "required": false,
  "unique": false,
  "defaultAccess": "fullAccess",
  "operatorAccess": "fullAccess",
  "sortPriority": 50,
  "accessGroups": [
    {
      "name": "All Staff"
    },
    {
      "name": "R&D Special Projects Group",
      "href": "https://localhost:8904/api/access_groups/352"
    }
  ],
  "notificationDefault": false,
  "regex": ".*@.*",
  "regexDescription": "@ least",
  "imageWidth": 600,
  "imageHeight": 800,
  "imageFormat": "jpg",
  "contentType": "image/jpeg",
  "isProfileImage": false
}
```

---

## Reception search

## Reception search:

An array of receptions, and a next link for more.

results:[Reception](https://gallaghersecurity.github.io/cc-rest-docs/ref/cardholders.html#/definitions/Reception)

An array of receptions.

[Reception](https://gallaghersecurity.github.io/cc-rest-docs/ref/cardholders.html#/definitions/Reception)next:object

The link to the next page of results. Absent if you have retrieved them all.

href:string(url)

##### Example

```json
{
  "results": [
    {
      "name": "Main lobby",
      "href": "https://localhost:8904/api/receptions/937"
    },
    {
      "name": "Green Dragon main desk",
      "href": "https://localhost:8904/api/receptions/979"
    }
  ],
  "next": {
    "href": "https://localhost:8904/api/receptions?pos=1000"
  }
}
```

---

## Reception

## Reception:

/api/receptions returns an array of these, and /api/receptions/{id} returns one. Each gives you enough about a reception to identify it and use it in a visit: its href, name, and (if you ask for them using the fields parameter) its description, default visitor type, and notes.

name:stringhref:string(url)

This is the href to use when creating a visit.

serverDisplayName:string

If you are running a multi-server installation and this item is homed on a remote server, this field will contain the name of that server. This field is missing from items that are held on the machine that served the API request. Added in 8.40.

This is a read-only field. The server will ignore it if you send it.

description:string

Short free-form text. Searches do not return an item's description - ask for it using the fields parameter.

division:object

The division containing this reception.

defaultVisitorType:object

Gallagher's visitor management applications use this to pre-fill a UI element prompting the user to pick a visitor type when they are creating a visit for this reception. The server does not use it.

notes:string

Free-form text. You will only get this field if you ask for it with the fields parameter.

##### Example

```json
{
  "name": "Main lobby",
  "href": "https://localhost:8904/api/receptions/937",
  "serverDisplayName": "ruatoria.satellite.int",
  "description": "Security foyer in B1",
  "division": {
    "id": "2",
    "href": "https://localhost:8904/api/divisions/2"
  },
  "defaultVisitorType": {
    "href": "https://localhost:8904/api/divisions/2/v_t/925",
    "accessGroup": {
      "name": "Visitor group 1",
      "href": "https://localhost:8904/api/access_groups/925"
    }
  },
  "notes": "string"
}
```

---

## Redaction

## Redaction:

An array of these comes from GET /cardholders/redactions .

[Cardholder redaction](https://gallaghersecurity.github.io/cc-rest-docs/ref/cardholders.html#/definitions/Cardholder%20redaction)
cardholder:object

The href of the cardholder whose events or item this redaction should affect.

Required.

finished:string(date-time)

When the redaction finished.

Will be missing from pending redactions.

message:string

Translated string from the redaction's error code. Will be absent if empty, or if the redaction is pending or complete.

details:string

A more detailed description of what went wrong.

Not translated. Will be absent if empty, or if the redaction is pending or complete.

##### Example

```json
{
  "href": "https://localhost:8904/api/cardholders/redactions/625",
  "type": "normalEvents",
  "when": "2023-01-01T00:00:00Z",
  "before": "2022-01-01T00:00:00Z",
  "status": "pending",
  "redactionOperator": {
    "name": "REST Operator",
    "href": "https://localhost:8904/api/items/100"
  },
  "cardholder": {
    "href": "https://localhost:8904/api/cardholders/630"
  },
  "finished": "2022-01-01T00:00:00Z",
  "message": "Invalid cardholder",
  "details": ""
}
```

---

## Redaction POST Example

## Redaction POST Example:

POST one of these to schedule a redaction.

cardholder:object

The href of the cardholder whose events or item this redaction should affect.

Required.

type:stringnormalEvents,                         cardholder

Whether this redaction is for events or cardholder information.

Required.

when:string(date-time)

When redaction is meant to happen. This should be in the future. If it is in the past, the service returns 400-Bad Request Invalid Start Time.

Optional. If it is absent, it means to do it asap.

before:string(date-time)

For event redactions, do not redact any events after this time. No effect on cardholder information redactions.

Optional.

##### Example

```json
{
  "cardholder": {
    "href": "https://localhost:8904/api/cardholders/630"
  },
  "type": "normalEvents",
  "when": "2023-01-01T00:00:00Z",
  "before": "2022-01-01T00:00:00Z"
}
```

---

## Role search

## Role search:

An array of roles, and a next link for more.

results:[Role](https://gallaghersecurity.github.io/cc-rest-docs/ref/cardholders.html#/definitions/Role)

An array of roles.

[Role](https://gallaghersecurity.github.io/cc-rest-docs/ref/cardholders.html#/definitions/Role)next:object

The link to the next page of results. Absent if you have retrieved them all.

href:string(url)

##### Example

```json
{
  "results": [
    {
      "name": "Supervisor",
      "id": "1383",
      "href": "https://localhost:8904/api/roles/1383"
    },
    {
      "name": "Contract manager",
      "id": "1399",
      "href": "https://localhost:8904/api/roles/1399",
      "serverDisplayName": "ruatoria.satellite.int"
    }
  ],
  "next": {
    "href": "https://localhost:8904/api/roles?pos=1000&sort=id"
  }
}
```

---

## Role

## Role:

/api/roles returns an array of these. Each element gives you enough about a role to identify it and use it in a cardholder PATCH: its href, name, description, and (if you ask for them using the fields parameter) notes.

In 9.10 or later you can send one of these in a POST to create a new role, or in a PATCH to modify an existing one.

name:stringhref:string(url)

This is the string to use when creating a relationship between cardholders using this role.

serverDisplayName:string

If you are running a multi-server installation and this item is homed on a remote server, this field will contain the name of that server. This field is missing from items that are held on the machine that served the API request. Added in 8.40.

This is a read-only field. The server will ignore it if you send it.

description:stringdivision:object

The division containing this role. Required in a POST, optional in a PATCH.

enableCompetencyExpiryWarnings:boolean

Controls whether CC sends notifications to a person holding this role when their staff's competencies are about to expire.

enableCardExpiryWarnings:boolean

Controls whether CC sends notifications to a person holding this role when their staff's cards are about to expire.

##### Example

```json
{
  "name": "Supervisor",
  "href": "https://localhost:8904/api/roles/1399",
  "serverDisplayName": "ruatoria.satellite.int",
  "description": "aka floor manager",
  "division": {
    "href": "https://localhost:8904/api/divisions/2"
  },
  "enableCompetencyExpiryWarnings": false,
  "enableCardExpiryWarnings": true
}
```

---

## Visit search

## Visit search:

An array of visits, and a next link for more.

results:[Visit](https://gallaghersecurity.github.io/cc-rest-docs/ref/cardholders.html#/definitions/Visit)

An array of visits.

[Visit](https://gallaghersecurity.github.io/cc-rest-docs/ref/cardholders.html#/definitions/Visit)next:object

The link to the next page of results. Absent if you have retrieved them all.

href:string(url)

##### Example

```json
{
  "results": [
    {
      "name": "Visit hosted by Greta Ginger",
      "href": "https://localhost:8904/api/visits/941",
      "serverDisplayName": "ruatoria.satellite.int",
      "description": "Initial scoping",
      "division": {
        "id": "2",
        "href": "https://localhost:8904/api/divisions/2"
      },
      "reception": {
        "name": "Main lobby",
        "href": "https://localhost:8904/api/receptions/937"
      },
      "visitorType": {
        "href": "https://localhost:8904/api/divisions/2/v_t/925",
        "accessGroup": {
          "name": "Visitor group 1",
          "href": "https://localhost:8904/api/access_groups/925"
        }
      },
      "host": {
        "name": "Ginger Greeter",
        "href": "https://localhost:8904/api/cardholders/526"
      },
      "from": "1971-03-08T14:35:00Z",
      "until": "2021-03-08T14:35:00Z",
      "location": "Gather in Ginger's office",
      "visitorAccessGroups": [
        {
          "accessGroup": {
            "name": "Access group 22",
            "href": "https://localhost:8904/api/access_groups/926"
          }
        }
      ],
      "visitors": [
        {
          "href": "https://localhost:8904/api/visits/941/visitors/940",
          "cardholder": {
            "name": "Red Adair",
            "href": "https://localhost:8904/api/cardholders/940"
          },
          "status": {
            "value": "Expected Back",
            "type": "expectedBack"
          },
          "invitation": "text to be encoded into a QR code for 940"
        },
        {
          "href": "https://localhost:8904/api/visits/941/visitors/9040",
          "cardholder": {
            "name": "James Page",
            "href": "https://localhost:8904/api/cardholders/9040"
          },
          "status": {
            "value": "On-Site",
            "type": "onSite"
          },
          "invitation": "text to be encoded into a QR code for 9040"
        }
      ]
    }
  ],
  "next": {
    "href": "https://localhost:8904/api/visits?pos=1000&sort=id"
  }
}
```

---

## Visit

## Visit:

GET /api/visits returns an array of these, GET /api/visits/{id} returns one, and you can send one in a POST or PATCH to /api/visits .

name:string

You can name a visit however you like. Gallagher's in-house applications name them after their hosts.

This field is mandatory when creating a visit. No blank names allowed!

href:string(url)

This is the URL to send a PATCH to when modifying an existing visit. You can find it in the body of a GET that returns a visit, or in the Location header of a POST that creates one.

It is not necessary in a POST or PATCH. The server will ignore it if you send it.

serverDisplayName:string

If you are running a multi-server installation and this item is homed on a remote server, this field will contain the name of that server. This field is missing from items that are held on the machine that served the API request. Added in 8.40.

This is a read-only field. The server will ignore it if you send it.

description:string

A visit's description is free text. Gallagher's visitor management clients use it to store the visit's purpose. You can change it at any time.

division:object

The division containing this visit.

This only comes out of a GET. It will be the visit's reception's division, or if that division did not have an active visitor management configuration when the visit was created, the first ancestor up the division tree that did.

The server will ignore it if you send it, because you cannot change a visit's division directly. You can affect it indirectly by changing the visit's reception.

reception:object

Every visit must have a reception. It represents the location at which your visitors will first arrive. Having one attached to a visit allows a kiosk at that location to hide visits for other receptions, revealing only those meant to start there.

Your choice of reception also determines the rules with which the visit must comply, because those rules come from the visitor management configuration on the reception's division.

Since every visit must have a reception, every POST that creates a visit must have one. Once you have a visit in the system you can change its reception, but be aware that changing receptions might change the visit's division, which also might change the rules to which the visit must conform, and if any validation fails--and there is a lot of it--the PATCH will fail.

The reception's name comes from the server in a GET, but you need not send it when creating or modifying a visit. Command Centre only needs an href from you.

Note that you must send the reception like it appears in the example, as an href inside a block called reception . This will not work:

"reception": "https://localhost:8904/api/receptions/123"

visitorType:object

Every visit must also have a visitor type. It provides an access group for your visitors so that they can have personal data (remember that a cardholder must be in an access group before he or she can have a PDF), and it is an index into the visitor management configuration that governs things like the host (the cardholder who is responsible for your visitors) and the access groups that the server will give your visitors when they sign in.

Use the href from the division's visitor management configuration, or the access group's href. Both will work.

You will receive the accessGroup object inside the visitorType block in a GET, but you needn't send it in a POST or PATCH. The server can find the visitor type using only its href.

Like the reception, when you send this to the server you must use the same form as the example, as an href inside a block called visitorType . The href can be to an access group that you got from an access group or the visitor management configuration on a division . For example, both of these will work as visitor type hrefs:

https://localhost:8904/api/access_groups/925 (taken from another visit, or a search of access groups)

https://localhost:8904/api/divisions/2/visitor_types/925 (taken from a division config)

Your operator must have the privilege to view the access group, otherwise you will be told 'Access denied when writing to one or more fields'. Having the 'Modify Access Control' privilege on the group's division is a good choice because it not only gives you a view of access groups but the ability to change cardholders' membership of them, which you need to add visitors and visitor groups.

host:object

A host is a cardholder, and every visit has one. A visit's host is the person who (optionally) receives an email or SMS notification when visitors start signing in, and is responsible for them until they sign out again.

A host must be a member of at least one of the visit's visitor type's host access groups at the time that you create or modify the visit. To find out what those are, get the visitor management configuration for your reception's division (remembering that your visit's division is its reception's division). That visitor type contains an array of access groups called hostAccessGroups . Your host cardholder must be a member of one of those groups or their descendants.

When you GET a visit the server will send you the host cardholder's name, but when you create a visit with a POST or modify one with a PATCH, you needn't send the name back. Do so if you must, but it will not have an effect.

from:string(date-time)

Every visit has a validity period, defined by start and end times called from and until .

Not only are they both required when you create a visit, but the server will insist that until is later than from and that you followed the date-time rules .

You can use short forms such as 2021-04-01+12 and the server will fill in zeroes for the parts you missed. But because the end time must always be later than the start time you cannot use the same date for both start and end. The server will treat them both like midnight, see that they are equal, and reject your API call. So if you have a one-day visit, bump until by a day or pad it out until the evening. 2021-04-01T23:59:59+12 , for example.

until:string(date-time)

Every visit has a validity period, defined by start and end times called from and until .

Not only are they both required when you create a visit, but the server will insist that until is later than from and that you followed the date-time rules .

You can use short forms such as 2021-04-01+12 and the server will fill in zeroes for the parts you missed. But because the end time must always be later than the start time you cannot use the same date for both start and end. The server will treat them both like midnight, see that they are equal, and reject your API call. So if you have a one-day visit, bump until by a day or pad it out until the evening. 2021-04-01T23:59:59+12 , for example.

location:string

Free text. Entirely optional in the POST and PATCH.

visitorAccessGroups:array

These are the access groups that the server will add your visitors to after they complete induction and sign in. They define the access that your visitors have to the site, as they do for all cardholders.

It is perfectly valid to have no visitor access groups on a visit - it just means that someone will be opening all your guests' doors for them. That might be the kind of thing they are used to anyway.

The visitor access groups on a visit must be a subset of the visitor access groups on the division's visitor type (in the list of visitor access groups in the visit's reception's division's visitor management configuration, in other words).

When you send a visitor access group block to the server in a POST, just send an array of access group hrefs. 8.50 does not accept the same kind of visitor access group hrefs here that it sends, so this is one reason why you cannot copy a visit by POSTing back the JSON from a GET.

When you send a visitor access group block in a PATCH to edit an existing visit, the server expects it to contain one or two arrays called add and remove . The add array should contain access group hrefs, exactly as you would send when creating a visit. The remove array can contain access group hrefs or the hrefs you received in the GET. The PATCH example shows two hrefs trying to remove the same access group.

Your operator must have a privilege on the groups' divisions that allows viewing the access groups and adding and removing cardholders to and from them, otherwise you will be told 'Access denied when writing to one or more fields'. 'Modify Access Control' is a good choice.

visitors:object[]

When the server sends this array to you, it contains two hrefs for each cardholder due to arrive in this visit. The first, outside the cardholder block, is the URL you PATCH to change the state of their visit (new in 8.90). The one inside the cardholder block is simply an href to the cardholder item. You can use either href when removing visitors from a visit with a PATCH.

The status block contains two fields. value is a human-readable description of their state in the server's language. type comes from a fixed enumeration.

The invitation string is opaque: please do not interpret it.

Note that a visit with no visitors on it will come out of the API but will not appear in Gallagher's visitor management application. Why clutter the screen if you're not expecting anybody, after all.

When you send a visitor block to the server in a POST to create a visit, just send an array of hrefs to cardholders. 8.50 does not accept the same kind of visitor hrefs here that it sends, so this is the other reason you cannot copy a visit by POSTing back the JSON from a GET. Don't bother sending a status block: the server will ignore it and set all visitors to 'expected'.

When you send a visitor block in a PATCH to edit an existing visit, the server expects it to contain an array called add , an array called remove , or both. The add array should contain cardholder hrefs, exactly as you would send when creating a visit. The remove array should contain either of the hrefs you received for the cardholder in the GET. The PATCH example shows two hrefs removing the same cardholder.

Your operator must have a privilege such as 'Modify Access Control' that allows both viewing the cardholders and changing their access groups memberships, otherwise you will be told 'Access denied when writing to one or more fields'.

objectcardholder:object

A block containing the name and cardholder href of the visitor.

href:string(url)

An href unique to this cardholder on this visit. You can PATCH it to change the visitor's state.

New to 8.90.

invitation:string

A blob of text which, when encoded into a QR code and presented to a Gallagher Visitor Management kiosk, allows the visitor to sign in.

This does not appear unless you ask for it using the fields query parameter. Because it is inside the visitors block, name it visitors.invitation in the field list.

status:object

The cardholder's status on this visit, in human-readable and machine-readable forms.

Also new to 8.90.

value:string

A human-readable version of the visitor's status, varying with the version of Command Centre and the server's locale.

type:stringexpected,                                         signingIn,                                         signedIn,                                         onSite,                                         expectedBack,                                         departed,                                         cancelled

The visitor's current state taken from a fixed list of strings. New visitors have a starting state of 'expected'.

##### Example

```json
{
  "name": "Visit hosted by Greta Ginger",
  "href": "https://localhost:8904/api/visits/941",
  "serverDisplayName": "ruatoria.satellite.int",
  "description": "Initial scoping",
  "division": {
    "id": "2",
    "href": "https://localhost:8904/api/divisions/2"
  },
  "reception": {
    "name": "Main lobby",
    "href": "https://localhost:8904/api/receptions/937"
  },
  "visitorType": {
    "href": "https://localhost:8904/api/divisions/2/v_t/925",
    "accessGroup": {
      "name": "Visitor group 1",
      "href": "https://localhost:8904/api/access_groups/925"
    }
  },
  "host": {
    "name": "Ginger Greeter",
    "href": "https://localhost:8904/api/cardholders/526"
  },
  "from": "1971-03-08T14:35:00Z",
  "until": "2021-03-08T14:35:00Z",
  "location": "Gather in Ginger's office",
  "visitorAccessGroups": [
    {
      "accessGroup": {
        "name": "Access group 22",
        "href": "https://localhost:8904/api/access_groups/926"
      }
    }
  ],
  "visitors": [
    {
      "href": "https://localhost:8904/api/visits/941/visitors/940",
      "cardholder": {
        "name": "Red Adair",
        "href": "https://localhost:8904/api/cardholders/940"
      },
      "status": {
        "value": "Expected Back",
        "type": "expectedBack"
      },
      "invitation": "text to be encoded into a QR code for 940"
    },
    {
      "href": "https://localhost:8904/api/visits/941/visitors/9040",
      "cardholder": {
        "name": "James Page",
        "href": "https://localhost:8904/api/cardholders/9040"
      },
      "status": {
        "value": "On-Site",
        "type": "onSite"
      },
      "invitation": "text to be encoded into a QR code for 9040"
    }
  ]
}
```

---

## Visit POST example

## Visit POST example:

This is a sample POST body that creates a visit when sent to /api/visits .

You need to put fewer fields in a POST than you receive from the server after a GET. Only the reception, visitor type, host, name, and start and end date-times are required. It will ignore href , and (unusually) division because it copies a visit's division from its reception.

name:string

You can name a visit however you like. Gallagher's in-house applications name them after their hosts.

This field is mandatory when creating a visit. No blank names allowed!

description:string

A visit's description is free text. Gallagher's visitor management clients use it to store the visit's purpose. You can change it at any time.

reception:object

Every visit must have a reception. It represents the location at which your visitors will first arrive. Having one attached to a visit allows a kiosk at that location to hide visits for other receptions, revealing only those meant to start there.

Your choice of reception also determines the rules with which the visit must comply, because those rules come from the visitor management configuration on the reception's division.

Since every visit must have a reception, every POST that creates a visit must have one. Once you have a visit in the system you can change its reception, but be aware that changing receptions might change the visit's division, which also might change the rules to which the visit must conform, and if any validation fails--and there is a lot of it--the PATCH will fail.

The reception's name comes from the server in a GET, but you need not send it when creating or modifying a visit. Command Centre only needs an href from you.

Note that you must send the reception like it appears in the example, as an href inside a block called reception . This will not work:

"reception": "https://localhost:8904/api/receptions/123"

visitorType:object

Every visit must also have a visitor type. It provides an access group for your visitors so that they can have personal data (remember that a cardholder must be in an access group before he or she can have a PDF), and it is an index into the visitor management configuration that governs things like the host (the cardholder who is responsible for your visitors) and the access groups that the server will give your visitors when they sign in.

Use the href from the division's visitor management configuration, or the access group's href. Both will work.

You will receive the accessGroup object inside the visitorType block in a GET, but you needn't send it in a POST or PATCH. The server can find the visitor type using only its href.

Like the reception, when you send this to the server you must use the same form as the example, as an href inside a block called visitorType . The href can be to an access group that you got from an access group or the visitor management configuration on a division . For example, both of these will work as visitor type hrefs:

https://localhost:8904/api/access_groups/925 (taken from another visit, or a search of access groups)

https://localhost:8904/api/divisions/2/visitor_types/925 (taken from a division config)

Your operator must have the privilege to view the access group, otherwise you will be told 'Access denied when writing to one or more fields'. Having the 'Modify Access Control' privilege on the group's division is a good choice because it not only gives you a view of access groups but the ability to change cardholders' membership of them, which you need to add visitors and visitor groups.

host:object

A host is a cardholder, and every visit has one. A visit's host is the person who (optionally) receives an email or SMS notification when visitors start signing in, and is responsible for them until they sign out again.

A host must be a member of at least one of the visit's visitor type's host access groups at the time that you create or modify the visit. To find out what those are, get the visitor management configuration for your reception's division (remembering that your visit's division is its reception's division). That visitor type contains an array of access groups called hostAccessGroups . Your host cardholder must be a member of one of those groups or their descendants.

When you GET a visit the server will send you the host cardholder's name, but when you create a visit with a POST or modify one with a PATCH, you needn't send the name back. Do so if you must, but it will not have an effect.

from:string(date-time)

Every visit has a validity period, defined by start and end times called from and until .

Not only are they both required when you create a visit, but the server will insist that until is later than from and that you followed the date-time rules .

You can use short forms such as 2021-04-01+12 and the server will fill in zeroes for the parts you missed. But because the end time must always be later than the start time you cannot use the same date for both start and end. The server will treat them both like midnight, see that they are equal, and reject your API call. So if you have a one-day visit, bump until by a day or pad it out until the evening. 2021-04-01T23:59:59+12 , for example.

until:string(date-time)

Every visit has a validity period, defined by start and end times called from and until .

Not only are they both required when you create a visit, but the server will insist that until is later than from and that you followed the date-time rules .

You can use short forms such as 2021-04-01+12 and the server will fill in zeroes for the parts you missed. But because the end time must always be later than the start time you cannot use the same date for both start and end. The server will treat them both like midnight, see that they are equal, and reject your API call. So if you have a one-day visit, bump until by a day or pad it out until the evening. 2021-04-01T23:59:59+12 , for example.

location:string

Free text. Entirely optional in the POST and PATCH.

visitorAccessGroups:array

These are the access groups that the server will add your visitors to after they complete induction and sign in. They define the access that your visitors have to the site, as they do for all cardholders.

It is perfectly valid to have no visitor access groups on a visit - it just means that someone will be opening all your guests' doors for them. That might be the kind of thing they are used to anyway.

The visitor access groups on a visit must be a subset of the visitor access groups on the division's visitor type (in the list of visitor access groups in the visit's reception's division's visitor management configuration, in other words).

When you send a visitor access group block to the server in a POST, just send an array of access group hrefs. 8.50 does not accept the same kind of visitor access group hrefs here that it sends, so this is one reason why you cannot copy a visit by POSTing back the JSON from a GET.

When you send a visitor access group block in a PATCH to edit an existing visit, the server expects it to contain one or two arrays called add and remove . The add array should contain access group hrefs, exactly as you would send when creating a visit. The remove array can contain access group hrefs or the hrefs you received in the GET. The PATCH example shows two hrefs trying to remove the same access group.

Your operator must have a privilege on the groups' divisions that allows viewing the access groups and adding and removing cardholders to and from them, otherwise you will be told 'Access denied when writing to one or more fields'. 'Modify Access Control' is a good choice.

visitors:object[]

When the server sends this array to you, it contains two hrefs for each cardholder due to arrive in this visit. The first, outside the cardholder block, is the URL you PATCH to change the state of their visit (new in 8.90). The one inside the cardholder block is simply an href to the cardholder item. You can use either href when removing visitors from a visit with a PATCH.

The status block contains two fields. value is a human-readable description of their state in the server's language. type comes from a fixed enumeration.

The invitation string is opaque: please do not interpret it.

Note that a visit with no visitors on it will come out of the API but will not appear in Gallagher's visitor management application. Why clutter the screen if you're not expecting anybody, after all.

When you send a visitor block to the server in a POST to create a visit, just send an array of hrefs to cardholders. 8.50 does not accept the same kind of visitor hrefs here that it sends, so this is the other reason you cannot copy a visit by POSTing back the JSON from a GET. Don't bother sending a status block: the server will ignore it and set all visitors to 'expected'.

When you send a visitor block in a PATCH to edit an existing visit, the server expects it to contain an array called add , an array called remove , or both. The add array should contain cardholder hrefs, exactly as you would send when creating a visit. The remove array should contain either of the hrefs you received for the cardholder in the GET. The PATCH example shows two hrefs removing the same cardholder.

Your operator must have a privilege such as 'Modify Access Control' that allows both viewing the cardholders and changing their access groups memberships, otherwise you will be told 'Access denied when writing to one or more fields'.

objectcardholder:object

A block containing the name and cardholder href of the visitor.

href:string(url)

An href unique to this cardholder on this visit. You can PATCH it to change the visitor's state.

New to 8.90.

invitation:string

A blob of text which, when encoded into a QR code and presented to a Gallagher Visitor Management kiosk, allows the visitor to sign in.

This does not appear unless you ask for it using the fields query parameter. Because it is inside the visitors block, name it visitors.invitation in the field list.

status:object

The cardholder's status on this visit, in human-readable and machine-readable forms.

Also new to 8.90.

value:string

A human-readable version of the visitor's status, varying with the version of Command Centre and the server's locale.

type:stringexpected,                                         signingIn,                                         signedIn,                                         onSite,                                         expectedBack,                                         departed,                                         cancelled

The visitor's current state taken from a fixed list of strings. New visitors have a starting state of 'expected'.

##### Example

```json
{
  "name": "Visit hosted by Greta Ginger",
  "description": "Initial scoping",
  "reception": {
    "href": "https://localhost:8904/api/receptions/937"
  },
  "visitorType": {
    "href": "https://localhost:8904/api/access_groups/925"
  },
  "host": {
    "href": "https://localhost:8904/api/cardholders/526"
  },
  "from": "1971-03-08T14:35:00Z",
  "until": "2023-03-08T14:35:00Z",
  "location": "Gather in Ginger's office",
  "visitorAccessGroups": [
    {
      "href": "https://localhost:8904/api/access_groups/926"
    },
    {
      "href": "https://localhost:8904/api/access_groups/9260"
    }
  ],
  "visitors": [
    {
      "href": "https://localhost:8904/api/cardholders/940"
    },
    {
      "href": "https://localhost:8904/api/cardholders/9040"
    }
  ]
}
```

---

## Visit PATCH example

## Visit PATCH example:

This is a sample PATCH body that modifies a visit when sent to its href, /api/visits/941 .

The differences from a POST are in the visitorAccessGroups and visitors arrays. In a POST they are arrays of hrefs, but in a PATCH they must be objects. Each object should contain an array called add and / or an array called remove . Each of those should contain hrefs to add to or remove from the visit.

The other big difference between a POST and a PATCH is that all fields are optional in a PATCH.

name:string

You can name a visit however you like. Gallagher's in-house applications name them after their hosts.

This field is mandatory when creating a visit. No blank names allowed!

description:string

A visit's description is free text. Gallagher's visitor management clients use it to store the visit's purpose. You can change it at any time.

reception:object

Every visit must have a reception. It represents the location at which your visitors will first arrive. Having one attached to a visit allows a kiosk at that location to hide visits for other receptions, revealing only those meant to start there.

Your choice of reception also determines the rules with which the visit must comply, because those rules come from the visitor management configuration on the reception's division.

Since every visit must have a reception, every POST that creates a visit must have one. Once you have a visit in the system you can change its reception, but be aware that changing receptions might change the visit's division, which also might change the rules to which the visit must conform, and if any validation fails--and there is a lot of it--the PATCH will fail.

The reception's name comes from the server in a GET, but you need not send it when creating or modifying a visit. Command Centre only needs an href from you.

Note that you must send the reception like it appears in the example, as an href inside a block called reception . This will not work:

"reception": "https://localhost:8904/api/receptions/123"

visitorType:object

Every visit must also have a visitor type. It provides an access group for your visitors so that they can have personal data (remember that a cardholder must be in an access group before he or she can have a PDF), and it is an index into the visitor management configuration that governs things like the host (the cardholder who is responsible for your visitors) and the access groups that the server will give your visitors when they sign in.

Use the href from the division's visitor management configuration, or the access group's href. Both will work.

You will receive the accessGroup object inside the visitorType block in a GET, but you needn't send it in a POST or PATCH. The server can find the visitor type using only its href.

Like the reception, when you send this to the server you must use the same form as the example, as an href inside a block called visitorType . The href can be to an access group that you got from an access group or the visitor management configuration on a division . For example, both of these will work as visitor type hrefs:

https://localhost:8904/api/access_groups/925 (taken from another visit, or a search of access groups)

https://localhost:8904/api/divisions/2/visitor_types/925 (taken from a division config)

Your operator must have the privilege to view the access group, otherwise you will be told 'Access denied when writing to one or more fields'. Having the 'Modify Access Control' privilege on the group's division is a good choice because it not only gives you a view of access groups but the ability to change cardholders' membership of them, which you need to add visitors and visitor groups.

host:object

A host is a cardholder, and every visit has one. A visit's host is the person who (optionally) receives an email or SMS notification when visitors start signing in, and is responsible for them until they sign out again.

A host must be a member of at least one of the visit's visitor type's host access groups at the time that you create or modify the visit. To find out what those are, get the visitor management configuration for your reception's division (remembering that your visit's division is its reception's division). That visitor type contains an array of access groups called hostAccessGroups . Your host cardholder must be a member of one of those groups or their descendants.

When you GET a visit the server will send you the host cardholder's name, but when you create a visit with a POST or modify one with a PATCH, you needn't send the name back. Do so if you must, but it will not have an effect.

from:string(date-time)

Every visit has a validity period, defined by start and end times called from and until .

Not only are they both required when you create a visit, but the server will insist that until is later than from and that you followed the date-time rules .

You can use short forms such as 2021-04-01+12 and the server will fill in zeroes for the parts you missed. But because the end time must always be later than the start time you cannot use the same date for both start and end. The server will treat them both like midnight, see that they are equal, and reject your API call. So if you have a one-day visit, bump until by a day or pad it out until the evening. 2021-04-01T23:59:59+12 , for example.

until:string(date-time)

Every visit has a validity period, defined by start and end times called from and until .

Not only are they both required when you create a visit, but the server will insist that until is later than from and that you followed the date-time rules .

You can use short forms such as 2021-04-01+12 and the server will fill in zeroes for the parts you missed. But because the end time must always be later than the start time you cannot use the same date for both start and end. The server will treat them both like midnight, see that they are equal, and reject your API call. So if you have a one-day visit, bump until by a day or pad it out until the evening. 2021-04-01T23:59:59+12 , for example.

location:string

Free text. Entirely optional in the POST and PATCH.

visitorAccessGroups:array

These are the access groups that the server will add your visitors to after they complete induction and sign in. They define the access that your visitors have to the site, as they do for all cardholders.

It is perfectly valid to have no visitor access groups on a visit - it just means that someone will be opening all your guests' doors for them. That might be the kind of thing they are used to anyway.

The visitor access groups on a visit must be a subset of the visitor access groups on the division's visitor type (in the list of visitor access groups in the visit's reception's division's visitor management configuration, in other words).

When you send a visitor access group block to the server in a POST, just send an array of access group hrefs. 8.50 does not accept the same kind of visitor access group hrefs here that it sends, so this is one reason why you cannot copy a visit by POSTing back the JSON from a GET.

When you send a visitor access group block in a PATCH to edit an existing visit, the server expects it to contain one or two arrays called add and remove . The add array should contain access group hrefs, exactly as you would send when creating a visit. The remove array can contain access group hrefs or the hrefs you received in the GET. The PATCH example shows two hrefs trying to remove the same access group.

Your operator must have a privilege on the groups' divisions that allows viewing the access groups and adding and removing cardholders to and from them, otherwise you will be told 'Access denied when writing to one or more fields'. 'Modify Access Control' is a good choice.

visitors:object[]

When the server sends this array to you, it contains two hrefs for each cardholder due to arrive in this visit. The first, outside the cardholder block, is the URL you PATCH to change the state of their visit (new in 8.90). The one inside the cardholder block is simply an href to the cardholder item. You can use either href when removing visitors from a visit with a PATCH.

The status block contains two fields. value is a human-readable description of their state in the server's language. type comes from a fixed enumeration.

The invitation string is opaque: please do not interpret it.

Note that a visit with no visitors on it will come out of the API but will not appear in Gallagher's visitor management application. Why clutter the screen if you're not expecting anybody, after all.

When you send a visitor block to the server in a POST to create a visit, just send an array of hrefs to cardholders. 8.50 does not accept the same kind of visitor hrefs here that it sends, so this is the other reason you cannot copy a visit by POSTing back the JSON from a GET. Don't bother sending a status block: the server will ignore it and set all visitors to 'expected'.

When you send a visitor block in a PATCH to edit an existing visit, the server expects it to contain an array called add , an array called remove , or both. The add array should contain cardholder hrefs, exactly as you would send when creating a visit. The remove array should contain either of the hrefs you received for the cardholder in the GET. The PATCH example shows two hrefs removing the same cardholder.

Your operator must have a privilege such as 'Modify Access Control' that allows both viewing the cardholders and changing their access groups memberships, otherwise you will be told 'Access denied when writing to one or more fields'.

objectcardholder:object

A block containing the name and cardholder href of the visitor.

href:string(url)

An href unique to this cardholder on this visit. You can PATCH it to change the visitor's state.

New to 8.90.

invitation:string

A blob of text which, when encoded into a QR code and presented to a Gallagher Visitor Management kiosk, allows the visitor to sign in.

This does not appear unless you ask for it using the fields query parameter. Because it is inside the visitors block, name it visitors.invitation in the field list.

status:object

The cardholder's status on this visit, in human-readable and machine-readable forms.

Also new to 8.90.

value:string

A human-readable version of the visitor's status, varying with the version of Command Centre and the server's locale.

type:stringexpected,                                         signingIn,                                         signedIn,                                         onSite,                                         expectedBack,                                         departed,                                         cancelled

The visitor's current state taken from a fixed list of strings. New visitors have a starting state of 'expected'.

##### Example

```json
{
  "name": "Visit hosted by Greta Ginger",
  "description": "Initial scoping",
  "reception": {
    "href": "https://localhost:8904/api/receptions/937"
  },
  "visitorType": {
    "accessgroup": {
      "href": "https://localhost:8904/api/access_groups/925"
    }
  },
  "host": {
    "href": "https://localhost:8904/api/cardholders/526"
  },
  "from": "1971-03-08T14:35:00Z",
  "until": "2023-03-08T14:35:00Z",
  "location": "Gather in Ginger's office",
  "visitorAccessGroups": {
    "add": [
      {
        "href": "https://localhost:8904/api/access_groups/926"
      },
      {
        "href": "https://localhost:8904/api/access_groups/9260"
      }
    ],
    "remove": [
      {
        "href": "https://localhost:8904/api/access_groups/930"
      },
      {
        "href": "https://localhost:8904/api/visits/941/visitor_access_groups/930"
      }
    ]
  },
  "visitors": {
    "add": [
      {
        "href": "https://localhost:8904/api/cardholders/940"
      },
      {
        "href": "https://localhost:8904/api/cardholders/9040"
      }
    ],
    "remove": [
      {
        "href": "https://localhost:8904/api/cardholders/937"
      },
      {
        "href": "https://localhost:8904/api/visits/941/visitors/937"
      }
    ]
  }
}
```

---

## Visitor PATCH example

## Visitor PATCH example:

This is a sample PATCH body that marks a visitor as signing in.

status:object

This is the same status that you see in a visit GET for each visitor except that you only need to send the value field.

value:stringexpected,                               signingIn,                               signedIn,                               onSite,                               expectedBack,                               departed,                               cancelled

An enum describing the state in which you want your visitor to be.

##### Example

```json
{
  "status": {
    "value": "signingIn"
  }
}
```

---

## Visitor notification POST

## Visitor notification POST:

This is a sample body for the methods that send notifications to a visit's host. Call them as a visitor moves through their reception process.

Command Centre can send email, an SMS, or both. It will send email if you give it either the subject or a message body. It will send a TXT only if you give it the SMS message.

emailSubject:string

If you wish to send an email notification, put its subject here. Email will go out if you leave it blank but provide a body, but it is generally easier on the recipient the other way around.

emailMessage:string

If you want your email notification to contain body text, put it here.

smsMessage:string

If you wish to send an SMS notification (a TXT), put it here. Leave it blank if you only wish to send email.

##### Example

```json
{
  "emailSubject": "Visitor arriving",
  "emailMessage": "",
  "smsMessage": "Visitor arriving"
}
```

---

