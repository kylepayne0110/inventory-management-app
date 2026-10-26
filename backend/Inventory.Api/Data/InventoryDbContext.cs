using Microsoft.EntityFrameworkCore;

namespace Inventory.Api.Data;

// The API's gateway to SQL Server. Entity sets (tables) are added in Section 3.
public class InventoryDbContext(DbContextOptions<InventoryDbContext> options) : DbContext(options);
