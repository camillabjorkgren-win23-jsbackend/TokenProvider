using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore;

namespace TokenProvider.Infrastructure.Data.Contexts;
public class DataContextFactory : IDesignTimeDbContextFactory<DataContext>
{
    public DataContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<DataContext>();
        optionsBuilder.UseSqlServer("Server=tcp:camillabjorkgren-silicon-sqlserver.database.windows.net,1433;Initial Catalog=silicon_database;Persist Security Info=False;User ID=SqlAdmin;Password=BytMig123!;MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;");

        return new DataContext(optionsBuilder.Options);
    }
}
