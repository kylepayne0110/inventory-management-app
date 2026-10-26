using System.Net;
using System.Net.Http.Json;
using Inventory.Api.Contracts;
using Inventory.Api.Data;
using Inventory.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Inventory.Api.Tests;

public class ReceiptEndpointTests : IClassFixture<InventoryApiFactory>
{
    private readonly InventoryApiFactory _factory;
    private readonly HttpClient _client;

    public ReceiptEndpointTests(InventoryApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private static string Unique() => Guid.NewGuid().ToString("N")[..10];

    private async Task<ProductResponse> CreateProductAsync()
    {
        var response = await _client.PostAsJsonAsync("/api/products", new { sku = "P-" + Unique(), name = "Widget" });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<ProductResponse>())!;
    }

    private async Task<WarehouseResponse> CreateWarehouseAsync()
    {
        var response = await _client.PostAsJsonAsync("/api/warehouses", new { code = "W-" + Unique(), name = "Main" });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<WarehouseResponse>())!;
    }

    private Task<HttpResponseMessage> ReceiveAsync(object body) => _client.PostAsJsonAsync("/api/receipt-batches", body);

    private async Task<ReceiptBatchResponse> ReceiveOkAsync(int productId, int warehouseId, decimal quantity, DateTimeOffset? receivedAt = null)
    {
        var response = await ReceiveAsync(new { productId, warehouseId, quantity, receivedAt });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<ReceiptBatchResponse>())!;
    }

    private async Task<List<StockMovement>> MovementsForBatchAsync(int batchId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
        return await db.StockMovements.Where(m => m.ReceiptBatchId == batchId).ToListAsync();
    }

    [Fact]
    public async Task Receive_CreatesBatchWithFullQuantityOnHand()
    {
        var product = await CreateProductAsync();
        var warehouse = await CreateWarehouseAsync();

        var response = await ReceiveAsync(new { productId = product.Id, warehouseId = warehouse.Id, quantity = 40, reference = "  PO-7 " });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var batch = (await response.Content.ReadFromJsonAsync<ReceiptBatchResponse>())!;
        Assert.Equal(40m, batch.QuantityReceived);
        Assert.Equal(40m, batch.QuantityOnHand);
        Assert.Equal("PO-7", batch.Reference);
        Assert.Equal(product.Id, batch.ProductId);
        Assert.Equal(warehouse.Id, batch.WarehouseId);
        Assert.True(DateTimeOffset.UtcNow - batch.ReceivedAt < TimeSpan.FromMinutes(1));
        Assert.Equal($"/api/receipt-batches/{batch.Id}", response.Headers.Location!.OriginalString);
    }

    [Fact]
    public async Task Receive_RecordsOneReceiveMovement()
    {
        var product = await CreateProductAsync();
        var warehouse = await CreateWarehouseAsync();
        var receivedAt = DateTimeOffset.UtcNow.AddDays(-10);

        var batch = await ReceiveOkAsync(product.Id, warehouse.Id, 12.5m, receivedAt);

        var movement = Assert.Single(await MovementsForBatchAsync(batch.Id));
        Assert.Equal(MovementType.Receive, movement.Type);
        Assert.Equal(12.5m, movement.QuantityChange);
        Assert.Equal(product.Id, movement.ProductId);
        Assert.Equal(warehouse.Id, movement.WarehouseId);
        Assert.Equal(batch.ReceivedAt, movement.OccurredAt);
    }

    [Fact]
    public async Task Receive_SameProductTwice_CreatesSeparateBatches()
    {
        var product = await CreateProductAsync();
        var warehouse = await CreateWarehouseAsync();

        var first = await ReceiveOkAsync(product.Id, warehouse.Id, 5);
        var second = await ReceiveOkAsync(product.Id, warehouse.Id, 7);

        Assert.NotEqual(first.Id, second.Id);
        Assert.Equal(5m, first.QuantityOnHand);
        Assert.Equal(7m, second.QuantityOnHand);
    }

    [Fact]
    public async Task Receive_BackdatedReceivedAt_IsKept()
    {
        var product = await CreateProductAsync();
        var warehouse = await CreateWarehouseAsync();
        var receivedAt = new DateTimeOffset(2026, 1, 15, 8, 0, 0, TimeSpan.Zero);

        var batch = await ReceiveOkAsync(product.Id, warehouse.Id, 3, receivedAt);

        Assert.Equal(receivedAt, batch.ReceivedAt);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    [InlineData(1.2345)]
    public async Task Receive_InvalidQuantity_Returns400_AndStoresNothing(double quantity)
    {
        var product = await CreateProductAsync();
        var warehouse = await CreateWarehouseAsync();

        var response = await ReceiveAsync(new { productId = product.Id, warehouseId = warehouse.Id, quantity });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var list = await _client.GetFromJsonAsync<List<ReceiptBatchResponse>>($"/api/receipt-batches?productId={product.Id}");
        Assert.Empty(list!);
    }

    [Fact]
    public async Task Receive_MissingFields_Returns400()
    {
        var response = await ReceiveAsync(new { quantity = 5 });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Receive_FutureReceivedAt_Returns400()
    {
        var product = await CreateProductAsync();
        var warehouse = await CreateWarehouseAsync();

        var response = await ReceiveAsync(new { productId = product.Id, warehouseId = warehouse.Id, quantity = 1, receivedAt = DateTimeOffset.UtcNow.AddDays(2) });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Receive_UnknownProductOrWarehouse_Returns400()
    {
        var product = await CreateProductAsync();
        var warehouse = await CreateWarehouseAsync();

        var badProduct = await ReceiveAsync(new { productId = 999999, warehouseId = warehouse.Id, quantity = 1 });
        var badWarehouse = await ReceiveAsync(new { productId = product.Id, warehouseId = 999999, quantity = 1 });

        Assert.Equal(HttpStatusCode.BadRequest, badProduct.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, badWarehouse.StatusCode);
    }

    [Fact]
    public async Task Receive_IntoInactiveProductOrWarehouse_Returns409()
    {
        var product = await CreateProductAsync();
        var warehouse = await CreateWarehouseAsync();
        var inactiveProduct = await CreateProductAsync();
        var inactiveWarehouse = await CreateWarehouseAsync();
        await _client.PostAsync($"/api/products/{inactiveProduct.Id}/deactivate", null);
        await _client.PostAsync($"/api/warehouses/{inactiveWarehouse.Id}/deactivate", null);

        var a = await ReceiveAsync(new { productId = inactiveProduct.Id, warehouseId = warehouse.Id, quantity = 1 });
        var b = await ReceiveAsync(new { productId = product.Id, warehouseId = inactiveWarehouse.Id, quantity = 1 });

        Assert.Equal(HttpStatusCode.Conflict, a.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, b.StatusCode);
    }

    [Fact]
    public async Task Get_ReturnsBatch_Or404()
    {
        var product = await CreateProductAsync();
        var warehouse = await CreateWarehouseAsync();
        var batch = await ReceiveOkAsync(product.Id, warehouse.Id, 9);

        var found = await _client.GetAsync($"/api/receipt-batches/{batch.Id}");
        var missing = await _client.GetAsync("/api/receipt-batches/999999");

        Assert.Equal(HttpStatusCode.OK, found.StatusCode);
        Assert.Equal(batch.Id, (await found.Content.ReadFromJsonAsync<ReceiptBatchResponse>())!.Id);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }

    [Fact]
    public async Task List_FiltersByProductAndWarehouse_NewestFirst()
    {
        var product = await CreateProductAsync();
        var otherProduct = await CreateProductAsync();
        var warehouse = await CreateWarehouseAsync();
        var otherWarehouse = await CreateWarehouseAsync();
        var older = await ReceiveOkAsync(product.Id, warehouse.Id, 1, DateTimeOffset.UtcNow.AddDays(-30));
        var newer = await ReceiveOkAsync(product.Id, warehouse.Id, 2, DateTimeOffset.UtcNow.AddDays(-1));
        await ReceiveOkAsync(otherProduct.Id, warehouse.Id, 3);
        await ReceiveOkAsync(product.Id, otherWarehouse.Id, 4);

        var list = await _client.GetFromJsonAsync<List<ReceiptBatchResponse>>($"/api/receipt-batches?productId={product.Id}&warehouseId={warehouse.Id}");

        Assert.Equal([newer.Id, older.Id], list!.Select(b => b.Id));
    }

    [Fact]
    public async Task BatchesCannotBeEditedOrDeleted()
    {
        var product = await CreateProductAsync();
        var warehouse = await CreateWarehouseAsync();
        var batch = await ReceiveOkAsync(product.Id, warehouse.Id, 1);

        var put = await _client.PutAsJsonAsync($"/api/receipt-batches/{batch.Id}", new { quantity = 99 });
        var delete = await _client.DeleteAsync($"/api/receipt-batches/{batch.Id}");

        Assert.Equal(HttpStatusCode.MethodNotAllowed, put.StatusCode);
        Assert.Equal(HttpStatusCode.MethodNotAllowed, delete.StatusCode);
    }
}
