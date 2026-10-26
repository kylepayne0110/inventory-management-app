using System.ComponentModel.DataAnnotations;
using Inventory.Api.Models;

namespace Inventory.Api.Contracts;

// The code is only accepted on create; it cannot be changed afterwards.
public record CreateWarehouseRequest(
    [Required, StringLength(20), RegularExpression(@"^[A-Za-z0-9][A-Za-z0-9._-]*$", ErrorMessage = "Code may contain only letters, digits, '.', '_' and '-', and must start with a letter or digit.")]
    string? Code,
    [Required, StringLength(200)] string? Name,
    [StringLength(500)] string? Location);

public record UpdateWarehouseRequest(
    [Required, StringLength(200)] string? Name,
    [StringLength(500)] string? Location);

public record WarehouseResponse(
    int Id,
    string Code,
    string Name,
    string? Location,
    bool IsActive,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt)
{
    public static WarehouseResponse From(Warehouse w) =>
        new(w.Id, w.Code, w.Name, w.Location, w.IsActive, w.CreatedAt, w.UpdatedAt);
}
