using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ClothingStore.Data;

/// <summary>Used by <c>dotnet ef migrations add</c>.</summary>
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<PosDbContext>
{
    public PosDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<PosDbContext>().UseSqlite("Data Source=design.db").Options);
}
