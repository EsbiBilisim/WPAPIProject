using Microsoft.EntityFrameworkCore;

namespace WPAPIProject.Models
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {

        }

        public DbSet<W_USERS> W_USERS { get; set; }
        public DbSet<W_FIRMS> W_FIRMS { get; set; }
        public DbSet<W_CUSTOMERS> W_CUSTOMERS { get; set; }
        public DbSet<W_MESSAGES> W_MESSAGES { get; set; }
    }
}