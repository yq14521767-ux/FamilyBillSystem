using FamilyBillSystem.Models;
using Microsoft.EntityFrameworkCore;

namespace FamilyBillSystem.Data
{
    public static class SeedData
    {
        public static async Task Initialize(AppDbContext context)
        {
            // 确保数据库已创建
            await context.Database.EnsureCreatedAsync();

            // 检查是否已有分类数据
            if (await context.Categories.AnyAsync())
            {
                return; // 数据已存在
            }

            // 创建系统默认分类
            var categories = new List<Category>
            {
                // 支出分类
                new Category { Name = "餐饮", Type = "expense", Icon = "food", Color = "#FF6B6B", SortOrder = 1, CreatedAt = DateTime.Now, UpdatedAt = DateTime.Now },
                new Category { Name = "交通", Type = "expense", Icon = "transport", Color = "#4ECDC4", SortOrder = 2, CreatedAt = DateTime.Now, UpdatedAt = DateTime.Now },
                new Category { Name = "购物", Type = "expense", Icon = "shopping", Color = "#45B7D1", SortOrder = 3, CreatedAt = DateTime.Now, UpdatedAt = DateTime.Now },
                new Category { Name = "娱乐", Type = "expense", Icon = "entertainment", Color = "#96CEB4", SortOrder = 4, CreatedAt = DateTime.Now, UpdatedAt = DateTime.Now },
                new Category { Name = "医疗", Type = "expense", Icon = "medical", Color = "#FFEAA7", SortOrder = 5, CreatedAt = DateTime.Now, UpdatedAt = DateTime.Now },
                new Category { Name = "教育", Type = "expense", Icon = "education", Color = "#DDA0DD", SortOrder = 6, CreatedAt = DateTime.Now, UpdatedAt = DateTime.Now },
                new Category { Name = "住房", Type = "expense", Icon = "housing", Color = "#98D8C8", SortOrder = 7, CreatedAt = DateTime.Now, UpdatedAt = DateTime.Now },
                new Category { Name = "生活费", Type = "expense", Icon = "daily", Color = "#F7DC6F", SortOrder = 8, CreatedAt = DateTime.Now, UpdatedAt = DateTime.Now },
                new Category { Name = "通讯", Type = "expense", Icon = "communication", Color = "#BB8FCE", SortOrder = 9, CreatedAt = DateTime.Now, UpdatedAt = DateTime.Now },
                new Category { Name = "其他支出", Type = "expense", Icon = "other", Color = "#85C1E9", SortOrder = 10, CreatedAt = DateTime.Now, UpdatedAt = DateTime.Now },

                // 收入分类
                new Category { Name = "工资", Type = "income", Icon = "salary", Color = "#58D68D", SortOrder = 1, CreatedAt = DateTime.Now, UpdatedAt = DateTime.Now },
                new Category { Name = "奖金", Type = "income", Icon = "bonus", Color = "#F8C471", SortOrder = 2, CreatedAt = DateTime.Now, UpdatedAt = DateTime.Now },
                new Category { Name = "投资收益", Type = "income", Icon = "investment", Color = "#82E0AA", SortOrder = 3, CreatedAt = DateTime.Now, UpdatedAt = DateTime.Now },
                new Category { Name = "兼职", Type = "income", Icon = "parttime", Color = "#AED6F1", SortOrder = 4, CreatedAt = DateTime.Now, UpdatedAt = DateTime.Now },
                new Category { Name = "礼金", Type = "income", Icon = "gift", Color = "#F1948A", SortOrder = 5, CreatedAt = DateTime.Now, UpdatedAt = DateTime.Now },
                new Category { Name = "其他收入", Type = "income", Icon = "other", Color = "#D7BDE2", SortOrder = 6, CreatedAt = DateTime.Now, UpdatedAt = DateTime.Now }
            };

            context.Categories.AddRange(categories);
            await context.SaveChangesAsync();
        }
    }
}