# Resume here

Last updated 2026-11-11 on the owner's box (local session).

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
  with the vocabulary, in [ARCHITECTURE.md](ARCHITECTURE.md). Nothing of
  it is built yet.
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

.NET SDK 10.0.401 is installed on the box (`dotnet --version`). RabbitMQ
and MongoDB are not running anywhere yet; they arrive in the compose rig
with build step 1. Once the solution skeleton
exists:

```bash
dotnet build
dotnet test
dotnet run --project src/Collector.Cli -- run clothing/shoes/sportm sportm.discover --strategy fast --brand adidas --limit 5
dotnet run --project src/Collector.Cli -- runs                      # progress, per item
dotnet run --project src/Collector.Cli -- decide <run> retry-elsewhere
```

No tests exist yet.

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

## Next steps, in order

1. **Decide the open items** in ARCHITECTURE.md § Open decisions (where
   it runs, lake body storage, live-check policy).
2. **Check the two blocked sites from the box's IP**:
   - `https://www.dsport.mk/media/sitemap/dsport.xml` and whether
     `/amfeed/feed/download` serves a public product feed
     (docs/research/dsport-mk.md has the plan).
   - `https://www.adidas.com/us/vs-pace-2.0-shoes/HP6005.html` resolves
     (confirms article-code URLs); look for a product API/feed.
3. **Build order step 1**: solution skeleton, `Core` (Entity, ids,
   message contracts, job and strategy schemas, Shoes domain), `Data`
   (platform collections, Product, Listing, Category, indexes),
   RabbitMQ + MongoDB in the compose rig.
4. Then steps 2 to 8 of the build order in ARCHITECTURE.md.

## Things explicitly not done / not to do

- No PR was opened; no tests written; no CI; no .NET code yet.
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
