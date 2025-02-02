// Data/AppDbContext.cs
using Microsoft.EntityFrameworkCore;
using UserManagementAPI.Models;

namespace UserManagementAPI.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
            // 這會在每次啟動時確保數據庫和模型同步
            Database.EnsureCreated();
        }

        public DbSet<User> Users { get; set; }
    }
}