namespace eShop.WebAppComponents.Catalog;

public record CatalogItem(
    int Id,
    string Name,
    string Description,
    decimal Price,
    string PictureUrl,
    int CatalogBrandId,
    CatalogBrand CatalogBrand,
    int CatalogTypeId,
    CatalogItemType CatalogType,
    int AvailableStock);

public record CatalogResult(int PageIndex, int PageSize, int Count, List<CatalogItem> Data);
public record CatalogBrand(int Id, string Brand);
public record CatalogItemType(int Id, string Type);
public record CatalogPriceRange(decimal MinPrice, decimal MaxPrice);
