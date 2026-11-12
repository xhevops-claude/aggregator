# Sport Reality MK — site research

Status: **verified with direct access** (2026-10-27). Live as
`sportreality_mk` through the shared Sport Vision adapter.

- Site: https://www.sportreality.mk (redirects to `/mk/`), Macedonian;
  `/al/` Albanian mirror exists.
- Operator: Sport Vision Group sister brand (value/outlet positioning:
  Kronos, Nike, adidas, Under Armour...). Platform: **NBSHOP 5.9.58**
  (sportvision.mk runs NBSHOP 7; markup is identical for our purposes).
- Currency: MKD.
- Quirk: the server occasionally resets the TCP connection on the first
  request (`Recv failure: Connection reset by peer`); the client's retry
  handles it.

## Everything matches sportvision.mk

| Item | Sport Reality |
|---|---|
| robots.txt | Same template as Sport Vision: blocks `?search=`, `?SID=`, `*.php`; allows products/listings. |
| Sitemap index | `/files/sitemap/sitemap.xml` → `MAK_mk/` and `MAK_al/` |
| Product sitemap | `/files/sitemap/MAK_mk/product.xml` — **5,605 URLs** |
| Listing sitemap | `/files/sitemap/MAK_mk/list_product.xml` — 121 campaign listings under `/mk/proizvodi/<slug>` (e.g. `sezonsko_namaluvanje`, `posledni-parcinja`, `na-akciji`) |
| Product URL | `/mk/<category>/<numeric-id>-<slug>`, e.g. `/mk/dukser/181711-under-armour-icon-fleece-hoodie` |
| Product page | Server-rendered, schema.org Product JSON-LD (name, sku, productID, brand, image[], offers.price/priceCurrency/availability) |
| Old price | `product-oldprice` block, `hide` class when not discounted — parsed correctly, e.g. Kronos Hell: 327 MKD, old 1,090 MKD |
| Listing tiles | NBSHOP tile anchor matches (48 tiles on `/mk/patiki/zenski`, duplicates per colour de-duped) and `/page-N` pagination works |
| Sale listing | `/mk/proizvodi/?outlet=yes` (NB: `/mk/proizvodi/sale` is 404 here, unlike Sport Vision) |

Verified parse of a live page, `/mk/dukser/181711-under-armour-icon-fleece-hoodie`:

| Field | Value |
|---|---|
| provider id | `181711` |
| sku | `6013544-622` |
| name | Under Armour Icon Fleece Hoodie |
| brand | Under Armour |
| category | `dukser` |
| price | 3490 MKD |
| old price | none |
| availability | `InStock` |
| images | 3 URLs |

## Findings 2026-11-12

Live, polite, while building the NBSHOP harvest commands and Shoes adapter.

- Product sitemap: 5,581 URLs; 2,142 in shoe categories. Only shop with
  `baletanki` (2). Counts per slug in [nbshop-platform.md](nbshop-platform.md).
- Outlet listing `/mk/proizvodi/?outlet=yes`: 28 pages of 24 tiles.
  - The next link is `/mk/proizvodi/?outlet=yes/page-2`: `/page-N` goes after the query.
  - Page 1 is all `papuci`.
- Manufacturer code label is Serbian: "Šifra proizvođača" in `div.product-model`.
- Sizes are listed out of order (`5`, `9-`, `9`, `8-`, …). Available sizes carry EAN-13s.
- Saved as test samples (gzipped, `tests/Collector.Providers.Nbshop.Tests/Samples/`):
  - outlet listing page 1;
  - `/mk/patiki/181427-adidas-vs-pace-2-0`: 3,288 MKD, not discounted, sku `HP6005`,
    model code `LSH59`, 10 of 20 sizes in stock;
  - `/mk/dukser/181711-under-armour-icon-fleece-hoodie`: the non-shoe page,
    ignored by the Shoes adapter.
- The hidden old-price block of a non-discounted page holds the current price (3.288).
- 30 old products keep 3- or 4-digit ids, e.g. `/mk/corapi/9446-slazenger-trainer`;
  one of them is in `patiki`. The sitemap finds them; listing tiles skip them.
