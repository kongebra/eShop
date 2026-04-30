# Oppgave 2 - Catalog Feature Spec

This document tracks the assignment implementation plan and can remain as project documentation after the feature work is complete.

## Assignment checklist

The original assignment lists five possible feature choices. This spec covers all five so we can choose a small base slice and still have documented follow-up work.

| # | Assignment item | Covered by |
|---|-----------------|------------|
| 1 | Price filter on catalog endpoint (`?maxPrice=50`) | Base feature |
| 2 | In-stock filter on catalog endpoint (`?inStock=true`) | Follow-up feature |
| 3 | New endpoint: get `CatalogType` by id | Follow-up feature |
| 4 | Show stock status visually on product cards (green/red) | UI verification feature |
| 5 | Free choice: find something in eShop and write spec quickly | Bonus B related products endpoint |

## Current catalog context

- `GET /api/catalog/items` already supports `name`, `type`, `brand`, `pageIndex`, and `pageSize`.
- `GET /api/catalog/catalogbrands` already returns available brands.
- `GET /api/catalog/catalogtypes` already returns available catalog types.
- Catalog items expose `AvailableStock`, which can drive both `inStock` filtering and UI stock status.
- Catalog endpoints live in `src\Catalog.API\Apis\CatalogApi.cs`.
- Catalog functional tests live in `tests\Catalog.FunctionalTests\CatalogApiTests.cs`.

## Base feature: price filter

Endpoint:

```http
GET /api/catalog/items?maxPrice=50
```

### Behavior

- `maxPrice` is optional.
- When `maxPrice` is present, returned catalog items must satisfy `Price <= maxPrice`.
- Filtering must happen before pagination and before the total count is calculated.
- `maxPrice` must compose with existing filters (`name`, `type`, `brand`) and planned filters (`inStock`, `minPrice`).
- `maxPrice < 0` is invalid and must return `400 Bad Request`.
- `maxPrice = 0` is valid and should return items priced at zero or an empty page.

### Required tests before implementation

1. **Catalog items supports maxPrice filtering**
   - Request: `GET /api/catalog/items?pageIndex=0&pageSize=50&maxPrice=50`
   - Expected: `200 OK`
   - Expected body: every returned item has `Price <= 50`
   - Expected metadata: `Count` equals the filtered total, not just the page count

2. **maxPrice composes with existing type and brand filters**
   - Request: `GET /api/catalog/items?pageIndex=0&pageSize=50&type=3&brand=3&maxPrice=250`
   - Expected: `200 OK`
   - Expected body: every returned item has `CatalogTypeId == 3`, `CatalogBrandId == 3`, and `Price <= 250`
   - Expected metadata: `Count` equals the composed filtered total

3. **Negative maxPrice is rejected**
   - Request: `GET /api/catalog/items?pageIndex=0&pageSize=5&maxPrice=-1`
   - Expected: `400 Bad Request`

### Implementation notes

- Extend `GetAllItems(...)` with `decimal? maxPrice`.
- Extend internal calls to `GetAllItems(...)` so existing v1 routes keep compiling.
- Apply the price predicate to the `IQueryable<CatalogItem>` before `LongCountAsync()`.
- If typed result signatures need to change for validation, keep route behavior explicit and avoid silently ignoring invalid values.

## Follow-up feature: in-stock filter

Endpoint:

```http
GET /api/catalog/items?inStock=true
```

### Behavior

- `inStock` is optional.
- `inStock=true` returns only items where `AvailableStock > 0`.
- `inStock=false` returns only items where `AvailableStock <= 0`.
- Omitting `inStock` preserves current behavior and returns both in-stock and out-of-stock items.
- `inStock` must compose with `name`, `type`, `brand`, `maxPrice`, and Bonus A `minPrice`.
- Filtering must happen before pagination and before the total count is calculated.

### Tests

- `inStock=true` returns only items with `AvailableStock > 0`.
- `inStock=false` returns only items with `AvailableStock <= 0`.
- `inStock=true` composes with `maxPrice`.

## Follow-up feature: get CatalogType by id

Endpoint:

```http
GET /api/catalog/catalogtypes/{id}
```

### Behavior

- Existing `GET /api/catalog/catalogtypes` remains unchanged.
- Existing catalog type returns `200 OK` and the `CatalogType`.
- Unknown id returns `404 Not Found`.
- `id <= 0` returns `400 Bad Request`.
- Route should use the same versioned catalog API group as the existing catalog type list route.

### Tests

- Existing type id returns the expected `CatalogType`.
- Unknown type id returns `404`.
- Non-positive type id returns `400`.

## Follow-up feature: product card stock status

Goal: show inventory status visually on product cards.

### Behavior

- Product cards display a stock indicator derived from `AvailableStock`.
- `AvailableStock > 0`: green indicator and text such as `In stock`.
- `AvailableStock <= 0`: red indicator and text such as `Out of stock`.
- The indicator must be visible without opening product details.
- UI verification is visual, but the markup should also be accessible/testable with clear text or labels.

### Tests

- If practical, add a Playwright check that at least one product card shows an `In stock` indicator.
- If test data includes or can create an out-of-stock item, add a Playwright check for an `Out of stock` indicator.
- If E2E setup is too heavy for the assignment, document manual visual verification with screenshots or notes.

## Free-choice feature: related products endpoint

Endpoint:

```http
GET /api/catalog/items/{id}/related
```

Optional query parameter:

```http
GET /api/catalog/items/{id}/related?limit=4
```

### Relatedness rules

- Related products have the same `CatalogTypeId` as the source product.
- The source product itself is excluded.
- Products with the same `CatalogBrandId` rank higher.
- Products closer in price rank higher.
- The intended price window is +/- 20%; if fewer than `limit` products are found in that window, fill with same-type products outside the window.

### Limits and status codes

- Default `limit`: 4.
- Maximum `limit`: 12.
- `limit <= 0` returns `400 Bad Request`.
- Missing source product returns `404 Not Found`.
- Existing source product with no related candidates returns `200 OK` and an empty array.

### Sort order

1. Same brand first.
2. Absolute price difference ascending.
3. Name ascending.
4. Id ascending.

### Tests

- Related products exclude the source product and all share its catalog type.
- Related products are sorted by same-brand preference, then price distance.
- Unknown source id returns `404`.
- Source product with no related candidates returns `200 OK` with `[]`.

## Bonus A: advanced filter page

Goal: add UI filters for brand, category, and price interval like Komplett/Elkjop.

### API behavior

Add price interval support to `GET /api/catalog/items`:

- `minPrice` is optional.
- `maxPrice` is optional.
- `minPrice >= 0`
- `maxPrice >= 0`
- If both are present, `minPrice <= maxPrice`
- Returned items must satisfy all provided filters.

Add a price range endpoint:

```http
GET /api/catalog/items/price-range
```

Response shape:

```json
{
  "minPrice": 0,
  "maxPrice": 299.99
}
```

The values should be computed from catalog data.

### UI behavior

- Load brands from `/api/catalog/catalogbrands`.
- Load types from `/api/catalog/catalogtypes`.
- Load price bounds from `/api/catalog/items/price-range`.
- Let users combine brand, category, min price, and max price.
- When filters change, reset to the first page.
- Preserve existing catalog paging behavior.

### Tests

- Functional API test for `GET /api/catalog/items/price-range`.
- Functional API test for `minPrice` + `maxPrice` interval filtering.
- If practical, Playwright test for selecting filters and seeing filtered product cards.

## Work sequence

1. Write the three required base feature tests first.
2. Run the new tests and confirm they fail for the expected reason.
3. Implement `maxPrice`.
4. Run catalog functional tests.
5. Implement `inStock`.
6. Implement `GET /api/catalog/catalogtypes/{id}`.
7. Add product-card stock indicator.
8. Implement Bonus A API support.
9. Wire Bonus A UI filters.
10. Implement the related products endpoint and tests.
