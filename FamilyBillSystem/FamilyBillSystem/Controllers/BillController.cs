using FamilyBillSystem.Data;
using FamilyBillSystem.DTOs;
using FamilyBillSystem.Models;
using FamilyBillSystem.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Text;

namespace FamilyBillSystem.Controllers
{
    [Route("api/bills")]
    [Authorize]
    public class BillController : BaseController
    {
        private readonly AppDbContext _context;
        private readonly ILogger<BillController> _logger;
        private readonly IBudgetService _budgetService;

        public BillController(AppDbContext context, ILogger<BillController> logger, IBudgetService budgetService)
        {
            _context = context;
            _logger = logger;
            _budgetService = budgetService;
        }

        // GET: api/bills
        [HttpGet]
        public async Task<IActionResult> GetBills(
            int page = 1,
            int pageSize = 10,
            string? type = null,
            int? categoryId = null,
            DateTime? startDate = null,
            DateTime? endDate = null,
            string sortBy = "BillDate",
            bool sortDescending = true,
            int? year = null,
            int? month = null,
            int? categoryType = null,
            int? familyId = null)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (userId == null)
                    return Unauthorized();

                var query = _context.Bills
                    .Include(b => b.Category)
                    .Include(b => b.User)
                    .Include(b => b.Family)
                    .Where(b => b.DeletedAt == null);

                var userFamilyIds = await _context.FamilyMembers
                    .Where(fm => fm.UserId == userId && fm.Status == "active")
                    .Select(fm => fm.FamilyId)
                    .ToListAsync();

                if (!userFamilyIds.Any())
                {
                    return Ok(new
                    {
                        data = new List<object>(),
                        pagination = new
                        {
                            page,
                            pageSize,
                            totalCount = 0,
                            totalPages = 0
                        }
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

                query = query.Where(b => familyIds.Contains(b.FamilyId));

                // 应用筛选条件
                if (!string.IsNullOrEmpty(type))
                {
                    query = query.Where(b => b.Type == type);
                }

                if (categoryType.HasValue)
                {
                    string? normalizedType = categoryType.Value switch
                    {
                        1 => "income",
                        2 => "expense",
                        _ => null
                    };

                    if (normalizedType != null)
                    {
                        query = query.Where(b => b.Type == normalizedType);
                    }
                }

                if (categoryId.HasValue)
                {
                    query = query.Where(b => b.CategoryId == categoryId);
                }

                // 时间筛选：优先 year/month，其次显式的 startDate/endDate
                if (year.HasValue && month.HasValue && month.Value >= 1 && month.Value <= 12)
                {
                    var rangeStart = new DateTime(year.Value, month.Value, 1);
                    var rangeEnd = rangeStart.AddMonths(1).AddDays(-1);
                    query = query.Where(b => b.BillDate >= rangeStart && b.BillDate <= rangeEnd);
                }
                else
                {
                    if (startDate.HasValue)
                    {
                        query = query.Where(b => b.BillDate >= startDate.Value);
                    }

                    if (endDate.HasValue)
                    {
                        query = query.Where(b => b.BillDate <= endDate.Value);
                    }
                }

                // 排序
                query = sortBy.ToLower() switch
                {
                    "amount" => sortDescending ? query.OrderByDescending(b => b.Amount) : query.OrderBy(b => b.Amount),
                    "createdat" => sortDescending ? query.OrderByDescending(b => b.CreatedAt) : query.OrderBy(b => b.CreatedAt),
                    _ => sortDescending ? query.OrderByDescending(b => b.BillDate) : query.OrderBy(b => b.BillDate)
                };

                var totalCount = await query.CountAsync();
                var bills = await query
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .Select(b => new
                    {
                        id = b.Id,
                        type = b.Type,
                        categoryType = b.Type == "income" ? 1 : 2,
                        amount = b.Amount,
                        description = b.Description,
                        billDate = b.BillDate,
                        paymentMethod = b.PaymentMethod,
                        status = b.Status,
                        createdAt = b.CreatedAt,
                        familyId = b.FamilyId,
                        familyName = b.Family != null ? b.Family.Name : null,
                        categoryId = b.CategoryId,
                        categoryName = b.Category != null ? b.Category.Name : "未分类",
                        categoryIcon = b.Category != null ? b.Category.Icon : "default",
                        categoryColor = b.Category != null ? b.Category.Color : "#666666",
                        userId = b.User.Id,
                        userNickName = b.User.Nickname,
                        avatarUrl = b.User.AvatarUrl
                    })
                    .ToListAsync();

                return Ok(new
                {
                    data = bills,
                    pagination = new
                    {
                        page,
                        pageSize,
                        totalCount,
                        totalPages = (int)Math.Ceiling((double)totalCount / pageSize)
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取账单列表失败");
                return StatusCode(500, new { message = "获取账单列表失败" });
            }
        }

        // GET: api/bills/{id}
        [HttpGet("{id}")]
        public async Task<IActionResult> GetBill(int id)
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

                var bill = await _context.Bills
                    .Include(b => b.Category)
                    .Include(b => b.Family)
                    .Include(b => b.User)
                    .Where(b => b.Id == id && (b.UserId == userId || userFamilyIds.Contains(b.FamilyId)))
                    .Select(b => new
                    {
                        id = b.Id,
                        type = b.Type,
                        categoryType = b.Type == "income" ? 1 : 2,
                        amount = b.Amount,
                        description = b.Description,
                        remark = b.Remark,
                        billDate = b.BillDate,
                        upDatedAt = b.UpdatedAt,
                        paymentMethod = b.PaymentMethod,
                        status = b.Status,
                        createdAt = b.CreatedAt,
                        familyId = b.FamilyId,
                        familyName = b.Family != null ? b.Family.Name : null,
                        categoryId = b.CategoryId,
                        categoryName = b.Category != null ? b.Category.Name : "未分类",
                        categoryIcon = b.Category != null ? b.Category.Icon : "default",
                        categoryColor = b.Category != null ? b.Category.Color : "#666666",
                        userId = b.User.Id,
                        userNickName = _context.FamilyMembers
                            .Where(fm => fm.UserId == b.UserId && fm.FamilyId == b.FamilyId && fm.Status == "active")
                            .Select(fm => fm.Nickname)
                            .FirstOrDefault() ?? b.User.Nickname,
                        avatarUrl = b.User.AvatarUrl
                    })
                    .FirstOrDefaultAsync();

                if (bill == null)
                {
                    return NotFound(new { message = "账单不存在或无权限访问" });
                }

                return Ok(bill);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取账单详情失败");
                return StatusCode(500, new { message = "获取账单详情失败" });
            }
        }

        // GET: api/bill/statistics/monthly
        [HttpGet("statistics/monthly")]
        public async Task<IActionResult> GetMonthlyStatistics(int year, int month, int? familyId = null)
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
                    return Ok(new
                    {
                        year,
                        month,
                        totalIncome = 0m,
                        totalExpense = 0m,
                        income = 0m,
                        expense = 0m,
                        balance = 0m,
                        billCount = 0,
                        categoryStats = new List<object>()
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

                var startDate = new DateTime(year, month, 1);
                var endDate = startDate.AddMonths(1).AddDays(-1);

                var bills = await _context.Bills
                    .Where(b => familyIds.Contains(b.FamilyId) &&
                               b.BillDate >= startDate &&
                               b.BillDate <= endDate &&
                               b.DeletedAt == null &&
                               b.Status == "confirmed")
                    .ToListAsync();

                var totalIncome = bills.Where(b => b.Type == "income").Sum(b => b.Amount);
                var totalExpense = bills.Where(b => b.Type == "expense").Sum(b => b.Amount);
                var balance = totalIncome - totalExpense;

                // 按分类统计
                var categoryStats = bills
                    .GroupBy(b => new { b.Type, b.CategoryId })
                    .Select(g => new
                    {
                        Type = g.Key.Type,
                        CategoryId = g.Key.CategoryId,
                        Amount = g.Sum(b => b.Amount),
                        Count = g.Count()
                    })
                    .ToList();

                // 获取分类信息
                var categoryIds = categoryStats.Select(cs => cs.CategoryId).Where(id => id.HasValue).ToList();
                var categories = await _context.Categories
                    .Where(c => categoryIds.Contains(c.Id))
                    .ToDictionaryAsync(c => c.Id, c => new { c.Name, c.Icon, c.Color });

                var categoryStatsWithNames = categoryStats.Select(cs => new
                {
                    cs.Type,
                    cs.CategoryId,
                    CategoryName = cs.CategoryId.HasValue && categories.ContainsKey(cs.CategoryId.Value)
                        ? categories[cs.CategoryId.Value].Name
                        : "未分类",
                    CategoryIcon = cs.CategoryId.HasValue && categories.ContainsKey(cs.CategoryId.Value)
                        ? categories[cs.CategoryId.Value].Icon
                        : "default",
                    CategoryColor = cs.CategoryId.HasValue && categories.ContainsKey(cs.CategoryId.Value)
                        ? categories[cs.CategoryId.Value].Color
                        : "#666666",
                    cs.Amount,
                    cs.Count
                }).ToList();

                return Ok(new
                {
                    year,
                    month,
                    totalIncome,
                    totalExpense,
                    income = totalIncome,
                    expense = totalExpense,
                    balance,
                    billCount = bills.Count,
                    categoryStats = categoryStatsWithNames
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取月度统计失败");
                return StatusCode(500, new { message = "获取月度统计失败" });
            }
        }

        // GET: api/bill/statistics/yearly
        [HttpGet("statistics/yearly")]
        public async Task<IActionResult> GetYearlyStatistics(int year, int? familyId = null)
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
                    return Ok(new
                    {
                        year,
                        totalIncome = 0m,
                        totalExpense = 0m,
                        income = 0m,
                        expense = 0m,
                        balance = 0m,
                        billCount = 0
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

                var startDate = new DateTime(year, 1, 1);
                var endDate = startDate.AddYears(1).AddDays(-1);

                var bills = await _context.Bills
                    .Where(b => familyIds.Contains(b.FamilyId) &&
                               b.BillDate >= startDate &&
                               b.BillDate <= endDate &&
                               b.DeletedAt == null &&
                               b.Status == "confirmed")
                    .ToListAsync();

                var totalIncome = bills.Where(b => b.Type == "income").Sum(b => b.Amount);
                var totalExpense = bills.Where(b => b.Type == "expense").Sum(b => b.Amount);
                var balance = totalIncome - totalExpense;

                return Ok(new
                {
                    year,
                    totalIncome,
                    totalExpense,
                    income = totalIncome,
                    expense = totalExpense,
                    balance,
                    billCount = bills.Count
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取年度统计失败");
                return StatusCode(500, new { message = "获取年度统计失败" });
            }
        }

        // GET: api/bill/statistics/category
        [HttpGet("statistics/category")]
        public async Task<IActionResult> GetCategoryStatistics(int year, int? month = null, int? categoryType = null, int? familyId = null)
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
                    return Ok(new List<object>());
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

                DateTime startDate;
                DateTime endDate;

                if (month.HasValue && month.Value >= 1 && month.Value <= 12)
                {
                    startDate = new DateTime(year, month.Value, 1);
                    endDate = startDate.AddMonths(1).AddDays(-1);
                }
                else
                {
                    startDate = new DateTime(year, 1, 1);
                    endDate = startDate.AddYears(1).AddDays(-1);
                }

                var query = _context.Bills
                    .Where(b => familyIds.Contains(b.FamilyId) &&
                               b.BillDate >= startDate &&
                               b.BillDate <= endDate &&
                               b.DeletedAt == null &&
                               b.Status == "confirmed");

                if (categoryType.HasValue)
                {
                    string? normalizedType = categoryType.Value switch
                    {
                        1 => "income",
                        2 => "expense",
                        _ => null
                    };

                    if (normalizedType != null)
                    {
                        query = query.Where(b => b.Type == normalizedType);
                    }
                }

                var bills = await query.ToListAsync();

                if (!bills.Any())
                {
                    return Ok(new List<object>());
                }

                var totalAmount = bills.Sum(b => b.Amount);

                var categoryGroups = bills
                    .GroupBy(b => b.CategoryId)
                    .Select(g => new
                    {
                        CategoryId = g.Key,
                        Amount = g.Sum(b => b.Amount),
                        BillCount = g.Count()
                    })
                    .ToList();

                var categoryIds = categoryGroups
                    .Where(g => g.CategoryId.HasValue)
                    .Select(g => g.CategoryId!.Value)
                    .ToList();

                var categories = await _context.Categories
                    .Where(c => categoryIds.Contains(c.Id))
                    .ToDictionaryAsync(c => c.Id, c => new { c.Name, c.Icon, c.Color });

                var result = categoryGroups
                    .Select(cs =>
                    {
                        var hasCategory = cs.CategoryId.HasValue && categories.ContainsKey(cs.CategoryId.Value);
                        var category = hasCategory ? categories[cs.CategoryId!.Value] : null;

                        var amount = cs.Amount;
                        var percentage = totalAmount > 0 ? Math.Round(amount / totalAmount * 100, 2) : 0m;

                        return new
                        {
                            categoryId = cs.CategoryId,
                            categoryName = hasCategory ? category!.Name : "未分类",
                            icon = hasCategory ? category!.Icon : "default",
                            color = hasCategory ? category!.Color : "#666666",
                            amount,
                            billCount = cs.BillCount,
                            percentage
                        };
                    })
                    .OrderByDescending(x => x.amount)
                    .ToList();

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取分类统计失败");
                return StatusCode(500, new { message = "获取分类统计失败" });
            }
        }

        // GET: api/bill/statistics/daily
        [HttpGet("statistics/daily")]
        public async Task<IActionResult> GetDailyStatistics(int year, int month, int? familyId = null)
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
                    return Ok(new List<object>());
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

                var startDate = new DateTime(year, month, 1);
                var endDate = startDate.AddMonths(1).AddDays(-1);

                var bills = await _context.Bills
                    .Where(b => familyIds.Contains(b.FamilyId) &&
                               b.BillDate >= startDate &&
                               b.BillDate <= endDate &&
                               b.DeletedAt == null &&
                               b.Status == "confirmed")
                    .ToListAsync();

                var result = bills
                    .GroupBy(b => b.BillDate.Date)
                    .OrderBy(g => g.Key)
                    .Select(g => new
                    {
                        period = $"{g.Key.Day}日",
                        income = g.Where(b => b.Type == "income").Sum(b => b.Amount),
                        expense = g.Where(b => b.Type == "expense").Sum(b => b.Amount)
                    })
                    .ToList();

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取每日趋势统计失败");
                return StatusCode(500, new { message = "获取每日趋势统计失败" });
            }
        }

        // GET: api/bill/statistics/monthly-trend
        [HttpGet("statistics/monthly-trend")]
        public async Task<IActionResult> GetMonthlyTrendStatistics(int year, int? familyId = null)
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
                    return Ok(new List<object>());
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

                var startDate = new DateTime(year, 1, 1);
                var endDate = startDate.AddYears(1).AddDays(-1);

                var bills = await _context.Bills
                    .Where(b => familyIds.Contains(b.FamilyId) &&
                               b.BillDate >= startDate &&
                               b.BillDate <= endDate &&
                               b.DeletedAt == null &&
                               b.Status == "confirmed")
                    .ToListAsync();

                var result = bills
                    .GroupBy(b => b.BillDate.Month)
                    .OrderBy(g => g.Key)
                    .Select(g => new
                    {
                        period = $"{g.Key}月",
                        income = g.Where(b => b.Type == "income").Sum(b => b.Amount),
                        expense = g.Where(b => b.Type == "expense").Sum(b => b.Amount)
                    })
                    .ToList();

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取月度趋势统计失败");
                return StatusCode(500, new { message = "获取月度趋势统计失败" });
            }
        }

        // POST: api/bill
        [HttpPost]
        public async Task<IActionResult> CreateBill([FromBody] CreateBillRequest request)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (userId == null)
                    return Unauthorized();

                // 验证用户是否属于指定家庭
                var isFamilyMember = await _context.FamilyMembers
                    .AnyAsync(fm => fm.UserId == userId && fm.FamilyId == request.FamilyId && fm.Status == "active");

                if (!isFamilyMember)
                    return Forbid("您不是该家庭的成员");

                var bill = new Bill
                {
                    FamilyId = request.FamilyId,
                    UserId = userId.Value,
                    CategoryId = request.CategoryId,
                    Type = request.Type,
                    Amount = request.Amount,
                    Description = request.Description,
                    Remark = request.Remark,
                    PaymentMethod = request.PaymentMethod ?? string.Empty,
                    BillDate = request.BillDate,
                    Status = "confirmed",
                    CreatedAt = DateTime.Now,
                    UpdatedAt = DateTime.Now
                };

                _context.Bills.Add(bill);
                await _context.SaveChangesAsync();

                // 更新家庭统计
                await UpdateFamilyStats(request.FamilyId);

                // 更新预算使用金额
                if (request.CategoryId.HasValue)
                {
                    await _budgetService.UpdateBudgetUsageAsync(request.FamilyId, request.CategoryId.Value, request.Amount, request.Type == "income");
                }

                return Ok(new { message = "账单创建成功", billId = bill.Id });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "创建账单失败");
                return StatusCode(500, new { message = "创建账单失败" });
            }
        }

        // PUT: api/bill/{id}
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateBill(int id, [FromBody] UpdateBillRequest request)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (userId == null)
                    return Unauthorized();

                var bill = await _context.Bills.FindAsync(id);
                if (bill == null || bill.DeletedAt != null)
                    return NotFound("账单不存在");

                // 验证权限
                var isFamilyMember = await _context.FamilyMembers
                    .AnyAsync(fm => fm.UserId == userId && fm.FamilyId == bill.FamilyId && fm.Status == "active");

                if (!isFamilyMember)
                    return Forbid("您没有权限修改此账单");

                // 保存原始值用于预算更新
                var originalCategoryId = bill.CategoryId;
                var originalAmount = bill.Amount;

                // 更新账单信息
                if (request.CategoryId.HasValue)
                    bill.CategoryId = request.CategoryId;
                if (!string.IsNullOrEmpty(request.Description))
                    bill.Description = request.Description;
                if (request.Amount.HasValue)
                    bill.Amount = request.Amount.Value;
                if (!string.IsNullOrEmpty(request.PaymentMethod))
                    bill.PaymentMethod = request.PaymentMethod;
                if (request.BillDate.HasValue)
                    bill.BillDate = request.BillDate.Value;
                if (!string.IsNullOrEmpty(request.Remark))
                    bill.Remark = request.Remark;

                bill.UpdatedAt = DateTime.Now;

                await _context.SaveChangesAsync();

                // 更新家庭统计
                await UpdateFamilyStats(bill.FamilyId);

                // 更新预算使用金额（如果分类或金额发生变化）
                if (originalCategoryId != bill.CategoryId || originalAmount != bill.Amount)
                {
                    // 重新计算原分类的预算
                    if (originalCategoryId.HasValue)
                    {
                        await _budgetService.UpdateBudgetUsageAsync(bill.FamilyId, originalCategoryId.Value, 0, bill.Type == "income");
                    }
                    // 重新计算新分类的预算
                    if (bill.CategoryId.HasValue)
                    {
                        await _budgetService.UpdateBudgetUsageAsync(bill.FamilyId, bill.CategoryId.Value, 0, bill.Type == "income");
                    }
                }

                return Ok(new { message = "账单更新成功" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "更新账单失败");
                return StatusCode(500, new { message = "更新账单失败" });
            }
        }

        // DELETE: api/bill/{id}
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteBill(int id)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (userId == null)
                    return Unauthorized();

                var bill = await _context.Bills.FindAsync(id);
                if (bill == null || bill.DeletedAt != null)
                    return NotFound("账单不存在");

                // 验证权限
                var isFamilyMember = await _context.FamilyMembers
                    .AnyAsync(fm => fm.UserId == userId && fm.FamilyId == bill.FamilyId && fm.Status == "active");

                if (!isFamilyMember)
                    return Forbid("您没有权限删除此账单");

                // 软删除
                bill.DeletedAt = DateTime.Now;
                bill.Status = "deleted";
                bill.UpdatedAt = DateTime.Now;

                await _context.SaveChangesAsync();

                // 更新家庭统计
                await UpdateFamilyStats(bill.FamilyId);

                // 更新预算使用金额
                if (bill.CategoryId.HasValue)
                {
                    await _budgetService.UpdateBudgetUsageAsync(bill.FamilyId, bill.CategoryId.Value, 0, bill.Type == "income");
                }

                return Ok(new { message = "账单删除成功" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "删除账单失败");
                return StatusCode(500, new { message = "删除账单失败" });
            }
        }

        private int? GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return int.TryParse(userIdClaim, out int userId) ? userId : null;
        }

        private async Task UpdateFamilyStats(int familyId)
        {
            var bills = await _context.Bills
                .Where(b => b.FamilyId == familyId && b.DeletedAt == null && b.Status == "confirmed")
                .ToListAsync();

            var totalIncome = bills.Where(b => b.Type == "income").Sum(b => b.Amount);
            var totalExpense = bills.Where(b => b.Type == "expense").Sum(b => b.Amount);

            var stats = await _context.FamilyStats.FirstOrDefaultAsync(fs => fs.FamilyId == familyId);
            if (stats == null)
            {
                stats = new FamilyStats
                {
                    FamilyId = familyId,
                    TotalIncome = totalIncome,
                    TotalExpense = totalExpense,
                    LastUpdated = DateTime.Now
                };
                _context.FamilyStats.Add(stats);
            }
            else
            {
                stats.TotalIncome = totalIncome;
                stats.TotalExpense = totalExpense;
                stats.LastUpdated = DateTime.Now;
            }

            await _context.SaveChangesAsync();
        }

        // GET: api/bills/export
        [HttpGet("export")]
        public async Task<IActionResult> ExportBills([FromQuery] string? range = "month", [FromQuery] string? format = "excel")
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
                    return BadRequest(new { message = "没有可导出的家庭账单" });
                }

                var query = _context.Bills
                    .Include(b => b.Category)
                    .Include(b => b.Family)
                    .Include(b => b.User)
                    .Where(b => userFamilyIds.Contains(b.FamilyId) &&
                                b.DeletedAt == null &&
                                b.Status == "confirmed");

                var now = DateTime.Now;
                if (string.Equals(range, "month", StringComparison.OrdinalIgnoreCase))
                {
                    var start = new DateTime(now.Year, now.Month, 1);
                    var end = start.AddMonths(1).AddDays(-1);
                    query = query.Where(b => b.BillDate >= start && b.BillDate <= end);
                }
                else if (string.Equals(range, "year", StringComparison.OrdinalIgnoreCase))
                {
                    var start = new DateTime(now.Year, 1, 1);
                    var end = start.AddYears(1).AddDays(-1);
                    query = query.Where(b => b.BillDate >= start && b.BillDate <= end);
                }

                var bills = await query
                    .OrderByDescending(b => b.BillDate)
                    .ToListAsync();

                if (!bills.Any())
                {
                    return BadRequest(new { message = "当前条件下没有账单数据" });
                }

                var sb = new StringBuilder();
                sb.AppendLine("家庭,类型,分类,金额,日期,备注,支付方式,记账人");

                foreach (var bill in bills)
                {
                    var familyName = bill.Family != null ? bill.Family.Name : string.Empty;
                    var typeText = bill.Type == "income" ? "收入" : "支出";
                    var categoryName = bill.Category != null ? bill.Category.Name : "未分类";
                    var dateText = bill.BillDate.ToString("yyyy-MM-dd HH:mm:ss");
                    var description = bill.Description ?? string.Empty;
                    var paymentMethod = bill.PaymentMethod ?? string.Empty;
                    var userName = bill.User != null ? (bill.User.Nickname ?? bill.User.Email ?? string.Empty) : string.Empty;

                    sb.AppendLine(string.Join(",", new[]
                    {
                CsvEscape(familyName),
                CsvEscape(typeText),
                CsvEscape(categoryName),
                bill.Amount.ToString("0.00"),
                CsvEscape(dateText),
                CsvEscape(description),
                CsvEscape(paymentMethod),
                CsvEscape(userName)
            }));
                }

                var fileNamePrefix = string.Equals(range, "year", StringComparison.OrdinalIgnoreCase)
                    ? $"{now.Year}年"
                    : string.Equals(range, "all", StringComparison.OrdinalIgnoreCase)
                        ? "全部"
                        : $"{now.Year}年{now.Month}月";

                var fileName = $"账单导出_{fileNamePrefix}_{now:yyyyMMddHHmmss}.csv";

                // 使用带 BOM 的 UTF8，兼容 Excel 对中文的显示
                var encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true);
                var bytes = encoding.GetBytes(sb.ToString());

                return File(bytes, "text/csv", fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "导出账单数据失败");
                return StatusCode(500, new { message = "导出账单数据失败" });
            }
        }

        private static string CsvEscape(string input)
        {
            if (string.IsNullOrEmpty(input))
            {
                return "\"\"";
            }

            var needsQuotes = input.Contains(',') || input.Contains('"') || input.Contains('\n') || input.Contains('\r');
            var value = input.Replace("\"", "\"\"");

            return needsQuotes ? $"\"{value}\"" : value;
        }

    }
}