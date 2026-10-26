namespace Inventory.Api.Models;

// Something the business stocks. Records are never deleted; IsActive = false retires one while keeping its history.
public class Product
{
    public int Id { get; set; }

    // Unique, stored upper-case.
    public string Sku { get; set; } = "";

    public string Name { get; set; } = "";

    public string? Description { get; set; }

    public string UnitOfMeasure { get; set; } = "each";

    // The low-stock alert fires when total on hand is at or below this. Never negative. 0 (the default) alerts only when the product is out of stock.
    public decimal ReorderThreshold { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
