using FamilyBillSystem.Data;
using FamilyBillSystem.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace FamilyBillSystem.Controllers
{
    [Route("api/notifications")]
    [ApiController]
    [Authorize]
    public class NotificationsController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly ILogger<NotificationsController> _logger;

        public NotificationsController(AppDbContext context, ILogger<NotificationsController> logger)
        {
            _context = context;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> GetNotifications(string? status = null, int page = 1, int pageSize = 20)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (userId == null)
                    return Unauthorized();

                if (page <= 0) page = 1;
                if (pageSize <= 0 || pageSize > 100) pageSize = 20;

                var userFamilyIds = await _context.FamilyMembers
                    .Where(fm => fm.UserId == userId && fm.Status == "active")
                    .Select(fm => fm.FamilyId)
                    .ToListAsync();

                var query = _context.Notifications.AsQueryable();

                query = query.Where(n =>
                    (n.UserId == userId) ||
                    (n.UserId == null && n.FamilyId.HasValue && userFamilyIds.Contains(n.FamilyId.Value)));

                if (!string.IsNullOrEmpty(status) && status != "all")
                {
                    status = status.ToLower();
                    if (status == "unread" || status == "read")
                    {
                        query = query.Where(n => n.Status == status);
                    }
                }

                var total = await query.CountAsync();

                var unreadCount = await query.Where(n => n.Status == "unread").CountAsync();

                var notifications = await query
                    .OrderByDescending(n => n.CreatedAt)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .Select(n => new NotificationListItem
                    {
                        Id = n.Id,
                        Title = n.Title,
                        Message = n.Message,
                        Status = n.Status,
                        CreatedAt = n.CreatedAt
                    })
                    .ToListAsync();

                return Ok(new
                {
                    data = notifications,
                    total,
                    page,
                    pageSize,
                    unreadCount
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取通知列表失败");
                return StatusCode(500, new { message = "获取通知列表失败" });
            }
        }

        [HttpPut("{id}/read")]
        public async Task<IActionResult> MarkAsRead(int id)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (userId == null)
                    return Unauthorized();

                var notification = await _context.Notifications.FindAsync(id);
                if (notification == null)
                    return NotFound(new { message = "通知不存在" });

                var userFamilyIds = await _context.FamilyMembers
                    .Where(fm => fm.UserId == userId && fm.Status == "active")
                    .Select(fm => fm.FamilyId)
                    .ToListAsync();

                var canAccess = (notification.UserId == userId) ||
                                (notification.UserId == null && notification.FamilyId.HasValue &&
                                 userFamilyIds.Contains(notification.FamilyId.Value));

                if (!canAccess)
                    return Forbid("您没有权限操作此通知");

                if (notification.Status != "read")
                {
                    notification.Status = "read";
                    await _context.SaveChangesAsync();
                }

                return Ok(new { message = "通知已标记为已读" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "标记通知为已读失败");
                return StatusCode(500, new { message = "标记通知为已读失败" });
            }
        }

        [HttpPut("read-all")]
        public async Task<IActionResult> MarkAllAsRead()
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

                var query = _context.Notifications.Where(n =>
                    (n.UserId == userId) ||
                    (n.UserId == null && n.FamilyId.HasValue && userFamilyIds.Contains(n.FamilyId.Value)));

                var unreadNotifications = await query.Where(n => n.Status == "unread").ToListAsync();
                if (unreadNotifications.Count == 0)
                {
                    return Ok(new { message = "没有未读通知", affected = 0 });
                }

                foreach (var n in unreadNotifications)
                {
                    n.Status = "read";
                }

                await _context.SaveChangesAsync();

                return Ok(new { message = "全部通知已标记为已读", affected = unreadNotifications.Count });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "标记全部通知为已读失败");
                return StatusCode(500, new { message = "标记全部通知为已读失败" });
            }
        }

        private int? GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return int.TryParse(userIdClaim, out var userId) ? userId : null;
        }
    }

    public class NotificationListItem
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }
}
