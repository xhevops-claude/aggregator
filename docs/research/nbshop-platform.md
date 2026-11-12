# NBSHOP platform — harvest and parse rules

NBSHOP is the Sport Vision Group platform behind three MK shops:
Sport Vision, Sport Reality and Buzz Sneakers. One set of harvest
commands and one adapter cover all three. Shop-specific findings live in
[sportvision-mk.md](sportvision-mk.md), [sportreality-mk.md](sportreality-mk.md)
and [buzzsneakers-mk.md](buzzsneakers-mk.md).

These rules were verified with live crawls on 2026-10-27 and re-checked
on 2026-11-12, when the harvest commands and the Shoes adapter were built.

## Storefronts

| Source key | Base URL | Sitemap region | Currency | Status |
|---|---|---|---|---|
| `sportvision_mk` | https://www.sportvision.mk | `MAK_mk` | MKD | verified |
| `sportreality_mk` | https://www.sportreality.mk | `MAK_mk` | MKD | verified |
| `buzzsneakers_mk` | https://www.buzzsneakers.mk | `MAK_mk` | MKD | verified |
| `sportvision_rs` | https://www.sportvision.rs | `SRB_rs` | RSD | guess, unchecked |
| `sportvision_ba` | https://www.sportvision.ba | `BIH_ba` | BAM | guess, unchecked |

The same platform runs Sport Vision in 13 countries in total. Region and
currency of the non-MK storefronts are guesses until checked.

## Discover from the sitemap

- URL: `<base>/files/sitemap/<region>/product.xml`.
- Standard sitemap XML, namespace `http://www.sitemaps.org/schemas/sitemap/0.9`.
- Every `url/loc` is one product URL.
- Product URL shape: `/<lang>/<category>/<numeric-id>-<slug>`.
- The numeric id can be short here (`/mk/patiki/108-adidas-patiki-gazelle`):
  accept any length in the sitemap (2026-11-12).
- To match a product by name without fetching it, use the slug after
  `/<numeric-id>-`. Never match on the numeric id.

## Discover from a listing

- First page: `<base>/mk/<path>`, e.g. `/mk/patiki/zenski/new-balance`.
- Next pages: `<base>/mk/<path>/page-N`, from N = 2.
- Stop when any of these holds:
  - the page answers 404;
  - the page has no tile that was not seen before;
  - the page has no link containing `page-<N+1>`.
- A tile is an anchor with the product URL and the title:

  ```html
  <a href="https://www.sportvision.mk/mk/patiki/8426431-harden-stepback" title="<product title>">
  ```

  - The numeric id has at least 5 digits.
  - Tiles repeat once per colour variant: de-duplicate on the numeric id.
  - The `title` attribute is the product title, known before the page is fetched.
- Brand listing: `proizvodi/<slug>`.
  - Slug = brand in lower case.
  - Every run of characters outside `a-z0-9` becomes one `-`.
  - Leading and trailing `-` are trimmed.
  - Example: "New Balance" → `proizvodi/new-balance`.
- Filter segments differ per shop. `/mk/patiki/zenski` is 404 on Buzz.

## Product page

### JSON-LD

- A page has several `<script type="application/ld+json">` blocks:
  - breadcrumbs;
  - a commented-out template that is not valid JSON;
  - the real Product.
- Take the first block that parses and has `"@type": "Product"`.
- Descriptions can hold raw newlines inside JSON strings.
  - A strict JSON parser rejects them.
  - Escape control characters inside strings before parsing.
- No Product block = no product data. Skip the page as a per-item failure.

### Field mapping

| Field | Source |
|---|---|
| provider id | `productID`; fallback: the numeric id from the URL |
| sku | `sku`, the brand's article code |
| name | `name`, trimmed |
| brand | `brand`, either a string or an object with `name` |
| category | the `<category>` segment of the URL, e.g. `patiki` |
| price | `offers.price` |
| currency | `offers.priceCurrency`; fallback: the storefront currency |
| availability | last path segment of `offers.availability`, e.g. `https://schema.org/InStock` → `InStock` |
| images | `image`, a list of strings or a single string |
| old price | HTML only, see below |

No provider id from either source = skip the page.

### Old price

- Read only the main product block:

  ```html
  <div class="... product-oldprice ..."><span class="product-oldprice-value value">5.790</span>
  ```

- When the div's class list also has `hide`, the product is not discounted.
- Related-product tiles use `prev-old-price`. Do not match those.

### Price formats

| Where | Format | Example | Value |
|---|---|---|---|
| JSON-LD | dot decimal | `4990.00` | 4990.00 |
| HTML | dot thousands, no decimals | `5.790` | 5790 |

- A string matching `^\d{1,3}(\.\d{3})+$` is the thousands form: drop the dots.
- Otherwise drop commas and parse as a dot decimal.

## Shoe categories

Verified 2026-11-12 against the live product sitemaps of all three MK
shops, counting the `<category>` segment of every URL. In code the list
lives in one place: `NbshopShoeCategories`.

| Slug | What it is | Sport Vision | Sport Reality | Buzz |
|---|---|---|---|---|
| `patiki` | sneakers | 2,968 | 1,661 | 1,746 |
| `papuci` | slippers, slides | 318 | 190 | 96 |
| `kopacki` | football boots | 140 | 60 | 0 |
| `konduri` | shoes, outdoor shoes | 95 | 78 | 24 |
| `sandali` | sandals | 92 | 43 | 8 |
| `cizmi` | boots | 65 | 58 | 63 |
| `apostolki` | strap sandals, flip-flops | 61 | 50 | 2 |
| `patiki-za-voda` | water shoes | 2 | 0 | 0 |
| `baletanki` | ballet flats | 0 | 2 | 0 |
| **shoes** | | **3,741 of 11,620** | **2,142 of 5,581** | **1,939 of 4,532** |

- Kept out on purpose:
  - `vloski` insoles, `vrvci` laces;
  - `sprej`, `kapsuli`, `maramici` shoe care, `ukras` Crocs charms;
  - `corapi`, `stucni` socks;
  - `rosuli`, `roleri` roller skates.
- The platform agrees: breadcrumbs and tiles put every slug above under
  "Обувки" (`Производи > Обувки > Патики`). Clothes sit under "Текстил".
- A slug added later by a shop stays invisible to the Shoes operation
  until it joins the list.

## Harvest commands

Built 2026-11-12 in `src/Collector.Providers.Nbshop`.

| Command | Input | Raw items |
|---|---|---|
| `nbshop.sitemap` | source setting `sitemapRegion` | one `sitemap`, provider id `product-sitemap` |
| `nbshop.listing-pages` | parameter `listing` (path after `/mk/`) or `brand` | one `listing-page` per page, provider id `<path>/page-<n>`; `Limit` = max pages |
| `nbshop.product-page` | targets: provider id + product URL | one `product-page` per target, provider id = numeric id; `Limit` = max targets |

- Failure is per page or target. A failed item carries an error class:

  | Error class | When |
  |---|---|
  | `http-<status>` | the page answered a non-2xx status |
  | `robots` | robots.txt disallows the URL, or robots.txt answered 5xx/429 and the host counts as disallowed |
  | `<exception type>` | the fetcher threw anything else, e.g. `HttpRequestException` |
  | `missing-setting` | the source has no `sitemapRegion` |
  | `missing-parameter` | neither `listing` nor `brand` is set |
  | `missing-url` | a product target has no URL |
  | `bad-url` | the URL is not http(s) |

- Listing walk, in order:
  1. Page 1 is `<base>/<lang>/<path>`, page N is the same URL plus `/page-N`.
  2. A 404 on page 1 is a failed item. A 404 on a later page ends the walk.
  3. A later page with no new tile ends the walk and is not kept.
  4. Any other failure ends the walk after its failed item.
  5. No link to `page-<N+1>` ends the walk after the page.
- The language segment is `mk`. The source setting `language` overrides it.
- A relative product URL is resolved against the source base URL.

## Shoes adapter

Built 2026-11-12: `nbshop.shoes`, domain `shoes`. It only ever emits
Shoes entities; anything outside the shoe categories is ignored.

| Raw kind | Entities |
|---|---|
| `sitemap` | one Listing per shoe URL; title = slug with `-` as spaces |
| `listing-page` | one Listing per shoe tile; title = tile `title` |
| `product-page` | Product, Listing, Category, for a shoe category only |

- Ids, all deterministic from the project seed and the provider:
  - Listing: `listing:<numeric id>`.
  - Product candidate: `ProductIdentity.Candidate`, used as the Listing's product id.
  - Category: `category:<slug>`.
- Product page mapping beyond the field table above:
  - Product `Fields` = Shoes fields; `ModelCode` = the manufacturer code when shown.
  - Listing title = product name; `infoCheckedAt` and `stockCheckedAt` = harvest time.
  - Category name = breadcrumb name, path = slug.
- A product page without a Product block, or a URL that is not a product
  URL, is a per-item failure (`InvalidDataException`).

## Findings 2026-11-12

Nineteen polite live requests, at most 1 per second per host: twelve for
research and samples, seven for the live `--limit 3` check below.

### Live check

Run once with a real HTTP fetcher, then the Shoes adapter on what came back:

| Command | Source | Result |
|---|---|---|
| `nbshop.sitemap` | Buzz | 1 item, 893 KB; 1,934 shoe listings (1,939 after the short-id fix below) |
| `nbshop.listing-pages` brand `Hoka`, limit 3 | Sport Vision | 3 pages; 24, 24 and 9 shoe listings |
| `nbshop.product-page`, limit 3 | Sport Vision | 3 Hoka pages: name, sku, price, old price, sizes with EANs, category "Патики" |

### Sitemap

- Counts: Sport Vision 11,620 (12,979 in August), Sport Reality 5,581,
  Buzz 4,532.
- The Shoes adapter on these sitemaps gives 3,741, 2,142 and 1,939
  listings, the shoe totals of the table above.
- Every URL is `/mk/<category>/<numeric-id>-<slug>`; no id repeats.
- Old products keep 3- or 4-digit ids.
  - Sport Reality 30 (1 shoe), Buzz 13 (5 shoes), Sport Vision none.
  - The sitemap rule accepts any id length.
  - Listing tiles keep the 5-digit rule, so these products are found
    through the sitemap only.
- Each `url` carries `lastmod` and `priority`.

### Listing pages

- 24 tiles per page on brand and outlet listings, not 48.
  - `/mk/proizvodi/converse`: 4 pages on Sport Vision, 3 on Buzz.
- The "load more" buttons carry the page count:
  - `a[rel=next]` and `a[rel=prev]` with `data-total-pages`;
  - `#auto-load-marker` with `data-page-count`.
- With a query, the platform appends `/page-N` after the query:
  - `/mk/proizvodi/?outlet=yes` → next `/mk/proizvodi/?outlet=yes/page-2`.
  - The command builds the same URL; the next-link check reads path and query.
- Tiles also carry data attributes, unused so far:
  - `data-productid`, `data-productCode` (= sku), `data-productbrand`;
  - `data-productPrice`, `data-productprevprice` in the HTML price form;
  - `data-productCatBread`, e.g. `Производи > Обувки > Патики`.
- The page holds `ItemList` and `CollectionPage` JSON-LD with raw tabs. Not used.

### Product page

- Manufacturer code: `div.product-model span`.
  - Label "Код на производителот" on Sport Vision, "Šifra proizvođača" on Sport Reality.
  - Absent on Converse and Crocs pages.
- `div.code span` ("Код на производ") repeats the JSON-LD `sku`.
- Category name: the BreadcrumbList item whose URL ends with the category
  slug. Fallback: `div.category a[title]`, then the slug.
- Buzz breadcrumbs have one more level: `Обувки > Папучи и апостолки > Папучи`.
- A hidden old-price block still carries a value, the current price. Ignore it.
- The Buzz Crocs page now carries raw TABs, not newlines, in the JSON-LD
  `description`. The sanitiser escapes every character below U+0020
  inside a string.
- Availability: `InStock` → in stock; `OutOfStock`, `SoldOut`,
  `Discontinued` → out of stock; anything else → unknown.

### Sizes

| Markup | Meaning |
|---|---|
| `ul.product-attributes > li` | one size, in page order; Sport Reality does not sort them |
| `.eur-size` | EU size, the default "Големини EU" tab: `42.5`, `41 1/3`, `39-40` |
| `.original-size` | the brand's size: US `9`, UK `7-` (adidas half sizes), `M7/W9` |
| `.cm-size` | foot length |
| class `disabled` | not available |
| `data-combination-code` | EAN-13, on available sizes only, on all three shops |

- Size label = `.eur-size`.
  - System `EU` when it starts with a digit.
  - Otherwise (Crocs `M14`, clothing `XL`) no system.
  - An empty `.eur-size` falls back to `.original-size`.
- In stock = no `disabled` class.
