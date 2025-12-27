using System.ComponentModel.DataAnnotations;

namespace FamilyBillSystem.DTOs
{
    public class UpdateProfileRequest
    {
        [StringLength(50, ErrorMessage = "昵称不能超过50个字符")]
        public string? Nickname { get; set; }
        
        [Range(0, 2, ErrorMessage = "性别参数错误")]
        public byte? Gender { get; set; }
        
        [StringLength(11, ErrorMessage = "手机号格式不正确")]
        [RegularExpression(@"^1[3-9]\d{9}$", ErrorMessage = "手机号格式不正确")]
        public string? Phone { get; set; }
    }
}
