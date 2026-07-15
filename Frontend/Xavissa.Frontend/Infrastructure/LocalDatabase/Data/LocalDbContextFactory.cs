using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Xavissa.Frontend.Data
{
    public class LocalDbContextFactory : IDesignTimeDbContextFactory<LocalDbContext>
    {
        public LocalDbContext CreateDbContext(string[] args)
        {
            var optionsBuilder = new DbContextOptionsBuilder<LocalDbContext>();
            optionsBuilder.UseSqlite(LocalDbContext.BuildConnectionString(LocalDbContext.GetLocalDbPath()));

            return new LocalDbContext(optionsBuilder.Options);
        }
    }
}
