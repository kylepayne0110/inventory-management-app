using System.Text.Json.Serialization;
using Inventory.Api.Models;

namespace Inventory.Api.Contracts;

// One line of the movement history. TransferId pairs the two lines of a transfer.
public record StockMovementResponse(
    int Id,
    [property: JsonConverter(typeof(JsonStringEnumConverter))] MovementType Type,
    int ProductId,
    string ProductSku,
    int WarehouseId,
    string WarehouseCode,
    int ReceiptBatchId,
    decimal QuantityChange,
    DateTimeOffset OccurredAt,
    string? Reference,
    string? Reason,
    Guid? TransferId,
    DateTimeOffset CreatedAt);

// A page of movement history. Total is the number of movements matching the filters, ignoring Skip and Take.
public record StockMovementPage(int Total, int Skip, int Take, IReadOnlyList<StockMovementResponse> Items);

// Stock on hand for one product in one warehouse, summed over its batches.
public record StockLevelResponse(
    int ProductId,
    string ProductSku,
    string ProductName,
    int WarehouseId,
    string WarehouseCode,
    decimal QuantityOnHand,
    int BatchCount);

// A product at or below its own reorder threshold (Threshold). WarehouseId is set only when the alert was limited to one warehouse.
public record LowStockAlertResponse(
    int ProductId,
    string ProductSku,
    string ProductName,
    int? WarehouseId,
    decimal QuantityOnHand,
    decimal Threshold);

// A batch that still has stock on hand and was received at least the requested number of days ago.
public record AgingStockAlertResponse(
    int ReceiptBatchId,
    int ProductId,
    string ProductSku,
    int WarehouseId,
    string WarehouseCode,
    DateTimeOffset ReceivedAt,
    int AgeDays,
    decimal QuantityOnHand,
    string? Reference);
