using System.Globalization;
using System.Net.Http.Json;
using System.Web;
using eShop.WebAppComponents.Catalog;

namespace eShop.WebAppComponents.Services;

public class CatalogService(HttpClient httpClient) : ICatalogService
{
    private readonly string remoteServiceBaseUrl = "api/catalog/";

    public Task<CatalogItem?> GetCatalogItem(int id)
    {
        var uri = $"{remoteServiceBaseUrl}items/{id}";
        return httpClient.GetFromJsonAsync<CatalogItem>(uri);
    }

    public async Task<CatalogResult> GetCatalogItems(int pageIndex, int pageSize, int? brand, int? type, decimal? minPrice = null, decimal? maxPrice = null)
    {
        var uri = GetAllCatalogItemsUri(remoteServiceBaseUrl, pageIndex, pageSize, brand, type, minPrice, maxPrice);
        var result = await httpClient.GetFromJsonAsync<CatalogResult>(uri);
        return result!;
    }

    public async Task<List<CatalogItem>> GetCatalogItems(IEnumerable<int> ids)
    {
        var uri = $"{remoteServiceBaseUrl}items/by?ids={string.Join("&ids=", ids)}";
        var result = await httpClient.GetFromJsonAsync<List<CatalogItem>>(uri);
        return result!;
    }

    public Task<CatalogResult> GetCatalogItemsWithSemanticRelevance(int page, int take, string text)
    {
        var url = $"{remoteServiceBaseUrl}items/withsemanticrelevance?text={HttpUtility.UrlEncode(text)}&pageIndex={page}&pageSize={take}";
        var result = httpClient.GetFromJsonAsync<CatalogResult>(url);
        return result!;
    }

    public async Task<IEnumerable<CatalogBrand>> GetBrands()
    {
        var uri = $"{remoteServiceBaseUrl}catalogBrands";
        var result = await httpClient.GetFromJsonAsync<CatalogBrand[]>(uri);
        return result!;
    }

    public async Task<IEnumerable<CatalogItemType>> GetTypes()
    {
        var uri = $"{remoteServiceBaseUrl}catalogTypes";
        var result = await httpClient.GetFromJsonAsync<CatalogItemType[]>(uri);
        return result!;
    }

    public async Task<CatalogPriceRange> GetPriceRange()
    {
        var uri = $"{remoteServiceBaseUrl}items/price-range";
        var result = await httpClient.GetFromJsonAsync<CatalogPriceRange>(uri);
        return result!;
    }

    private static string GetAllCatalogItemsUri(string baseUri, int pageIndex, int pageSize, int? brand, int? type, decimal? minPrice, decimal? maxPrice)
    {
        var query = new List<string>();

        if (type.HasValue)
        {
            query.Add($"type={type.Value}");
        }
        if (brand.HasValue)
        {
            query.Add($"brand={brand.Value}");
        }
        if (minPrice.HasValue)
        {
            query.Add($"minPrice={minPrice.Value.ToString(CultureInfo.InvariantCulture)}");
        }
        if (maxPrice.HasValue)
        {
            query.Add($"maxPrice={maxPrice.Value.ToString(CultureInfo.InvariantCulture)}");
        }

        query.Add($"pageIndex={pageIndex}");
        query.Add($"pageSize={pageSize}");

        return $"{baseUri}items?{string.Join("&", query)}";
    }
}
