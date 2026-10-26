using System.Net;
using System.Net.Http.Json;
using Inventory.Api.Contracts;
using Inventory.Api.Models;

namespace Inventory.Api.Tests;

public class InventoryViewEndpointTests : IClassFixture<InventoryApiFactory>
{
    private readonly HttpClient _client;

    public InventoryViewEndpointTests(InventoryApiFactory factory) => _client = factory.CreateClient();

    private static string Unique() => Guid.NewGuid().ToString("N")[..10];

    private async Task<ProductResponse> NewProductAsync(decimal? reorderThreshold = null)
    {
        var r = await _client.PostAsJsonAsync("/api/products", new { sku = "P-" + Unique(), name = "Widget", reorderThreshold });
        return (await r.Content.ReadFromJsonAsync<ProductResponse>())!;
    }

    private async Task<WarehouseResponse> NewWarehouseAsync()
    {
        var r = await _client.PostAsJsonAsync("/api/warehouses", new { code = "W-" + Unique(), name = "WH" });
        return (await r.Content.ReadFromJsonAsync<WarehouseResponse>())!;
    }

    private async Task<ReceiptBatchResponse> ReceiveAsync(int productId, int warehouseId, decimal quantity, DateTimeOffset? receivedAt = null)
    {
        var r = await _client.PostAsJsonAsync("/api/receipt-batches", new { productId, warehouseId, quantity, receivedAt });
        Assert.Equal(HttpStatusCode.Created, r.StatusCode);
        return (await r.Content.ReadFromJsonAsync<ReceiptBatchResponse>())!;
    }

    private Task<StockMovementPage> MovementsAsync(string query) =>
        _client.GetFromJsonAsync<StockMovementPage>("/api/stock-movements?" + query)!;

    [Fact]
    public async Task Movements_ShowAllOperationsNewestFirst()
    {
        var product = await NewProductAsync();
        var wh = await NewWarehouseAsync();
        var other = await NewWarehouseAsync();
        var batch = await ReceiveAsync(product.Id, wh.Id, 50, DateTimeOffset.UtcNow.AddDays(-5));
        await _client.PostAsJsonAsync("/api/stock/issue", new { receiptBatchId = batch.Id, quantity = 5, occurredAt = DateTimeOffset.UtcNow.AddDays(-4) });
        await _client.PostAsJsonAsync("/api/stock/adjust", new { receiptBatchId = batch.Id, quantityChange = -1, reason = "damaged", occurredAt = DateTimeOffset.UtcNow.AddDays(-3) });
        await _client.PostAsJsonAsync("/api/stock/transfer", new { sourceReceiptBatchId = batch.Id, destinationWarehouseId = other.Id, quantity = 10, occurredAt = DateTimeOffset.UtcNow.AddDays(-2) });

        var page = await MovementsAsync($"productId={product.Id}");

        Assert.Equal(5, page.Total);
        Assert.Equal(
            [MovementType.TransferIn, MovementType.TransferOut, MovementType.Adjust, MovementType.Issue, MovementType.Receive],
            page.Items.Select(i => i.Type));
        Assert.All(page.Items, i => Assert.Equal(product.Sku, i.ProductSku));
        var adjust = page.Items.Single(i => i.Type == MovementType.Adjust);
        Assert.Equal("damaged", adjust.Reason);
        Assert.Equal(-1m, adjust.QuantityChange);
        var transfers = page.Items.Where(i => i.TransferId is not null).ToList();
        Assert.Equal(2, transfers.Count);
        Assert.Equal(transfers[0].TransferId, transfers[1].TransferId);
    }

    [Fact]
    public async Task Movements_FilterByWarehouseTypeBatchAndTransfer()
    {
        var product = await NewProductAsync();
        var wh = await NewWarehouseAsync();
        var other = await NewWarehouseAsync();
        var batch = await ReceiveAsync(product.Id, wh.Id, 20);
        var transfer = (await (await _client.PostAsJsonAsync("/api/stock/transfer", new { sourceReceiptBatchId = batch.Id, destinationWarehouseId = other.Id, quantity = 4 }))
            .Content.ReadFromJsonAsync<TransferStockResponse>())!;

        Assert.Equal(2, (await MovementsAsync($"warehouseId={wh.Id}")).Total);
        Assert.Equal(1, (await MovementsAsync($"warehouseId={other.Id}")).Total);
        Assert.Equal(1, (await MovementsAsync($"productId={product.Id}&type=Receive")).Total);
        Assert.Equal(1, (await MovementsAsync($"productId={product.Id}&type=TransferOut")).Total);
        Assert.Equal(2, (await MovementsAsync($"receiptBatchId={batch.Id}")).Total);
        Assert.Equal(2, (await MovementsAsync($"transferId={transfer.TransferId}")).Total);
        Assert.Equal(0, (await MovementsAsync($"transferId={Guid.NewGuid()}")).Total);
    }

    [Fact]
    public async Task Movements_FilterByDateRange()
    {
        var product = await NewProductAsync();
        var wh = await NewWarehouseAsync();
        await ReceiveAsync(product.Id, wh.Id, 1, DateTimeOffset.UtcNow.AddDays(-30));
        await ReceiveAsync(product.Id, wh.Id, 1, DateTimeOffset.UtcNow.AddDays(-10));
        await ReceiveAsync(product.Id, wh.Id, 1, DateTimeOffset.UtcNow.AddDays(-1));

        var from = Uri.EscapeDataString(DateTimeOffset.UtcNow.AddDays(-15).ToString("o"));
        var to = Uri.EscapeDataString(DateTimeOffset.UtcNow.AddDays(-5).ToString("o"));

        Assert.Equal(2, (await MovementsAsync($"productId={product.Id}&from={from}")).Total);
        Assert.Equal(1, (await MovementsAsync($"productId={product.Id}&from={from}&to={to}")).Total);
    }

    [Fact]
    public async Task Movements_PageResults()
    {
        var product = await NewProductAsync();
        var wh = await NewWarehouseAsync();
        for (var i = 0; i < 5; i++)
        {
            await ReceiveAsync(product.Id, wh.Id, 1, DateTimeOffset.UtcNow.AddDays(-10 + i));
        }

        var first = await MovementsAsync($"productId={product.Id}&take=2");
        var second = await MovementsAsync($"productId={product.Id}&skip=2&take=2");
        var last = await MovementsAsync($"productId={product.Id}&skip=4&take=2");

        Assert.Equal(5, first.Total);
        Assert.Equal(2, first.Items.Count);
        Assert.Equal(2, second.Items.Count);
        Assert.Single(last.Items);
        Assert.Empty(first.Items.Select(i => i.Id).Intersect(second.Items.Select(i => i.Id)));
    }

    [Theory]
    [InlineData("skip=-1")]
    [InlineData("take=0")]
    [InlineData("take=501")]
    [InlineData("type=Bogus")]
    [InlineData("from=2026-02-01T00:00:00Z&to=2026-01-01T00:00:00Z")]
    public async Task Movements_InvalidQuery_Returns400(string query)
    {
        var response = await _client.GetAsync("/api/stock-movements?" + query);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task StockLevels_SumBatchesPerProductAndWarehouse()
    {
        var product = await NewProductAsync();
        var wh = await NewWarehouseAsync();
        var empty = await NewWarehouseAsync();
        var a = await ReceiveAsync(product.Id, wh.Id, 10);
        await ReceiveAsync(product.Id, wh.Id, 5.5m);
        var drained = await ReceiveAsync(product.Id, empty.Id, 3);
        await _client.PostAsJsonAsync("/api/stock/issue", new { receiptBatchId = drained.Id, quantity = 3 });
        await _client.PostAsJsonAsync("/api/stock/issue", new { receiptBatchId = a.Id, quantity = 2 });

        var levels = (await _client.GetFromJsonAsync<List<StockLevelResponse>>($"/api/inventory/stock-levels?productId={product.Id}"))!;

        var level = Assert.Single(levels);
        Assert.Equal(wh.Id, level.WarehouseId);
        Assert.Equal(13.5m, level.QuantityOnHand);
        Assert.Equal(2, level.BatchCount);
    }

    private Task<List<LowStockAlertResponse>> LowStockAsync(string query = "") =>
        _client.GetFromJsonAsync<List<LowStockAlertResponse>>("/api/inventory/alerts/low-stock" + query)!;

    [Fact]
    public async Task LowStock_ComparesEachProductAgainstItsOwnThreshold()
    {
        var low = await NewProductAsync(20);            // 7 on hand, below 20
        var exact = await NewProductAsync(20);          // exactly 20, alerts (at or below)
        var plenty = await NewProductAsync(20);         // 21 on hand, above 20
        var sameStockHigherBar = await NewProductAsync(5); // 7 on hand, above its own 5
        var none = await NewProductAsync(5);            // nothing on hand
        var retired = await NewProductAsync(5);
        var wh = await NewWarehouseAsync();
        var other = await NewWarehouseAsync();
        await ReceiveAsync(low.Id, wh.Id, 3);
        await ReceiveAsync(low.Id, other.Id, 4);
        await ReceiveAsync(exact.Id, wh.Id, 20);
        await ReceiveAsync(plenty.Id, wh.Id, 21);
        await ReceiveAsync(sameStockHigherBar.Id, wh.Id, 7);
        await _client.PostAsync($"/api/products/{retired.Id}/deactivate", null);

        var alerts = await LowStockAsync();

        Assert.Contains(alerts, a => a.ProductId == low.Id && a.QuantityOnHand == 7 && a.Threshold == 20);
        Assert.Contains(alerts, a => a.ProductId == exact.Id && a.QuantityOnHand == 20 && a.Threshold == 20);
        Assert.Contains(alerts, a => a.ProductId == none.Id && a.QuantityOnHand == 0 && a.Threshold == 5);
        Assert.DoesNotContain(alerts, a => a.ProductId == plenty.Id);
        Assert.DoesNotContain(alerts, a => a.ProductId == sameStockHigherBar.Id);
        Assert.DoesNotContain(alerts, a => a.ProductId == retired.Id);
        Assert.Equal(alerts.OrderBy(a => a.QuantityOnHand).Select(a => a.QuantityOnHand), alerts.Select(a => a.QuantityOnHand));
    }

    [Fact]
    public async Task LowStock_DefaultThresholdZero_AlertsOnlyWhenOutOfStock()
    {
        var stocked = await NewProductAsync();
        var empty = await NewProductAsync();
        var wh = await NewWarehouseAsync();
        await ReceiveAsync(stocked.Id, wh.Id, 1);

        var alerts = await LowStockAsync();

        Assert.DoesNotContain(alerts, a => a.ProductId == stocked.Id);
        Assert.Contains(alerts, a => a.ProductId == empty.Id && a.Threshold == 0);
    }

    [Fact]
    public async Task LowStock_UsesUpdatedThreshold()
    {
        var product = await NewProductAsync();
        var wh = await NewWarehouseAsync();
        await ReceiveAsync(product.Id, wh.Id, 8);
        Assert.DoesNotContain(await LowStockAsync(), a => a.ProductId == product.Id);

        await _client.PutAsJsonAsync($"/api/products/{product.Id}", new { name = "Widget", reorderThreshold = 10 });

        Assert.Contains(await LowStockAsync(), a => a.ProductId == product.Id && a.Threshold == 10);
    }

    [Fact]
    public async Task LowStock_CanBeLimitedToOneWarehouse()
    {
        var product = await NewProductAsync(10);
        var wh = await NewWarehouseAsync();
        var other = await NewWarehouseAsync();
        await ReceiveAsync(product.Id, wh.Id, 100);
        await ReceiveAsync(product.Id, other.Id, 2);

        var all = await LowStockAsync();
        var inOther = await LowStockAsync($"?warehouseId={other.Id}");
        var inMain = await LowStockAsync($"?warehouseId={wh.Id}");

        Assert.DoesNotContain(all, a => a.ProductId == product.Id);
        Assert.Contains(inOther, a => a.ProductId == product.Id && a.QuantityOnHand == 2 && a.WarehouseId == other.Id);
        Assert.DoesNotContain(inMain, a => a.ProductId == product.Id);
    }

    [Fact]
    public async Task LowStock_UnknownWarehouse_Returns400()
    {
        var response = await _client.GetAsync("/api/inventory/alerts/low-stock?warehouseId=999999");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task AgingStock_ListsOldBatchesWithStockOldestFirst()
    {
        var product = await NewProductAsync();
        var wh = await NewWarehouseAsync();
        var old = await ReceiveAsync(product.Id, wh.Id, 5, DateTimeOffset.UtcNow.AddDays(-200));
        var older = await ReceiveAsync(product.Id, wh.Id, 5, DateTimeOffset.UtcNow.AddDays(-300));
        var fresh = await ReceiveAsync(product.Id, wh.Id, 5, DateTimeOffset.UtcNow.AddDays(-10));
        var emptied = await ReceiveAsync(product.Id, wh.Id, 5, DateTimeOffset.UtcNow.AddDays(-400));
        await _client.PostAsJsonAsync("/api/stock/issue", new { receiptBatchId = emptied.Id, quantity = 5 });

        var alerts = (await _client.GetFromJsonAsync<List<AgingStockAlertResponse>>($"/api/inventory/alerts/aging-stock?productId={product.Id}"))!;

        Assert.Equal([older.Id, old.Id], alerts.Select(a => a.ReceiptBatchId));
        Assert.InRange(alerts[0].AgeDays, 299, 300);
        Assert.DoesNotContain(alerts, a => a.ReceiptBatchId == fresh.Id);

        var strict = (await _client.GetFromJsonAsync<List<AgingStockAlertResponse>>($"/api/inventory/alerts/aging-stock?productId={product.Id}&days=250"))!;
        Assert.Equal([older.Id], strict.Select(a => a.ReceiptBatchId));

        var all = (await _client.GetFromJsonAsync<List<AgingStockAlertResponse>>($"/api/inventory/alerts/aging-stock?productId={product.Id}&days=0"))!;
        Assert.Equal(3, all.Count);
    }

    [Fact]
    public async Task AgingStock_TransferredStockKeepsItsOriginalAge()
    {
        var product = await NewProductAsync();
        var wh = await NewWarehouseAsync();
        var other = await NewWarehouseAsync();
        var batch = await ReceiveAsync(product.Id, wh.Id, 10, DateTimeOffset.UtcNow.AddDays(-120));
        var transfer = (await (await _client.PostAsJsonAsync("/api/stock/transfer", new { sourceReceiptBatchId = batch.Id, destinationWarehouseId = other.Id, quantity = 10 }))
            .Content.ReadFromJsonAsync<TransferStockResponse>())!;

        var alerts = (await _client.GetFromJsonAsync<List<AgingStockAlertResponse>>($"/api/inventory/alerts/aging-stock?warehouseId={other.Id}"))!;

        var alert = Assert.Single(alerts);
        Assert.Equal(transfer.DestinationBatch.Id, alert.ReceiptBatchId);
        Assert.InRange(alert.AgeDays, 119, 120);
        // The emptied source batch no longer appears.
        Assert.Empty((await _client.GetFromJsonAsync<List<AgingStockAlertResponse>>($"/api/inventory/alerts/aging-stock?warehouseId={wh.Id}"))!);
    }

    [Theory]
    [InlineData("days=-1")]
    [InlineData("days=40000")]
    public async Task AgingStock_InvalidQuery_Returns400(string query)
    {
        var response = await _client.GetAsync("/api/inventory/alerts/aging-stock?" + query);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
