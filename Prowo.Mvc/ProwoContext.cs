using Microsoft.EntityFrameworkCore;
using Prowo.Mvc.DB;

namespace Prowo.Mvc
{
    public class ProwoContext : DbContext
    {
        public ProwoContext(DbContextOptions options) : base(options)
        {
        }
        public DbSet<Project> Projects { get; set; }
    }
}
