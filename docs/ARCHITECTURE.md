# Architecture — the Collector platform

Status: **design agreed 2026-10-27/28; build steps 1 and 2 done, step 4 in
part (2026-11-12).** Stack: **C# on .NET 10 / ASP.NET Core, RabbitMQ,
MongoDB.**
Site behaviour for the first five shops is specified in `docs/research/`.

Read the vocabulary first. Every later section uses those words and no
others.

## Goal

A multi-purpose ingestion platform. A **project** is a tenant. Inside it,
a tree of **operations** says what we want: `clothing/shoes`,
`electronics/laptops`, each with the providers that supply it as leaves.
Each operation harvests only what it needs from its providers into a
lake, adapts it into entities of its domain with ids of our own, and the
project serves them through an API, MCP or exports, always scoped by
project id. Today: shoes in North Macedonia. Tomorrow: electronics. Later:
car parts. Operations run side by side.

The platform does not crawl a whole site to answer a question. It learns
what a provider has once, then checks only what it needs: stock for the
products it already knows, details for the ones that lack them, one live
check for the product someone is asking about.

## Vocabulary

### The pipeline

| Term | Meaning |
|---|---|
| **Collector** | The whole pipeline: Harvester → Lake → Adapter → Staging → Store writer → Store, under a Conductor. |
| **Provider** | The party whose data we ingest and whose ids we keep: a shop (`sportvision`), a brand (`adidas`). |
| **Source** | One site or endpoint of a provider: base URL, platform, rate limit floor, robots. `sportvision_mk`, `sportm_mk`. |
| **Harvester** | Fetches. Nothing else. Given a command, it fetches with a mechanism (plain HTTP, or a browser that clicks) and writes one raw item per page to the lake. A leaf: it never starts another harvest or an adapt. Not "connector", not "scraper", not "collector". |
| **Raw item** | One fetched thing: a listing page, a product page, a sitemap, a feed, the JSON a page loaded after a click. Keyed by project, source, kind, provider id; stamped with harvest time. |
| **Lake** | Where raw items live. Retention per project: full history, changes only, latest only. |
| **Adapter** | Reads raw items from the lake and converts them into entities of one domain. One per platform × domain: `nbshop.shoes` serves three shops. A leaf: it never starts a harvest or another adapt. Re-runnable and idempotent. |
| **Staging** | A MongoDB collection of adapted entities waiting for the store writer. |
| **Store writer** | Reads staging, matches entities inside the project (same brand + `sku` = same product), upserts them into the store. The only writer of products, listings and categories. A leaf. |
| **Store** | MongoDB. Entities as documents, plus everything the platform needs to run. One database for all projects. |
| **Node** | A device that executes harvest commands. Today the box. Later a Raspberry Pi abroad, a laptop, a cloud VM. Declares capabilities (http, browser) and location. |

### What runs, and how

| Term | Meaning |
|---|---|
| **Command** | The smallest unit of work. A provider plus an id, and the provider decides what happens: "fetch product 4711 from Sport M", "fetch listing page 3 from Sport M", "adapt all unadapted Sport M listing pages". Commands are code, registered per provider. |
| **Job** | A template, a checklist. The ordered list of commands to run. Knows *what* and *in which order*, nothing about speed or nodes. Jobs are data. |
| **Strategy** | *How* a job executes: mechanism, nodes and geo, pacing, batch or streaming adapt, retry budget. Strategies are data. The same job can run with different strategies; the same strategy can drive many jobs. |
| **Run** | Job X executed with strategy Y, for one project, with parameters, at a time. Progress, failures and administrator decisions live on the run. |
| **Conductor** | Runs a run. Holds the sequence, dispatches commands, counts what comes back, applies the retry budget, pauses for decisions. The only component that knows what comes next. |
| **Schedule** | When runs start. Nightly, hourly, on demand. Creates runs; nothing else. |
| **Pacing** | The gaps between requests inside a command. Part of the strategy. |
| **Retry budget** | How long a run keeps trying before it stops and asks. Part of the strategy. |

### What we produce

| Term | Meaning |
|---|---|
| **Entity** | The base of everything we produce: our deterministic id, project, provider, provider id, kind, source URL, harvest time. Categories, products and listings are all entities. |
| **Domain** | A set of entity kinds with their extra fields, stored in the entity's document. `Shoes` first; `Electronics`, `CarParts` later. |
| **Product** | Ours. One per project per real thing. Name, brand, sku, category, chosen images, domain fields. |
| **Listing** | One provider's view of a product. Their title and description, price, stock, sizes, branch availability, URL, provider id, and the dates `infoCheckedAt` and `stockCheckedAt`. A product has one listing per provider that sells it. |
| **Provider id** | The id the provider uses. May equal the brand `sku` or be a shop-internal id kept next to the `sku`. Always prefixed with the provider in keys. |

### Where things live

| Term | Meaning |
|---|---|
| **Project** | A tenant, and the ecosystem: the whole tree of operations and everything they produce. Owns its operations, sources, jobs, strategies, runs, lake items, entities and id seed. A hard partition: nothing is shared across projects, even when two projects ingest the same site. Exported, archived or deleted as a unit. |
| **Operation** | A node in the project's tree. It says *what we want*: `clothing`, `clothing/shoes`, `clothing/shoes/sportm`. An operation carrying a domain owns that common model. Operations run in parallel. |
| **Leaf operation** | An operation bound to one provider: `clothing/shoes/sportm`. It says how that provider fills the operation's model, and holds the job × strategy pairs for it. |
| **Namespace** | The path of an operation in the tree. Runs, listings, adapters and settings attach to a namespace. |

## Operations: the tree

```
project mk-market                  the ecosystem
├─ electronics                     operation, a grouping
│  ├─ laptops                      operation, domain Electronics
│  │  ├─ anhoch.mk                 leaf: provider anhoch.mk, its own job × strategy pairs
│  │  ├─ setec.mk
│  │  └─ neptun.mk
│  └─ keyboards                    operation, domain Electronics
│     ├─ anhoch.mk                 same source record as above, another leaf
│     ├─ setec.mk
│     └─ neptun.mk
└─ clothing                        operation, a grouping
   └─ shoes                        operation, domain Shoes
      ├─ sportm.com.mk             leaf
      └─ sportvision.mk            leaf
```

Rules:

- **Every node is an operation.** The project is the root and the
  ecosystem; a leaf is an operation bound to a provider. Depth is not
  fixed; if laptops ever needs `gaming` and `office`, that is one more
  level. There is no separate "ecosystem" level.
- **Job × strategy pairs can sit on any node.** A leaf's pairs are its
  own: `shoes/sportm` discovers with a burst, `shoes/sportvision` checks
  stock with a browser strategy because of the sizes. A pair on a parent
  is the default for children that define none.
- **Running a node runs everything under it, in parallel.** "Run
  `clothing/shoes` stock" starts one run per leaf, each with its own
  strategy. "Run `clothing/shoes/sportm` stock" starts one.
- **Settings inherit down.** A request budget on `electronics` applies to
  every leaf under it. The pacing floor sits on the source and applies
  wherever that source appears.
- **One provider, one source record, many leaves.** anhoch.mk under
  laptops and under keyboards is one source: one rate floor, one budget
  across both, one quarantine when it blocks a node. What differs per leaf
  is the jobs and the adapter.
- **The adapter is selected by the leaf.** The leaf's source gives the
  platform, the nearest operation with a domain gives the domain:
  `sportm` × `shoes` → `sportm.shoes`. Nothing is guessed from the page.
- **A listing belongs to exactly one namespace.** A keyboard never shows
  up in `laptops`.

### The apples rule

The common model is the centre, and the operation owns it. We want
apples; the stores are where we get them. We go to each store, pick
apples, and leave.

- An operation defines its model. `shoes` says what a Shoes product and
  listing look like. Sport M's site structure never changes that.
- A leaf only says how its provider fills that model: which category
  slugs, brand codes and page fields map onto Shoes.
- We harvest only what the operation needs. The discover job under
  `shoes/sportm` walks the shoes category, not the whole site. If a
  provider cannot filter and a page comes back mixed, the adapter takes
  the apples and ignores the rest.
- A provider never decides which common models get updated. A page
  harvested under `shoes/sportm` can only produce Shoes entities. Sport M
  clothes, one day, is a new leaf under `clothing/<something>` with its own
  jobs, never a side effect of the shoes run.
- There is no shared "discover everything from provider X" job feeding
  several operations. Each operation discovers its own, even from the same
  store.

## The pipeline

```
                      ┌─────────────────────────────────────────────────┐
  schedule / CLI ───▶ │ Conductor   run = job × strategy × project      │
                      │ dispatches commands, counts items, applies      │
                      │ retry budget, pauses for decisions              │
                      └──────┬──────────────────────┬───────────────────┘
                harvest.command               adapt.command        ▲ item.completed
                             ▼                      ▼               │
 providers ──HTTP──▶ Harvester ─lake.item.stored─▶ Adapter ─entity.adapted─▶ Store writer ─▶ Store ─▶ API · MCP · exports
 (on a node)        (fetch, write)                (raw → model)             (match, upsert) (by project id)
                        │ writes                    ▲ reads  │ writes           ▲ reads
                        ▼                           │        ▼                  │
                      Lake ─────────────────────────┘     Staging ──────────────┘
                   (retention)
```

Every arrow with a message name is a RabbitMQ message. Messages are
signals: ids, keys, versions and counts, never a page body or an entity.
Data goes into the lake or the staging collection and is read from there.
Harvesters, adapters and the store writer know only their input message
and their output message. They never decide what happens next; the
conductor does.

### Conductor, runs, and what a command reads

A run attaches to a namespace. Its job is a list such as this one, on the
leaf `clothing/shoes/sportm`:

```yaml
job: sportm.discover
commands:
  - harvest: sportm.catalog-pages      # the shoe listings only, optionally by brand
  - adapt:   sportm.shoes              # tiles → Listing + Product candidates, no details yet
```

Other jobs on the same leaf:

```yaml
job: sportm.details
commands:
  - harvest: sportm.product-page       # every Sport M listing with no details, or stale ones
  - adapt:   sportm.shoes

job: sportm.stock                       # planned: no stock command yet
job: sportm.live-check                  # planned: parameter one listing id
```

Seeded jobs: `nbshop.discover`, `nbshop.sitemap`, `nbshop.details`,
`sportm.discover`, `sportm.details`. One adapt command per platform ×
domain.

A command does not receive a list from the previous command. It reads the
state of the store: "every Sport M listing with no details", "every
listing whose `stockCheckedAt` is older than its refresh interval". That is
why a stock job needs no listing walk, and why a listing can be created
from a single URL ("watch this product") and have its details and stock
harvested by the ordinary jobs.

A run keeps a live record, updated on every message:

- **run**: namespace, job, strategy, project, parameters, who started it,
  state, started, finished; a run started on a parent node is one run per
  leaf, grouped;
- **run command**: one per line of the job; order, state, derived totals;
- **run item**: one per message; queued, running, done, failed, retrying,
  skipped, waiting for decision; attempts, node, started, finished, error
  class and text, the lake key or entity ids it produced;
- **run event**: an append-only log; every state change and every
  administrator decision.

Progress for the CLI, the API or a page is a query over run items, never a
counter.

### Strategy: how a run executes

```yaml
strategy: slow-and-spread
mechanism: http                       # or browser
nodes: { count: 1, geo: any }         # or geo: eu, or a named node
pacing: { mode: spread, window: 24h, jitter: 30s }
adapt: batch                          # or streaming
retry:
  attemptsPerItem: 3
  deadline: 36h
  failureRatio: 0.2                   # stop the command if more than 20 % of items fail
  nodesTried: 2                       # give up on an item after two different nodes failed it
```

Pacing modes: **burst** (everything at once), **interval** (one request
every N seconds), **jitter** (random gap in a range), **spread** (N requests
spread over a window), **budget** (at most N per hour against a host). The
source carries the politeness floor; a strategy can only be slower than
it.

### Failure, the retry budget, and decisions

- Failure is per item, never per batch. One blocked node fails its own
  items; those go back to the queue for another node, and the node is
  quarantined for that host for a while. The other 99 products never
  notice.
- The retry budget says how long to keep trying: attempts per item, a
  deadline for the command, a failure ratio, nodes tried, cost later.
- When the budget is spent, the item, or the whole command if the ratio
  tripped, goes to **waiting for decision** and the run pauses there. The
  system never marks anything failed on its own.
- An administrator decides: retry on another node, retry later, skip these
  items, abort the run, quarantine the provider. CLI first, a page later.
  Every decision is a run event.

### Harvester

- Input: a `harvest.command` with the command name, provider, the ids to
  fetch, the strategy's mechanism and pacing.
- Does: fetches, with the source's politeness floor and the strategy's
  pacing, on the node the strategy selected.
- Output: each page is written to the lake through the lake library,
  `{ project, source, kind, providerId, url, harvestedAt, contentType,
  body }`.
  - After a write that made a new version, one `lake.item.stored`.
  - Then one `item.completed` per command: `Done`, or `Failed` with the
    first five failed items in the text.
  - A failed page is a per-item failure; the other pages carry on.
- Mechanisms: HTTP; a browser for pages that need clicks (sizes per branch,
  forum buttons). A browser harvest is an ordinary command that needs a
  node with a browser, paces slower, and emits whatever the page loaded,
  often the site's own JSON.
- Does not: parse, normalize, know what a product is, or start anything.

Commands built 2026-11-12: `nbshop.sitemap`, `nbshop.listing-pages`,
`nbshop.product-page`, `sportm.catalog-pages`, `sportm.product-page`.

- Each lives in `Collector.Providers.<Platform>`, listed in its
  `<Platform>Module.HarvestCommands`.
- Their rules are in `docs/research/`.
- HTTP goes through `PoliteFetcher`: one connection per host, a minimum
  interval per host, robots.txt, 3 attempts on network errors, 5xx and 429
  with Retry-After honoured.
- `PacedFetcher` adds the strategy's pacing on top; it can only be slower
  than the source floor.

### Nodes and rate limits

- A node executes harvest commands. It declares mechanisms (http, browser)
  and location, and paces per host on its own IP.
- The conductor keeps a per-source budget across all nodes and all
  operations: a hundred polite IPs hitting one shop at once still looks
  like an attack, and so do the shoes run and the clothes run arriving in
  the same hour.
- The box is the first node and talks to RabbitMQ directly. Nodes outside
  the house never join RabbitMQ: they pull work and upload page bodies
  through a small HTTPS work API with a per-node token. The work API
  writes them to the lake and sends the same signal, so nothing after the
  lake sees the difference. A Raspberry Pi is fine; an Arduino is below
  the line.

### Lake

- Key: `project / source / kind / providerId`; every item carries
  `harvestedAt`.
- Retention per project (`projects.lakeRetention`), versions count from 1
  per key:
  - **full history**: every harvest is a new version;
  - **changes only**: a new version only when the body hash differs from
    the latest; an unchanged page writes no body, only moves the latest
    version's `checkedAt` forward, and sends no signal;
  - **latest only**: the harvest is written as the next version, then
    every older version is deleted, index row and body.
- `checkedAt` is the last time the page was seen. Adapters stamp it as
  every entity's `harvestedAt`, the observation time the store writer
  compares, and as a detail page listing's `infoCheckedAt` and
  `stockCheckedAt`. Tiles never set the checked dates.
- Storage, built 2026-11-12: bodies gzip-compressed on disk, one file per
  version; `lakeItems` index in MongoDB `{ key, source, kind, providerId,
  version, url, contentType, harvestedAt, checkedAt, hash, size, location }`.
  - `hash` = SHA-256 of the uncompressed body; every read checks it.
  - Root: `COLLECTOR_LAKE`, default `.lake/` next to `Collector.slnx`
    (git-ignored).
  - Path: `<projectId>/<source>/<kind>/<providerId>/<version, 6 digits>.gz`.
  - The provider id becomes a readable prefix plus 16 hex of its SHA-256,
    so any id is a safe folder name.
  - The body file is written before its index row; a body with no row is
    never read.
- A library, not a service: the harvester and the work API write through
  it, the adapter reads through it. It publishes nothing.
- The harvester publishes `lake.item.stored { projectId, runId, itemId,
  key, version, hash, size }` after each write that made a new version.
- Answers queries by key or time for re-runs and later aggregations.
- Price history, where a project keeps history, is a reader of the lake,
  not a special collection.

### Adapter

- Input: an `adapt.command { namespace, command, lakeKeys }`, either as
  items land (streaming) or once the harvest command is done (batch), as
  the strategy says.
  - Empty `lakeKeys` = the latest version of every lake item of the leaf's
    source in the adapter's raw kinds.
  - Raw items of another source or kind are skipped; entities of another
    project or namespace are rejected.
- Selection: one adapter per platform × domain. The leaf's source gives the
  platform, the nearest operation with a domain gives the domain:
  `sportm` × `shoes` → `sportm.shoes`. `nbshop.shoes` serves Sport Vision,
  Sport Reality and Buzz.
- Does: parses the provider's raw shape, maps it to entities of the
  domain, assigns our ids, keeps provider id and `sku` both, sets
  `infoCheckedAt` or `stockCheckedAt` on the listing.
- Output: one document per entity in the `staging` collection, then one
  `entity.adapted` signal per document; then `item.completed`.
- Does not: write products, listings or categories, or match anything.
  It maps one provider's raw shape to the common model, nothing more.
- Idempotent: the same raw item always yields the same entities with the
  same ids. Because ids are deterministic, an adapter can reference an
  entity it has not seen yet, a product's category, by computing its id.

### Staging

- A MongoDB collection between the adapter and the store writer.
- One document per adapted entity: `{ projectId, runId, kind, entityId,
  lakeKey, lakeVersion, adaptedAt, state, entity }`.
- `state`: `pending` until the store writer applies it, then `applied` or
  `rejected` with a reason.
- Re-applying a staging document gives the same result, so a run can be
  re-applied after a matching rule changes without harvesting or adapting
  again.

### Store writer

- Input: an `entity.adapted` signal `{ projectId, stagingId }`. No consumer
  runs yet: CLI `apply` reads `pending` staging documents in harvest order
  (entity `harvestedAt`, then `adaptedAt`).
- Does, in one MongoDB transaction per staging document:
  - reads the staging document;
  - matches it inside the project:
    - a product with a brand and a `sku` gets the canonical id
      `uuid5(projectSeed, "product:<brand, lower case>:<SKU, upper case>")`;
    - the provider's own product id is kept in the product's `aliases`;
    - a product without a brand or `sku` keeps its provider id and
      matches nothing yet;
    - a listing points at its provider's product id; the store writer
      resolves it through `aliases`, in whichever order they arrive;
  - upserts products, listings and categories in the store;
  - recomputes the product's `anyListingInStock`;
  - marks the staging document `applied`, or `rejected` with the reason.
- Merge rules:
  - identity fields and `createdAt` are set on insert only;
  - an empty value never overwrites a stored one;
  - a staged entity harvested earlier than the stored one only fills
    fields that are missing, so applying in any order gives the same
    documents;
  - `infoCheckedAt` and `stockCheckedAt` only move forward;
  - each alias carries a claim time (`aliasClaims.<candidateId>`, only
    moves forward): a provider's product moves to another canonical
    product, e.g. after a corrected `sku`, only when the staged page is at
    least as new as the claim. A stale page only fills gaps on the
    current owner.
- `anyListingInStock`: true when any listing is in stock, false when at
  least one listing was stock-checked and none is, otherwise unknown
  (null).
- Output: none yet; `item.completed` arrives with the conductor.
- The only component that writes products, listings and categories.
- A leaf like the others: it never starts a harvest, an adapt or a run.

### Ids

Deterministic: a UUIDv5-style hash of the project seed and the
provider-qualified key, `uuid5(projectSeed, "sportvision:listing:12345")`.
The same provider item in two projects yields two different ids; the same
item harvested twice in one project yields the same id. The seed is the
project id.

### Entities, domains, products and listings

```
Entity:   id, projectId, namespace, provider, providerId, kind, sourceUrl,
          harvestedAt, createdAt, updatedAt

Product:  Entity + name, brand, sku, categoryId, images, aliases,
          anyListingInStock, domain fields
Listing:  Entity + productId, title, description, price, oldPrice, currency,
          availability, sizes[] (label, system, ean, inStock, branch),
          url, refreshInterval, infoCheckedAt, stockCheckedAt
Category: Entity + name, parentId, path

Shoes domain fields on Product: colourway, model code.
Electronics, CarParts (later): their own.
```

The dates live on the listing because stock at one shop and stock at
another are checked by different runs at different times. The product
carries a derived "any listing in stock". Matching listings into one
product happens inside a project, in the store writer, on brand + `sku`.
Providers without a `sku` are not matched yet. `refreshInterval` is a
generic field on every listing so a job can select "due for a check";
nothing in it is specific to a domain.

### Store

MongoDB, chosen 2026-11-12. One database, `projectId` on every document,
including sources, jobs, strategies, runs and the lake index. Every index
starts with `projectId`, except the unique project name on `projects`,
whose `_id` is the project id. Export, archive and delete of a project are
"everything where projectId = X".

- Our deterministic id is the document `_id`.
- Domain fields live in the entity document. A new domain needs no
  schema change.
- A listing's `sizes[]` is embedded in the listing document.
- Run progress is an aggregation over `runItems`, grouped by command and
  state.
- Writes that must land together, such as a run's state and its events,
  use multi-document transactions. Those need a replica set, so even the
  local rig runs MongoDB as a single-node replica set.
- Access through the official MongoDB C# driver. No ORM.

Platform collections: `projects { _id, name, lakeRetention, seed }`,
`operations { _id, projectId, parentId, path, domain, sourceId,
settings, jobs: [{ jobId, strategyId, schedule }] }`, `providers`,
`sources`, `nodes`, `jobs`, `strategies`, `runs`, `runCommands`,
`runItems`, `runEvents`, `lakeItems`, `staging`. The domain lives on the operation,
not on the project.

### Serving

Everything that reads takes a project id: the ASP.NET Core API, an MCP
server, CSV/Parquet exports, dashboards. A read returns what is known plus
`infoCheckedAt` and `stockCheckedAt`; the caller decides whether that is
fresh enough. A live check is a run of the provider's live-check job with
a priority strategy, for one listing. Who may trigger one, and how often,
is a per-project policy, decided per tenant later.

### Brand catalogs and matching

A brand's own catalog (adidas first, Nike second) is another provider
with its own commands and adapter into the same domain. Its output is
products and listings like any other; matching to shop listings on brand + `sku`
is in-project work that comes after the pipeline runs end to end.

## Messaging

RabbitMQ, one queue per consumer:

| Message | Producer | Queues (consumer) |
|---|---|---|
| `harvest.command` | Conductor | `harvester.harvest-command` (priority, max 10, prefetch 1) |
| `lake.item.stored` | Harvester (or the work API) | `conductor.lake-item-stored`, `adapter.lake-item-stored` |
| `adapt.command` | Conductor | `adapter.adapt-command` |
| `entity.adapted` | Adapter | `store-writer.entity-adapted` |
| `item.completed` | Harvester, Adapter (store writer: not yet) | `conductor.item-completed` |

- One durable topic exchange named by the prefix (`collector`); the
  routing key is the message name.
- Queue names are `<prefix>.<queue>`.
- Live checks share the harvest queue and jump it with a higher message
  priority. Prefetch 1 there, so priority works; 16 on the other queues.
- Every queue has `<queue>.retry` and `<queue>.dead`.
- A failed message waits in `.retry` (per-message TTL) and comes back:
  1 s, doubling, at most 10 min, 5 attempts.
- After the last attempt, or with an unreadable body, it goes through
  `<prefix>.dead` to `<queue>.dead`.
- Messages are persistent JSON, published with confirms; headers carry
  `projectId`, `runId`, `itemId`.
- `collector mq init` declares it all; `COLLECTOR_RABBIT` overrides the URI.

Messages are signals and stay small: no page body ever travels over
RabbitMQ, so there is no message size cap to manage (decided 2026-11-12).

Decided 2026-11-12: the plain `RabbitMQ.Client`, no MassTransit. Our code
declares every exchange, queue, binding, dead-letter and priority setting
itself, so the topology is ours to shape.

## Solution layout

- Workers, Node and Api are created with their first real code, and join
  the rig in the same change.
- A new platform gets its own `Collector.Providers.<Platform>` project,
  holding its harvest commands and adapters, and its own test project.
- Each library has its own test project, so parallel work never shares a
  build.

```
Collector.slnx
rig/compose.yaml                    the local rig: MongoDB (single-node replica set) and RabbitMQ
src/Collector.Core/                 Entity, domains, ids, message contracts, job and strategy schemas
src/Collector.Data/                 MongoDB collections, class maps, index setup
src/Collector.Harvesting/           polite and paced fetchers, robots, host budget, HarvestRunner
src/Collector.Lake/                 lake library: write with retention modes, read, query
src/Collector.Messaging/            RabbitMQ topology, publisher, consumer with retry and dead letters
src/Collector.Adapting/             IAdapter, adapter catalog, AdaptRunner: lake → staging
src/Collector.Storing/              store writer: staging reads, matching, merge upserts, derived fields
src/Collector.Providers.Nbshop/     NBSHOP harvest commands, nbshop.shoes adapter
src/Collector.Providers.SportM/     Sport M harvest commands, sportm.shoes adapter
src/Collector.Conducting/           conductor: runs, items, retry budget, decisions (empty so far)
src/Collector.Cli/                  dev CLI: db init, mq init, seed, harvest, adapt, apply, stats
src/Collector.Workers/              not created yet: one host, its role is configuration
src/Collector.Node/                 not created yet: the external node over HTTPS
src/Collector.Api/                  not created yet: project-scoped reads, decisions, work API
tests/Collector.Tests/              ids, BSON mapping
tests/Collector.<Library>.Tests/    one per library and provider; Lake, Messaging, Storing need the rig
```

Libraries: AngleSharp for HTML, `System.Text.Json` for JSON-LD and
messages, `MongoDB.Driver`, `RabbitMQ.Client`, xUnit. HTTP is our own
`PoliteFetcher`. Playwright arrives with the browser mechanism (step 7).

## Build order

1. **Done 2026-11-12.** Solution skeleton; `Core` with Entity, ids,
   message contracts, the job and strategy schemas, the operations tree,
   the Shoes domain; `Data` with the platform collections, Product,
   Listing, Category and their indexes. RabbitMQ and MongoDB running
   locally in the compose rig.
2. **Done 2026-11-12.** Harvesting: HTTP fetcher, pacing, the NBSHOP and
   Sport M commands built from `docs/research/`; the lake library with the
   three retention modes; `lake.item.stored` from the harvester; the
   RabbitMQ topology.
3. Conductor: jobs and strategies as data, runs with commands, items and
   events, retry budget, waiting for decision; CLI `run` and `decide`.
   Not started: `Collector.Conducting` is empty.
4. **In part, 2026-11-12.** Shoes adapters on both platforms into staging
   and the store writer with brand + `sku` matching are built. `discover`
   and `details` run end to end through the dev CLI, stage by stage,
   without the conductor. Open: through the conductor; discovery by title
   text.
5. `stock` job with spread pacing; schedule; `live-check` job on the
   priority queue.
6. API: project-scoped reads with the two dates, run progress, decisions.
7. Node: `Collector.Node` and the work API; browser mechanism.
8. Brand catalogs and matching; project export, archive, delete.

## Open decisions (owner: you)

- **Where it runs, later.** For now everything runs on the box, in the
  WSL compose rig, and the box is the first node. Moving to the Proxmox
  VM stays open.
- **Lake bodies**: disk for now (built). An S3-compatible store or GridFS
  stays open for later.
- **Unchanged pages and the conductor**: under changes-only an unchanged
  page sends no `lake.item.stored`. A conductor that adapts from signals
  needs those keys too: a signal with `changed: false`, or the keys on the
  run item.
- **Live-check policy** per project: who may trigger, how often.
- **Refresh tiers**: `refreshInterval` exists on every listing from day
  one; the rule that sets it (popular daily, idle weekly) comes later.
- **Aggregations over the lake**: mechanism defined later.
