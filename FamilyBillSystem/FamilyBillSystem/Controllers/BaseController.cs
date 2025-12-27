using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using FamilyBillSystem.DTOs;

namespace FamilyBillSystem.Controllers
{
    [ApiController]
    public abstract class BaseController : ControllerBase
    {
        /// <summary>
        /// 获取当前用户ID
        /// </summary>
        /// <returns>用户ID，如果未认证则返回null</returns>
        protected int? GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim != null && int.TryParse(userIdClaim.Value, out int userId))
            {
                return userId;
            }
            return null;
        }

        /// <summary>
        /// 获取当前用户ID，如果未认证则返回未授权响应
        /// </summary>
        /// <returns>用户ID</returns>
        protected ActionResult<int> GetCurrentUserIdOrUnauthorized()
        {
            var userId = GetCurrentUserId();
            if (userId == null)
            {
                return Unauthorized(ServiceResponse.Error("用户未认证"));
            }
            return userId.Value;
        }

        /// <summary>
        /// 获取当前用户名
        /// </summary>
        /// <returns>用户名</returns>
        protected string? GetCurrentUserName()
        {
            return User.FindFirst(ClaimTypes.Name)?.Value;
        }

        /// <summary>
        /// 获取当前用户邮箱
        /// </summary>
        /// <returns>用户邮箱</returns>
        protected string? GetCurrentUserEmail()
        {
            return User.FindFirst(ClaimTypes.Email)?.Value;
        }

        /// <summary>
        /// 创建成功响应
        /// </summary>
        /// <typeparam name="T">数据类型</typeparam>
        /// <param name="data">响应数据</param>
        /// <param name="message">响应消息</param>
        /// <returns>成功响应</returns>
        protected ActionResult<ServiceResponse<T>> Success<T>(T data, string message = "操作成功")
        {
            return Ok(ServiceResponse.CreateSuccess(data, message));
        }

        /// <summary>
        /// 创建成功响应（无数据）
        /// </summary>
        /// <param name="message">响应消息</param>
        /// <returns>成功响应</returns>
        protected ActionResult<ServiceResponse<object>> Success(string message = "操作成功")
        {
            return Ok(ServiceResponse.CreateSuccess(new { }, message));
        }

        /// <summary>
        /// 创建错误响应
        /// </summary>
        /// <param name="message">错误消息</param>
        /// <param name="statusCode">HTTP状态码</param>
        /// <returns>错误响应</returns>
        protected ActionResult<ServiceResponse<object>> Error(string message, int statusCode = 400)
        {
            return StatusCode(statusCode, ServiceResponse.Error(message));
        }

        /// <summary>
        /// 验证模型状态
        /// </summary>
        /// <returns>如果模型无效则返回错误响应，否则返回null</returns>
        protected ActionResult<ServiceResponse<object>>? ValidateModelState()
        {
            if (!ModelState.IsValid)
            {
                var errors = ModelState
                    .Where(x => x.Value.Errors.Count > 0)
                    .Select(x => new { Field = x.Key, Errors = x.Value.Errors.Select(e => e.ErrorMessage) })
                    .ToList();

                return BadRequest(ServiceResponse.Error("输入验证失败", errors));
            }
            return null;
        }

        /// <summary>
        /// 处理异常并返回统一的错误响应
        /// </summary>
        /// <param name="ex">异常</param>
        /// <param name="logger">日志记录器</param>
        /// <param name="defaultMessage">默认错误消息</param>
        /// <returns>错误响应</returns>
        protected ActionResult<ServiceResponse<object>> HandleException(Exception ex, ILogger logger, string defaultMessage = "操作失败")
        {
            logger.LogError(ex, defaultMessage);
            
            // 在开发环境中返回详细错误信息，生产环境中返回通用错误信息
            var message = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development" 
                ? ex.Message 
                : defaultMessage;

            return StatusCode(500, ServiceResponse.Error(message));
        }

        /// <summary>
        /// 验证分页参数
        /// </summary>
        /// <param name="page">页码</param>
        /// <param name="pageSize">页大小</param>
        /// <returns>验证结果</returns>
        protected (int validPage, int validPageSize) ValidatePaginationParams(int page = 1, int pageSize = 10)
        {
            var validPage = Math.Max(1, page);
            var validPageSize = Math.Min(Math.Max(1, pageSize), 100); // 限制最大页大小为100
            return (validPage, validPageSize);
        }
    }
}