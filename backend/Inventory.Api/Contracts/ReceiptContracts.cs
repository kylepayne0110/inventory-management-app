using System.ComponentModel.DataAnnotations;
using Inventory.Api.Models;

namespace Inventory.Api.Contracts;

// ReceivedAt is optional and defaults to now. It cannot be in the future.
public record ReceiveStockRequest(
    [Required, Range(1, int.MaxValue)] int? ProductId,
    [Required, Range(1, int.MaxValue)] int? WarehouseId,
    [Required, Range(0.001, 1000000000, ErrorMessage = "Quantity must be greater than zero.")] decimal? Quantity,
    DateTimeOffset? ReceivedAt,
    [StringLength(100)] string? Reference);

public record ReceiptBatchResponse(
    int Id,
    int ProductId,
    int WarehouseId,
    DateTimeOffset ReceivedAt,
    decimal QuantityReceived,
    decimal QuantityOnHand,
    string? Reference,
    DateTimeOffset CreatedAt)
{
    public static ReceiptBatchResponse From(ReceiptBatch b) =>
        new(b.Id, b.ProductId, b.WarehouseId, b.ReceivedAt, b.QuantityReceived, b.QuantityOnHand, b.Reference, b.CreatedAt);
}
