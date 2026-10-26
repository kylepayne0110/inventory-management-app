using System.Net;
using System.Net.Http.Json;
using Inventory.Api.Contracts;

namespace Inventory.Api.Tests;

public class WarehouseEndpointTests : IClassFixture<InventoryApiFactory>
{
    private readonly HttpClient _client;

    public WarehouseEndpointTests(InventoryApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    private async Task<WarehouseResponse> CreateAsync(string code, string name = "Main")
    {
        var response = await _client.PostAsJsonAsync("/api/warehouses", new { code, name });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<WarehouseResponse>())!;
    }

    [Fact]
    public async Task Create_ReturnsCreatedWithNormalizedCodeAndDefaults()
    {
        var response = await _client.PostAsJsonAsync("/api/warehouses", new { code = "wh-1", name = " Depot " });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var warehouse = await response.Content.ReadFromJsonAsync<WarehouseResponse>();
        Assert.Equal("WH-1", warehouse!.Code);
        Assert.Equal("Depot", warehouse.Name);
        Assert.True(warehouse.IsActive);
        Assert.Equal($"/api/warehouses/{warehouse.Id}", response.Headers.Location!.OriginalString);
    }

    [Fact]
    public async Task Create_DuplicateCode_IgnoringCase_Returns409()
    {
        await CreateAsync("DUP-1");

        var response = await _client.PostAsJsonAsync("/api/warehouses", new { code = "dup-1", name = "Other" });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Theory]
    [InlineData("", "Name")]
    [InlineData("   ", "Name")]
    [InlineData("has space", "Name")]
    [InlineData("-leading", "Name")]
    [InlineData("OK-1", "")]
    [InlineData("OK-1", "   ")]
    public async Task Create_InvalidInput_Returns400(string code, string name)
    {
        var response = await _client.PostAsJsonAsync("/api/warehouses", new { code, name });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_TooLongValues_Returns400()
    {
        var response = await _client.PostAsJsonAsync("/api/warehouses", new { code = new string('A', 21), name = new string('n', 201) });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Get_ReturnsWarehouse_Or404WhenMissing()
    {
        var created = await CreateAsync("GET-1");

        var found = await _client.GetAsync($"/api/warehouses/{created.Id}");
        var missing = await _client.GetAsync("/api/warehouses/999999");

        Assert.Equal(HttpStatusCode.OK, found.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }

    [Fact]
    public async Task Update_ChangesFieldsButNotCode()
    {
        var created = await CreateAsync("UPD-1", "Old");

        var response = await _client.PutAsJsonAsync($"/api/warehouses/{created.Id}",
            new { code = "CHANGED", name = "New", location = "Dock 4" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.Content.ReadFromJsonAsync<WarehouseResponse>();
        Assert.Equal("UPD-1", updated!.Code);
        Assert.Equal("New", updated.Name);
        Assert.Equal("Dock 4", updated.Location);
    }

    [Fact]
    public async Task Update_InvalidOrMissing_Returns400Or404()
    {
        var created = await CreateAsync("UPD-2");

        var invalid = await _client.PutAsJsonAsync($"/api/warehouses/{created.Id}", new { name = "" });
        var missing = await _client.PutAsJsonAsync("/api/warehouses/999999", new { name = "X" });

        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }

    [Fact]
    public async Task Deactivate_KeepsRecord_IsRepeatable_AndFiltersList()
    {
        var created = await CreateAsync("DEA-1");

        var first = await _client.PostAsync($"/api/warehouses/{created.Id}/deactivate", null);
        var second = await _client.PostAsync($"/api/warehouses/{created.Id}/deactivate", null);
        var fetched = await _client.GetFromJsonAsync<WarehouseResponse>($"/api/warehouses/{created.Id}");
        var active = await _client.GetFromJsonAsync<List<WarehouseResponse>>("/api/warehouses?isActive=true");
        var inactive = await _client.GetFromJsonAsync<List<WarehouseResponse>>("/api/warehouses?isActive=false");
        var all = await _client.GetFromJsonAsync<List<WarehouseResponse>>("/api/warehouses");

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
        var response = await _client.PostAsync("/api/warehouses/999999/deactivate", null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Delete_IsNotAllowed()
    {
        var created = await CreateAsync("DEL-1");

        var response = await _client.DeleteAsync($"/api/warehouses/{created.Id}");

        Assert.Equal(HttpStatusCode.MethodNotAllowed, response.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await _client.GetAsync($"/api/warehouses/{created.Id}")).StatusCode);
    }
}
