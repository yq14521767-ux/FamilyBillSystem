using System.ComponentModel.DataAnnotations;

namespace FamilyBillSystem.DTOs
{
    public class SendCodeRequest
    {
        [Required(ErrorMessage = "邮箱不能为空")]
        [EmailAddress(ErrorMessage = "邮箱格式不正确")]
        public string Email { get; set; }
    }
}
