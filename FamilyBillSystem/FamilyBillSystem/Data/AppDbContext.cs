using FamilyBillSystem.Models;
using Microsoft.EntityFrameworkCore;
using FamilyBillSystem.Services;

namespace FamilyBillSystem.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<User> Users { get; set; }
        public DbSet<Family> Families { get; set; }
        public DbSet<FamilyMember> FamilyMembers { get; set; }
        public DbSet<Category> Categories { get; set; }
        public DbSet<Bill> Bills { get; set; }
        public DbSet<Budget> Budgets { get; set; }
        public DbSet<Notification> Notifications { get; set; }
        public DbSet<FamilyStats> FamilyStats { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // 配置软删除全局过滤器
            foreach (var entityType in modelBuilder.Model.GetEntityTypes())
            {
                if (typeof(ISoftDeletable).IsAssignableFrom(entityType.ClrType))
                {
                    modelBuilder.Entity(entityType.ClrType)
                        .HasQueryFilter(GetSoftDeleteFilter(entityType.ClrType));
                }
            }

            // User 配置
            modelBuilder.Entity<User>(entity =>
            {
                entity.HasIndex(u => u.OpenId).IsUnique();
                entity.HasIndex(u => u.Phone);
                entity.HasIndex(u => u.Email).IsUnique();
                entity.HasIndex(u => u.Status);
            });

            // Family 配置
            modelBuilder.Entity<Family>(entity =>
            {
                entity.HasIndex(f => f.CreatorId);
                entity.HasIndex(f => f.InviteCode).IsUnique();
                entity.HasIndex(f => f.Status);
                entity.HasOne(f => f.Creator)
                    .WithMany(u => u.CreatedFamilies)
                    .HasForeignKey(f => f.CreatorId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // FamilyMember 配置
            modelBuilder.Entity<FamilyMember>(entity =>
            {
                entity.HasIndex(fm => new { fm.FamilyId, fm.UserId }).IsUnique();
                entity.HasIndex(fm => fm.FamilyId);
                entity.HasIndex(fm => fm.UserId);
            });

            // Category 配置
            modelBuilder.Entity<Category>(entity =>
            {
                entity.HasIndex(c => c.Type);
                entity.HasIndex(c => c.ParentId);
                entity.HasIndex(c => c.FamilyId);
                entity.HasOne(c => c.Parent)
                    .WithMany(c => c.Children)
                    .HasForeignKey(c => c.ParentId)
                    .OnDelete(DeleteBehavior.SetNull);
            });

            // Bill 配置
            modelBuilder.Entity<Bill>(entity =>
            {
                entity.HasIndex(b => b.FamilyId);
                entity.HasIndex(b => b.UserId);
                entity.HasIndex(b => b.CategoryId);
                entity.HasIndex(b => b.BillDate);
                entity.HasIndex(b => b.Type);
                entity.HasIndex(b => b.Status);
                entity.HasIndex(b => new { b.FamilyId, b.BillDate });
                entity.HasIndex(b => new { b.UserId, b.Type });
                entity.Property(b => b.Amount).HasColumnType("decimal(10,2)");
                
                entity.HasOne(b => b.Category)
                    .WithMany(c => c.Bills)
                    .HasForeignKey(b => b.CategoryId)
                    .OnDelete(DeleteBehavior.SetNull);
                    

            });

            // Notification 配置
            modelBuilder.Entity<Notification>(entity =>
            {
                entity.HasIndex(n => n.Status);
                entity.HasIndex(n => n.CreatedAt);
            });

            // FamilyStats 配置
            modelBuilder.Entity<FamilyStats>(entity =>
            {
                entity.HasIndex(fs => fs.FamilyId).IsUnique();
                entity.Property(fs => fs.TotalIncome).HasColumnType("decimal(12,2)");
                entity.Property(fs => fs.TotalExpense).HasColumnType("decimal(12,2)");
                entity.HasOne(fs => fs.Family)
                    .WithOne(f => f.Stats)
                    .HasForeignKey<FamilyStats>(fs => fs.FamilyId)
                    .OnDelete(DeleteBehavior.Cascade);
            });
        }

        private static System.Linq.Expressions.LambdaExpression GetSoftDeleteFilter(Type entityType)
        {
            var parameter = System.Linq.Expressions.Expression.Parameter(entityType, "e");
            var property = System.Linq.Expressions.Expression.Property(parameter, "DeletedAt");
            var nullConstant = System.Linq.Expressions.Expression.Constant(null, typeof(DateTime?));
            var condition = System.Linq.Expressions.Expression.Equal(property, nullConstant);
            return System.Linq.Expressions.Expression.Lambda(condition, parameter);
        }
    }
}
