using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Notes.Api.Data;

public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
        optionsBuilder.UseMySql(
            "Server=localhost;Port=3306;Database=notes_db;User=root;Password=123456;CharSet=utf8mb4;",
            new MySqlServerVersion(new Version(8, 0, 30)));

        return new AppDbContext(optionsBuilder.Options);
    }
}
