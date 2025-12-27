using System.ComponentModel.DataAnnotations;

namespace FamilyBillSystem.DTOs
{
    public class RegisterRequest
    {
        [Required(ErrorMessage = "邮箱不能为空")]
        [EmailAddress(ErrorMessage = "邮箱格式不正确")]
        public string Email { get; set; }

        [Required(ErrorMessage = "验证码不能为空")]
        [StringLength(6, MinimumLength = 6, ErrorMessage = "验证码必须为6位数字")]
        public string VerificationCode { get; set; }

        [Required(ErrorMessage = "密码不能为空")]
        [StringLength(100, MinimumLength = 6, ErrorMessage = "密码长度必须在6-100个字符之间")]
        public string Password { get; set; }

        public string? NickName { get; set; }
    }
}
