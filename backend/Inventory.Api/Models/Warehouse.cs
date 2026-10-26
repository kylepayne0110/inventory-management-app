namespace Inventory.Api.Models;

// A place where stock is held. Records are never deleted; IsActive = false retires one while keeping its history.
public class Warehouse
{
    public int Id { get; set; }

    // Unique, stored upper-case.
    public string Code { get; set; } = "";

    public string Name { get; set; } = "";

    public string? Location { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
