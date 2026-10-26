using Inventory.Api.Contracts;
using Inventory.Api.Data;
using Inventory.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Inventory.Api.Endpoints;

public static class TransferEndpoints
{
    public static void MapTransferEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/stock").WithTags("Stock operations");

        // The caller names the source batch (no FIFO). The stock arrives in a NEW batch in the destination warehouse
        // that keeps the source batch's ReceivedAt, so the stock does not look newer after it is moved.
        group.MapPost("/transfer", async (TransferStockRequest request, InventoryDbContext db, CancellationToken ct) =>
        {
            var quantity = request.Quantity!.Value;
            if (decimal.Round(quantity, 3) != quantity)
            {
                return Problem("Quantity can have at most 3 decimal places.");
            }

            var now = DateTimeOffset.UtcNow;
            var occurredAt = request.OccurredAt ?? now;
            if (occurredAt > now.AddMinutes(5))
            {
                return Problem("OccurredAt cannot be in the future.");
            }

            var source = await db.ReceiptBatches.Include(b => b.Product).FirstOrDefaultAsync(b => b.Id == request.SourceReceiptBatchId, ct);
            if (source is null)
            {
                return Problem($"Receipt batch {request.SourceReceiptBatchId} does not exist.");
            }

            var destination = await db.Warehouses.AsNoTracking().FirstOrDefaultAsync(w => w.Id == request.DestinationWarehouseId, ct);
            if (destination is null)
            {
                return Problem($"Warehouse {request.DestinationWarehouseId} does not exist.");
            }

            if (destination.Id == source.WarehouseId)
            {
                return Problem("The destination warehouse must differ from the batch's current warehouse.");
            }

            if (occurredAt < source.ReceivedAt)
            {
                return Problem("OccurredAt cannot be before the batch was received.");
            }

            // Like receiving, a transfer puts stock into the destination, so inactive records are refused there.
            // The source warehouse may be inactive: stock that is already there must always be movable.
            if (!destination.IsActive)
            {
                return Results.Conflict(new { error = $"Warehouse '{destination.Code}' is inactive and cannot receive stock." });
            }

            if (!source.Product!.IsActive)
            {
                return Results.Conflict(new { error = $"Product '{source.Product.Sku}' is inactive and cannot be transferred." });
            }

            if (quantity > source.QuantityOnHand)
            {
                return Results.Conflict(new { error = $"Batch {source.Id} has only {source.QuantityOnHand} on hand; cannot transfer {quantity}." });
            }

            var reference = string.IsNullOrWhiteSpace(request.Reference) ? null : request.Reference.Trim();
            source.QuantityOnHand -= quantity;

            var target = new ReceiptBatch
            {
                ProductId = source.ProductId,
                WarehouseId = destination.Id,
                ReceivedAt = source.ReceivedAt,
                QuantityReceived = quantity,
                QuantityOnHand = quantity,
                Reference = source.Reference,
                CreatedAt = now,
            };
            var transferId = Guid.NewGuid();
            var movementOut = NewMovement(MovementType.TransferOut, source, -quantity, occurredAt, now, reference, transferId);
            var movementIn = NewMovement(MovementType.TransferIn, target, quantity, occurredAt, now, reference, transferId);
            movementIn.ReceiptBatch = target;
            db.ReceiptBatches.Add(target);
            db.StockMovements.AddRange(movementOut, movementIn);

            // One SaveChanges is one transaction: both movements, the new batch and the source change are stored, or none are.
            try
            {
                await db.SaveChangesAsync(ct);
            }
            catch (DbUpdateConcurrencyException)
            {
                return Results.Conflict(new { error = $"Batch {source.Id} was changed by someone else. Reload it and try again." });
            }

            return Results.Ok(new TransferStockResponse(
                transferId,
                StockOperationResponse.From(movementOut, source),
                StockOperationResponse.From(movementIn, target),
                ReceiptBatchResponse.From(target)));
        }).WithName("TransferStock");
    }

    private static StockMovement NewMovement(MovementType type, ReceiptBatch batch, decimal change, DateTimeOffset occurredAt, DateTimeOffset now, string? reference, Guid transferId) => new()
    {
        Type = type,
        ProductId = batch.ProductId,
        WarehouseId = batch.WarehouseId,
        ReceiptBatch = batch,
        QuantityChange = change,
        OccurredAt = occurredAt,
        Reference = reference,
        TransferId = transferId,
        CreatedAt = now,
    };

    private static IResult Problem(string message) => Results.BadRequest(new { error = message });
}
