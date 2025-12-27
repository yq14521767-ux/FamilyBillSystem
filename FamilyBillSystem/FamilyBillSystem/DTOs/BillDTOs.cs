using System.ComponentModel.DataAnnotations;

namespace FamilyBillSystem.DTOs
{
    public class CreateBillRequest
    {
        [Required(ErrorMessage = "家庭ID不能为空")]
        [Range(1, int.MaxValue, ErrorMessage = "家庭ID必须大于0")]
        public int FamilyId { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "分类ID必须大于0")]
        public int? CategoryId { get; set; }

        [Required(ErrorMessage = "账单类型不能为空")]
        [RegularExpression("^(income|expense)$", ErrorMessage = "账单类型只能是income或expense")]
        public string Type { get; set; } = "";

        [Required(ErrorMessage = "金额不能为空")]
        [Range(0.01, 999999999.99, ErrorMessage = "金额必须在0.01到999999999.99之间")]
        public decimal Amount { get; set; }

        [StringLength(200, ErrorMessage = "描述不能超过200个字符")]
        public string? Description { get; set; }

        [StringLength(50, ErrorMessage = "支付方式不能超过50个字符")]
        public string? PaymentMethod { get; set; }

        [Required(ErrorMessage = "账单日期不能为空")]
        public DateTime BillDate { get; set; }

        [StringLength(500, ErrorMessage = "标签不能超过500个字符")]
        public string? Remark { get; set; }
    }

    public class UpdateBillRequest
    {
        public int? CategoryId { get; set; }

        [StringLength(200)]
        public string? Description { get; set; }

        [Range(0.01, double.MaxValue, ErrorMessage = "金额必须大于0")]
        public decimal? Amount { get; set; }

        [StringLength(50)]
        public string? PaymentMethod { get; set; }

        public DateTime? BillDate { get; set; }

        public string? Remark { get; set; }
    }

    public class BillListResponse
    {
        public int Id { get; set; }
        public string Type { get; set; } = "";
        public decimal Amount { get; set; }
        public string Description { get; set; } = "";
        public DateTime BillDate { get; set; }
        public string PaymentMethod { get; set; } = "";
        public string Status { get; set; } = "";
        public DateTime CreatedAt { get; set; }
        public CategoryInfo? Category { get; set; }
        public UserInfo User { get; set; } = new();
    }

    public class CategoryInfo
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public string Icon { get; set; } = "";
        public string Color { get; set; } = "";
    }

    public class UserInfo
    {
        public int Id { get; set; }
        public string Nickname { get; set; } = "";
        public string AvatarUrl { get; set; } = "";
    }

    public class MonthlyStatisticsResponse
    {
        public int Year { get; set; }
        public int Month { get; set; }
        public decimal TotalIncome { get; set; }
        public decimal TotalExpense { get; set; }
        public decimal Balance { get; set; }
        public int BillCount { get; set; }
        public List<CategoryStatistics> CategoryStats { get; set; } = new();
    }

    public class CategoryStatistics
    {
        public string Type { get; set; } = "";
        public int? CategoryId { get; set; }
        public string CategoryName { get; set; } = "";
        public string CategoryIcon { get; set; } = "";
        public string CategoryColor { get; set; } = "";
        public decimal Amount { get; set; }
        public int Count { get; set; }
    }
}