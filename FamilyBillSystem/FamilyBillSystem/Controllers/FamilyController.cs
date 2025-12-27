using FamilyBillSystem.Data;
using FamilyBillSystem.DTOs;
using FamilyBillSystem.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace FamilyBillSystem.Controllers
{
    [Route("api/families")]
    [ApiController]
    [Authorize]
    public class FamilyController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly ILogger<FamilyController> _logger;

        public FamilyController(AppDbContext context, ILogger<FamilyController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // GET: api/families
        [HttpGet]
        public async Task<IActionResult> GetUserFamilies()
        {
            try
            {
                var userId = GetCurrentUserId();
                if (userId == null)
                    return Unauthorized();

                var families = await _context.FamilyMembers
                    .Include(fm => fm.Family)
                        .ThenInclude(f => f.Creator)
                    .Include(fm => fm.Family)
                        .ThenInclude(f => f.Members)
                            .ThenInclude(m => m.User)
                    .Where(fm => fm.UserId == userId && fm.Status == "active")
                    .Select(fm => new
                    {
                        fm.Family.Id,
                        fm.Family.Name,
                        fm.Family.Description,
                        fm.Family.InviteCode,
                        fm.Family.CreatedAt,
                        Creator = new
                        {
                            fm.Family.Creator.Id,
                            fm.Family.Creator.Nickname,
                            fm.Family.Creator.AvatarUrl
                        },
                        MemberCount = fm.Family.Members.Count(m => m.Status == "active"),
                        Role = fm.Role,
                        JoinedAt = fm.JoinedAt
                    })
                    .ToListAsync();

                return Ok(new { data = families });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取用户家庭列表失败");
                return StatusCode(500, new { message = "获取家庭列表失败" });
            }
        }

        // POST: api/families
        [HttpPost]
        public async Task<IActionResult> CreateFamily([FromBody] CreateFamilyRequest request)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (userId == null)
                    return Unauthorized();

                if (string.IsNullOrWhiteSpace(request.Name))
                    return BadRequest(new { message = "家庭名称不能为空" });

                // 生成邀请码
                var inviteCode = GenerateInviteCode();

                var family = new Family
                {
                    Name = request.Name,
                    Description = request.Description ?? "",
                    InviteCode = inviteCode,
                    CreatorId = userId.Value,
                    CreatedAt = DateTime.Now,
                    UpdatedAt = DateTime.Now
                };

                _context.Families.Add(family);
                await _context.SaveChangesAsync();

                // 创建者自动成为管理员
                var memberRecord = new FamilyMember
                {
                    FamilyId = family.Id,
                    UserId = userId.Value,
                    Role = "admin",
                    Status = "active",
                    JoinedAt = DateTime.Now,
                    Nickname = request.CreatorNickname ?? "管理员"
                };

                _context.FamilyMembers.Add(memberRecord);
                await _context.SaveChangesAsync();
                // 统计当前家庭成员数量（仅统计 active 状态）
                var memberCount = await _context.FamilyMembers
                    .CountAsync(fm => fm.FamilyId == family.Id && fm.Status == "active");

                return Ok(new
                {
                    message = "家庭创建成功",
                    data = new
                    {
                        family.Id,
                        family.Name,
                        family.Description,
                        family.InviteCode,
                        family.CreatedAt,
                        memberCount
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "创建家庭失败");
                return StatusCode(500, new { message = "创建家庭失败" });
            }
        }

        // POST: api/families/join
        [HttpPost("join")]
        public async Task<IActionResult> JoinFamily([FromBody] JoinFamilyRequest request)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (userId == null)
                    return Unauthorized();

                if (string.IsNullOrWhiteSpace(request.InviteCode))
                    return BadRequest(new { message = "邀请码不能为空" });

                var family = await _context.Families
                    .FirstOrDefaultAsync(f => f.InviteCode == request.InviteCode && f.DeletedAt == null);

                if (family == null)
                    return BadRequest(new { message = "邀请码无效" });

                // 检查家庭成员数量限制
                var currentMemberCount = await _context.FamilyMembers
                    .CountAsync(fm => fm.FamilyId == family.Id && fm.Status == "active");

                if (family.MemberLimit.HasValue && currentMemberCount >= family.MemberLimit.Value)
                    return BadRequest(new { message = $"家庭成员数量已达到上限({family.MemberLimit.Value}人)" });

                // 检查是否已经是成员
                var existingMember = await _context.FamilyMembers
                    .FirstOrDefaultAsync(fm => fm.FamilyId == family.Id && fm.UserId == userId);

                if (existingMember != null)
                {
                    if (existingMember.Status == "active")
                        return BadRequest(new { message = "您已经是该家庭的成员" });
                    
                    // 重新激活成员
                    existingMember.Status = "active";
                    existingMember.JoinedAt = DateTime.Now;
                    existingMember.Nickname = request.Nickname ?? "成员";
                }
                else
                {
                    // 创建新成员记录
                    var memberRecord = new FamilyMember
                    {
                        FamilyId = family.Id,
                        UserId = userId.Value,
                        Role = "member",
                        Status = "active",
                        JoinedAt = DateTime.Now,
                        Nickname = request.Nickname ?? "成员"
                    };

                    _context.FamilyMembers.Add(memberRecord);
                }

                await _context.SaveChangesAsync();
                
                // 为家庭成员加入创建通知（家庭广播）
                var joinUser = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId.Value);
                var joinDisplayName = request.Nickname ?? joinUser?.Nickname ?? "成员";
                var joinMessage = $"{joinDisplayName} 加入了家庭 {family.Name}";
                _context.Notifications.Add(new Notification
                {
                    FamilyId = family.Id,
                    Title = "家庭成员加入",
                    Message = joinMessage,
                    Status = "unread",
                    CreatedAt = DateTime.Now
                });

                await _context.SaveChangesAsync();
                // 统计当前家庭成员数量（仅统计 active 状态）
                var memberCount = await _context.FamilyMembers
                    .CountAsync(fm => fm.FamilyId == family.Id && fm.Status == "active");

                return Ok(new
                {
                    message = "加入家庭成功",
                    data = new
                    {
                        family.Id,
                        family.Name,
                        family.Description,
                        family.InviteCode,
                        memberCount
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "加入家庭失败");
                return StatusCode(500, new { message = "加入家庭失败" });
            }
        }

        // POST: api/families/{id}/invite-code
        [HttpPost("{id}/invite-code")]
        public async Task<IActionResult> GenerateFamilyInviteCode(int id)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (userId == null)
                    return Unauthorized();

                // 仅允许该家庭的管理员生成邀请码
                var member = await _context.FamilyMembers
                    .Include(fm => fm.Family)
                    .Include(fm => fm.User)
                    .FirstOrDefaultAsync(fm => fm.FamilyId == id && fm.UserId == userId && fm.Status == "active");

                if (member == null)
                    return BadRequest(new { message = "您不是该家庭的成员" });

                if (member.Role != "admin")
                    return Forbid("只有管理员可以生成邀请码");

                var family = member.Family;
                if (family == null || family.DeletedAt != null)
                    return BadRequest(new { message = "家庭不存在或已被删除" });

                // 生成新的唯一邀请码
                string inviteCode;
                int attempts = 0;
                do
                {
                    inviteCode = GenerateInviteCode();
                    attempts++;
                } while (await _context.Families.AnyAsync(f => f.InviteCode == inviteCode) && attempts < 5);

                family.InviteCode = inviteCode;
                family.UpdatedAt = DateTime.Now;

                await _context.SaveChangesAsync();

                return Ok(new { inviteCode = family.InviteCode });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "生成家庭邀请码失败");
                return StatusCode(500, new { message = "生成邀请码失败" });
            }
        }

        // GET: api/families/{id}/members
        [HttpGet("{id}/members")]
        public async Task<IActionResult> GetFamilyMembers(int id)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (userId == null)
                    return Unauthorized();

                // 验证用户是否是该家庭成员
                var isMember = await _context.FamilyMembers
                    .AnyAsync(fm => fm.FamilyId == id && fm.UserId == userId && fm.Status == "active");

                if (!isMember)
                    return Forbid("您不是该家庭的成员");

                var members = await _context.FamilyMembers
                    .Include(fm => fm.User)
                    .Where(fm => fm.FamilyId == id && fm.Status == "active")
                    .Select(fm => new
                    {
                        id = fm.Id,
                        userId = fm.UserId,
                        joinedAt = fm.JoinedAt,
                        // 供前端直接使用的字段
                        avatar = fm.User.AvatarUrl,
                        nickName = fm.Nickname ?? fm.User.Nickname,
                        username = fm.User.Email,
                        // 数值型角色：1-管理员，0-成员
                        role = fm.Role == "admin" ? 1 : 0,
                        // 保留嵌套的用户信息，便于后续扩展
                        user = new
                        {
                            id = fm.User.Id,
                            nickname = fm.User.Nickname,
                            avatarUrl = fm.User.AvatarUrl,
                            email = fm.User.Email
                        }
                    })
                    .ToListAsync();

                return Ok(new { data = members });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取家庭成员失败");
                return StatusCode(500, new { message = "获取家庭成员失败" });
            }
        }

        // POST: api/families/{familyId}/members/{memberId}/role
        [HttpPost("{familyId}/members/{memberId}/role")]
        public async Task<IActionResult> UpdateMemberRole(int familyId, int memberId, [FromBody] UpdateMemberRoleRequest request)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (userId == null)
                    return Unauthorized();

                var currentMember = await _context.FamilyMembers
                    .FirstOrDefaultAsync(fm => fm.FamilyId == familyId && fm.UserId == userId && fm.Status == "active");

                if (currentMember == null)
                    return Forbid("您不是该家庭的成员");

                if (currentMember.Role != "admin")
                    return Forbid("只有管理员可以修改成员角色");

                var targetMember = await _context.FamilyMembers
                    .FirstOrDefaultAsync(fm => fm.FamilyId == familyId && fm.Id == memberId && fm.Status == "active");

                if (targetMember == null)
                    return NotFound(new { message = "成员不存在或已退出家庭" });

                // 不允许将自己从管理员降级并导致家庭没有任何管理员
                if (!request.IsAdmin && targetMember.UserId == userId)
                {
                    var otherAdminExists = await _context.FamilyMembers
                        .AnyAsync(fm => fm.FamilyId == familyId && fm.UserId != userId && fm.Status == "active" && fm.Role == "admin");

                    if (!otherAdminExists)
                    {
                        return BadRequest(new { message = "请先设置其他成员为管理员后再取消自己的管理员身份" });
                    }
                }

                targetMember.Role = request.IsAdmin ? "admin" : "member";
                await _context.SaveChangesAsync();

                return Ok(new
                {
                    message = "成员角色已更新",
                    role = targetMember.Role
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "更新成员角色失败");
                return StatusCode(500, new { message = "更新成员角色失败" });
            }
        }

        // POST: api/families/{familyId}/members/{memberId}/remove
        [HttpPost("{familyId}/members/{memberId}/remove")]
        public async Task<IActionResult> RemoveMember(int familyId, int memberId)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (userId == null)
                    return Unauthorized();

                var currentMember = await _context.FamilyMembers
                    .Include(fm => fm.Family)
                    .FirstOrDefaultAsync(fm => fm.FamilyId == familyId && fm.UserId == userId && fm.Status == "active");

                if (currentMember == null)
                    return Forbid("您不是该家庭的成员");

                if (currentMember.Role != "admin")
                    return Forbid("只有管理员可以移除成员");

                var targetMember = await _context.FamilyMembers
                    .Include(fm => fm.User)
                    .FirstOrDefaultAsync(fm => fm.FamilyId == familyId && fm.Id == memberId && fm.Status == "active");

                if (targetMember == null)
                    return NotFound(new { message = "成员不存在或已退出家庭" });

                // 不允许通过此接口移除自己，请使用退出家庭接口
                if (targetMember.UserId == userId)
                    return BadRequest(new { message = "如需退出家庭，请使用退出家庭功能" });

                targetMember.Status = "left";
                targetMember.LeftAt = DateTime.Now;

                // 为家庭成员被移除创建通知（家庭广播）
                var removedDisplayName = targetMember.Nickname ?? targetMember.User?.Nickname ?? "成员";
                var removedFamilyName = currentMember.Family.Name;
                var removedMessage = $"{removedDisplayName} 已被移出家庭 {removedFamilyName}";
                _context.Notifications.Add(new Notification
                {
                    FamilyId = familyId,
                    Title = "家庭成员移除",
                    Message = removedMessage,
                    Status = "unread",
                    CreatedAt = DateTime.Now
                });

                await _context.SaveChangesAsync();

                return Ok(new { message = "成员已移除" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "移除家庭成员失败");
                return StatusCode(500, new { message = "移除成员失败" });
            }
        }

        // PUT: api/families/{familyId}/members/{memberId}/nickname
        [HttpPut("{familyId}/members/{memberId}/nickname")]
        public async Task<IActionResult> UpdateMemberNickname(int familyId, int memberId, [FromBody] UpdateMemberNicknameRequest request)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (userId == null)
                    return Unauthorized();

                var currentMember = await _context.FamilyMembers
                    .FirstOrDefaultAsync(fm => fm.FamilyId == familyId && fm.UserId == userId && fm.Status == "active");

                if (currentMember == null)
                    return Forbid("您不是该家庭的成员");

                var targetMember = await _context.FamilyMembers
                    .FirstOrDefaultAsync(fm => fm.FamilyId == familyId && fm.Id == memberId && fm.Status == "active");

                if (targetMember == null)
                    return NotFound(new { message = "成员不存在或已退出家庭" });

                // 普通成员只能修改自己的昵称，管理员可以修改任何成员
                if (targetMember.UserId != userId && currentMember.Role != "admin")
                    return Forbid("只有管理员可以修改其他成员的昵称");

                var nickname = (request.Nickname ?? "").Trim();
                targetMember.Nickname = string.IsNullOrWhiteSpace(nickname) ? null : nickname;

                await _context.SaveChangesAsync();

                return Ok(new
                {
                    message = "成员昵称已更新",
                    nickname = targetMember.Nickname
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "更新成员昵称失败");
                return StatusCode(500, new { message = "更新成员昵称失败" });
            }
        }

        [HttpPost("{id}/leave")] 
        public async Task<IActionResult> LeaveFamily(int id)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (userId == null)
                    return Unauthorized();

                var member = await _context.FamilyMembers
                    .Include(fm => fm.Family)
                    .FirstOrDefaultAsync(fm => fm.FamilyId == id && fm.UserId == userId && fm.Status == "active");

                if (member == null)
                    return BadRequest(new { message = "您不是该家庭的成员" });

                // 检查是否是创建者
                if (member.Family.CreatorId == userId)
                {
                    // 检查是否还有其他成员
                    var otherMembersCount = await _context.FamilyMembers
                        .CountAsync(fm => fm.FamilyId == id && fm.UserId != userId && fm.Status == "active");

                    if (otherMembersCount > 0)
                        return BadRequest(new { message = "作为创建者，请先转让管理权或删除家庭" });
                }

                member.Status = "left";
                member.LeftAt = DateTime.Now;

                // 为家庭成员退出创建通知（家庭广播）
                var leaveDisplayName = member.Nickname ?? member.User?.Nickname ?? "成员";
                var leaveFamilyName = member.Family.Name;
                var leaveMessage = $"{leaveDisplayName} 退出了家庭 {leaveFamilyName}";
                _context.Notifications.Add(new Notification
                {
                    FamilyId = id,
                    Title = "家庭成员退出",
                    Message = leaveMessage,
                    Status = "unread",
                    CreatedAt = DateTime.Now
                });

                await _context.SaveChangesAsync();

                return Ok(new { message = "已退出家庭" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "退出家庭失败");
                return StatusCode(500, new { message = "退出家庭失败" });
            }
        }

        // PUT: api/families/{id}
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateFamily(int id, [FromBody] UpdateFamilyRequest request)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (userId == null)
                    return Unauthorized();

                var member = await _context.FamilyMembers
                    .Include(fm => fm.Family)
                    .FirstOrDefaultAsync(fm => fm.FamilyId == id && fm.UserId == userId && fm.Status == "active");

                if (member == null)
                    return BadRequest(new { message = "您不是该家庭的成员" });

                if (member.Role != "admin")
                    return Forbid("只有管理员可以编辑家庭信息");

                var family = member.Family;
                if (family == null || family.DeletedAt != null)
                    return BadRequest(new { message = "家庭不存在或已被删除" });

                var oldName = family.Name;
                var oldDescription = family.Description;

                if (!string.IsNullOrWhiteSpace(request.Name))
                {
                    family.Name = request.Name.Trim();
                }

                if (request.Description != null)
                {
                    family.Description = request.Description.Trim();
                }

                family.UpdatedAt = DateTime.Now;

                // 如有实际变更，则为家庭信息编辑创建通知（家庭广播）
                var nameChanged = oldName != family.Name;
                var descChanged = (oldDescription ?? string.Empty) != (family.Description ?? string.Empty);
                if (nameChanged || descChanged)
                {
                    string updateMessage;
                    if (nameChanged && descChanged)
                    {
                        updateMessage = $"家庭 {family.Name} 名称已修改，简介已更新";
                    }
                    else if (nameChanged)
                    {
                        updateMessage = $"家庭 {family.Name} 名称已修改为 {family.Name}";
                    }
                    else
                    {
                        updateMessage = $"家庭 {family.Name} 简介已更新";
                    }

                    _context.Notifications.Add(new Notification
                    {
                        FamilyId = family.Id,
                        Title = "家庭信息更新",
                        Message = updateMessage,
                        Status = "unread",
                        CreatedAt = DateTime.Now
                    });
                }

                await _context.SaveChangesAsync();

                var memberCount = await _context.FamilyMembers
                    .CountAsync(fm => fm.FamilyId == family.Id && fm.Status == "active");

                return Ok(new
                {
                    message = "家庭信息已更新",
                    data = new
                    {
                        family.Id,
                        family.Name,
                        family.Description,
                        family.InviteCode,
                        family.CreatedAt,
                        memberCount,
                        role = member.Role
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "更新家庭信息失败");
                return StatusCode(500, new { message = "更新家庭信息失败" });
            }
        }

        // DELETE: api/families/{id}
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteFamily(int id)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (userId == null)
                    return Unauthorized();

                var member = await _context.FamilyMembers
                    .Include(fm => fm.Family)
                    .FirstOrDefaultAsync(fm => fm.FamilyId == id && fm.UserId == userId && fm.Status == "active");

                if (member == null)
                    return BadRequest(new { message = "您不是该家庭的成员" });

                if (member.Role != "admin")
                    return Forbid("只有管理员可以删除家庭");

                var family = member.Family;
                if (family == null || family.DeletedAt != null)
                    return BadRequest(new { message = "家庭不存在或已被删除" });

                family.Status = "inactive";
                family.DeletedAt = DateTime.Now;
                family.UpdatedAt = DateTime.Now;

                var activeMembers = await _context.FamilyMembers
                    .Where(fm => fm.FamilyId == id && fm.Status == "active")
                    .ToListAsync();

                foreach (var m in activeMembers)
                {
                    m.Status = "left";
                    m.LeftAt = DateTime.Now;
                }

                await _context.SaveChangesAsync();

                return Ok(new { message = "家庭已删除" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "删除家庭失败");
                return StatusCode(500, new { message = "删除家庭失败" });
            }
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

        private string GenerateInviteCode()
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            var random = new Random();
            return new string(Enumerable.Repeat(chars, 6)
                .Select(s => s[random.Next(s.Length)]).ToArray());
        }
    }

    public class CreateFamilyRequest
    {
        public string Name { get; set; } = "";
        public string? Description { get; set; }
        public string? CreatorNickname { get; set; }
    }

    public class JoinFamilyRequest
    {
        public string InviteCode { get; set; } = "";
        public string? Nickname { get; set; }
    }

    public class UpdateFamilyRequest
    {
        public string? Name { get; set; }
        public string? Description { get; set; }
    }

    public class UpdateMemberRoleRequest
    {
        public bool IsAdmin { get; set; }
    }

    public class UpdateMemberNicknameRequest
    {
        public string? Nickname { get; set; }
    }
}