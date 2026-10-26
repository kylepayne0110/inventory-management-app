using System.ComponentModel.DataAnnotations;

namespace Inventory.Api.Contracts;

// Moves stock from one chosen batch into another warehouse. OccurredAt is optional, defaults to now, and cannot be in the future.
public record TransferStockRequest(
    [Required, Range(1, int.MaxValue)] int? SourceReceiptBatchId,
    [Required, Range(1, int.MaxValue)] int? DestinationWarehouseId,
    [Required, Range(0.001, 1000000000, ErrorMessage = "Quantity must be greater than zero.")] decimal? Quantity,
    DateTimeOffset? OccurredAt,
    [StringLength(100)] string? Reference);

// Both halves of the transfer: the TransferOut on the source batch and the TransferIn on the new destination batch.
public record TransferStockResponse(
    Guid TransferId,
    StockOperationResponse Out,
    StockOperationResponse In,
    ReceiptBatchResponse DestinationBatch);
