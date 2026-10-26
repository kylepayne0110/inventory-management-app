using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Inventory.Api.Models;

namespace Inventory.Api.Contracts;

// Issues stock from one chosen batch. OccurredAt is optional, defaults to now, and cannot be in the future.
public record IssueStockRequest(
    [Required, Range(1, int.MaxValue)] int? ReceiptBatchId,
    [Required, Range(0.001, 1000000000, ErrorMessage = "Quantity must be greater than zero.")] decimal? Quantity,
    DateTimeOffset? OccurredAt,
    [StringLength(100)] string? Reference);

// QuantityChange is signed: positive adds stock to the batch, negative removes it. It cannot be zero.
// A reason is required so every correction can be explained later.
public record AdjustStockRequest(
    [Required, Range(1, int.MaxValue)] int? ReceiptBatchId,
    [Required, Range(-1000000000, 1000000000)] decimal? QuantityChange,
    [Required, StringLength(200)] string? Reason,
    DateTimeOffset? OccurredAt,
    [StringLength(100)] string? Reference);

public record StockOperationResponse(
    int MovementId,
    [property: JsonConverter(typeof(JsonStringEnumConverter))] MovementType Type,
    int ProductId,
    int WarehouseId,
    int ReceiptBatchId,
    decimal QuantityChange,
    decimal QuantityOnHandAfter,
    DateTimeOffset OccurredAt,
    string? Reference,
    string? Reason,
    DateTimeOffset CreatedAt)
{
    public static StockOperationResponse From(StockMovement m, ReceiptBatch b) =>
        new(m.Id, m.Type, m.ProductId, m.WarehouseId, m.ReceiptBatchId, m.QuantityChange, b.QuantityOnHand, m.OccurredAt, m.Reference, m.Reason, m.CreatedAt);
}
