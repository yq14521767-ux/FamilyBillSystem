using System.ComponentModel.DataAnnotations;

namespace FamilyBillSystem.DTOs
{
    public class WeChatLoginRequest
    {
        [Required(ErrorMessage = "微信登录凭证不能为空")]
        public string Code { get; set; }

        public string? NickName { get; set; }

        public string? Avatar { get; set; }
    }
}
