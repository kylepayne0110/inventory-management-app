namespace Inventory.Api.Models;

// A quantity of one product received into one warehouse on one date. Stock is tracked per batch so its age is known.
// Batches are never deleted. Later sections change QuantityOnHand; QuantityReceived and ReceivedAt never change.
public class ReceiptBatch
{
    public int Id { get; set; }

    public int ProductId { get; set; }

    public Product? Product { get; set; }

    public int WarehouseId { get; set; }

    public Warehouse? Warehouse { get; set; }

    // When the stock arrived. Used for stock age, and preserved when stock is transferred later.
    public DateTimeOffset ReceivedAt { get; set; }

    public decimal QuantityReceived { get; set; }

    public decimal QuantityOnHand { get; set; }

    // Optional outside reference, such as a purchase order or delivery note number.
    public string? Reference { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
