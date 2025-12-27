using System.ComponentModel.DataAnnotations;

namespace FamilyBillSystem.DTOs
{
    public class RefreshTokenRequest
    {
        [Required(ErrorMessage = "RefreshToken不能为空")]
        public string RefreshToken { get; set; } = string.Empty;
    }
}
