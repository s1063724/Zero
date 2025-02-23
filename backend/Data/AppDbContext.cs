// Data/AppDbContext.cs
using Microsoft.EntityFrameworkCore;
using UserManagementAPI.Models;

namespace UserManagementAPI.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
            // 確保數據庫存在並與模型同步
            Database.EnsureCreated();
        }

        // 定義 Users 表
        public DbSet<User> Users { get; set; }
    }
}