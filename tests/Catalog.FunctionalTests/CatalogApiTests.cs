using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Asp.Versioning;
using Asp.Versioning.Http;
using eShop.Catalog.API.Model;
using Microsoft.AspNetCore.Mvc.Testing;

namespace eShop.Catalog.FunctionalTests;

public sealed class CatalogApiTests : IClassFixture<CatalogApiFixture>
{
    private readonly WebApplicationFactory<Program> _webApplicationFactory;
    private readonly JsonSerializerOptions _jsonSerializerOptions = new(JsonSerializerDefaults.Web);

    public CatalogApiTests(CatalogApiFixture fixture)
    {
        _webApplicationFactory = fixture;
    }

    private HttpClient CreateHttpClient(ApiVersion apiVersion)
    {
        var handler = new ApiVersionHandler(new QueryStringApiVersionWriter(), apiVersion);
        return _webApplicationFactory.CreateDefaultClient(handler);
    }

    private async Task<PaginatedItems<CatalogItem>> GetCatalogItemsAsync(HttpClient httpClient, string requestUri)
    {
        var response = await httpClient.GetAsync(requestUri, TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var result = JsonSerializer.Deserialize<PaginatedItems<CatalogItem>>(body, _jsonSerializerOptions);

        Assert.NotNull(result);
        return result;
    }

    private static CatalogItem CreateCatalogItem(int id, string name, int availableStock) => new(name)
    {
        Id = id,
        Description = $"{name} description",
        Price = 42.50m,
        PictureFileName = null,
        CatalogTypeId = 8,
        CatalogType = null,
        CatalogBrandId = 13,
        CatalogBrand = null,
        AvailableStock = availableStock,
        RestockThreshold = 10,
        MaxStockThreshold = 200,
        OnReorder = false
    };

    private sealed record CatalogPriceRangeResult(decimal MinPrice, decimal MaxPrice);

    [Theory]
    [InlineData(1.0)]
    [InlineData(2.0)]
    public async Task GetCatalogItemsRespectsPageSize(double version)
    {
        var _httpClient = CreateHttpClient(new ApiVersion(version));

        // Act
        var response = await _httpClient.GetAsync("/api/catalog/items?pageIndex=0&pageSize=5", TestContext.Current.CancellationToken);

        // Assert
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var result = JsonSerializer.Deserialize<PaginatedItems<CatalogItem>>(body, _jsonSerializerOptions);

        // Assert 103 total items (101 seeded + 2 added by AddCatalogItem tests) with 5 retrieved from index 0
        Assert.Equal(103, result.Count);
        Assert.Equal(0, result.PageIndex);
        Assert.Equal(5, result.PageSize);
    }

    [Theory]
    [InlineData(1.0)]
    [InlineData(2.0)]
    public async Task GetCatalogItemsWithMaxPriceReturnsOnlyItemsAtOrBelowMaxPrice(double version)
    {
        var _httpClient = CreateHttpClient(new ApiVersion(version));
        const decimal maxPrice = 50m;

        var allItems = await GetCatalogItemsAsync(_httpClient, "/api/catalog/items?pageIndex=0&pageSize=500");
        var expectedItems = allItems.Data.Where(item => item.Price <= maxPrice).ToList();

        Assert.NotEmpty(expectedItems);

        var result = await GetCatalogItemsAsync(_httpClient, $"/api/catalog/items?pageIndex=0&pageSize=500&maxPrice={maxPrice}");

        Assert.Equal(expectedItems.Count, result.Count);
        Assert.All(result.Data, item => Assert.True(item.Price <= maxPrice, $"Expected {item.Name} price {item.Price} to be <= {maxPrice}."));
    }

    [Theory]
    [InlineData(1.0, "/api/catalog/items/type/3/brand/3?pageIndex=0&pageSize=500", "/api/catalog/items/type/3/brand/3?pageIndex=0&pageSize=500&maxPrice=100")]
    [InlineData(2.0, "/api/catalog/items?pageIndex=0&pageSize=500&type=3&brand=3", "/api/catalog/items?pageIndex=0&pageSize=500&type=3&brand=3&maxPrice=100")]
    public async Task GetCatalogItemsWithMaxPriceComposesWithTypeAndBrandFilters(double version, string unfilteredRequestUri, string filteredRequestUri)
    {
        var _httpClient = CreateHttpClient(new ApiVersion(version));
        const decimal maxPrice = 100m;

        var unfilteredItems = await GetCatalogItemsAsync(_httpClient, unfilteredRequestUri);
        var expectedItems = unfilteredItems.Data.Where(item => item.Price <= maxPrice).ToList();

        Assert.NotEmpty(expectedItems);
        Assert.True(expectedItems.Count < unfilteredItems.Count, "Test data should include at least one item above maxPrice for this type/brand filter.");

        var result = await GetCatalogItemsAsync(_httpClient, filteredRequestUri);

        Assert.Equal(expectedItems.Count, result.Count);
        Assert.All(result.Data, item =>
        {
            Assert.Equal(3, item.CatalogTypeId);
            Assert.Equal(3, item.CatalogBrandId);
            Assert.True(item.Price <= maxPrice, $"Expected {item.Name} price {item.Price} to be <= {maxPrice}.");
        });
    }

    [Theory]
    [InlineData(1.0)]
    [InlineData(2.0)]
    public async Task GetCatalogItemsWithNegativeMaxPriceReturnsBadRequest(double version)
    {
        var _httpClient = CreateHttpClient(new ApiVersion(version));

        var response = await _httpClient.GetAsync("/api/catalog/items?pageIndex=0&pageSize=5&maxPrice=-1", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData(1.0)]
    [InlineData(2.0)]
    public async Task GetCatalogItemsWithInStockTrueReturnsOnlyItemsWithAvailableStock(double version)
    {
        var _httpClient = CreateHttpClient(new ApiVersion(version));

        var allItems = await GetCatalogItemsAsync(_httpClient, "/api/catalog/items?pageIndex=0&pageSize=500");
        var expectedItems = allItems.Data.Where(item => item.AvailableStock > 0).ToList();

        Assert.NotEmpty(expectedItems);

        var result = await GetCatalogItemsAsync(_httpClient, "/api/catalog/items?pageIndex=0&pageSize=500&inStock=true");

        Assert.Equal(expectedItems.Count, result.Count);
        Assert.All(result.Data, item => Assert.True(item.AvailableStock > 0, $"Expected {item.Name} to have available stock."));
    }

    [Theory]
    [InlineData(1.0, 11001)]
    [InlineData(2.0, 11002)]
    public async Task GetCatalogItemsWithInStockFalseReturnsOnlyItemsWithoutAvailableStock(double version, int itemId)
    {
        var _httpClient = CreateHttpClient(new ApiVersion(version));
        var outOfStockItem = CreateCatalogItem(itemId, $"Out of stock test item {version}", availableStock: 0);

        var createResponse = await _httpClient.PostAsJsonAsync("/api/catalog/items", outOfStockItem, TestContext.Current.CancellationToken);
        createResponse.EnsureSuccessStatusCode();

        var result = await GetCatalogItemsAsync(_httpClient, "/api/catalog/items?pageIndex=0&pageSize=500&inStock=false");

        Assert.Contains(result.Data, item => item.Id == itemId);
        Assert.All(result.Data, item => Assert.True(item.AvailableStock <= 0, $"Expected {item.Name} to have no available stock."));
    }

    [Theory]
    [InlineData(1.0)]
    [InlineData(2.0)]
    public async Task GetCatalogItemsWithInStockComposesWithMaxPrice(double version)
    {
        var _httpClient = CreateHttpClient(new ApiVersion(version));
        const decimal maxPrice = 50m;

        var allItems = await GetCatalogItemsAsync(_httpClient, "/api/catalog/items?pageIndex=0&pageSize=500");
        var expectedItems = allItems.Data
            .Where(item => item.AvailableStock > 0 && item.Price <= maxPrice)
            .ToList();

        Assert.NotEmpty(expectedItems);

        var result = await GetCatalogItemsAsync(_httpClient, $"/api/catalog/items?pageIndex=0&pageSize=500&inStock=true&maxPrice={maxPrice}");

        Assert.Equal(expectedItems.Count, result.Count);
        Assert.All(result.Data, item =>
        {
            Assert.True(item.AvailableStock > 0, $"Expected {item.Name} to have available stock.");
            Assert.True(item.Price <= maxPrice, $"Expected {item.Name} price {item.Price} to be <= {maxPrice}.");
        });
    }

    [Theory]
    [InlineData(1.0)]
    [InlineData(2.0)]
    public async Task GetCatalogItemsWithPriceIntervalReturnsOnlyItemsWithinRange(double version)
    {
        var _httpClient = CreateHttpClient(new ApiVersion(version));
        const decimal minPrice = 50m;
        const decimal maxPrice = 150m;

        var allItems = await GetCatalogItemsAsync(_httpClient, "/api/catalog/items?pageIndex=0&pageSize=500");
        var expectedItems = allItems.Data
            .Where(item => item.Price >= minPrice && item.Price <= maxPrice)
            .ToList();

        Assert.NotEmpty(expectedItems);

        var result = await GetCatalogItemsAsync(_httpClient, $"/api/catalog/items?pageIndex=0&pageSize=500&minPrice={minPrice}&maxPrice={maxPrice}");

        Assert.Equal(expectedItems.Count, result.Count);
        Assert.All(result.Data, item =>
        {
            Assert.True(item.Price >= minPrice, $"Expected {item.Name} price {item.Price} to be >= {minPrice}.");
            Assert.True(item.Price <= maxPrice, $"Expected {item.Name} price {item.Price} to be <= {maxPrice}.");
        });
    }

    [Theory]
    [InlineData(1.0)]
    [InlineData(2.0)]
    public async Task GetCatalogItemsWithInvalidPriceIntervalReturnsBadRequest(double version)
    {
        var _httpClient = CreateHttpClient(new ApiVersion(version));

        var response = await _httpClient.GetAsync("/api/catalog/items?pageIndex=0&pageSize=5&minPrice=150&maxPrice=50", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData(1.0)]
    [InlineData(2.0)]
    public async Task GetCatalogItemPriceRangeReturnsCatalogMinimumAndMaximumPrices(double version)
    {
        var _httpClient = CreateHttpClient(new ApiVersion(version));

        var allItems = await GetCatalogItemsAsync(_httpClient, "/api/catalog/items?pageIndex=0&pageSize=500");

        var response = await _httpClient.GetAsync("/api/catalog/items/price-range", TestContext.Current.CancellationToken);

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var result = JsonSerializer.Deserialize<CatalogPriceRangeResult>(body, _jsonSerializerOptions);

        Assert.NotNull(result);
        Assert.Equal(allItems.Data.Min(item => item.Price), result.MinPrice);
        Assert.Equal(allItems.Data.Max(item => item.Price), result.MaxPrice);
    }

    [Theory]
    [InlineData(1.0)]
    [InlineData(2.0)]
    public async Task GetCatalogTypeWithIdReturnsCatalogType(double version)
    {
        var _httpClient = CreateHttpClient(new ApiVersion(version));

        var typesResponse = await _httpClient.GetAsync("api/catalog/catalogtypes", TestContext.Current.CancellationToken);
        typesResponse.EnsureSuccessStatusCode();
        var typesBody = await typesResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var types = JsonSerializer.Deserialize<List<CatalogType>>(typesBody, _jsonSerializerOptions);

        Assert.NotNull(types);
        var expectedType = types.First();

        var response = await _httpClient.GetAsync($"api/catalog/catalogtypes/{expectedType.Id}", TestContext.Current.CancellationToken);

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var result = JsonSerializer.Deserialize<CatalogType>(body, _jsonSerializerOptions);

        Assert.NotNull(result);
        Assert.Equal(expectedType.Id, result.Id);
        Assert.Equal(expectedType.Type, result.Type);
    }

    [Theory]
    [InlineData(1.0)]
    [InlineData(2.0)]
    public async Task GetCatalogTypeWithUnknownIdReturnsNotFound(double version)
    {
        var _httpClient = CreateHttpClient(new ApiVersion(version));

        var response = await _httpClient.GetAsync("api/catalog/catalogtypes/999999", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData(1.0)]
    [InlineData(2.0)]
    public async Task GetCatalogTypeWithNonPositiveIdReturnsBadRequest(double version)
    {
        var _httpClient = CreateHttpClient(new ApiVersion(version));

        var response = await _httpClient.GetAsync("api/catalog/catalogtypes/0", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData(1.0)]
    [InlineData(2.0)]
    public async Task GetRelatedCatalogItemsReturnsSameTypeItemsAndExcludesSourceItem(double version)
    {
        var _httpClient = CreateHttpClient(new ApiVersion(version));

        var sourceResponse = await _httpClient.GetAsync("/api/catalog/items/1", TestContext.Current.CancellationToken);
        sourceResponse.EnsureSuccessStatusCode();
        var sourceBody = await sourceResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var sourceItem = JsonSerializer.Deserialize<CatalogItem>(sourceBody, _jsonSerializerOptions);

        Assert.NotNull(sourceItem);

        var response = await _httpClient.GetAsync("/api/catalog/items/1/related?limit=4", TestContext.Current.CancellationToken);

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var relatedItems = JsonSerializer.Deserialize<List<CatalogItem>>(body, _jsonSerializerOptions);

        Assert.NotNull(relatedItems);
        Assert.NotEmpty(relatedItems);
        Assert.True(relatedItems.Count <= 4);
        Assert.DoesNotContain(relatedItems, item => item.Id == sourceItem.Id);
        Assert.All(relatedItems, item => Assert.Equal(sourceItem.CatalogTypeId, item.CatalogTypeId));
    }

    [Theory]
    [InlineData(1.0)]
    [InlineData(2.0)]
    public async Task GetRelatedCatalogItemsWithUnknownSourceItemReturnsNotFound(double version)
    {
        var _httpClient = CreateHttpClient(new ApiVersion(version));

        var response = await _httpClient.GetAsync("/api/catalog/items/999999/related", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData(1.0)]
    [InlineData(2.0)]
    public async Task GetRelatedCatalogItemsWithInvalidLimitReturnsBadRequest(double version)
    {
        var _httpClient = CreateHttpClient(new ApiVersion(version));

        var response = await _httpClient.GetAsync("/api/catalog/items/1/related?limit=0", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData(1.0)]
    [InlineData(2.0)]
    public async Task UpdateCatalogItemWorksWithoutPriceUpdate(double version)
    {
        var _httpClient = CreateHttpClient(new ApiVersion(version));

        // Act - 1
        var response = await _httpClient.GetAsync("/api/catalog/items/1", TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var itemToUpdate = JsonSerializer.Deserialize<CatalogItem>(body, _jsonSerializerOptions);

        // Act - 2
        var priorAvailableStock = itemToUpdate.AvailableStock;
        itemToUpdate.AvailableStock -= 1;
        response = version switch
        {
            1.0 => await _httpClient.PutAsJsonAsync("/api/catalog/items", itemToUpdate, TestContext.Current.CancellationToken),
            2.0 => await _httpClient.PutAsJsonAsync($"/api/catalog/items/{itemToUpdate.Id}", itemToUpdate, TestContext.Current.CancellationToken),
            _ => throw new ArgumentOutOfRangeException(nameof(version), version, null)
        };
        response.EnsureSuccessStatusCode();

        // Act - 3
        response = await _httpClient.GetAsync("/api/catalog/items/1", TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
        body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var updatedItem = JsonSerializer.Deserialize<CatalogItem>(body, _jsonSerializerOptions);

        // Assert - 1
        Assert.Equal(itemToUpdate.Id, updatedItem.Id);
        Assert.NotEqual(priorAvailableStock, updatedItem.AvailableStock);
    }

    [Theory]
    [InlineData(1.0)]
    [InlineData(2.0)]
    public async Task UpdateCatalogItemWorksWithPriceUpdate(double version)
    {
        var _httpClient = CreateHttpClient(new ApiVersion(version));

        // Act - 1
        var response = await _httpClient.GetAsync("/api/catalog/items/1", TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var itemToUpdate = JsonSerializer.Deserialize<CatalogItem>(body, _jsonSerializerOptions);

        // Act - 2
        var priorAvailableStock = itemToUpdate.AvailableStock;
        itemToUpdate.AvailableStock -= 1;
        itemToUpdate.Price = 1.99m;
        response = version switch
        {
            1.0 => await _httpClient.PutAsJsonAsync("/api/catalog/items", itemToUpdate, TestContext.Current.CancellationToken),
            2.0 => await _httpClient.PutAsJsonAsync($"/api/catalog/items/{itemToUpdate.Id}", itemToUpdate, TestContext.Current.CancellationToken),
            _ => throw new ArgumentOutOfRangeException(nameof(version), version, null)
        };
        response.EnsureSuccessStatusCode();

        // Act - 3
        response = await _httpClient.GetAsync("/api/catalog/items/1", TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
        body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var updatedItem = JsonSerializer.Deserialize<CatalogItem>(body, _jsonSerializerOptions);

        // Assert - 1
        Assert.Equal(itemToUpdate.Id, updatedItem.Id);
        Assert.Equal(1.99m, updatedItem.Price);
        Assert.NotEqual(priorAvailableStock, updatedItem.AvailableStock);
    }

    [Theory]
    [InlineData(1.0)]
    [InlineData(2.0)]
    public async Task GetCatalogItemsbyIds(double version)
    {
        var _httpClient = CreateHttpClient(new ApiVersion(version));

        // Act
        var response = await _httpClient.GetAsync("/api/catalog/items/by?ids=1&ids=2&ids=3", TestContext.Current.CancellationToken);

        // Arrange
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var result = JsonSerializer.Deserialize<List<CatalogItem>>(body, _jsonSerializerOptions);

        // Assert 3 items
        Assert.Equal(3, result.Count);
    }

    [Theory]
    [InlineData(1.0)]
    [InlineData(2.0)]
    public async Task GetCatalogItemWithId(double version)
    {
        var _httpClient = CreateHttpClient(new ApiVersion(version));

        // Act
        var response = await _httpClient.GetAsync("/api/catalog/items/2", TestContext.Current.CancellationToken);

        // Arrange
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var result = JsonSerializer.Deserialize<CatalogItem>(body, _jsonSerializerOptions);

        // Assert
        Assert.Equal(2, result.Id);
        Assert.NotNull(result);
    }

    [Theory]
    [InlineData(1.0)]
    [InlineData(2.0)]
    public async Task GetCatalogItemWithExactName(double version)
    {
        var _httpClient = CreateHttpClient(new ApiVersion(version));

        // Act
        var response = version switch
        {
            1.0 => await _httpClient.GetAsync("api/catalog/items/by/Wanderer%20Black%20Hiking%20Boots?PageSize=5&PageIndex=0", TestContext.Current.CancellationToken),
            2.0 => await _httpClient.GetAsync("api/catalog/items?name=Wanderer%20Black%20Hiking%20Boots&PageSize=5&PageIndex=0", TestContext.Current.CancellationToken),
            _ => throw new ArgumentOutOfRangeException(nameof(version), version, null)
        };

        // Arrange
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var result = JsonSerializer.Deserialize<PaginatedItems<CatalogItem>>(body, _jsonSerializerOptions);

        // Assert
        Assert.NotNull(result.Data);
        Assert.Equal(1, result.Count);
        Assert.Equal(0, result.PageIndex);
        Assert.Equal(5, result.PageSize);
        Assert.Equal("Wanderer Black Hiking Boots", result.Data.ToList().FirstOrDefault().Name);
    }

    [Theory]
    [InlineData(1.0)]
    [InlineData(2.0)]
    public async Task GetCatalogItemWithPartialName(double version)
    {
        var _httpClient = CreateHttpClient(new ApiVersion(version));

        // Act
        var response = version switch
        {
            1.0 => await _httpClient.GetAsync("api/catalog/items/by/Alpine?PageSize=5&PageIndex=0", TestContext.Current.CancellationToken),
            2.0 => await _httpClient.GetAsync("api/catalog/items?name=Alpine&PageSize=5&PageIndex=0", TestContext.Current.CancellationToken),
            _ => throw new ArgumentOutOfRangeException(nameof(version), version, null)
        };

        // Arrange
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var result = JsonSerializer.Deserialize<PaginatedItems<CatalogItem>>(body, _jsonSerializerOptions);

        // Assert
        Assert.NotNull(result.Data);
        Assert.Equal(4, result.Count);
        Assert.Equal(0, result.PageIndex);
        Assert.Equal(5, result.PageSize);
        Assert.Contains("Alpine", result.Data.ToList().FirstOrDefault().Name);
    }

    [Theory]
    [InlineData(1.0)]
    [InlineData(2.0)]
    public async Task GetCatalogItemPicWithId(double version)
    {
        var _httpClient = CreateHttpClient(new ApiVersion(version));

        // Act
        var response = await _httpClient.GetAsync("api/catalog/items/1/pic", TestContext.Current.CancellationToken);

        // Arrange
        response.EnsureSuccessStatusCode();
        var result = response.Content.Headers.ContentType.MediaType;

        // Assert
        Assert.Equal("image/webp", result);
    }

    [Theory]
    [InlineData(1.0)]
    [InlineData(2.0)]
    public async Task GetCatalogItemWithsemanticrelevance(double version)
    {
        var _httpClient = CreateHttpClient(new ApiVersion(version));

        // Act
        var response = version switch
        {
            1.0 => await _httpClient.GetAsync("api/catalog/items/withsemanticrelevance/Wanderer?PageSize=5&PageIndex=0", TestContext.Current.CancellationToken),
            2.0 => await _httpClient.GetAsync("api/catalog/items/withsemanticrelevance?text=Wanderer&PageSize=5&PageIndex=0", TestContext.Current.CancellationToken),
            _ => throw new ArgumentOutOfRangeException(nameof(version), version, null)
        };

        // Arrange
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var result = JsonSerializer.Deserialize<PaginatedItems<CatalogItem>>(body, _jsonSerializerOptions);

        // Assert
        Assert.Equal(1, result.Count);
        Assert.NotNull(result.Data);
        Assert.Equal(0, result.PageIndex);
        Assert.Equal(5, result.PageSize);
    }

    [Theory]
    [InlineData(1.0)]
    [InlineData(2.0)]
    public async Task GetCatalogItemWithTypeIdBrandId(double version)
    {
        var _httpClient = CreateHttpClient(new ApiVersion(version));

        // Act
        var response = version switch
        {
            1.0 => await _httpClient.GetAsync("api/catalog/items/type/3/brand/3?PageSize=5&PageIndex=0", TestContext.Current.CancellationToken),
            2.0 => await _httpClient.GetAsync("api/catalog/items?type=3&brand=3&PageSize=5&PageIndex=0", TestContext.Current.CancellationToken),
            _ => throw new ArgumentOutOfRangeException(nameof(version), version, null)
        };

        // Arrange
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var result = JsonSerializer.Deserialize<PaginatedItems<CatalogItem>>(body, _jsonSerializerOptions);

        // Assert
        Assert.NotNull(result.Data);
        Assert.Equal(4, result.Count);
        Assert.Equal(0, result.PageIndex);
        Assert.Equal(5, result.PageSize);
        Assert.Equal(3, result.Data.ToList().FirstOrDefault().CatalogTypeId);
        Assert.Equal(3, result.Data.ToList().FirstOrDefault().CatalogBrandId);
    }

    [Theory]
    [InlineData(1.0)]
    [InlineData(2.0)]
    public async Task GetAllCatalogTypeItemWithBrandId(double version)
    {
        var _httpClient = CreateHttpClient(new ApiVersion(version));

        // Act
        var response = version switch
        {
            1.0 => await _httpClient.GetAsync("api/catalog/items/type/all/brand/3?PageSize=5&PageIndex=0", TestContext.Current.CancellationToken),
            2.0 => await _httpClient.GetAsync("api/catalog/items?brand=3&PageSize=5&PageIndex=0", TestContext.Current.CancellationToken),
            _ => throw new ArgumentOutOfRangeException(nameof(version), version, null)
        };

        // Arrange
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var result = JsonSerializer.Deserialize<PaginatedItems<CatalogItem>>(body, _jsonSerializerOptions);

        // Assert
        Assert.NotNull(result.Data);
        Assert.Equal(11, result.Count);
        Assert.Equal(0, result.PageIndex);
        Assert.Equal(5, result.PageSize);
        Assert.Equal(3, result.Data.ToList().FirstOrDefault().CatalogBrandId);
    }

    [Theory]
    [InlineData(1.0)]
    [InlineData(2.0)]
    public async Task GetAllCatalogTypes(double version)
    {
        var _httpClient = CreateHttpClient(new ApiVersion(version));

        // Act
        var response = await _httpClient.GetAsync("api/catalog/catalogtypes", TestContext.Current.CancellationToken);

        // Arrange
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var result = JsonSerializer.Deserialize<List<CatalogType>>(body, _jsonSerializerOptions);

        // Assert
        Assert.Equal(8, result.Count);
        Assert.NotNull(result);
    }

    [Theory]
    [InlineData(1.0)]
    [InlineData(2.0)]
    public async Task GetAllCatalogBrands(double version)
    {
        var _httpClient = CreateHttpClient(new ApiVersion(version));

        // Act
        var response = await _httpClient.GetAsync("api/catalog/catalogbrands", TestContext.Current.CancellationToken);

        // Arrange
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var result = JsonSerializer.Deserialize<List<CatalogBrand>>(body, _jsonSerializerOptions);

        // Assert
        Assert.Equal(13, result.Count);
        Assert.NotNull(result);
    }

    [Theory]
    [InlineData(1.0)]
    [InlineData(2.0)]
    public async Task AddCatalogItem(double version)
    {
        var _httpClient = CreateHttpClient(new ApiVersion(version));

        var id = version switch {
            1.0 => 10015,
            2.0 => 10016,
            _ => 0
        };

        // Act - 1
        var bodyContent = new CatalogItem("TestCatalog1") {
            Id = id,
            Description = "Test catalog description 1",
            Price = 11000.08m,
            PictureFileName = null,
            CatalogTypeId = 8,
            CatalogType = null,
            CatalogBrandId = 13,
            CatalogBrand = null,
            AvailableStock = 100,
            RestockThreshold = 10,
            MaxStockThreshold = 200,
            OnReorder = false
        };
        var response = await _httpClient.PostAsJsonAsync("/api/catalog/items", bodyContent, TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();

        // Act - 2
        response = await _httpClient.GetAsync($"/api/catalog/items/{id}", TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var addedItem = JsonSerializer.Deserialize<CatalogItem>(body, _jsonSerializerOptions);

        // Assert - 1
        Assert.Equal(bodyContent.Id, addedItem.Id);

    }

    [Theory]
    [InlineData(1.0)]
    [InlineData(2.0)]
    public async Task DeleteCatalogItem(double version)
    {
        var _httpClient = CreateHttpClient(new ApiVersion(version));

        var id = version switch {
            1.0 => 5,
            2.0 => 6,
            _ => 0
        };

        //Act - 1
        var response = await _httpClient.DeleteAsync($"/api/catalog/items/{id}", TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();

        // Act - 2
        var response1 = await _httpClient.GetAsync($"/api/catalog/items/{id}", TestContext.Current.CancellationToken);
        var responseStatus = response1.StatusCode;

        // Assert - 1
        Assert.Equal("NoContent", response.StatusCode.ToString());
        Assert.Equal("NotFound", responseStatus.ToString());
    }
}
