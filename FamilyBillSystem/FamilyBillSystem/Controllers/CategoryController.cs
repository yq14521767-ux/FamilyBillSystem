using FamilyBillSystem.Data;
using FamilyBillSystem.DTOs;
using FamilyBillSystem.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace FamilyBillSystem.Controllers
{
    [Route("api/categories")]
    [ApiController]
    [Authorize]
    public class CategoryController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly ILogger<CategoryController> _logger;

        public CategoryController(AppDbContext context, ILogger<CategoryController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // GET: api/categories
        [HttpGet]
        public async Task<IActionResult> GetCategories(string? type = null)
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

                var query = _context.Categories
                    .Where(c => c.DeletedAt == null &&
                               c.Status == "active" &&
                               (c.FamilyId == null || userFamilyIds.Contains(c.FamilyId.Value)));

                // 按类型筛选，兼容数值型（1-收入，2-支出）和字符串型（income/expense）
                if (!string.IsNullOrEmpty(type))
                {
                    string? normalizedType = null;
                    var lowered = type.ToLower();
                    if (lowered == "1" || lowered == "income")
                    {
                        normalizedType = "income";
                    }
                    else if (lowered == "2" || lowered == "expense")
                    {
                        normalizedType = "expense";
                    }

                    if (normalizedType != null)
                    {
                        query = query.Where(c => c.Type == normalizedType);
                    }
                }

                var categories = await query
                    .OrderBy(c => c.SortOrder)
                    .ThenBy(c => c.Name)
                    .Select(c => new
                    {
                        id = c.Id,
                        name = c.Name,
                        // 前端使用数值型：1-收入，2-支出
                        type = c.Type == "income" ? 1 : 2,
                        icon = c.Icon,
                        color = c.Color,
                        parentId = c.ParentId,
                        familyId = c.FamilyId,
                        isSystem = c.FamilyId == null,
                        sortOrder = c.SortOrder
                    })
                    .ToListAsync();

                return Ok(new { data = categories });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取分类列表失败");
                return StatusCode(500, new { message = "获取分类列表失败" });
            }
        }

        // POST: api/categories
        [HttpPost]
        public async Task<IActionResult> CreateCategory([FromBody] CreateCategoryRequest request)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (userId == null)
                    return Unauthorized();

                // 确定分类归属家庭：优先使用前端传入的 FamilyId，否则默认使用当前用户所属的第一个家庭
                int? familyId = request.FamilyId;

                if (!familyId.HasValue)
                {
                    var firstFamilyId = await _context.FamilyMembers
                        .Where(fm => fm.UserId == userId && fm.Status == "active")
                        .Select(fm => fm.FamilyId)
                        .FirstOrDefaultAsync();

                    if (firstFamilyId == 0)
                    {
                        // 当前用户未加入任何家庭，不允许创建自定义分类
                        return BadRequest(new { message = "当前用户未加入任何家庭，无法创建自定义分类" });
                    }

                    familyId = firstFamilyId;
                }

                // 验证用户是否属于指定家庭
                var isFamilyMemberOfTarget = await _context.FamilyMembers
                    .AnyAsync(fm => fm.UserId == userId && fm.FamilyId == familyId.Value && fm.Status == "active");

                if (!isFamilyMemberOfTarget)
                    return Forbid("您不是该家庭的成员");

                // 兼容前端传入的类型：1/2 或 income/expense
                string? typeValue = null;
                if (!string.IsNullOrWhiteSpace(request.Type))
                {
                    var lowered = request.Type.ToLower();
                    if (lowered == "1" || lowered == "income")
                    {
                        typeValue = "income";
                    }
                    else if (lowered == "2" || lowered == "expense")
                    {
                        typeValue = "expense";
                    }
                }

                if (typeValue == null)
                {
                    return BadRequest(new { message = "无效的分类类型" });
                }

                var category = new Category
                {
                    Name = request.Name,
                    Type = typeValue,
                    Icon = request.Icon ?? string.Empty,
                    Color = string.IsNullOrEmpty(request.Color) ? "#666666" : request.Color,
                    ParentId = request.ParentId,
                    FamilyId = familyId,
                    SortOrder = request.SortOrder ?? 0,
                    CreatedAt = DateTime.Now,
                    UpdatedAt = DateTime.Now
                };

                _context.Categories.Add(category);
                await _context.SaveChangesAsync();

                // 返回创建后的完整分类对象，供前端直接使用
                var result = new
                {
                    id = category.Id,
                    name = category.Name,
                    type = category.Type == "income" ? 1 : 2,
                    icon = category.Icon,
                    color = category.Color,
                    parentId = category.ParentId,
                    familyId = category.FamilyId,
                    isSystem = category.FamilyId == null,
                    sortOrder = category.SortOrder
                };

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "创建分类失败");
                return StatusCode(500, new { message = "创建分类失败" });
            }
        }

        // PUT: api/category/{id}
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateCategory(int id, [FromBody] UpdateCategoryRequest request)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (userId == null)
                    return Unauthorized();

                var category = await _context.Categories.FindAsync(id);
                if (category == null || category.DeletedAt != null)
                    return NotFound("分类不存在");

                // 系统分类不允许修改
                if (category.FamilyId == null)
                    return BadRequest(new { message = "系统分类不允许修改" });

                // 验证权限
                var isFamilyMember = await _context.FamilyMembers
                    .AnyAsync(fm => fm.UserId == userId && fm.FamilyId == category.FamilyId && fm.Status == "active");

                if (!isFamilyMember)
                    return Forbid("您没有权限修改此分类");

                // 更新分类信息
                if (!string.IsNullOrEmpty(request.Name))
                    category.Name = request.Name;
                if (!string.IsNullOrEmpty(request.Icon))
                    category.Icon = request.Icon;
                if (!string.IsNullOrEmpty(request.Color))
                    category.Color = request.Color;
                if (request.SortOrder.HasValue)
                    category.SortOrder = request.SortOrder.Value;

                category.UpdatedAt = DateTime.Now;

                await _context.SaveChangesAsync();

                // 返回更新后的完整分类对象，供前端直接使用
                var result = new
                {
                    id = category.Id,
                    name = category.Name,
                    type = category.Type == "income" ? 1 : 2,
                    icon = category.Icon,
                    color = category.Color,
                    parentId = category.ParentId,
                    familyId = category.FamilyId,
                    isSystem = category.FamilyId == null,
                    sortOrder = category.SortOrder
                };

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "更新分类失败");
                return StatusCode(500, new { message = "更新分类失败" });
            }
        }

        // DELETE: api/category/{id}
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteCategory(int id)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (userId == null)
                    return Unauthorized();

                var category = await _context.Categories.FindAsync(id);
                if (category == null || category.DeletedAt != null)
                    return NotFound("分类不存在");

                // 系统分类不允许删除
                if (category.FamilyId == null)
                    return BadRequest(new { message = "系统分类不允许删除" });

                // 验证权限
                var isFamilyMember = await _context.FamilyMembers
                    .AnyAsync(fm => fm.UserId == userId && fm.FamilyId == category.FamilyId && fm.Status == "active");

                if (!isFamilyMember)
                    return Forbid("您没有权限删除此分类");

                // 查找使用此分类的账单
                var bills = await _context.Bills
                    .Where(b => b.CategoryId == id && b.DeletedAt == null)
                    .ToListAsync();

                if (bills.Any())
                {
                    // 找到对应类型的“其他”系统分类
                    var isExpense = category.Type == "expense";
                    var otherName = isExpense ? "其他支出" : "其他收入";

                    var fallbackCategory = await _context.Categories
                        .FirstOrDefaultAsync(c =>
                            c.FamilyId == null &&
                            c.Type == category.Type &&
                            c.Name == otherName &&
                            c.DeletedAt == null);

                    if (fallbackCategory == null)
                    {
                        return BadRequest(new { message = "系统分类中未找到“其他”分类，无法自动迁移账单" });
                    }

                    // 将账单迁移到“其他”分类
                    foreach (var bill in bills)
                    {
                        bill.CategoryId = fallbackCategory.Id;
                        bill.UpdatedAt = DateTime.Now;
                    }
                }

                // 软删除
                category.DeletedAt = DateTime.Now;
                category.UpdatedAt = DateTime.Now;

                await _context.SaveChangesAsync();

                return Ok(new { message = "分类删除成功" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "删除分类失败");
                return StatusCode(500, new { message = "删除分类失败" });
            }
        }

        private int? GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return int.TryParse(userIdClaim, out int userId) ? userId : null;
        }
    }

    // DTO类
    public class CreateCategoryRequest
    {
        public string Name { get; set; } = "";
        public string Type { get; set; } = ""; // income | expense
        public string? Icon { get; set; }
        public string? Color { get; set; }
        public int? ParentId { get; set; }
        public int? FamilyId { get; set; }
        public int? SortOrder { get; set; }
    }

    public class UpdateCategoryRequest
    {
        public string? Name { get; set; }
        public string? Icon { get; set; }
        public string? Color { get; set; }
        public int? SortOrder { get; set; }
    }
}