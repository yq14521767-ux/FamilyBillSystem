using FamilyBillSystem.Data;
using FamilyBillSystem.Models;
using Microsoft.EntityFrameworkCore;

namespace FamilyBillSystem.Services
{
    public interface IBudgetService
    {
        Task UpdateBudgetUsageAsync(int familyId, int categoryId, decimal amount, bool isIncome = false);
        Task RecalculateBudgetUsageAsync(int budgetId);
        Task RecalculateAllBudgetsAsync(int familyId);
    }

    public class BudgetService : IBudgetService
    {
        private readonly AppDbContext _context;
        private readonly ILogger<BudgetService> _logger;

        public BudgetService(AppDbContext context, ILogger<BudgetService> logger)
        {
            _context = context;
            _logger = logger;
        }

        /// <summary>
        /// 更新预算使用金额
        /// </summary>
        public async Task UpdateBudgetUsageAsync(int familyId, int categoryId, decimal amount, bool isIncome = false)
        {
            try
            {
                // 只处理支出类型的预算更新
                if (isIncome) return;

                var currentDate = DateTime.Now;
                var currentYear = currentDate.Year;
                var currentMonth = currentDate.Month;

                // 查找相关的预算
                var budgets = await _context.Budgets
                    .Where(b => b.FamilyId == familyId && 
                               b.CategoryId == categoryId && 
                               b.IsActive &&
                               b.DeletedAt == null &&
                               b.Year == currentYear)
                    .ToListAsync();

                foreach (var budget in budgets)
                {
                    // 检查预算周期
                    bool shouldUpdate = false;
                    
                    if (budget.Period == "monthly" && budget.Month == currentMonth)
                    {
                        shouldUpdate = true;
                    }
                    else if (budget.Period == "yearly")
                    {
                        shouldUpdate = true;
                    }
                    else if (budget.Period == "quarterly")
                    {
                        var quarter = (currentMonth - 1) / 3 + 1;
                        var budgetQuarter = budget.Month.HasValue ? (budget.Month.Value - 1) / 3 + 1 : 1;
                        if (quarter == budgetQuarter)
                        {
                            shouldUpdate = true;
                        }
                    }

                    if (shouldUpdate)
                    {
                        await RecalculateBudgetUsageAsync(budget.Id);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "更新预算使用金额时发生错误: FamilyId={FamilyId}, CategoryId={CategoryId}, Amount={Amount}", 
                    familyId, categoryId, amount);
            }
        }

        /// <summary>
        /// 重新计算预算使用金额
        /// </summary>
        public async Task RecalculateBudgetUsageAsync(int budgetId)
        {
            try
            {
                var budget = await _context.Budgets
                    .FirstOrDefaultAsync(b => b.Id == budgetId && b.DeletedAt == null);

                if (budget == null) return;

                // 计算时间范围
                DateTime startDate, endDate;
                GetBudgetDateRange(budget, out startDate, out endDate);

                // 计算该预算期间内的实际支出
                var totalExpense = await _context.Bills
                    .Where(b => b.FamilyId == budget.FamilyId &&
                               b.CategoryId == budget.CategoryId &&
                               b.Type == "expense" &&
                               b.Status == "confirmed" &&
                               b.DeletedAt == null &&
                               b.BillDate >= startDate &&
                               b.BillDate <= endDate)
                    .SumAsync(b => b.Amount);

                // 更新预算使用金额
                budget.UsedAmount = totalExpense;
                budget.UpdatedAt = DateTime.Now;

                await _context.SaveChangesAsync();

                _logger.LogInformation("预算使用金额已更新: BudgetId={BudgetId}, UsedAmount={UsedAmount}", 
                    budgetId, totalExpense);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "重新计算预算使用金额时发生错误: BudgetId={BudgetId}", budgetId);
            }
        }

        /// <summary>
        /// 重新计算家庭所有预算
        /// </summary>
        public async Task RecalculateAllBudgetsAsync(int familyId)
        {
            try
            {
                var budgets = await _context.Budgets
                    .Where(b => b.FamilyId == familyId && b.IsActive && b.DeletedAt == null)
                    .ToListAsync();

                foreach (var budget in budgets)
                {
                    await RecalculateBudgetUsageAsync(budget.Id);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "重新计算家庭所有预算时发生错误: FamilyId={FamilyId}", familyId);
            }
        }

        /// <summary>
        /// 获取预算的日期范围
        /// </summary>
        private void GetBudgetDateRange(Budget budget, out DateTime startDate, out DateTime endDate)
        {
            var year = budget.Year;
            
            switch (budget.Period.ToLower())
            {
                case "monthly":
                    var month = budget.Month ?? 1;
                    startDate = new DateTime(year, month, 1);
                    endDate = startDate.AddMonths(1).AddDays(-1);
                    break;
                    
                case "quarterly":
                    var quarter = budget.Month.HasValue ? (budget.Month.Value - 1) / 3 + 1 : 1;
                    var quarterStartMonth = (quarter - 1) * 3 + 1;
                    startDate = new DateTime(year, quarterStartMonth, 1);
                    endDate = startDate.AddMonths(3).AddDays(-1);
                    break;
                    
                case "yearly":
                default:
                    startDate = new DateTime(year, 1, 1);
                    endDate = new DateTime(year, 12, 31);
                    break;
            }
        }
    }
}