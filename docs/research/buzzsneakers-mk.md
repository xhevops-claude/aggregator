# Buzz Sneakers MK — site research

Status: **verified with direct access** (2026-10-27). Live as
`buzzsneakers_mk` through the shared Sport Vision adapter.

- Site: https://www.buzzsneakers.mk (redirects to `/mk/`). `buzz.mk` is a
  parked domain (atom.com landing page) — not the shop.
- Operator: Sport Vision Group sneaker/streetwear brand (Jordan, adidas,
  The North Face, Crocs, Juicy Couture...). Platform: **NBSHOP 5.9.58**.
- Currency: MKD. Macedonian only (`/mk/`), no `/al/` links on the homepage
  though an `MAK_al` sitemap exists.

## Everything matches sportvision.mk

| Item | Buzz |
|---|---|
| robots.txt | Same NBSHOP template; sitemap at `/files/sitemap/sitemap.xml` |
| Product sitemap | `/files/sitemap/MAK_mk/product.xml` — **4,337 URLs** |
| Listing sitemap | `list_product.xml` — 118 campaign listings under `/mk/proizvodi/<slug>` (`sale`, `new`, `mid_season_sale`, `sneaker_of_the_week`, ...) |
| Product URL | `/mk/<category>/<numeric-id>-<slug>`, e.g. `/mk/jakna/396309-the-north-face-jakna-w-saikuru-jacket` |
| Product page | JSON-LD Product, same fields; product names are prefixed with the Cyrillic category word ("The North Face Јакна W SAIKURU JACKET") |
| Old price | `product-oldprice` block; verified on Crocs Classic: 3,032 MKD, old 3,790 MKD |
| Listing tiles | NBSHOP tile anchor matches (83 tiles on `/mk/patiki`); `/page-N` pagination works |
| Sale listing | `/mk/proizvodi/sale` works (77 tiles) |

Caveat on filter vocabulary: `/mk/patiki/zenski` is **404** on Buzz
(works on Sport Vision and Sport Reality) — each shop defines its own
filter segments, so listing paths are not portable across the family.

## Raw newlines in JSON-LD

- The discounted Crocs page carries a literal newline inside the JSON-LD
  `description` string.
- A strict JSON parser rejects it as an invalid control character.
- This affects every NBSHOP shop, not just Buzz.
- Rule in [nbshop-platform.md](nbshop-platform.md): escape control
  characters inside strings before parsing.

## Findings 2026-11-12

Live, polite, while building the NBSHOP harvest commands and Shoes adapter.

- Product sitemap: 4,532 URLs; 1,939 in shoe categories. No `kopacki`.
  Counts per slug in [nbshop-platform.md](nbshop-platform.md).
- `/mk/proizvodi/converse`: 3 pages of 24 tiles. Tile titles carry the
  Cyrillic category word: "Converse Патики Chuck 70".
- Crocs Classic `/mk/papuci/236427-crocs-papuci-classic`:
  - 3,032 MKD, old price 3,790, sku `10001-100`, no model code;
  - the JSON-LD `description` now holds raw TABs, not the raw newline of
    October; the `VideoObject` block too;
  - breadcrumbs: `Обувки > Папучи и апостолки > Папучи`;
  - sizes are Crocs `M7/W9` with EU ranges `39-40`; four sizes show the
    Crocs size in the EU tab too (`M14`, `M15`, `M2/W4`, `M3/W5`);
  - 1 of 18 sizes in stock, with EAN-13 `883503185630`.
- Saved as test samples (gzipped, `tests/Collector.Providers.Nbshop.Tests/Samples/`):
  converse listing page 2 and the Crocs page.
- 13 old products keep 3- or 4-digit ids, 5 of them sneakers, e.g.
  `/mk/patiki/108-adidas-patiki-gazelle`. The sitemap finds them; listing
  tiles skip them.
- Live check: `nbshop.sitemap` plus the Shoes adapter gave 1,934 shoe
  listings before short ids were accepted; the same sitemap gives 1,939 now.
