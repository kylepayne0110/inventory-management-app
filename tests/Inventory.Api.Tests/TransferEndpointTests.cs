using System.Net;
using System.Net.Http.Json;
using Inventory.Api.Contracts;
using Inventory.Api.Data;
using Inventory.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Inventory.Api.Tests;

public class TransferEndpointTests : IClassFixture<InventoryApiFactory>
{
    private readonly InventoryApiFactory _factory;
    private readonly HttpClient _client;

    public TransferEndpointTests(InventoryApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private static string Unique() => Guid.NewGuid().ToString("N")[..10];

    private async Task<WarehouseResponse> NewWarehouseAsync()
    {
        var w = await _client.PostAsJsonAsync("/api/warehouses", new { code = "W-" + Unique(), name = "WH" });
        return (await w.Content.ReadFromJsonAsync<WarehouseResponse>())!;
    }

    private async Task<(ProductResponse Product, WarehouseResponse Source, WarehouseResponse Destination, ReceiptBatchResponse Batch)> SetupAsync(decimal quantity, DateTimeOffset? receivedAt = null)
    {
        var p = await _client.PostAsJsonAsync("/api/products", new { sku = "P-" + Unique(), name = "Widget" });
        var product = (await p.Content.ReadFromJsonAsync<ProductResponse>())!;
        var source = await NewWarehouseAsync();
        var destination = await NewWarehouseAsync();
        var b = await _client.PostAsJsonAsync("/api/receipt-batches", new { productId = product.Id, warehouseId = source.Id, quantity, receivedAt, reference = "PO-1" });
        Assert.Equal(HttpStatusCode.Created, b.StatusCode);
        return (product, source, destination, (await b.Content.ReadFromJsonAsync<ReceiptBatchResponse>())!);
    }

    private Task<HttpResponseMessage> TransferAsync(object body) => _client.PostAsJsonAsync("/api/stock/transfer", body);

    private async Task<decimal> OnHandAsync(int batchId) =>
        (await _client.GetFromJsonAsync<ReceiptBatchResponse>($"/api/receipt-batches/{batchId}"))!.QuantityOnHand;

    private async Task<int> BatchCountAsync()
    {
        using var scope = _factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<InventoryDbContext>().ReceiptBatches.CountAsync();
    }

    [Fact]
    public async Task Transfer_MovesStockToNewBatch_PreservingReceivedAt()
    {
        var receivedAt = DateTimeOffset.UtcNow.AddDays(-90);
        var (product, source, destination, batch) = await SetupAsync(40, receivedAt);

        var response = await TransferAsync(new { sourceReceiptBatchId = batch.Id, destinationWarehouseId = destination.Id, quantity = 15.5, reference = " TR-1 " });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = (await response.Content.ReadFromJsonAsync<TransferStockResponse>())!;
        Assert.Equal(24.5m, await OnHandAsync(batch.Id));
        Assert.Equal(40m, (await _client.GetFromJsonAsync<ReceiptBatchResponse>($"/api/receipt-batches/{batch.Id}"))!.QuantityReceived);
        Assert.NotEqual(batch.Id, result.DestinationBatch.Id);
        Assert.Equal(destination.Id, result.DestinationBatch.WarehouseId);
        Assert.Equal(product.Id, result.DestinationBatch.ProductId);
        Assert.Equal(batch.ReceivedAt, result.DestinationBatch.ReceivedAt);
        Assert.Equal(15.5m, result.DestinationBatch.QuantityOnHand);
        Assert.Equal(15.5m, result.DestinationBatch.QuantityReceived);
        Assert.Equal("PO-1", result.DestinationBatch.Reference);
        Assert.Equal(MovementType.TransferOut, result.Out.Type);
        Assert.Equal(-15.5m, result.Out.QuantityChange);
        Assert.Equal(source.Id, result.Out.WarehouseId);
        Assert.Equal(24.5m, result.Out.QuantityOnHandAfter);
        Assert.Equal(MovementType.TransferIn, result.In.Type);
        Assert.Equal(15.5m, result.In.QuantityChange);
        Assert.Equal(destination.Id, result.In.WarehouseId);
        Assert.Equal(result.DestinationBatch.Id, result.In.ReceiptBatchId);
        Assert.Equal("TR-1", result.Out.Reference);
        Assert.Equal("TR-1", result.In.Reference);
    }

    [Fact]
    public async Task Transfer_RecordsBothMovementsInDatabase()
    {
        var (_, _, destination, batch) = await SetupAsync(10);

        var result = (await (await TransferAsync(new { sourceReceiptBatchId = batch.Id, destinationWarehouseId = destination.Id, quantity = 4 }))
            .Content.ReadFromJsonAsync<TransferStockResponse>())!;

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
        var source = await db.StockMovements.Where(m => m.ReceiptBatchId == batch.Id).OrderBy(m => m.Id).ToListAsync();
        Assert.Equal([MovementType.Receive, MovementType.TransferOut], source.Select(m => m.Type));
        var target = await db.StockMovements.Where(m => m.ReceiptBatchId == result.DestinationBatch.Id).ToListAsync();
        Assert.Equal(MovementType.TransferIn, Assert.Single(target).Type);
    }

    [Fact]
    public async Task Transfer_BothMovementsShareTheSameTransferId()
    {
        var (_, _, destination, batch) = await SetupAsync(10);

        var result = (await (await TransferAsync(new { sourceReceiptBatchId = batch.Id, destinationWarehouseId = destination.Id, quantity = 4 }))
            .Content.ReadFromJsonAsync<TransferStockResponse>())!;

        Assert.NotEqual(Guid.Empty, result.TransferId);
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
        var movements = await db.StockMovements.Where(m => m.TransferId == result.TransferId).OrderBy(m => m.Id).ToListAsync();
        Assert.Equal([MovementType.TransferOut, MovementType.TransferIn], movements.Select(m => m.Type));
        Assert.Equal(batch.Id, movements[0].ReceiptBatchId);
        Assert.Equal(result.DestinationBatch.Id, movements[1].ReceiptBatchId);
        Assert.All(movements, m => Assert.Equal(result.TransferId, m.TransferId));
    }

    [Fact]
    public async Task Transfer_EachTransferGetsItsOwnTransferId_AndOtherMovementsHaveNone()
    {
        var (_, _, destination, batch) = await SetupAsync(10);

        var first = (await (await TransferAsync(new { sourceReceiptBatchId = batch.Id, destinationWarehouseId = destination.Id, quantity = 1 }))
            .Content.ReadFromJsonAsync<TransferStockResponse>())!;
        var second = (await (await TransferAsync(new { sourceReceiptBatchId = batch.Id, destinationWarehouseId = destination.Id, quantity = 1 }))
            .Content.ReadFromJsonAsync<TransferStockResponse>())!;

        Assert.NotEqual(first.TransferId, second.TransferId);
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
        var receive = await db.StockMovements.SingleAsync(m => m.ReceiptBatchId == batch.Id && m.Type == MovementType.Receive);
        Assert.Null(receive.TransferId);
    }

    [Fact]
    public async Task Transfer_WholeBatch_LeavesSourceAtZero()
    {
        var (_, _, destination, batch) = await SetupAsync(10);

        var response = await TransferAsync(new { sourceReceiptBatchId = batch.Id, destinationWarehouseId = destination.Id, quantity = 10 });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(0m, await OnHandAsync(batch.Id));
    }

    [Fact]
    public async Task Transfer_MoreThanOnHand_Returns409_AndChangesNothing()
    {
        var (_, _, destination, batch) = await SetupAsync(10);
        var before = await BatchCountAsync();

        var response = await TransferAsync(new { sourceReceiptBatchId = batch.Id, destinationWarehouseId = destination.Id, quantity = 10.001 });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(10m, await OnHandAsync(batch.Id));
        Assert.Equal(before, await BatchCountAsync());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(1.0005)]
    public async Task Transfer_InvalidQuantity_Returns400(double quantity)
    {
        var (_, _, destination, batch) = await SetupAsync(10);

        var response = await TransferAsync(new { sourceReceiptBatchId = batch.Id, destinationWarehouseId = destination.Id, quantity });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(10m, await OnHandAsync(batch.Id));
    }

    [Fact]
    public async Task Transfer_MissingFields_Returns400()
    {
        var response = await TransferAsync(new { quantity = 1 });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Transfer_UnknownBatch_Returns400()
    {
        var destination = await NewWarehouseAsync();

        var response = await TransferAsync(new { sourceReceiptBatchId = 999999, destinationWarehouseId = destination.Id, quantity = 1 });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Transfer_UnknownDestination_Returns400()
    {
        var (_, _, _, batch) = await SetupAsync(10);

        var response = await TransferAsync(new { sourceReceiptBatchId = batch.Id, destinationWarehouseId = 999999, quantity = 1 });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Transfer_ToSameWarehouse_Returns400()
    {
        var (_, source, _, batch) = await SetupAsync(10);

        var response = await TransferAsync(new { sourceReceiptBatchId = batch.Id, destinationWarehouseId = source.Id, quantity = 1 });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Transfer_FutureOccurredAt_Returns400()
    {
        var (_, _, destination, batch) = await SetupAsync(10);

        var response = await TransferAsync(new { sourceReceiptBatchId = batch.Id, destinationWarehouseId = destination.Id, quantity = 1, occurredAt = DateTimeOffset.UtcNow.AddDays(1) });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Transfer_OccurredBeforeBatchReceived_Returns400()
    {
        var (_, _, destination, batch) = await SetupAsync(10, DateTimeOffset.UtcNow.AddDays(-5));

        var response = await TransferAsync(new { sourceReceiptBatchId = batch.Id, destinationWarehouseId = destination.Id, quantity = 1, occurredAt = DateTimeOffset.UtcNow.AddDays(-10) });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Transfer_ToInactiveWarehouse_Returns409()
    {
        var (_, _, destination, batch) = await SetupAsync(10);
        await _client.PostAsync($"/api/warehouses/{destination.Id}/deactivate", null);

        var response = await TransferAsync(new { sourceReceiptBatchId = batch.Id, destinationWarehouseId = destination.Id, quantity = 1 });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(10m, await OnHandAsync(batch.Id));
    }

    [Fact]
    public async Task Transfer_InactiveProduct_Returns409()
    {
        var (product, _, destination, batch) = await SetupAsync(10);
        await _client.PostAsync($"/api/products/{product.Id}/deactivate", null);

        var response = await TransferAsync(new { sourceReceiptBatchId = batch.Id, destinationWarehouseId = destination.Id, quantity = 1 });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Transfer_FromInactiveSourceWarehouse_IsAllowed()
    {
        var (_, source, destination, batch) = await SetupAsync(10);
        await _client.PostAsync($"/api/warehouses/{source.Id}/deactivate", null);

        var response = await TransferAsync(new { sourceReceiptBatchId = batch.Id, destinationWarehouseId = destination.Id, quantity = 3 });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(7m, await OnHandAsync(batch.Id));
    }

    [Fact]
    public async Task TransferredBatch_CanBeIssuedFromAndTransferredAgain_KeepingOriginalDate()
    {
        var (_, _, destination, batch) = await SetupAsync(10, DateTimeOffset.UtcNow.AddDays(-30));
        var third = await NewWarehouseAsync();
        var first = (await (await TransferAsync(new { sourceReceiptBatchId = batch.Id, destinationWarehouseId = destination.Id, quantity = 6 }))
            .Content.ReadFromJsonAsync<TransferStockResponse>())!;

        var issue = await _client.PostAsJsonAsync("/api/stock/issue", new { receiptBatchId = first.DestinationBatch.Id, quantity = 1 });
        var second = (await (await TransferAsync(new { sourceReceiptBatchId = first.DestinationBatch.Id, destinationWarehouseId = third.Id, quantity = 2 }))
            .Content.ReadFromJsonAsync<TransferStockResponse>())!;

        Assert.Equal(HttpStatusCode.OK, issue.StatusCode);
        Assert.Equal(batch.ReceivedAt, second.DestinationBatch.ReceivedAt);
        Assert.Equal(3m, await OnHandAsync(first.DestinationBatch.Id));
    }

    [Fact]
    public async Task Responses_ReturnMovementTypesAsNames()
    {
        var (_, _, destination, batch) = await SetupAsync(10);

        var json = await (await TransferAsync(new { sourceReceiptBatchId = batch.Id, destinationWarehouseId = destination.Id, quantity = 1 })).Content.ReadAsStringAsync();

        Assert.Contains("\"TransferOut\"", json);
        Assert.Contains("\"TransferIn\"", json);
    }
}
