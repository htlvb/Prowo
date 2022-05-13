using Microsoft.EntityFrameworkCore;
using Prowo.Web.Models;

namespace Prowo.Web
{
    public class ProwoContext : DbContext
    {
        public ProwoContext(DbContextOptions options) : base(options)
        {
        }
        public DbSet<Project> Projects { get; set; }
    }
}
