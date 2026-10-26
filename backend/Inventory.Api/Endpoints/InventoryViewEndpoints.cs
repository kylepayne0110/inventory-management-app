using Inventory.Api.Contracts;
using Inventory.Api.Data;
using Inventory.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Inventory.Api.Endpoints;

// Read-only views over stock and its history. Nothing here changes data.
public static class InventoryViewEndpoints
{
    public const int DefaultAgingDays = 90;
    public const int DefaultPageSize = 100;
    public const int MaxPageSize = 500;
    private const int MaxAgingDays = 36500;

    public static void MapInventoryViewEndpoints(this IEndpointRouteBuilder app)
    {
        // Optional filters: productId, warehouseId, receiptBatchId, type, transferId, from, to (inclusive, on OccurredAt), skip, take.
        // Newest first.
        app.MapGet("/api/stock-movements", async (
            int? productId, int? warehouseId, int? receiptBatchId, MovementType? type, Guid? transferId,
            DateTimeOffset? from, DateTimeOffset? to, int? skip, int? take,
            InventoryDbContext db, CancellationToken ct) =>
        {
            var skipCount = skip ?? 0;
            var takeCount = take ?? DefaultPageSize;
            if (skipCount < 0)
            {
                return Problem("Skip cannot be negative.");
            }

            if (takeCount is < 1 or > MaxPageSize)
            {
                return Problem($"Take must be between 1 and {MaxPageSize}.");
            }

            if (from is not null && to is not null && from > to)
            {
                return Problem("From cannot be after To.");
            }

            var query = db.StockMovements.AsNoTracking();
            if (productId is not null) query = query.Where(m => m.ProductId == productId);
            if (warehouseId is not null) query = query.Where(m => m.WarehouseId == warehouseId);
            if (receiptBatchId is not null) query = query.Where(m => m.ReceiptBatchId == receiptBatchId);
            if (type is not null) query = query.Where(m => m.Type == type);
            if (transferId is not null) query = query.Where(m => m.TransferId == transferId);
            if (from is not null) query = query.Where(m => m.OccurredAt >= from);
            if (to is not null) query = query.Where(m => m.OccurredAt <= to);

            var total = await query.CountAsync(ct);
            var items = await query
                .OrderByDescending(m => m.OccurredAt)
                .ThenByDescending(m => m.Id)
                .Skip(skipCount)
                .Take(takeCount)
                .Select(m => new StockMovementResponse(
                    m.Id, m.Type, m.ProductId, m.Product!.Sku, m.WarehouseId, m.Warehouse!.Code, m.ReceiptBatchId,
                    m.QuantityChange, m.OccurredAt, m.Reference, m.Reason, m.TransferId, m.CreatedAt))
                .ToListAsync(ct);

            return Results.Ok(new StockMovementPage(total, skipCount, takeCount, items));
        }).WithName("ListStockMovements").WithTags("Inventory views");

        var group = app.MapGroup("/api/inventory").WithTags("Inventory views");

        // Stock on hand per product and warehouse. Combinations with nothing on hand are left out.
        group.MapGet("/stock-levels", async (int? productId, int? warehouseId, InventoryDbContext db, CancellationToken ct) =>
        {
            var query = db.ReceiptBatches.AsNoTracking().Where(b => b.QuantityOnHand > 0);
            if (productId is not null) query = query.Where(b => b.ProductId == productId);
            if (warehouseId is not null) query = query.Where(b => b.WarehouseId == warehouseId);

            var batches = await query
                .Select(b => new { b.ProductId, b.Product!.Sku, b.Product.Name, b.WarehouseId, b.Warehouse!.Code, b.QuantityOnHand })
                .ToListAsync(ct);

            return Results.Ok(batches
                .GroupBy(b => (b.ProductId, b.Sku, b.Name, b.WarehouseId, b.Code))
                .Select(g => new StockLevelResponse(g.Key.ProductId, g.Key.Sku, g.Key.Name, g.Key.WarehouseId, g.Key.Code, g.Sum(b => b.QuantityOnHand), g.Count()))
                .OrderBy(l => l.ProductSku, StringComparer.Ordinal)
                .ThenBy(l => l.WarehouseCode, StringComparer.Ordinal));
        }).WithName("ListStockLevels");

        // Active products whose total on hand is at or below their own saved ReorderThreshold, including products with no stock at all.
        // With ?warehouseId= only that warehouse's stock is counted against the product's threshold. Lowest stock first.
        group.MapGet("/alerts/low-stock", async (int? warehouseId, InventoryDbContext db, CancellationToken ct) =>
        {
            if (warehouseId is not null && !await db.Warehouses.AnyAsync(w => w.Id == warehouseId, ct))
            {
                return Problem($"Warehouse {warehouseId} does not exist.");
            }

            var batchQuery = db.ReceiptBatches.AsNoTracking();
            if (warehouseId is not null) batchQuery = batchQuery.Where(b => b.WarehouseId == warehouseId);

            var onHand = (await batchQuery.Select(b => new { b.ProductId, b.QuantityOnHand }).ToListAsync(ct))
                .GroupBy(b => b.ProductId)
                .ToDictionary(g => g.Key, g => g.Sum(b => b.QuantityOnHand));

            var products = await db.Products.AsNoTracking().Where(p => p.IsActive).ToListAsync(ct);
            return Results.Ok(products
                .Select(p => new LowStockAlertResponse(p.Id, p.Sku, p.Name, warehouseId, onHand.GetValueOrDefault(p.Id), p.ReorderThreshold))
                .Where(a => a.QuantityOnHand <= a.Threshold)
                .OrderBy(a => a.QuantityOnHand)
                .ThenBy(a => a.ProductSku, StringComparer.Ordinal));
        }).WithName("GetLowStockAlerts");

        // Batches with stock still on hand that were received at least ?days= days ago (default 90). Oldest first.
        // Age is counted from the original receipt date, so transferred stock keeps its age.
        group.MapGet("/alerts/aging-stock", async (int? days, int? productId, int? warehouseId, InventoryDbContext db, CancellationToken ct) =>
        {
            var minimumDays = days ?? DefaultAgingDays;
            if (minimumDays is < 0 or > MaxAgingDays)
            {
                return Problem($"Days must be between 0 and {MaxAgingDays}.");
            }

            var now = DateTimeOffset.UtcNow;
            var cutoff = now.AddDays(-minimumDays);
            var query = db.ReceiptBatches.AsNoTracking().Where(b => b.QuantityOnHand > 0 && b.ReceivedAt <= cutoff);
            if (productId is not null) query = query.Where(b => b.ProductId == productId);
            if (warehouseId is not null) query = query.Where(b => b.WarehouseId == warehouseId);

            var batches = await query
                .Select(b => new { b.Id, b.ProductId, b.Product!.Sku, b.WarehouseId, b.Warehouse!.Code, b.ReceivedAt, b.QuantityOnHand, b.Reference })
                .ToListAsync(ct);

            return Results.Ok(batches
                .OrderBy(b => b.ReceivedAt)
                .ThenBy(b => b.Id)
                .Select(b => new AgingStockAlertResponse(b.Id, b.ProductId, b.Sku, b.WarehouseId, b.Code, b.ReceivedAt, (int)(now - b.ReceivedAt).TotalDays, b.QuantityOnHand, b.Reference)));
        }).WithName("GetAgingStockAlerts");
    }

    private static IResult Problem(string message) => Results.BadRequest(new { error = message });
}
