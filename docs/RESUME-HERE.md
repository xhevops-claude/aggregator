# Resume here

Last updated 2026-11-12 on the owner's box (local session).

## Where things stand, in one minute

- **Design agreed 2026-10-27/28: the Collector platform.** C# on .NET 10,
  RabbitMQ between stages, MongoDB.
  A project is a tree of
  **operations** (`clothing/shoes/sportm`); each leaf holds job ×
  strategy pairs; a Conductor runs **runs** on a namespace; harvesters and
  adapters are leaves that never decide anything; a lake keeps raw items;
  adapters produce Products and Listings of the operation's domain with
  deterministic ids; the operation owns the model and harvests only what
  it needs (the apples rule); projects are hard partitions. All of it,
  with the vocabulary, in [ARCHITECTURE.md](ARCHITECTURE.md).
- **Night session 2026-11-12, 01:20 to 03:45: build steps 1 and 2 done,
  step 4 in part. Nothing is committed: review the diff, then commit.**
- **Step 1**: solution, `Core` (entities, Shoes domain, UUIDv5 ids,
  platform records, runs, lake, staging, messages), `Data` (conventions,
  class maps, typed collections, 24 indexes), `rig/compose.yaml` (MongoDB
  8.0 single-node replica set, RabbitMQ 4.1).
- **Step 2 and most of step 4**, built by parallel agents, each reviewed
  adversarially and fixed:
  - `Harvesting`: `PoliteFetcher` (per-host floor, robots, retries,
    Retry-After), `PacedFetcher` (strategy pacing), `HarvestRunner`.
  - `Lake`: gzip bodies under `.lake/`, `lakeItems` index, three retention
    modes, `checkedAt` on unchanged pages.
  - `Messaging`: raw `RabbitMQ.Client` topology, publisher with confirms,
    consumer with delayed retries and dead letters.
  - `Adapting`: adapter catalog, `AdaptRunner` (lake → staging).
  - `Storing`: store writer, brand + `sku` matching with aliases, merge
    rules by harvest time, tri-state `anyListingInStock`.
  - `Providers.Nbshop`: `nbshop.sitemap`, `nbshop.listing-pages`,
    `nbshop.product-page`, `nbshop.shoes`.
  - `Providers.SportM`: `sportm.catalog-pages`, `sportm.product-page`,
    `sportm.shoes`.
- **Tests**: 507, one test project per library and provider. The
  Lake, Messaging and Storing tests need the rig.
- **Live end-to-end run on the rig** (project `mk-shoes`, adidas shoes):
  - Sport M, Intersport and Sport Vision: one listing page each, three
    product pages for Sport M and for Sport Vision.
  - 38 products, 67 listings, 6 with details (sizes, EANs, old prices).
  - adidas `JS3036` matched across Sport M and Intersport into one product.
  - Found live and fixed: a Sport M product page with an empty breadcrumb
    (`HQ8708`) was dropped; an empty breadcrumb now counts as unknown.
- **Integration review** (4 lenses, skeptic per finding): 11 confirmed.
  Fixed and verified the same night:
  - an unchanged re-harvest now moves `checkedAt`, and with it the
    listing's `infoCheckedAt` and `stockCheckedAt`. Live: `HQ8708`
    re-checked at 02:02 UTC, lake still version 1, listing dates moved;
  - the store writer no longer lets an older tile overwrite a newer
    product page, and applying in any order gives the same documents;
  - `anyListingInStock` is unknown (null) when no listing was
    stock-checked. Live: 6 true, 32 unknown, 0 false;
  - run records end `WaitingForDecision` on failures and `Aborted` on
    errors;
  - a second review of those fixes (03:05): entities now carry the last
    observation time (`checkedAt`) as `harvestedAt`, so a re-checked page
    is not "older" than a tile;
  - the last high finding, fixed at 03:15 behind a backup and checked by
    a skeptic over 13 scenarios: a corrected `sku` is now
    order-independent (alias claim times, ARCHITECTURE.md § Store writer).
    Only a staged entity with no harvest time can still win by order; the
    pipeline never produces one.
- The store, listings and staging in the rig were rebuilt from the lake
  with the final code at 03:17; the lake itself was kept.
- **Not built yet**: the conductor (`Conducting` is empty), `Workers`,
  `Api`, `Node`. Each host joins the rig when it gets real code.
- **Nothing consumes the queues yet**: the night run's signals sit in the
  rig's queues. Harmless; purge them or let the first workers drain them.
- **Five Macedonian sportswear shops on two platforms are researched and
  verified with live crawls.** Their harvest and parse rules are written
  down in `docs/research/`:
  - [nbshop-platform.md](research/nbshop-platform.md): Sport Vision, Sport Reality, Buzz.
  - [sport-m-platform.md](research/sport-m-platform.md): Sport M, Intersport.
  - [sources.md](research/sources.md): overview and fetching defaults.
- Every shop exposes the brand's own article code as `sku`, so matching to
  brand catalogs later is exact (adidas `HP6005` etc.). First project:
  shoes in North Macedonia; adidas first, Nike second.
- The cloud session's IP was blocked by Cloudflare on dsport.mk and by
  Akamai on adidas.com. That is why work moved to the owner's box, which
  is the first node.
- System map (visual): https://claude.ai/artifact/KVfRL3UyPBNhiJv6AG9FNn

## Set up locally

.NET SDK 10.0.401 is installed on the box (`dotnet --version`). Docker
28 with Compose 2.35 runs in WSL Ubuntu. Rig, ports and logins: README.md
§ Local rig.

```bash
wsl docker compose -f rig/compose.yaml up
dotnet build
dotnet test
```

The repo has its own `nuget.config` with nuget.org only, so restores never
reach for a work feed.

## Sources

| Source key | Shop | Platform | Status | adidas | Nike |
|---|---|---|---|---|---|
| `sportvision_mk` | sportvision.mk | NBSHOP | verified | 2,051 | 2,784 |
| `sportreality_mk` | sportreality.mk | NBSHOP | verified | 1,324 | 1,183 |
| `buzzsneakers_mk` | buzzsneakers.mk | NBSHOP | verified | 911 | 1,569 |
| `sportm_mk` | sport-m.com.mk | Sport M (ASP.NET) | verified | ~4,000 | ~380 |
| `intersport_mk` | intersport.mk | Sport M (ASP.NET) | verified | ~1,150 | ~160 |
| — | dsport.mk | Magento 2 + Cloudflare | **blocked from cloud IP** | ? | ? |

Per-site research: `docs/research/*.md`; overview with SKU levels:
`docs/research/sources.md`. Wrong domains to avoid: `sportm.mk` (news
site), `buzz.mk` (parked), `intersport.com.mk` / `sportm.com.mk`
(unreachable).

## Verified facts worth not re-deriving

- adidas VS Pace 2.0 on Sport Vision/Sport Reality: article codes
  `HP6005`, `HP6002`, `KI6514` (three colourways); model code `LSH59`
  ("Код на производителот" on Sport Vision); per-size EAN-13 in
  `data-combination-code` on Sport Vision size `<li>`s.
- Sport M platform brand codes (same on both shops): adidas `01`, Nike
  `02`, Puma `04`; full table in `docs/research/sport-m-platform.md`.
- Sport M listing: 18 tiles/page; stop when a page yields no new tiles
  (the "next" arrow is always rendered). Sport M and Intersport use
  different tile layouts; both are specified.
- NBSHOP: JSON-LD descriptions can contain raw newlines; `System.Text.Json`
  needs the string sanitised before parsing. `/mk/proizvodi/sale` is 404
  on Sport Reality; use `/mk/proizvodi/?outlet=yes`. Filter slugs differ
  per shop (`/mk/patiki/zenski` is 404 on Buzz).
- Sizes on the Sport M platform: any listed size = purchasable; sold-out
  sizes are omitted, not marked. `Listing.sizes[]` has a place for them,
  with branch.
- Brand casing differs ("adidas" NBSHOP vs "Adidas" Sport M).

## Known issues from the night review (not fixed yet)

- **Lost signal**: `HarvestRunner` writes the lake, then publishes
  `lake.item.stored`. If RabbitMQ fails in between, the page is in the lake
  but the signal is gone, and a changes-only re-run will not resend it. Fix
  with the conductor: an outbox, or unchanged keys on the run item.
- **Unchanged pages send no signal** under changes-only: ARCHITECTURE.md
  § Open decisions.
- **An adapt command with no lake keys** re-adapts every latest lake item
  of the source under a new run id, so staging grows with every adapt.
  The conductor will pass the keys of its own harvest.
- **Sport M sizes** are stored without a size system (the shop's own, UK
  for adidas); the EU size on the same element is dropped.
- **Sport Vision listing-page tiles** keep only title and URL: the tile's
  price is not used, and those listings have no product until their
  product page is harvested.
- **Price scale**: JSON-LD prices keep 2 decimals, HTML old prices none.
- **The store writer publishes no `item.completed` yet.**
- **Lake touch race** (low): `LakeStore.TouchAsync` moves `checkedAt` on the
  version it read; a concurrent changes-only writer can add a newer
  version in between, so the touch lands on a superseded version.
- **Equal harvest times** (low): two staged entities with the same
  `harvestedAt` and different values end in apply order.

## Next steps, in order

1. **Review and commit the night's work** (nothing is committed).
2. **Decide the open items** in ARCHITECTURE.md § Open decisions,
   including how unchanged pages reach the conductor.
3. **Build order step 3, the conductor**: jobs and strategies as data,
   runs with commands, items and events, retry budget, waiting for
   decision; `Workers` as the first host, added to the rig.
4. **Check the two blocked sites from the box's IP**:
   - `https://www.dsport.mk/media/sitemap/dsport.xml` and whether
     `/amfeed/feed/download` serves a public product feed
     (docs/research/dsport-mk.md has the plan).
   - `https://www.adidas.com/us/vs-pace-2.0-shoes/HP6005.html` resolves
     (confirms article-code URLs); look for a product API/feed.
5. Then steps 5 to 8 of the build order in ARCHITECTURE.md.

## Things explicitly not done / not to do

- No CI yet.
- Do not try to get around Cloudflare/Akamai. Use an allowed node and the
  polite fetcher.
- The non-MK Sport Vision storefronts are not enabled; their sitemap
  region and currency are guesses until checked
  (`docs/research/nbshop-platform.md`).

## Artifacts

- System map of the Collector platform (private to the owner):
  https://claude.ai/artifact/KVfRL3UyPBNhiJv6AG9FNn
- Older one-page plan from the cloud session, superseded:
  https://claude.ai/artifact/Sz7KqmAcHnceRKNyMeamys
- Cloud session: https://claude.ai/code/session_01TaFuhUT1hkbaxPQeREF9wU
