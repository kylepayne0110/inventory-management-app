using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Inventory.Api.Data;

public static class DbErrors
{
    // SQL Server error 2601 = duplicate key in a unique index; 2627 = unique constraint violation.
    public static bool IsUniqueViolation(DbUpdateException ex) =>
        ex.InnerException is SqlException { Number: 2601 or 2627 };
}
