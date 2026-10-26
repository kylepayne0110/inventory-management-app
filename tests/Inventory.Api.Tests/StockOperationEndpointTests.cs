using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Inventory.Api.Contracts;
using Inventory.Api.Data;
using Inventory.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Inventory.Api.Tests;

public class StockOperationEndpointTests : IClassFixture<InventoryApiFactory>
{
    private readonly InventoryApiFactory _factory;
    private readonly HttpClient _client;

    public StockOperationEndpointTests(InventoryApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private static string Unique() => Guid.NewGuid().ToString("N")[..10];

    private async Task<(ProductResponse Product, WarehouseResponse Warehouse, ReceiptBatchResponse Batch)> ReceiveAsync(decimal quantity, DateTimeOffset? receivedAt = null)
    {
        var p = await _client.PostAsJsonAsync("/api/products", new { sku = "P-" + Unique(), name = "Widget" });
        var product = (await p.Content.ReadFromJsonAsync<ProductResponse>())!;
        var w = await _client.PostAsJsonAsync("/api/warehouses", new { code = "W-" + Unique(), name = "Main" });
        var warehouse = (await w.Content.ReadFromJsonAsync<WarehouseResponse>())!;
        var b = await _client.PostAsJsonAsync("/api/receipt-batches", new { productId = product.Id, warehouseId = warehouse.Id, quantity, receivedAt });
        Assert.Equal(HttpStatusCode.Created, b.StatusCode);
        return (product, warehouse, (await b.Content.ReadFromJsonAsync<ReceiptBatchResponse>())!);
    }

    private Task<HttpResponseMessage> IssueAsync(object body) => _client.PostAsJsonAsync("/api/stock/issue", body);

    private Task<HttpResponseMessage> AdjustAsync(object body) => _client.PostAsJsonAsync("/api/stock/adjust", body);

    private async Task<decimal> OnHandAsync(int batchId) =>
        (await _client.GetFromJsonAsync<ReceiptBatchResponse>($"/api/receipt-batches/{batchId}"))!.QuantityOnHand;

    private async Task<List<StockMovement>> MovementsAsync(int batchId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
        return await db.StockMovements.Where(m => m.ReceiptBatchId == batchId).OrderBy(m => m.Id).ToListAsync();
    }

    [Fact]
    public async Task Responses_ReturnMovementTypeAsName_NotNumber()
    {
        var (_, _, batch) = await ReceiveAsync(10);

        var issue = await IssueAsync(new { receiptBatchId = batch.Id, quantity = 1 });
        var adjust = await AdjustAsync(new { receiptBatchId = batch.Id, quantityChange = -1, reason = "Count" });

        using var issueJson = JsonDocument.Parse(await issue.Content.ReadAsStringAsync());
        using var adjustJson = JsonDocument.Parse(await adjust.Content.ReadAsStringAsync());
        Assert.Equal(JsonValueKind.String, issueJson.RootElement.GetProperty("type").ValueKind);
        Assert.Equal("Issue", issueJson.RootElement.GetProperty("type").GetString());
        Assert.Equal("Adjust", adjustJson.RootElement.GetProperty("type").GetString());
    }

    [Fact]
    public async Task Issue_ReducesChosenBatch_AndRecordsNegativeMovement()
    {
        var (product, warehouse, batch) = await ReceiveAsync(40);

        var response = await IssueAsync(new { receiptBatchId = batch.Id, quantity = 15.5, reference = " SO-1 " });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = (await response.Content.ReadFromJsonAsync<StockOperationResponse>())!;
        Assert.Equal(MovementType.Issue, result.Type);
        Assert.Equal(-15.5m, result.QuantityChange);
        Assert.Equal(24.5m, result.QuantityOnHandAfter);
        Assert.Equal(24.5m, await OnHandAsync(batch.Id));
        var movements = await MovementsAsync(batch.Id);
        Assert.Equal([MovementType.Receive, MovementType.Issue], movements.Select(m => m.Type));
        Assert.Equal(product.Id, movements[1].ProductId);
        Assert.Equal(warehouse.Id, movements[1].WarehouseId);
        Assert.Equal("SO-1", movements[1].Reference);
    }

    [Fact]
    public async Task Issue_EntireBatch_LeavesZeroOnHand()
    {
        var (_, _, batch) = await ReceiveAsync(5);

        var response = await IssueAsync(new { receiptBatchId = batch.Id, quantity = 5 });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(0m, await OnHandAsync(batch.Id));
    }

    [Fact]
    public async Task Issue_MoreThanOnHand_Returns409_AndChangesNothing()
    {
        var (_, _, batch) = await ReceiveAsync(5);

        var response = await IssueAsync(new { receiptBatchId = batch.Id, quantity = 5.001 });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(5m, await OnHandAsync(batch.Id));
        Assert.Single(await MovementsAsync(batch.Id));
    }

    [Fact]
    public async Task Issue_OnlyTouchesTheChosenBatch()
    {
        var (product, warehouse, older) = await ReceiveAsync(10, DateTimeOffset.UtcNow.AddDays(-30));
        var newerResponse = await _client.PostAsJsonAsync("/api/receipt-batches", new { productId = product.Id, warehouseId = warehouse.Id, quantity = 10 });
        var newer = (await newerResponse.Content.ReadFromJsonAsync<ReceiptBatchResponse>())!;

        await IssueAsync(new { receiptBatchId = newer.Id, quantity = 4 });

        Assert.Equal(10m, await OnHandAsync(older.Id));
        Assert.Equal(6m, await OnHandAsync(newer.Id));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(1.2345)]
    public async Task Issue_InvalidQuantity_Returns400(double quantity)
    {
        var (_, _, batch) = await ReceiveAsync(5);

        var response = await IssueAsync(new { receiptBatchId = batch.Id, quantity });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(5m, await OnHandAsync(batch.Id));
    }

    [Fact]
    public async Task Issue_MissingFieldsOrUnknownBatch_Returns400()
    {
        var missing = await IssueAsync(new { quantity = 1 });
        var unknown = await IssueAsync(new { receiptBatchId = 999999, quantity = 1 });

        Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, unknown.StatusCode);
    }

    [Fact]
    public async Task Issue_FutureOrBeforeReceiptDate_Returns400()
    {
        var (_, _, batch) = await ReceiveAsync(5, DateTimeOffset.UtcNow.AddDays(-5));

        var future = await IssueAsync(new { receiptBatchId = batch.Id, quantity = 1, occurredAt = DateTimeOffset.UtcNow.AddDays(1) });
        var early = await IssueAsync(new { receiptBatchId = batch.Id, quantity = 1, occurredAt = DateTimeOffset.UtcNow.AddDays(-6) });

        Assert.Equal(HttpStatusCode.BadRequest, future.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, early.StatusCode);
    }

    [Fact]
    public async Task Issue_FromInactiveProductAndWarehouse_IsAllowed()
    {
        var (product, warehouse, batch) = await ReceiveAsync(5);
        await _client.PostAsync($"/api/products/{product.Id}/deactivate", null);
        await _client.PostAsync($"/api/warehouses/{warehouse.Id}/deactivate", null);

        var response = await IssueAsync(new { receiptBatchId = batch.Id, quantity = 5 });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Adjust_Down_RecordsMovementWithReason()
    {
        var (_, _, batch) = await ReceiveAsync(10);

        var response = await AdjustAsync(new { receiptBatchId = batch.Id, quantityChange = -2, reason = " Damaged " });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = (await response.Content.ReadFromJsonAsync<StockOperationResponse>())!;
        Assert.Equal(MovementType.Adjust, result.Type);
        Assert.Equal(8m, result.QuantityOnHandAfter);
        Assert.Equal("Damaged", result.Reason);
        Assert.Equal(8m, await OnHandAsync(batch.Id));
        var movement = (await MovementsAsync(batch.Id)).Last();
        Assert.Equal(MovementType.Adjust, movement.Type);
        Assert.Equal(-2m, movement.QuantityChange);
        Assert.Equal("Damaged", movement.Reason);
    }

    [Fact]
    public async Task Adjust_Up_AfterIssue_RestoresUpToQuantityReceived()
    {
        var (_, _, batch) = await ReceiveAsync(10);
        await IssueAsync(new { receiptBatchId = batch.Id, quantity = 6 });

        var response = await AdjustAsync(new { receiptBatchId = batch.Id, quantityChange = 6, reason = "Issued in error" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(10m, await OnHandAsync(batch.Id));
    }

    [Fact]
    public async Task Adjust_AboveQuantityReceivedOrBelowZero_Returns409()
    {
        var (_, _, batch) = await ReceiveAsync(10);

        var above = await AdjustAsync(new { receiptBatchId = batch.Id, quantityChange = 0.001, reason = "Found more" });
        var below = await AdjustAsync(new { receiptBatchId = batch.Id, quantityChange = -10.001, reason = "Lost" });

        Assert.Equal(HttpStatusCode.Conflict, above.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, below.StatusCode);
        Assert.Equal(10m, await OnHandAsync(batch.Id));
        Assert.Single(await MovementsAsync(batch.Id));
    }

    [Theory]
    [InlineData(0, "Reason")]
    [InlineData(1.2345, "Reason")]
    [InlineData(-1, null)]
    [InlineData(-1, "   ")]
    public async Task Adjust_InvalidInput_Returns400(double quantityChange, string? reason)
    {
        var (_, _, batch) = await ReceiveAsync(10);

        var response = await AdjustAsync(new { receiptBatchId = batch.Id, quantityChange, reason });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(10m, await OnHandAsync(batch.Id));
    }

    [Fact]
    public async Task Adjust_UnknownBatch_Returns400()
    {
        var response = await AdjustAsync(new { receiptBatchId = 999999, quantityChange = -1, reason = "x" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Receipt_KeepsOriginalQuantityReceivedAfterOperations()
    {
        var (_, _, batch) = await ReceiveAsync(10);

        await IssueAsync(new { receiptBatchId = batch.Id, quantity = 4 });
        await AdjustAsync(new { receiptBatchId = batch.Id, quantityChange = -1, reason = "Count" });

        var current = (await _client.GetFromJsonAsync<ReceiptBatchResponse>($"/api/receipt-batches/{batch.Id}"))!;
        Assert.Equal(10m, current.QuantityReceived);
        Assert.Equal(5m, current.QuantityOnHand);
        Assert.Equal(batch.ReceivedAt, current.ReceivedAt);
    }
}
