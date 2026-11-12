# Sport M platform — sport-m.com.mk and intersport.mk

Status: **verified with direct access** (2026-10-27), **re-verified and
built 2026-11-12** (harvest commands `sportm.catalog-pages`,
`sportm.product-page` and adapter `sportm.shoes` in
`src/Collector.Providers.SportM`; see § Findings 2026-11-12), source keys
`sportm_mk` and `intersport_mk`. One set of harvest commands and one
adapter cover both shops; they run the same custom codebase (Intersport's own meta
description reads "Official online shop of Sport M", and both share
identical routes, markup and JS).

| | Sport M | Intersport MK |
|---|---|---|
| URL | https://sport-m.com.mk (`www.` redirects here) | https://intersport.mk |
| Wrong domains | `sportm.mk` = sports news site; `sportm.com.mk` unreachable | `intersport.com.mk` unreachable |
| Catalog size | ~357 pages × 18 = **~6,400 products** (binary-searched: page 357 has 10 tiles, page 358 is empty) | ~260 pages × 18 = **~4,700 products** (pager shows last page directly) |
| Brands | 4F, adidas, Nike, Puma, Skechers, Salomon, Energetics, Jack Wolfskin... | adidas, Nike, Puma, On, Terinda, Asics... |
| Currency | MKD (`12.399,00 ден.` — dot thousands, comma decimals) | same |

Platform: ASP.NET MVC with DevExpress controls (`/DXR.axd`, `ASPx.*`),
PagedList pagination, jQuery. **No robots.txt** (404 — crawl is not
restricted, but keep the 1 req/s default), **no sitemap**, **no JSON-LD**,
no product feed. Pages are fully server-rendered; no JS needed.

## URL structure

| Purpose | Pattern |
|---|---|
| Whole catalog, paginated | `/ProductCatalog?Page=N` (N from 1; empty page = past the end) |
| Segment (top nav) | `/ProductCatalog?Segment=NN` — Sport M: 03 Мажи, 04 Жени, 05 Деца, 06 Опрема, 07 Спортови, 11 Брендови. Intersport: 28 Мажи, 29 Жени, 30 Деца, 31 Опрема, 32 Бренд, 34 Спортови |
| Filters (query params) | `Grupa`, `PodGrupa`, `Podklasa`, `Pol` (M/W), `Vozrast` (A/DECA/C), `Brend` (code, e.g. `01` adidas, `02` Nike, `04` Puma, `69`/`70` 4F/Skechers — see `/Home/Brands`), `MarketingStatus`, `Popust=True`, `CenaOd`/`CenaDo`, `SortBy`, `SearchKey` |
| Product | `/ProductCatalog/ProductDetails?ProductID=<id>` — extra params (Segment/Grupa/...) are breadcrumb context: the breadcrumb echoes them, so always drop them (2026-11-12, § Breadcrumb follows the URL context) |
| Sizes (AJAX) | `POST /ProductCatalog/GetVelicini/` with `ProductID` — used by the add-to-cart popup; not needed, sizes are also in the page |
| Images | `/ProductImages/<id>.webp` (also via `/Image/Show?Pateka=...`); brand logos `/BrandsImages/<Brand>.png` |

**Product id** = the supplier article code, e.g. `3WG10035259` (On),
`JQ5211` (adidas), `414714-569` (Nike), `1304-BLK`, `4FWACABU551-20S`.
Alphanumeric with dashes; stable; use as provider id and `sku`. Colour
variants are separate ids (linked from the product page's colour picker).

## Listing page (18 tiles per page)

Two tile layouts. **Intersport** (newer theme) — each `div.product_partial`
contains:

```html
<img class="ow-brand-image" alt="Terinda" src="/BrandsImages/Terinda.png">   <!-- brand -->
<a href="/ProductCatalog/ProductDetails?ProductID=1304-BLK&Segment=34&..."><img src=/ProductImages/1304-BLK.webp alt=...></a>
<a id="Description 1304-BLK" href="...">Terinda Sport socks FOTTSY</a>          <!-- name -->
<div class="price-box"> 799,00 ден. </div>
<button onclick="OnAddToShoppingCartFromProdList('1304-BLK','/ProductImages/1304-BLK.webp',' 799,00 ден.','799,00 ден.','0','799')">
```

The `OnAddToShoppingCartFromProdList(id, image, normalPrice,
priceWithDiscount, discountFlag, priceNumeric)` call is the cleanest
machine-readable source: id, both prices, a `'1'`/`'0'` discount flag and
the numeric price. Discounted tiles render the regular price in
`.pricePositionDecoration` and the sale price in `.colorRed`.
**Correction 2026-11-12:** the JS parameter names are misleading. The 3rd
argument is the price to pay (the sale price on a discounted tile), the
4th is the regular price, the 6th is the price to pay as an integer, e.g.
`('400603N-BKYL', ..., ' 2.239,00 ден.', '2.799,00 ден.', '1', '2239')`
next to `.colorRed` 2.239 and `.textDecorLine#old_price_pl` 2.799.

**Sport M** (older theme) tiles have only the image link, with the product
name in an *unquoted* `alt` (`<img src=/ProductImages/JZ8106.webp alt=Adidas
LINEAR DUF M CB class="img-list">`), and a `.price-box` with the price; a
discounted tile shows the regular price in `.pricePositionDecoration` and
the sale price in `.colorRed`. No brand logo, so brand is derived from the
first word of the name. **2026-11-12:** Sport M tiles now also carry the
name in `<a class="description-product searchProdDesc">`, and a discounted
tile renders the regular price in `.textDecorLine` (not
`.pricePositionDecoration`) above `.colorRed`, with the badge
`.discount-details1 span` ("20%").

So a **listing-only crawl** (~360 + ~260 requests for both shops) yields
id, name, brand, image, price, old price and discount flag for the whole
catalog. Product pages are only needed for sizes and category.

Pagination: walk `Page=N` until a page has zero tiles. The "next" arrow is
rendered even on the last page, so don't rely on it. The pager window
only shows a few page numbers (Sport M), while Intersport also shows the
last page number. Ordering stability across pages is unverified — expect
some duplicates/misses between runs and de-dup on id.

## Product page

```html
<h2 class="details_description" id='JQ5211 description'>Adidas HOOPS 4.0</h2>
<div class="price-box product-info__price"><span class="price-box__regular">
  <!-- discounted: -->
  <span class=" price-withdiscount">30%</span>
  <div class="normal-price pricePositionDecoration " id="old_price_pd"> 3.990,00 ден.</div>
  <span class="price-withdiscount" id='JQ5211 Price'> 2.793,00 ден.</span><span>(заштеда 1197 ден.)</span>
  <!-- not discounted: -->
  <div class="normal-price" id='3WG10035259 Price'> 12.399,00 ден.</div>
</span></div>
Шифра на производ: JQ5211
<ol class="breadcrumb"> Почетна › Мажи (Segment=03) › Обувки (Segment=03&Grupa=01) </ol>
<ul class="options-swatch options-swatch--size"><li class="size-option"><div> 36.5</div></li> ...</ul>
<img id="ImageDetails" src="/ProductImages/3WG10035259.webp">
```

- Price: element whose `id` is `"<ProductID> Price"`; old price in
  `#old_price_pd` when present.
- Category: breadcrumb trail (segment + group names in Macedonian).
- Brand: first word of the name on the product page; or the
  `ow-brand-image` alt on the listing tile (more reliable).
- Sizes: `li.size-option` list. Only one class variant was observed, so
  out-of-stock sizes appear to be omitted rather than marked — treat
  "no size-option elements" as sold out and the listed sizes as in stock.
  **Needs confirmation** on a product known to be sold out.
- Availability per size, if needed, could be read from the
  `GetVelicini` POST, but a GET-only crawl is preferred.
- `og:` meta tags are site-wide boilerplate (og:url points at the homepage) —
  ignore them.

## Harvest and parse rules

Verified with live crawls on 2026-10-27.

### Storefronts

| Source key | Base URL | Currency |
|---|---|---|
| `sportm_mk` | https://sport-m.com.mk | MKD |
| `intersport_mk` | https://intersport.mk | MKD |

### Catalog walk

- Whole catalog: `<base>/ProductCatalog?Page=N`, from N = 1.
- Filtered: `<base>/ProductCatalog?<query>&Page=N`, e.g. `Segment=03&Grupa=01&Brend=01`.
- Brand filter: `Brend=<code>`, codes below.
- Stop at the first page with no tile that was not seen before.
- De-duplicate tiles on the product id.

### Shoes filter (verified live 2026-11-12)

The shoes operation never walks the whole catalog. Footwear is
`Grupa=01` ("Обувки"), but **only under a gender segment**:

| Source key | Shoe listings (the default of `sportm.catalog-pages`) |
|---|---|
| `sportm_mk` | `Segment=03&Grupa=01` (Мажи), `Segment=04&Grupa=01` (Жени), `Segment=05&Grupa=01` (Деца) |
| `intersport_mk` | `Segment=28&Grupa=01` (Мажи), `Segment=29&Grupa=01` (Жени), `Segment=30&Grupa=01` (Деца) |

- `Grupa=01` alone, and `Segment=11&Grupa=01` (the brands segment), are
  **ignored by the server**: page 1 mixes socks, caps, trousers and shoes.
- Page 1 of all six shoe listings: 18 of 18 tiles are footwear.
- Size on 2026-11-12: Intersport men 32 pages (page 33 is empty), Intersport kids 29 pages.
- Sport M men: page 400 is empty; its pager does not show the last page.
- A unisex shoe can appear under two segments; tiles de-duplicate on the product id.
- Group codes: `01` Обувки, `02` Текстил, `03` Додатоци, `04` Велосипеди, `06` Справи за вежбање (Intersport).
- Shoe sub-groups (`PodGrupa`), same on both shops:

| Code | Name | Code | Name |
|---|---|---|---|
| `503` | Патики-ниски | `779` | Патики за вештачка трева |
| `502` | Патики-високи | `500` | Папучи |
| `599` | Чизми | `508` | Сандали |
| `515` | Фудбал-сала | `587` | Обувки за вода |
| `514` | Фудбал-копачки | `537` | Апостолки |

- Promotions live in their own segment: Sport M `Segment=17&Popust=True`
  (`Grupa=103` = Патики), Intersport `Segment=35&Popust=True`
  (`Grupa=014` = Патики). Those `Grupa` values are menu codes, not product groups.
- `Popust=True` on a gender shoe listing did not restrict page 1 to
  discounted tiles; discounted tiles were only found in the promotion
  segments and on brand-filtered listings.

### Tile footwear check (verified live 2026-11-12)

- Every tile's product link carries **the product's own** `Grupa` and
  `PodGrupa`, whatever the page's filter, e.g.
  `ProductDetails?ProductID=KQ6776&Grupa=03&PodGrupa=518` on the mixed
  `Grupa=01` page, `&Segment=17&Grupa=01&PodGrupa=503` on the promotion page.
- The adapter keeps a tile only when its link says `Grupa=01`. A mixed
  page, or a promotion page, therefore yields shoes only.

### Brand codes

Shared by both shops, checked against `/Home/Brands` on each.

| Brand | Code | Brand | Code |
|---|---|---|---|
| adidas | `01` | New Balance | `81` |
| Nike | `02` | Energetics | `100` |
| Puma | `04` | McKinley | `104` |
| Reebok | `07` | On | `122` |
| 4F | `69` | Crocs | `00050` |
| Skechers | `70` | Jack Wolfskin | `00071` |
| Asics | `72` | Salewa | `00074` |
| Salomon | `73` | | |

Match brand names case-insensitively.

### Tile parsing

- Split the page at each `<div class="... product_partial ...">`.
- Product id: `ProductDetails?ProductID=<id>` inside the tile.
- Name, first match wins:
  1. Intersport: text of `<a id="Description <id>">`.
  2. Sport M: text of `a.description-product` (2026-11-12).
  3. ~~Sport M: the `alt` of the `img-list` image, quoted or unquoted.~~
     Dropped 2026-11-12: an HTML parser reads the unquoted `alt` as its
     first word only and the rest as attribute names.
- A tile with no name is skipped.
- A tile whose link is not `Grupa=01` is skipped (see Tile footwear check).
- Brand:
  - Intersport: `alt` of `img.ow-brand-image`.
  - Sport M: no logo. When the listing has a `Brend` filter, the brand is that code's brand.
  - Otherwise derive it from the name, see Brand from name.
- Image: first `src` starting with `/ProductImages/`.
- Price, first source that exists wins:
  1. The `OnAddToShoppingCartFromProdList(...)` call (Intersport tiles).
     - Price = the 3rd argument, always.
     - Flag (5th argument) `'1'`: old price = the 4th argument.
     - Flag `'0'`: no old price.
  2. Sale price in `.colorRed`, old price in `.textDecorLine` or `.pricePositionDecoration`.
  3. The first `N.NNN,NN ден` amount in the tile's `.price-box`.
- Tile values fill the Listing before the product page is fetched.
  The product page keeps them when it has no value of its own.

### Product page parsing

- Product URL: `<base>/ProductCatalog/ProductDetails?ProductID=<id>`.
- Id and name: `<h2 class="details_description" id='<id> description'>`.
  - No such heading = no product data.
  - URL id differs from heading id = the server sent another product. Treat it as missing.
- Price: text of the element with `id="<id> Price"`.
- Old price: text of `#old_price_pd`, when present.
- No price on the page: keep the tile's price and old price.
- Category: texts of every `.breadcrumb-link`, joined with `/`, e.g. `Мажи/Обувки`.
  - The breadcrumb is there without any context parameter in the URL.
  - Intersport writes the segment in capitals: `МАЖИ/Обувки`, `ДЕЦА/Обувки`.
  - Only the clean product URL gives the product's own breadcrumb.
    With `Segment`/`Grupa` in the URL the breadcrumb is that context (2026-11-12).
- Footwear, from the `.breadcrumb-link` hrefs:
  - one with `Grupa=01`: footwear;
  - none carrying `Segment` or `Grupa`: unknown, kept (no Category);
  - otherwise not footwear: no entities.
- Why unknown is kept (2026-11-12, live run): `HQ8708` (Adidas CAMPUS 00s)
  has one empty crumb at the clean URL. The product page is only harvested
  for a listing that already passed the tile footwear check, so an empty
  breadcrumb is a site data gap, not evidence against footwear.
- Trusted only on a page fetched at the clean URL, see § Breadcrumb follows
  the URL context.
  - Trusted only on a page fetched at the clean URL, see § Breadcrumb follows the URL context.
- Sizes: one label per `li.size-option` (class may carry a trailing space).
  - Label = text of the `<div>` inside the `li`, whitespace collapsed (2026-11-12).
  - Fallback when there is no `<div>`: the `li` text.
  - Any listed size = in stock.
  - No size block = unknown, not sold out.
  - The `li` also holds `<span class="tooltiptext">` with the EU size
    (`41⅓`, `42⅔`; kids' pages have none), so the old "whole `li` text"
    rule produced `41⅓ 7.5`. The `<div>` is the shop's own size: UK on
    adidas (`7.5`), US on Nike (`7.5` = EU `40.5`), EU on kids' Skechers
    (`21`). The EU tooltip is not mapped yet, see Open items.
- Images, in order, de-duplicated:
  1. `src` of `#ImageDetails`.
  2. Every `'s':'/ProductImages/...'` entry in the gallery script.
  - Gallery names contain spaces, e.g. `KD5927_1_APPAREL_Photography_Standard Top Part View_grey.webp`.
  - Percent-encode them, keeping `/ : _ . -`.
  - Relative paths get the base URL in front.
  - No image on the page: keep the tile's image.
- sku = provider id = the product id.

### Brand from name

- These brands are matched as a prefix of the name followed by a space:
  New Balance, Jack Wolfskin, The North Face, Under Armour, Dare 2b, D.Franklin.
- Otherwise the brand is the first word of the name.
- Product pages: always from the name. The brand logos on a product page
  are the site's brand menu, not the product's (2026-11-12).

### Brand spelling (2026-11-12)

- The same brand arrives spelled three ways: Intersport logo `Adidas`,
  Sport M name `NIKE CHARGE SUEDE`, Intersport name `Mckinley KONA VI`.
- The adapter writes the spelling of the brand-code table (and of the
  multi-word list above) whenever the brand matches it case-insensitively:
  `adidas`, `Nike`, `McKinley`, `Crocs`, `The North Face`.
- Other brands are kept as found (`Terinda`).

### Price format

- Examples: `12.399,00 ден.`, ` 799,00 ден.`, `6099`.
- Keep only digits, dots and commas.
- With a comma: drop the dots, then the comma is the decimal point.
- Without a comma: a string matching `^\d{1,3}(\.\d{3})+$` is the thousands form, drop the dots.

### Harvest commands (built 2026-11-12)

Both are `IHarvestCommand`s with platform `sportm`, listed in
`SportMModule.HarvestCommands`. A failure never throws: it becomes a
failed `HarvestItem` with an error class, and the command goes on.

`sportm.catalog-pages`, one item per page, kind `catalog-page`:

- Parameter `listing`: the site's own query, e.g. `Segment=03&Grupa=01&Brend=01`.
  - Several queries separated by `|`.
  - A leading `?` and any `Page=` are dropped.
- Parameter `brand`: a brand name from the code table (any case), or a
  numeric `Brend` code. It sets `Brend=<code>` on every query.
- No `listing`: the provider's shoe listings (§ Shoes filter), chosen by
  `Source.Provider` (`sportm`, `intersport`).
  - `Source.Settings["shoeListings"]` (queries separated by `|`) overrides them.
- Provider id: `<query>/page-<n>`, e.g. `Segment=03&Grupa=01&Brend=02/page-1`.
- Each query is walked from page 1 until a page has no new tile. That last page is stored too.
- `Limit` = the most pages fetched, counted across all queries.
- A walk ends at `http-404`, at `robots`, or after 3 failed pages in a row.

`sportm.product-page`, one item per target, kind `product-page`:

- Targets: `ProviderId` = the article code; `Url` is optional, built from
  the Product URL rule when absent.
- A given `Url` is fetched without its context (2026-11-12).
  - Only its `ProductID` parameter is kept; the fragment is dropped too.
  - E.g. `...ProductDetails?ProductID=KQ6776&Segment=03&Grupa=01` is fetched as `...ProductDetails?ProductID=KQ6776`.
  - A `Url` without `ProductID` is fetched as given.
  - The item's `Url` is the URL actually fetched.
- `Limit` = the most targets fetched.
- The page must show the requested product in its heading id
  (compared case-insensitively), else the item fails.

Error classes:

| Class | When |
|---|---|
| `http-<status>` | Non-2xx response, or an `HttpRequestException` carrying a status |
| `robots` | The fetcher threw an exception whose type name contains `Robots` |
| `<exception type>` | Any other fetch exception, e.g. `HttpRequestException`, `TimeoutException` |
| `product-mismatch` | The heading id is another product |
| `product-missing` | HTTP 200 with no product heading; an unknown `ProductID` answers that way |
| `missing-id` | A product target without provider id |
| `unknown-brand` | `brand` is neither in the code table nor numeric; nothing is fetched |
| `no-listing` | No `listing` and no shoe listing known for the provider; nothing is fetched |

### Adapter `sportm.shoes` (built 2026-11-12)

Platform `sportm`, domain `shoes`, raw kinds `catalog-page` and `product-page`.

- Catalog page, per footwear tile: a Product candidate and a Listing.
  - Product: id `ProductIdentity.Candidate(seed, provider, articleCode)`, name, brand, `sku` = article code, the tile image.
  - Listing: id `DeterministicId.For(seed, provider, "listing", articleCode)`, title, price, old price, currency, URL.
  - `infoCheckedAt` and `stockCheckedAt` stay empty: a tile is not a check.
- Product page, only when the breadcrumb says footwear: a Category, a Product and a Listing.
  - Category: id `DeterministicId.For(seed, provider, "category", path)`, name = last crumb, path.
  - Product: name, brand from the name, `sku`, category id, gallery images.
  - Listing: price, old price, sizes, availability, and both check dates = the page's harvest time.
  - Availability: `InStock` when sizes are listed, else `Unknown`.
  - A page whose heading id differs from the raw item's provider id yields nothing.
  - A page whose raw URL has any parameter besides `ProductID` yields nothing (2026-11-12).
    Its breadcrumb is the URL's context, so neither footwear nor category can be trusted.
- Listing URL: always the clean product URL, `<base>/ProductCatalog/ProductDetails?ProductID=<id>`.
- Every entity: project id, namespace, provider, provider id, source URL = the raw item's URL, harvest time.

### Findings 2026-11-12

32 polite live requests in total (1.5 s apart, desktop Chrome
User-Agent, `Accept-Language: mk,en;q=0.8`), including a live
`--limit 3` run of both commands on both shops through the adapter.

- `Grupa=01` filters only under a gender segment (§ Shoes filter).
- Tile links carry the product's own group (§ Tile footwear check).
- Cart call arguments: 3rd = price to pay, 4th = regular price (§ Listing page).
- Sport M tiles gained `a.description-product` and use `.textDecorLine` for the old price.
- Sizes: `<div>` = the shop's size, tooltip = EU size (§ Product page parsing).
- Gallery names with spaces confirmed on both shops for adidas images,
  e.g. `JQ6920_3_FOOTWEAR_Photography_Top Portrait View_white.webp`.
- An unknown `ProductID` answers HTTP 200 with the page shell and no heading.
- Saved samples (gzipped, under `tests/Collector.Providers.SportM.Tests/Samples/`):

| Sample | What it shows |
|---|---|
| `sportm-catalog-men-shoes-page-1` | Sport M tile layout, no discount |
| `sportm-catalog-men-shoes-nike-page-1` | `Brend=02`, 13 discounted tiles |
| `sportm-catalog-men-shoes-page-400` | Past the end, no tile |
| `sportm-catalog-grupa01-mixed-page-1` | `Grupa=01` alone: 5 shoes among socks, caps, trousers |
| `intersport-catalog-men-shoes-page-1` | Intersport tile layout with logos and cart calls |
| `intersport-catalog-sale-sneakers-page-1` | Promotion segment, every tile discounted |
| `intersport-catalog-men-shoes-page-33` | Past the end, no tile |
| `sportm-product-IH7234` | Not discounted, sizes with EU tooltip, 8 gallery images |
| `sportm-product-IB2750-001` | Discounted (Nike, US sizes) |
| `sportm-product-KQ6776` | Not footwear (socks, `Опрема/Додатоци`) |
| `sportm-product-missing-ZZ0000000` | Unknown product id |
| `intersport-product-JQ6920` | Gallery names with spaces |
| `intersport-product-400603N-BKYL` | Discounted kids' shoe, sizes without tooltip |
| `sportm-product-HQ8708-empty-breadcrumb` | Shoe at the clean URL with one empty crumb: kept, no category |
| `sportm-product-KQ6776-shoe-context` | Socks fetched with `&Segment=03&Grupa=01`: breadcrumb `Мажи/Обувки` |
| `intersport-product-1304-BLK-shoe-context` | Terinda socks fetched with `&Segment=28&Grupa=01`: breadcrumb `МАЖИ/Обувки` |

### Breadcrumb follows the URL context (2026-11-12)

Found in review: a context URL made socks pass the footwear check.
Verified with 5 more polite live requests (same pacing and headers).

| Request | Breadcrumb |
|---|---|
| Sport M `ProductID=KQ6776` (socks), clean | `Опрема/Додатоци` (`Segment=06&Grupa=03`) |
| Sport M `ProductID=KQ6776&Segment=03&Grupa=01` | `Мажи/Обувки` (`Segment=03&Grupa=01`) |
| Intersport `ProductID=1304-BLK&Segment=28&Grupa=01` (socks) | `МАЖИ/Обувки` (`Segment=28&Grupa=01`) |
| Sport M `ProductID=IH7234&Segment=17&Grupa=103` (a shoe) | `ПРОМОЦИИ/` with an empty second crumb |

- The server builds the breadcrumb from the URL's `Segment`/`Grupa`, not from the product.
  - A non-shoe looks like a shoe under shoe context.
  - A shoe under a promotion menu code loses its footwear crumb.
  - One shoe gets a different category path under each segment.
- The clean URL gives the product's own breadcrumb.
- The session does not leak into it.
  - A shoe catalog page was fetched first with a cookie jar (`ASP.NET_SessionId`, `lang`).
  - The clean socks URL in that session still showed `Опрема/Додатоци`.
  - The shared fetcher keeps cookies, so this matters.
- Rules that follow, both built:
  - `sportm.product-page` always fetches the clean URL, see § Harvest commands.
  - `sportm.shoes` drops a product page whose raw URL carries any other parameter.
- Sport M only: the current colour option links to the product with its own `Grupa`/`PodGrupa`.
  - E.g. `ProductID=KQ6776&Grupa=03&PodGrupa=518`, also under shoe context.
  - Intersport renders that link as `#`, so it is not used as a footwear signal.

### Open items

- Size-level stock has a place in `Listing.sizes[]`, not harvested yet.
- A listing-only stock refresh, without product pages, is possible and not built.
- The EU size in the size tooltip is not mapped: `Size` has one label (2026-11-12).
- Title search (`contains` parameter → `SearchKey`) is not built; whether
  `SearchKey` combines with the shoe listings is unverified (2026-11-12).
- `Brend` codes seen in the menu but not in the table: `16`, `132`, `00017`, `00091` (2026-11-12).
- The `robots` error class depends on the shared HTTP fetcher's robots
  exception; until it exists, the type-name rule is a convention (2026-11-12).
