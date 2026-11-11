# NBSHOP platform — harvest and parse rules

NBSHOP is the Sport Vision Group platform behind three MK shops:
Sport Vision, Sport Reality and Buzz Sneakers. One set of harvest
commands and one adapter cover all three. Shop-specific findings live in
[sportvision-mk.md](sportvision-mk.md), [sportreality-mk.md](sportreality-mk.md)
and [buzzsneakers-mk.md](buzzsneakers-mk.md).

These rules were verified with live crawls on 2026-10-27.

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
