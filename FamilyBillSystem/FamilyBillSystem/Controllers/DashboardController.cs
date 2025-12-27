using FamilyBillSystem.Data;
using FamilyBillSystem.DTOs;
using FamilyBillSystem.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace FamilyBillSystem.Controllers
{
    [Route("api/dashboard")]
    [ApiController]
    [Authorize]
    public class DashboardController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly ILogger<DashboardController> _logger;

        public DashboardController(AppDbContext context, ILogger<DashboardController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // GET: api/dashboard/overview
        [HttpGet("overview")]
        public async Task<IActionResult> GetDashboardOverview()
        {
            try
            {
                var userId = GetCurrentUserId();
                if (userId == null)
                    return Unauthorized();

                var now = DateTime.Now;
                var year = now.Year;
                var month = now.Month;

                // 获取用户所属的家庭
                var userFamilyIds = await _context.FamilyMembers
                    .Where(fm => fm.UserId == userId && fm.Status == "active")
                    .Select(fm => fm.FamilyId)
                    .ToListAsync();

                if (!userFamilyIds.Any())
                {
                    return Ok(ServiceResponse.CreateSuccess(new
                    {
                        monthlyStats = new
                        {
                            totalIncome = 0m,
                            totalExpense = 0m,
                            balance = 0m
                        },
                        recentBills = new List<object>(),
                        budgetAlerts = new List<object>()
                    }));
                }

                // 获取月度统计
                var monthlyStats = await GetMonthlyStatistics(userFamilyIds, year, month);

                // 获取最近账单
                var recentBills = await GetRecentBills(userFamilyIds, 5);

                // 获取预算提醒
                var budgetAlerts = await GetBudgetAlerts(userFamilyIds, year, month);

                return Ok(ServiceResponse.CreateSuccess(new
                {
                    monthlyStats,
                    recentBills,
                    budgetAlerts
                }));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取首页概览数据失败");
                return StatusCode(500, ServiceResponse.Error("获取首页数据失败"));
            }
        }

        private async Task<object> GetMonthlyStatistics(List<int> userFamilyIds, int year, int month)
        {
            var startDate = new DateTime(year, month, 1);
            var endDate = startDate.AddMonths(1).AddDays(-1);

            var bills = await _context.Bills
                .Where(b => userFamilyIds.Contains(b.FamilyId) &&
                           b.BillDate >= startDate &&
                           b.BillDate <= endDate &&
                           b.DeletedAt == null &&
                           b.Status == "confirmed")
                .ToListAsync();

            var totalIncome = bills.Where(b => b.Type == "income").Sum(b => b.Amount);
            var totalExpense = bills.Where(b => b.Type == "expense").Sum(b => b.Amount);
            var balance = totalIncome - totalExpense;

            return new
            {
                totalIncome,
                totalExpense,
                balance,
                billCount = bills.Count
            };
        }

        private async Task<List<object>> GetRecentBills(List<int> userFamilyIds, int count)
        {
            var query = from b in _context.Bills
                        join fm in _context.FamilyMembers
                            on new { b.FamilyId, b.UserId } equals new { fm.FamilyId, fm.UserId } into fmGroup
                        from fm in fmGroup.Where(m => m.Status == "active").DefaultIfEmpty()
                        where userFamilyIds.Contains(b.FamilyId) &&
                              b.DeletedAt == null
                        orderby b.BillDate descending, b.CreatedAt descending
                        select new
                        {
                            id = b.Id,
                            amount = b.Amount,
                            description = b.Description,
                            billDate = b.BillDate,
                            familyId = b.FamilyId,
                            familyName = b.Family != null ? b.Family.Name : null,
                            categoryId = b.CategoryId,
                            categoryName = b.Category != null ? b.Category.Name : "未分类",
                            categoryIcon = b.Category != null ? b.Category.Icon : "default",
                            categoryColor = b.Category != null ? b.Category.Color : "#666666",
                            categoryType = b.Type == "income" ? 1 : 2,
                            memberNickName = fm != null ? fm.Nickname : null,
                            userNickName = b.User.Nickname,
                            username = b.User.Email
                        };

            var bills = await query
                .Take(count)
                .ToListAsync();

            return bills.Cast<object>().ToList();
        }

        private async Task<List<object>> GetBudgetAlerts(List<int> userFamilyIds, int year, int month)
        {
            var startDate = new DateTime(year, month, 1);
            var endDate = startDate.AddMonths(1).AddDays(-1);

            // 获取当月的预算记录（按家庭 + 分类）
            var budgets = await _context.Budgets
                .Include(b => b.Category)
                .Where(b => userFamilyIds.Contains(b.FamilyId) &&
                            b.Year == year &&
                            b.Month == month &&
                            b.DeletedAt == null &&
                            b.IsActive)
                .ToListAsync();

            if (!budgets.Any())
            {
                return new List<object>();
            }

            // 计算对应分类在各家庭下的实际支出
            var monthlyExpenses = await _context.Bills
                .Where(b => userFamilyIds.Contains(b.FamilyId) &&
                           b.Type == "expense" &&
                           b.BillDate >= startDate &&
                           b.BillDate <= endDate &&
                           b.DeletedAt == null &&
                           b.Status == "confirmed")
                .GroupBy(b => new { b.FamilyId, b.CategoryId })
                .Select(g => new
                {
                    g.Key.FamilyId,
                    g.Key.CategoryId,
                    Amount = g.Sum(b => b.Amount)
                })
                .ToListAsync();

            var alerts = new List<object>();

            foreach (var budget in budgets)
            {
                var spent = monthlyExpenses
                    .FirstOrDefault(e => e.FamilyId == budget.FamilyId && e.CategoryId == budget.CategoryId)?.Amount ?? 0m;

                if (budget.Amount <= 0)
                {
                    continue;
                }

                var utilizationRate = (spent / budget.Amount) * 100;

                // 使用率超过80%的预算生成提醒
                if (utilizationRate >= 80)
                {
                    var isOverBudget = spent > budget.Amount;
                    var category = budget.Category;
                    var categoryName = category?.Name ?? "未分类";

                    alerts.Add(new
                    {
                        id = budget.Id,
                        categoryName,
                        categoryIcon = category?.Icon,
                        categoryColor = category?.Color ?? "#666666",
                        amount = budget.Amount,
                        usedAmount = spent,
                        usagePercentage = Math.Round(utilizationRate, 1),
                        isOverBudget,
                        message = isOverBudget
                            ? $"{categoryName}已超出预算"
                            : $"{categoryName}接近预算上限"
                    });
                }
            }

            return alerts;
        }

        private decimal GetDefaultBudgetForCategory(string categoryName)
        {
            // 模拟预算数据，实际项目中应该从预算表获取
            return categoryName switch
            {
                "餐饮" => 1000m,
                "交通" => 500m,
                "购物" => 800m,
                "娱乐" => 300m,
                "医疗" => 200m,
                "教育" => 600m,
                "住房" => 2000m,
                "通讯" => 100m,
                _ => 500m
            };
        }

        private int? GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim != null && int.TryParse(userIdClaim.Value, out int userId))
            {
                return userId;
            }
            return null;
        }
    }
}