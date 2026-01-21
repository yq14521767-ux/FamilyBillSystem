using FamilyBillSystem.Data;
using FamilyBillSystem.DTOs;
using FamilyBillSystem.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace FamilyBillSystem.Controllers
{
    [Route("api/budgets")]
    [ApiController]
    [Authorize]
    public class BudgetController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly ILogger<BudgetController> _logger;

        public BudgetController(AppDbContext context, ILogger<BudgetController> logger)
        {
            _context = context;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> GetBudgets(int year, int month, int? familyId = null, string period = "monthly")
        {
            try
            {
                var userId = GetCurrentUserId();
                if (userId == null)
                    return Unauthorized();

                var userFamilyIds = await _context.FamilyMembers
                    .Where(fm => fm.UserId == userId && fm.Status == "active")
                    .Select(fm => fm.FamilyId)
                    .ToListAsync();

                if (!userFamilyIds.Any())
                {
                    return Ok(new { data = new List<object>() });
                }

                var familyIds = userFamilyIds.AsEnumerable();
                if (familyId.HasValue)
                {
                    if (!userFamilyIds.Contains(familyId.Value))
                    {
                        return Forbid("您不是该家庭的成员");
                    }

                    familyIds = new[] { familyId.Value };
                }

                var normalizedPeriod = NormalizePeriod(period);
                if (normalizedPeriod == null)
                {
                    return BadRequest(new { message = "无效的预算周期类型" });
                }

                GetPeriodDateRange(normalizedPeriod, year, month, out var startDate, out var endDate);

                var budgetsQuery = _context.Budgets
                    .Include(b => b.Category)
                    .Where(b => familyIds.Contains(b.FamilyId) &&
                                b.Period == normalizedPeriod &&
                                b.Year == year &&
                                b.DeletedAt == null &&
                                b.IsActive);

                if (normalizedPeriod == "monthly")
                {
                    budgetsQuery = budgetsQuery.Where(b => b.Month == month);
                }
                else if (normalizedPeriod == "quarterly")
                {
                    var quarterStartMonth = GetQuarterStartMonth(month);
                    budgetsQuery = budgetsQuery.Where(b => b.Month == quarterStartMonth);
                }
                // yearly 不需要额外的月份过滤

                var budgets = await budgetsQuery.ToListAsync();

                var monthlyExpenses = await _context.Bills
                    .Where(b => familyIds.Contains(b.FamilyId) &&
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

                var budgetsWithUsage = budgets.Select(b =>
                {
                    var used = monthlyExpenses
                        .FirstOrDefault(e => e.FamilyId == b.FamilyId && e.CategoryId == b.CategoryId)?.Amount ?? 0m;

                    return new
                    {
                        id = b.Id,
                        familyId = b.FamilyId,
                        categoryId = b.CategoryId,
                        amount = b.Amount,
                        usedAmount = used,
                        description = b.Description,
                        year = b.Year,
                        month = b.Month,
                        category = b.Category == null
                            ? null
                            : new
                            {
                                id = b.Category.Id,
                                name = b.Category.Name,
                                icon = b.Category.Icon,
                                color = b.Category.Color
                            }
                    };
                }).ToList();

                return Ok(new { data = budgetsWithUsage });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取预算列表失败");
                return StatusCode(500, new { message = "获取预算列表失败" });
            }
        }

        // GET: api/budget/summary
        [HttpGet("summary")]
        public async Task<IActionResult> GetBudgetSummary(int year, int month, int? familyId = null, string period = "monthly")
        {
            try
            {
                var userId = GetCurrentUserId();
                if (userId == null)
                    return Unauthorized();

                // 获取用户所属的家庭
                var userFamilyIds = await _context.FamilyMembers
                    .Where(fm => fm.UserId == userId && fm.Status == "active")
                    .Select(fm => fm.FamilyId)
                    .ToListAsync();

                if (!userFamilyIds.Any())
                {
                    return Ok(new
                    {
                        year,
                        month,
                        totalBudget = 0m,
                        totalSpent = 0m,
                        remaining = 0m,
                        utilizationRate = 0m,
                        budgets = new List<object>(),
                        categoryBudgets = new List<object>(),
                        alerts = new List<object>()
                    });
                }

                var familyIds = userFamilyIds.AsEnumerable();
                if (familyId.HasValue)
                {
                    if (!userFamilyIds.Contains(familyId.Value))
                    {
                        return Forbid("您不是该家庭的成员");
                    }

                    familyIds = new[] { familyId.Value };
                }

                var normalizedPeriod = NormalizePeriod(period);
                if (normalizedPeriod == null)
                {
                    return BadRequest(new { message = "无效的预算周期类型" });
                }

                GetPeriodDateRange(normalizedPeriod, year, month, out var startDate, out var endDate);

                // 获取当前周期、当前家庭范围内的实际预算记录
                var budgetsQuery = _context.Budgets
                    .Include(b => b.Category)
                    .Where(b => familyIds.Contains(b.FamilyId) &&
                                b.Period == normalizedPeriod &&
                                b.Year == year &&
                                b.DeletedAt == null &&
                                b.IsActive);

                if (normalizedPeriod == "monthly")
                {
                    budgetsQuery = budgetsQuery.Where(b => b.Month == month);
                }
                else if (normalizedPeriod == "quarterly")
                {
                    var quarterStartMonth = GetQuarterStartMonth(month);
                    budgetsQuery = budgetsQuery.Where(b => b.Month == quarterStartMonth);
                }
                // yearly 不需要额外的月份过滤

                var budgetsInMonth = await budgetsQuery.ToListAsync();

                if (!budgetsInMonth.Any())
                {
                    return Ok(new
                    {
                        year,
                        month,
                        totalBudget = 0m,
                        totalSpent = 0m,
                        remaining = 0m,
                        utilizationRate = 0m,
                        budgets = new List<object>(),
                        categoryBudgets = new List<object>(),
                        alerts = new List<object>()
                    });
                }

                // 计算当月实际支出（仅限有预算的分类/家庭）
                var categoryKeys = budgetsInMonth
                    .Select(b => new { b.FamilyId, b.CategoryId })
                    .Distinct()
                    .ToList();

                var monthlyExpenses = await _context.Bills
                    .Where(b => familyIds.Contains(b.FamilyId) &&
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

                var budgetsWithUsage = budgetsInMonth.Select(b =>
                {
                    var used = monthlyExpenses
                        .FirstOrDefault(e => e.FamilyId == b.FamilyId && e.CategoryId == b.CategoryId)?.Amount ?? 0m;

                    return new
                    {
                        b.FamilyId,
                        b.CategoryId,
                        b.Amount,
                        UsedAmount = used,
                        b.Category,
                        b.AlertThreshold
                    };
                }).ToList();

                var budgets = budgetsWithUsage.Select(b => new
                {
                    amount = b.Amount,
                    usedAmount = b.UsedAmount
                }).ToList();

                var totalBudget = budgets.Sum(b => b.amount);
                var totalSpent = budgets.Sum(b => b.usedAmount);
                var remaining = totalBudget - totalSpent;
                var overallUtilizationRate = totalBudget > 0 ? (totalSpent / totalBudget) * 100 : 0;

                var categoryBudgets = budgetsWithUsage.Select(b =>
                {
                    var budget = b.Amount;
                    var spent = b.UsedAmount;
                    var utilizationRate = budget > 0 ? (spent / budget) * 100 : 0;
                    var alertThreshold = b.AlertThreshold > 0 ? b.AlertThreshold : 80m;

                    return new
                    {
                        categoryId = b.CategoryId,
                        categoryName = b.Category?.Name,
                        categoryIcon = b.Category?.Icon,
                        categoryColor = b.Category?.Color,
                        budget,
                        spent,
                        remaining = Math.Max(0, budget - spent),
                        utilizationRate = Math.Round(utilizationRate, 2),
                        isOverBudget = spent > budget,
                        alertThreshold
                    };
                }).ToList();

                // 生成预算提醒
                var alerts = categoryBudgets
                    .Where(cb => cb.utilizationRate >= cb.alertThreshold)
                    .Select(cb => new
                    {
                        type = cb.isOverBudget ? "over_budget" : "budget_warning",
                        categoryName = cb.categoryName,
                        message = cb.isOverBudget
                            ? $"{cb.categoryName}已超出预算 ¥{cb.spent - cb.budget:F2}"
                            : $"{cb.categoryName}预算使用率已达{cb.utilizationRate:F1}%",
                        severity = cb.isOverBudget ? "high" : "medium"
                    })
                    .ToList();

                // 将预算提醒同步为当前用户的通知，避免重复创建同一条提醒
                if (alerts.Any())
                {
                    var monthStart = new DateTime(year, month, 1);
                    var alertMessages = alerts.Select(a => a.message).ToList();

                    var existingMessages = await _context.Notifications
                        .Where(n =>
                            n.UserId == userId.Value &&
                            n.Title == "预算提醒" &&
                            n.CreatedAt >= monthStart &&
                            alertMessages.Contains(n.Message))
                        .Select(n => n.Message)
                        .ToListAsync();

                    var newNotifications = alerts
                        .Where(a => !existingMessages.Contains(a.message))
                        .Select(a => new Notification
                        {
                            UserId = userId.Value,
                            Title = "预算提醒",
                            Message = a.message,
                            Status = "unread",
                            CreatedAt = DateTime.Now
                        })
                        .ToList();

                    if (newNotifications.Count > 0)
                    {
                        _context.Notifications.AddRange(newNotifications);
                        await _context.SaveChangesAsync();
                    }
                }

                return Ok(new
                {
                    year,
                    month,
                    totalBudget,
                    totalSpent,
                    remaining = Math.Max(0, remaining),
                    utilizationRate = Math.Round(overallUtilizationRate, 2),
                    budgets,
                    categoryBudgets,
                    alerts
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取预算汇总失败");
                return StatusCode(500, new { message = "获取预算汇总失败" });
            }
        }

        // GET: api/budget/categories
        [HttpGet("categories")]
        public async Task<IActionResult> GetCategoryBudgets(int year, int month)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (userId == null)
                    return Unauthorized();

                var userFamilyIds = await _context.FamilyMembers
                    .Where(fm => fm.UserId == userId && fm.Status == "active")
                    .Select(fm => fm.FamilyId)
                    .ToListAsync();

                var startDate = new DateTime(year, month, 1);
                var endDate = startDate.AddMonths(1).AddDays(-1);

                // 获取支出分类和实际支出
                var expenseCategories = await _context.Categories
                    .Where(c => c.Type == "expense" &&
                               (c.FamilyId == null || userFamilyIds.Contains(c.FamilyId.Value)))
                    .ToListAsync();

                var actualExpenses = await _context.Bills
                    .Where(b => userFamilyIds.Contains(b.FamilyId) &&
                               b.Type == "expense" &&
                               b.BillDate >= startDate &&
                               b.BillDate <= endDate &&
                               b.DeletedAt == null &&
                               b.Status == "confirmed")
                    .GroupBy(b => b.CategoryId)
                    .Select(g => new
                    {
                        CategoryId = g.Key,
                        Amount = g.Sum(b => b.Amount)
                    })
                    .Where(x => x.CategoryId.HasValue)
                    .ToDictionaryAsync(x => x.CategoryId!.Value, x => x.Amount);

                // 获取当月的真实预算金额（仅使用月度预算，以避免与季度/年度预算重复统计）
                var budgetAmounts = await _context.Budgets
                    .Where(b => userFamilyIds.Contains(b.FamilyId) &&
                                b.Period == "monthly" &&
                                b.Year == year &&
                                b.Month == month &&
                                b.DeletedAt == null &&
                                b.IsActive)
                    .GroupBy(b => b.CategoryId)
                    .Select(g => new
                    {
                        CategoryId = g.Key,
                        Amount = g.Sum(b => b.Amount)
                    })
                    .ToDictionaryAsync(x => x.CategoryId, x => x.Amount);

                var categoryBudgets = expenseCategories.Select(c =>
                {
                    var actualAmount = actualExpenses.GetValueOrDefault(c.Id, 0m);
                    var budgetAmount = budgetAmounts.GetValueOrDefault(c.Id, 0m);

                    return new
                    {
                        categoryId = c.Id,
                        categoryName = c.Name,
                        categoryIcon = c.Icon,
                        categoryColor = c.Color,
                        budgetAmount,
                        actualAmount,
                        remainingAmount = Math.Max(0, budgetAmount - actualAmount),
                        utilizationRate = budgetAmount > 0 ? Math.Round((actualAmount / budgetAmount) * 100, 2) : 0,
                        isOverBudget = actualAmount > budgetAmount
                    };
                }).OrderByDescending(x => x.utilizationRate).ToList();

                return Ok(categoryBudgets);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取分类预算失败");
                return StatusCode(500, new { message = "获取分类预算失败" });
            }
        }

        [HttpPost]
        public async Task<IActionResult> CreateBudget([FromBody] CreateBudgetRequest request)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (userId == null)
                    return Unauthorized();

                if (!request.FamilyId.HasValue)
                    return BadRequest(new { message = "家庭ID不能为空" });

                var familyId = request.FamilyId.Value;

                var isFamilyMember = await _context.FamilyMembers
                    .AnyAsync(fm => fm.UserId == userId && fm.FamilyId == familyId && fm.Status == "active");

                if (!isFamilyMember)
                    return Forbid("您不是该家庭的成员");

                if (request.Amount <= 0)
                    return BadRequest(new { message = "预算金额必须大于0" });

                if (request.Year <= 0)
                    return BadRequest(new { message = "无效的年份" });

                var normalizedPeriod = NormalizePeriod(request.Period);
                if (normalizedPeriod == null)
                    return BadRequest(new { message = "无效的预算周期类型" });

                int? storedMonth = null;
                if (normalizedPeriod == "monthly")
                {
                    if (!request.Month.HasValue || request.Month < 1 || request.Month > 12)
                        return BadRequest(new { message = "月度预算必须提供 1-12 的月份" });
                    storedMonth = request.Month.Value;
                }
                else if (normalizedPeriod == "quarterly")
                {
                    if (!request.Month.HasValue || request.Month < 1 || request.Month > 12)
                        return BadRequest(new { message = "季度预算的月份必须在 1-12 之间（可以是季度第一个月或该季度内任意月份）" });
                    storedMonth = GetQuarterStartMonth(request.Month.Value);
                }
                else if (normalizedPeriod == "yearly")
                {
                    // 年度预算不需要月份
                    storedMonth = null;
                }

                var category = await _context.Categories
                    .FirstOrDefaultAsync(c => c.Id == request.CategoryId && c.DeletedAt == null);

                if (category == null)
                    return BadRequest(new { message = "分类不存在" });

                if (category.FamilyId.HasValue && category.FamilyId.Value != familyId)
                    return BadRequest(new { message = "分类不属于该家庭" });

                var budget = new Budget
                {
                    FamilyId = familyId,
                    CategoryId = request.CategoryId,
                    Amount = request.Amount,
                    Period = normalizedPeriod,
                    Year = request.Year,
                    Month = storedMonth,
                    UsedAmount = 0m,
                    AlertThreshold = 80m,
                    IsActive = true,
                    Description = request.Description,
                    CreatedBy = userId.Value,
                    CreatedAt = DateTime.Now,
                    UpdatedAt = DateTime.Now
                };

                // 显式设置 Shadow 外键 CreatorId，满足 budgets.CreatorId -> Users(Id) 的外键约束
                _context.Entry(budget).Property("CreatorId").CurrentValue = userId.Value;

                _context.Budgets.Add(budget);
                await _context.SaveChangesAsync();

                return Ok(new { message = "预算添加成功", id = budget.Id });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "创建预算失败");
                return StatusCode(500, new { message = "创建预算失败" });
            }
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateBudget(int id, [FromBody] UpdateBudgetRequest request)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (userId == null)
                    return Unauthorized();

                var budget = await _context.Budgets.FindAsync(id);
                if (budget == null || budget.DeletedAt != null || !budget.IsActive)
                    return NotFound(new { message = "预算不存在" });

                var isFamilyMember = await _context.FamilyMembers
                    .AnyAsync(fm => fm.UserId == userId && fm.FamilyId == budget.FamilyId && fm.Status == "active");

                if (!isFamilyMember)
                    return Forbid("您没有权限修改此预算");

                if (request.Amount <= 0)
                    return BadRequest(new { message = "预算金额必须大于0" });

                if (request.Year <= 0)
                    return BadRequest(new { message = "无效的年份" });

                var normalizedPeriod = NormalizePeriod(request.Period);
                if (normalizedPeriod == null)
                    return BadRequest(new { message = "无效的预算周期类型" });

                int? storedMonth = null;
                if (normalizedPeriod == "monthly")
                {
                    if (!request.Month.HasValue || request.Month < 1 || request.Month > 12)
                        return BadRequest(new { message = "月度预算必须提供 1-12 的月份" });
                    storedMonth = request.Month.Value;
                }
                else if (normalizedPeriod == "quarterly")
                {
                    if (!request.Month.HasValue || request.Month < 1 || request.Month > 12)
                        return BadRequest(new { message = "季度预算的月份必须在 1-12 之间（可以是季度第一个月或该季度内任意月份）" });
                    storedMonth = GetQuarterStartMonth(request.Month.Value);
                }
                else if (normalizedPeriod == "yearly")
                {
                    // 年度预算不需要月份
                    storedMonth = null;
                }

                var category = await _context.Categories
                    .FirstOrDefaultAsync(c => c.Id == request.CategoryId && c.DeletedAt == null);

                if (category == null)
                    return BadRequest(new { message = "分类不存在" });

                if (category.FamilyId.HasValue && category.FamilyId.Value != budget.FamilyId)
                    return BadRequest(new { message = "分类不属于该家庭" });

                budget.CategoryId = request.CategoryId;
                budget.Amount = request.Amount;
                budget.Period = normalizedPeriod;
                budget.Year = request.Year;
                budget.Month = storedMonth;
                budget.Description = request.Description;
                budget.UpdatedAt = DateTime.Now;

                await _context.SaveChangesAsync();

                return Ok(new { message = "预算更新成功" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "更新预算失败");
                return StatusCode(500, new { message = "更新预算失败" });
            }
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteBudget(int id)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (userId == null)
                    return Unauthorized();

                var budget = await _context.Budgets.FindAsync(id);
                if (budget == null || budget.DeletedAt != null || !budget.IsActive)
                    return NotFound(new { message = "预算不存在" });

                var isFamilyMember = await _context.FamilyMembers
                    .AnyAsync(fm => fm.UserId == userId && fm.FamilyId == budget.FamilyId && fm.Status == "active");

                if (!isFamilyMember)
                    return Forbid("您没有权限删除此预算");

                budget.IsActive = false;
                budget.DeletedAt = DateTime.Now;
                budget.UpdatedAt = DateTime.Now;

                await _context.SaveChangesAsync();

                return Ok(new { message = "预算删除成功" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "删除预算失败");
                return StatusCode(500, new { message = "删除预算失败" });
            }
        }

        private int? GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return int.TryParse(userIdClaim, out int userId) ? userId : null;
        }

        /// <summary>
        /// 规范化周期字符串：monthly / quarterly / yearly
        /// </summary>
        private static string? NormalizePeriod(string? period)
        {
            if (string.IsNullOrWhiteSpace(period)) return "monthly";

            var p = period.Trim().ToLower();
            return p switch
            {
                "monthly" or "month" => "monthly",
                "quarterly" or "quarter" => "quarterly",
                "yearly" or "year" or "annual" => "yearly",
                _ => null
            };
        }

        /// <summary>
        /// 根据传入的月份(1-12)获取对应季度的起始月份
        /// </summary>
        private static int GetQuarterStartMonth(int month)
        {
            if (month < 1 || month > 12)
            {
                throw new ArgumentOutOfRangeException(nameof(month), "月份必须在 1-12 之间");
            }

            var quarter = (month - 1) / 3 + 1; // 1-3 -> 1, 4-6 -> 2, 7-9 -> 3, 10-12 -> 4
            return (quarter - 1) * 3 + 1;      // 1 -> 1, 2 -> 4, 3 -> 7, 4 -> 10
        }

        /// <summary>
        /// 根据周期类型和传入的年份、月份，计算该周期的起止日期
        /// </summary>
        private static void GetPeriodDateRange(string period, int year, int month, out DateTime startDate, out DateTime endDate)
        {
            switch (period)
            {
                case "monthly":
                    startDate = new DateTime(year, month, 1);
                    endDate = startDate.AddMonths(1).AddDays(-1);
                    break;

                case "quarterly":
                    var quarterStartMonth = GetQuarterStartMonth(month);
                    startDate = new DateTime(year, quarterStartMonth, 1);
                    endDate = startDate.AddMonths(3).AddDays(-1);
                    break;

                case "yearly":
                    startDate = new DateTime(year, 1, 1);
                    endDate = new DateTime(year, 12, 31);
                    break;

                default:
                    throw new ArgumentException("无效的预算周期类型", nameof(period));
            }
        }
    }

    public class CreateBudgetRequest
    {
        public int? FamilyId { get; set; }
        public int CategoryId { get; set; }
        public decimal Amount { get; set; }
        public int Year { get; set; }
        public int? Month { get; set; }
        public string Period { get; set; } = "monthly";
        public string? Description { get; set; }
    }

    public class UpdateBudgetRequest
    {
        public int CategoryId { get; set; }
        public decimal Amount { get; set; }
        public int Year { get; set; }
        public int? Month { get; set; }
        public string Period { get; set; } = "monthly";
        public string? Description { get; set; }
    }
}