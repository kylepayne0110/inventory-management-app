using Inventory.Api.Contracts;
using Inventory.Api.Data;
using Inventory.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Inventory.Api.Endpoints;

public static class ReceiptEndpoints
{
    public static void MapReceiptEndpoints(this IEndpointRouteBuilder app)
    {
        // Batches are created by receiving stock. There is deliberately no PUT or DELETE:
        // quantities change only through later stock operations, each of which records a movement.
        var group = app.MapGroup("/api/receipt-batches").WithTags("Receipts");

        group.MapPost("/", async (ReceiveStockRequest request, InventoryDbContext db, CancellationToken ct) =>
        {
            var quantity = request.Quantity!.Value;
            if (decimal.Round(quantity, 3) != quantity)
            {
                return Problem("Quantity can have at most 3 decimal places.");
            }

            var now = DateTimeOffset.UtcNow;
            var receivedAt = request.ReceivedAt ?? now;
            if (receivedAt > now.AddMinutes(5))
            {
                return Problem("ReceivedAt cannot be in the future.");
            }

            var product = await db.Products.AsNoTracking().FirstOrDefaultAsync(p => p.Id == request.ProductId, ct);
            if (product is null)
            {
                return Problem($"Product {request.ProductId} does not exist.");
            }

            var warehouse = await db.Warehouses.AsNoTracking().FirstOrDefaultAsync(w => w.Id == request.WarehouseId, ct);
            if (warehouse is null)
            {
                return Problem($"Warehouse {request.WarehouseId} does not exist.");
            }

            // Inactive records stay in history but cannot receive new stock.
            if (!product.IsActive)
            {
                return Results.Conflict(new { error = $"Product '{product.Sku}' is inactive and cannot receive stock." });
            }

            if (!warehouse.IsActive)
            {
                return Results.Conflict(new { error = $"Warehouse '{warehouse.Code}' is inactive and cannot receive stock." });
            }

            var reference = string.IsNullOrWhiteSpace(request.Reference) ? null : request.Reference.Trim();
            var batch = new ReceiptBatch
            {
                ProductId = product.Id,
                WarehouseId = warehouse.Id,
                ReceivedAt = receivedAt,
                QuantityReceived = quantity,
                QuantityOnHand = quantity,
                Reference = reference,
                CreatedAt = now,
            };
            db.ReceiptBatches.Add(batch);

            // The batch and its movement are saved by one SaveChanges call, which is a single transaction:
            // either both are stored or neither is.
            db.StockMovements.Add(new StockMovement
            {
                Type = MovementType.Receive,
                ProductId = product.Id,
                WarehouseId = warehouse.Id,
                ReceiptBatch = batch,
                QuantityChange = quantity,
                OccurredAt = receivedAt,
                Reference = reference,
                CreatedAt = now,
            });
            await db.SaveChangesAsync(ct);

            return Results.Created($"/api/receipt-batches/{batch.Id}", ReceiptBatchResponse.From(batch));
        }).WithName("ReceiveStock");

        // Optional filters: ?productId=, ?warehouseId=. Newest receipts first.
        group.MapGet("/", async (int? productId, int? warehouseId, InventoryDbContext db, CancellationToken ct) =>
        {
            var query = db.ReceiptBatches.AsNoTracking();
            if (productId is not null)
            {
                query = query.Where(b => b.ProductId == productId);
            }

            if (warehouseId is not null)
            {
                query = query.Where(b => b.WarehouseId == warehouseId);
            }

            // Ordered in memory to keep the query simple.
            var batches = await query.ToListAsync(ct);
            return Results.Ok(batches
                .OrderByDescending(b => b.ReceivedAt)
                .ThenByDescending(b => b.Id)
                .Select(ReceiptBatchResponse.From));
        }).WithName("ListReceiptBatches");

        group.MapGet("/{id:int}", async (int id, InventoryDbContext db, CancellationToken ct) =>
            await db.ReceiptBatches.AsNoTracking().FirstOrDefaultAsync(b => b.Id == id, ct) is { } batch
                ? Results.Ok(ReceiptBatchResponse.From(batch))
                : Results.NotFound()).WithName("GetReceiptBatch");
    }

    private static IResult Problem(string message) => Results.BadRequest(new { error = message });
}
