using Inventory.Api.Contracts;
using Inventory.Api.Data;
using Inventory.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Inventory.Api.Endpoints;

public static class StockOperationEndpoints
{
    public static void MapStockOperationEndpoints(this IEndpointRouteBuilder app)
    {
        // The caller names the batch to take stock from; the API never picks one (no FIFO).
        // Unlike receiving, issuing and adjusting are allowed for inactive products and warehouses,
        // so stock that is still on hand can always be removed or corrected.
        var group = app.MapGroup("/api/stock").WithTags("Stock operations");

        group.MapPost("/issue", async (IssueStockRequest request, InventoryDbContext db, CancellationToken ct) =>
        {
            var quantity = request.Quantity!.Value;
            if (decimal.Round(quantity, 3) != quantity)
            {
                return Problem("Quantity can have at most 3 decimal places.");
            }

            if (!TryGetOccurredAt(request.OccurredAt, out var now, out var occurredAt))
            {
                return Problem("OccurredAt cannot be in the future.");
            }

            var batch = await db.ReceiptBatches.FirstOrDefaultAsync(b => b.Id == request.ReceiptBatchId, ct);
            if (batch is null)
            {
                return Problem($"Receipt batch {request.ReceiptBatchId} does not exist.");
            }

            if (occurredAt < batch.ReceivedAt)
            {
                return Problem("OccurredAt cannot be before the batch was received.");
            }

            if (quantity > batch.QuantityOnHand)
            {
                return Results.Conflict(new { error = $"Batch {batch.Id} has only {batch.QuantityOnHand} on hand; cannot issue {quantity}." });
            }

            batch.QuantityOnHand -= quantity;
            var movement = NewMovement(MovementType.Issue, batch, -quantity, occurredAt, now, request.Reference, null);
            return await SaveAsync(db, batch, movement, ct);
        }).WithName("IssueStock");

        group.MapPost("/adjust", async (AdjustStockRequest request, InventoryDbContext db, CancellationToken ct) =>
        {
            var change = request.QuantityChange!.Value;
            if (change == 0 || decimal.Round(change, 3) != change)
            {
                return Problem("QuantityChange must not be zero and can have at most 3 decimal places.");
            }

            var reason = request.Reason!.Trim();
            if (reason.Length == 0)
            {
                return Problem("Reason is required.");
            }

            if (!TryGetOccurredAt(request.OccurredAt, out var now, out var occurredAt))
            {
                return Problem("OccurredAt cannot be in the future.");
            }

            var batch = await db.ReceiptBatches.FirstOrDefaultAsync(b => b.Id == request.ReceiptBatchId, ct);
            if (batch is null)
            {
                return Problem($"Receipt batch {request.ReceiptBatchId} does not exist.");
            }

            if (occurredAt < batch.ReceivedAt)
            {
                return Problem("OccurredAt cannot be before the batch was received.");
            }

            // A correction cannot take the batch below zero, or above what was originally received.
            var after = batch.QuantityOnHand + change;
            if (after < 0)
            {
                return Results.Conflict(new { error = $"Batch {batch.Id} has only {batch.QuantityOnHand} on hand; cannot adjust by {change}." });
            }

            if (after > batch.QuantityReceived)
            {
                return Results.Conflict(new { error = $"Batch {batch.Id} received {batch.QuantityReceived}; on hand cannot be adjusted above that. Receive the extra stock as a new batch." });
            }

            batch.QuantityOnHand = after;
            var movement = NewMovement(MovementType.Adjust, batch, change, occurredAt, now, request.Reference, reason);
            return await SaveAsync(db, batch, movement, ct);
        }).WithName("AdjustStock");
    }

    private static bool TryGetOccurredAt(DateTimeOffset? requested, out DateTimeOffset now, out DateTimeOffset occurredAt)
    {
        now = DateTimeOffset.UtcNow;
        occurredAt = requested ?? now;
        return occurredAt <= now.AddMinutes(5);
    }

    private static StockMovement NewMovement(MovementType type, ReceiptBatch batch, decimal change, DateTimeOffset occurredAt, DateTimeOffset now, string? reference, string? reason) => new()
    {
        Type = type,
        ProductId = batch.ProductId,
        WarehouseId = batch.WarehouseId,
        ReceiptBatchId = batch.Id,
        QuantityChange = change,
        OccurredAt = occurredAt,
        Reference = string.IsNullOrWhiteSpace(reference) ? null : reference.Trim(),
        Reason = reason,
        CreatedAt = now,
    };

    // The batch change and its movement are saved together in one transaction. The batch's row version means that
    // if someone else changed the batch after it was read, nothing is saved and the caller is told to retry.
    private static async Task<IResult> SaveAsync(InventoryDbContext db, ReceiptBatch batch, StockMovement movement, CancellationToken ct)
    {
        db.StockMovements.Add(movement);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Results.Conflict(new { error = $"Batch {batch.Id} was changed by someone else. Reload it and try again." });
        }

        return Results.Ok(StockOperationResponse.From(movement, batch));
    }

    private static IResult Problem(string message) => Results.BadRequest(new { error = message });
}
