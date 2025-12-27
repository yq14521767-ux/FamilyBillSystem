using FamilyBillSystem.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace FamilyBillSystem.Controllers
{
    [Route("api/users")]
    [ApiController]
    [Authorize]
    public class UsersController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly ILogger<UsersController> _logger;

        public UsersController(AppDbContext context, ILogger<UsersController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // GET: api/users/stats
        [HttpGet("stats")]
        public async Task<IActionResult> GetUserStats()
        {
            try
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var userId))
                {
                    return Unauthorized(new { message = "用户未认证" });
                }

                var today = DateTime.Today;
                var year = today.Year;
                var month = today.Month;
                var startDate = new DateTime(year, month, 1);
                var endDate = startDate.AddMonths(1).AddDays(-1);

                // 总账单数（当前用户作为操作人、未删除、已确认）
                var totalBills = await _context.Bills
                    .Where(b => b.UserId == userId && b.DeletedAt == null && b.Status == "confirmed")
                    .CountAsync();

                // 参与家庭数（当前用户为 active 成员）
                var totalFamilies = await _context.FamilyMembers
                    .Where(fm => fm.UserId == userId && fm.Status == "active")
                    .CountAsync();

                // 本月账单数
                var thisMonthBills = await _context.Bills
                    .Where(b => b.UserId == userId &&
                                b.DeletedAt == null &&
                                b.Status == "confirmed" &&
                                b.BillDate >= startDate &&
                                b.BillDate <= endDate)
                    .CountAsync();

                // 本月金额（仅统计支出）
                var thisMonthAmount = await _context.Bills
                    .Where(b => b.UserId == userId &&
                                b.DeletedAt == null &&
                                b.Status == "confirmed" &&
                                b.Type == "expense" &&
                                b.BillDate >= startDate &&
                                b.BillDate <= endDate)
                    .SumAsync(b => (decimal?)b.Amount) ?? 0m;

                return Ok(new
                {
                    totalBills,
                    totalFamilies,
                    thisMonthBills,
                    thisMonthAmount
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取用户统计数据失败");
                return StatusCode(500, new { message = "获取用户统计数据失败" });
            }
        }
    }
}
