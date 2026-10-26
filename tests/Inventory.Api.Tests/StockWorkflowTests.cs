using System.Net;
using System.Net.Http.Json;
using Inventory.Api.Contracts;

namespace Inventory.Api.Tests;

// Follows stock through its whole life, the way the UI does, and checks that every view agrees.
public class StockWorkflowTests : IClassFixture<InventoryApiFactory>
{
    private readonly HttpClient _client;

    public StockWorkflowTests(InventoryApiFactory factory) => _client = factory.CreateClient();

    private static string Unique() => Guid.NewGuid().ToString("N")[..10];

    private async Task<T> CreateAsync<T>(string url, object body)
    {
        var response = await _client.PostAsJsonAsync(url, body);
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<T>())!;
    }

    [Fact]
    public async Task ReceiveTransferIssueAdjust_KeepsLevelsMovementsAndAlertsConsistent()
    {
        var product = await CreateAsync<ProductResponse>("/api/products", new { sku = "WF-" + Unique(), name = "Workflow item", reorderThreshold = 30 });
        var main = await CreateAsync<WarehouseResponse>("/api/warehouses", new { code = "M-" + Unique(), name = "Main" });
        var overflow = await CreateAsync<WarehouseResponse>("/api/warehouses", new { code = "O-" + Unique(), name = "Overflow" });
        var receivedAt = DateTimeOffset.UtcNow.AddDays(-120);

        var batch = await CreateAsync<ReceiptBatchResponse>("/api/receipt-batches",
            new { productId = product.Id, warehouseId = main.Id, quantity = 100, receivedAt });
        var transfer = await CreateAsync<TransferStockResponse>("/api/stock/transfer",
            new { sourceReceiptBatchId = batch.Id, destinationWarehouseId = overflow.Id, quantity = 40 });
        await CreateAsync<StockOperationResponse>("/api/stock/issue", new { receiptBatchId = batch.Id, quantity = 25 });
        await CreateAsync<StockOperationResponse>("/api/stock/adjust",
            new { receiptBatchId = transfer.DestinationBatch.Id, quantityChange = -5, reason = "Damaged" });

        // Main: 100 - 40 - 25 = 35. Overflow: 40 - 5 = 35. Total 70, above the threshold of 30.
        var levels = (await _client.GetFromJsonAsync<List<StockLevelResponse>>($"/api/inventory/stock-levels?productId={product.Id}"))!;
        Assert.Equal(new[] { 35m, 35m }, levels.Select(l => l.QuantityOnHand).ToArray());

        // Every operation recorded a movement: receive, transferOut, transferIn, issue, adjust.
        var page = (await _client.GetFromJsonAsync<StockMovementPage>($"/api/stock-movements?productId={product.Id}"))!;
        Assert.Equal(5, page.Total);
        Assert.Equal(70m, page.Items.Sum(m => m.QuantityChange));

        // The transferred batch keeps its age, so both batches appear as aging stock.
        var aging = (await _client.GetFromJsonAsync<List<AgingStockAlertResponse>>($"/api/inventory/alerts/aging-stock?days=100&productId={product.Id}"))!;
        Assert.Equal(2, aging.Count);
        Assert.All(aging, a => Assert.InRange(a.AgeDays, 119, 121));

        // Issuing more drops the product to its threshold, so it alerts.
        await CreateAsync<StockOperationResponse>("/api/stock/issue", new { receiptBatchId = batch.Id, quantity = 35 });
        await CreateAsync<StockOperationResponse>("/api/stock/issue", new { receiptBatchId = transfer.DestinationBatch.Id, quantity = 5 });
        var low = (await _client.GetFromJsonAsync<List<LowStockAlertResponse>>("/api/inventory/alerts/low-stock"))!;
        var alert = Assert.Single(low, a => a.ProductId == product.Id);
        Assert.Equal(30m, alert.QuantityOnHand);
        Assert.Equal(30m, alert.Threshold);
    }

    [Fact]
    public async Task DeactivatedProduct_CanStillBeIssuedButNotReceivedOrTransferred()
    {
        var product = await CreateAsync<ProductResponse>("/api/products", new { sku = "WF-" + Unique(), name = "Retiring" });
        var a = await CreateAsync<WarehouseResponse>("/api/warehouses", new { code = "A-" + Unique(), name = "A" });
        var b = await CreateAsync<WarehouseResponse>("/api/warehouses", new { code = "B-" + Unique(), name = "B" });
        var batch = await CreateAsync<ReceiptBatchResponse>("/api/receipt-batches", new { productId = product.Id, warehouseId = a.Id, quantity = 10 });

        Assert.Equal(HttpStatusCode.OK, (await _client.PostAsync($"/api/products/{product.Id}/deactivate", null)).StatusCode);

        Assert.Equal(HttpStatusCode.OK, (await _client.PostAsJsonAsync("/api/stock/issue", new { receiptBatchId = batch.Id, quantity = 2 })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await _client.PostAsJsonAsync("/api/receipt-batches", new { productId = product.Id, warehouseId = a.Id, quantity = 1 })).StatusCode);
        // Retired products no longer raise low-stock alerts.
        var low = (await _client.GetFromJsonAsync<List<LowStockAlertResponse>>("/api/inventory/alerts/low-stock"))!;
        Assert.DoesNotContain(low, x => x.ProductId == product.Id);
        // Like receiving, a transfer needs an active product (and destination warehouse).
        Assert.Equal(HttpStatusCode.Conflict, (await _client.PostAsJsonAsync("/api/stock/transfer", new { sourceReceiptBatchId = batch.Id, destinationWarehouseId = b.Id, quantity = 3 })).StatusCode);
    }
}
