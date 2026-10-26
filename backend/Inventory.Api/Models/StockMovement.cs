namespace Inventory.Api.Models;

public enum MovementType
{
    Receive,
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

    public DateTimeOffset CreatedAt { get; set; }
}
