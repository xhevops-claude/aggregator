# Sport M platform — sport-m.com.mk and intersport.mk

Status: **verified with direct access** (2026-10-27), source keys
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
| Product | `/ProductCatalog/ProductDetails?ProductID=<id>` — extra params (Segment/Grupa/...) are only breadcrumb context and can be dropped |
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

**Sport M** (older theme) tiles have only the image link, with the product
name in an *unquoted* `alt` (`<img src=/ProductImages/JZ8106.webp alt=Adidas
LINEAR DUF M CB class="img-list">`), and a `.price-box` with the price; a
discounted tile shows the regular price in `.pricePositionDecoration` and
the sale price in `.colorRed`. No brand logo, so brand is derived from the
first word of the name.

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
  2. Sport M: the `alt` of the `img-list` image, quoted or unquoted.
- A tile with no name is skipped.
- Brand:
  - Intersport: `alt` of `img.ow-brand-image`.
  - Sport M: no logo. When the listing has a `Brend` filter, the brand is that code's brand.
  - Otherwise derive it from the name, see Brand from name.
- Image: first `src` starting with `/ProductImages/`.
- Price, first source that exists wins:
  1. The `OnAddToShoppingCartFromProdList(...)` call.
     - Flag `'1'`: price = discounted price, old price = normal price.
     - Flag `'0'`: price = normal price, no old price.
  2. Sale price in `.colorRed`, old price in `.pricePositionDecoration`.
  3. The first `N.NNN,NN ден` amount in the tile.
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
- Sizes: text of every `li.size-option`, tags stripped, whitespace collapsed.
  - Any listed size = in stock.
  - No size block = unknown, not sold out.
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

### Price format

- Examples: `12.399,00 ден.`, ` 799,00 ден.`, `6099`.
- Keep only digits, dots and commas.
- With a comma: drop the dots, then the comma is the decimal point.
- Without a comma: a string matching `^\d{1,3}(\.\d{3})+$` is the thousands form, drop the dots.

### Open items

- Size-level stock has a place in `Listing.sizes[]`, not harvested yet.
- A listing-only stock refresh, without product pages, is possible and not built.
