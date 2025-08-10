using Microsoft.EntityFrameworkCore;
using DotnetMVCApp.Models;

namespace DotnetMVCApp.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        // Example DbSet
        public DbSet<Product> Products { get; set; }
    }
}
