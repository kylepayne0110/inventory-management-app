using System.Net;
using System.Net.Http.Json;
using Inventory.Api.Contracts;

namespace Inventory.Api.Tests;

public class ProductEndpointTests : IClassFixture<InventoryApiFactory>
{
    private readonly HttpClient _client;

    public ProductEndpointTests(InventoryApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    private async Task<ProductResponse> CreateAsync(string sku, string name = "Widget")
    {
        var response = await _client.PostAsJsonAsync("/api/products", new { sku, name });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<ProductResponse>())!;
    }

    [Fact]
    public async Task Create_ReturnsCreatedWithNormalizedSkuAndDefaults()
    {
        var response = await _client.PostAsJsonAsync("/api/products", new { sku = "abc-1", name = " Bolt " });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var product = await response.Content.ReadFromJsonAsync<ProductResponse>();
        Assert.Equal("ABC-1", product!.Sku);
        Assert.Equal("Bolt", product.Name);
        Assert.Equal("each", product.UnitOfMeasure);
        Assert.Equal(0m, product.ReorderThreshold);
        Assert.True(product.IsActive);
        Assert.Equal($"/api/products/{product.Id}", response.Headers.Location!.OriginalString);
    }

    [Fact]
    public async Task Create_WithReorderThreshold_StoresIt()
    {
        var response = await _client.PostAsJsonAsync("/api/products", new { sku = "RO-1", name = "Bolt", reorderThreshold = 12.5 });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var product = (await response.Content.ReadFromJsonAsync<ProductResponse>())!;
        Assert.Equal(12.5m, product.ReorderThreshold);
        Assert.Equal(12.5m, (await _client.GetFromJsonAsync<ProductResponse>($"/api/products/{product.Id}"))!.ReorderThreshold);
    }

    [Fact]
    public async Task Update_ChangesReorderThreshold_AndKeepsItWhenOmitted()
    {
        var product = await CreateAsync("RO-2");

        var set = await _client.PutAsJsonAsync($"/api/products/{product.Id}", new { name = "Widget", reorderThreshold = 30 });
        Assert.Equal(30m, (await set.Content.ReadFromJsonAsync<ProductResponse>())!.ReorderThreshold);

        var omitted = await _client.PutAsJsonAsync($"/api/products/{product.Id}", new { name = "Renamed" });
        Assert.Equal(30m, (await omitted.Content.ReadFromJsonAsync<ProductResponse>())!.ReorderThreshold);

        var zero = await _client.PutAsJsonAsync($"/api/products/{product.Id}", new { name = "Renamed", reorderThreshold = 0 });
        Assert.Equal(0m, (await zero.Content.ReadFromJsonAsync<ProductResponse>())!.ReorderThreshold);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(-0.001)]
    [InlineData(1.2345)]
    [InlineData(2000000000)]
    public async Task ReorderThreshold_Invalid_Returns400OnCreateAndUpdate(double threshold)
    {
        var create = await _client.PostAsJsonAsync("/api/products", new { sku = "BAD-" + Guid.NewGuid().ToString("N")[..8], name = "Bolt", reorderThreshold = threshold });
        Assert.Equal(HttpStatusCode.BadRequest, create.StatusCode);

        var product = await CreateAsync("RO-" + Guid.NewGuid().ToString("N")[..8]);
        var update = await _client.PutAsJsonAsync($"/api/products/{product.Id}", new { name = "Widget", reorderThreshold = threshold });
        Assert.Equal(HttpStatusCode.BadRequest, update.StatusCode);
        Assert.Equal(0m, (await _client.GetFromJsonAsync<ProductResponse>($"/api/products/{product.Id}"))!.ReorderThreshold);
    }

    [Fact]
    public async Task Create_DuplicateSku_IgnoringCase_Returns409()
    {
        await CreateAsync("DUP-1");

        var response = await _client.PostAsJsonAsync("/api/products", new { sku = "dup-1", name = "Other" });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Theory]
    [InlineData("", "Name")]
    [InlineData("   ", "Name")]
    [InlineData("has space", "Name")]
    [InlineData("-leading", "Name")]
    [InlineData("OK-1", "")]
    [InlineData("OK-1", "   ")]
    public async Task Create_InvalidInput_Returns400(string sku, string name)
    {
        var response = await _client.PostAsJsonAsync("/api/products", new { sku, name });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_TooLongValues_Returns400()
    {
        var response = await _client.PostAsJsonAsync("/api/products", new { sku = new string('A', 51), name = new string('n', 201) });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Get_ReturnsProduct_Or404WhenMissing()
    {
        var created = await CreateAsync("GET-1");

        var found = await _client.GetAsync($"/api/products/{created.Id}");
        var missing = await _client.GetAsync("/api/products/999999");

        Assert.Equal(HttpStatusCode.OK, found.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }

    [Fact]
    public async Task Update_ChangesFieldsButNotSku()
    {
        var created = await CreateAsync("UPD-1", "Old");

        var response = await _client.PutAsJsonAsync($"/api/products/{created.Id}",
            new { sku = "CHANGED", name = "New", description = "Desc", unitOfMeasure = "box" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.Content.ReadFromJsonAsync<ProductResponse>();
        Assert.Equal("UPD-1", updated!.Sku);
        Assert.Equal("New", updated.Name);
        Assert.Equal("Desc", updated.Description);
        Assert.Equal("box", updated.UnitOfMeasure);
    }

    [Fact]
    public async Task Update_InvalidOrMissing_Returns400Or404()
    {
        var created = await CreateAsync("UPD-2");

        var invalid = await _client.PutAsJsonAsync($"/api/products/{created.Id}", new { name = "" });
        var missing = await _client.PutAsJsonAsync("/api/products/999999", new { name = "X" });

        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }

    [Fact]
    public async Task Deactivate_KeepsRecord_IsRepeatable_AndFiltersList()
    {
        var created = await CreateAsync("DEA-1");

        var first = await _client.PostAsync($"/api/products/{created.Id}/deactivate", null);
        var second = await _client.PostAsync($"/api/products/{created.Id}/deactivate", null);
        var fetched = await _client.GetFromJsonAsync<ProductResponse>($"/api/products/{created.Id}");
        var active = await _client.GetFromJsonAsync<List<ProductResponse>>("/api/products?isActive=true");
        var inactive = await _client.GetFromJsonAsync<List<ProductResponse>>("/api/products?isActive=false");
        var all = await _client.GetFromJsonAsync<List<ProductResponse>>("/api/products");

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.False(fetched!.IsActive);
        Assert.DoesNotContain(active!, p => p.Id == created.Id);
        Assert.Contains(inactive!, p => p.Id == created.Id);
        Assert.Contains(all!, p => p.Id == created.Id);
    }

    [Fact]
    public async Task Deactivate_Missing_Returns404()
    {
        var response = await _client.PostAsync("/api/products/999999/deactivate", null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Delete_IsNotAllowed()
    {
        var created = await CreateAsync("DEL-1");

        var response = await _client.DeleteAsync($"/api/products/{created.Id}");

        Assert.Equal(HttpStatusCode.MethodNotAllowed, response.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await _client.GetAsync($"/api/products/{created.Id}")).StatusCode);
    }
}
