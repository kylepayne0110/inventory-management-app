namespace Inventory.Api.Models;

public enum MovementType
{
    Receive,
    Issue,
    Adjust,
    // The two halves of a transfer: stock leaves the source batch and arrives in a new batch in the destination warehouse.
    TransferOut,
    TransferIn,
}

// An append-only record of one change to stock. Movements are never edited or deleted.
public class StockMovement
{
    public int Id { get; set; }

    public MovementType Type { get; set; }

    public int ProductId { get; set; }

    public Product? Product { get; set; }

    public int WarehouseId { get; set; }

    public Warehouse? Warehouse { get; set; }

    public int ReceiptBatchId { get; set; }

    public ReceiptBatch? ReceiptBatch { get; set; }

    // Positive when stock is added, negative when it is removed.
    public decimal QuantityChange { get; set; }

    // When the change happened (for a receipt, the same as the batch's ReceivedAt).
    public DateTimeOffset OccurredAt { get; set; }

    public string? Reference { get; set; }

    // Why the movement happened. Required for adjustments (for example "cycle count" or "damaged").
    public string? Reason { get; set; }

    // Shared by the TransferOut and TransferIn movements of one transfer; null for every other movement type.
    public Guid? TransferId { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
