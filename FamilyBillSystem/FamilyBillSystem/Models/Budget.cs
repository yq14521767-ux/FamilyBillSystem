using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;
using FamilyBillSystem.Services;

namespace FamilyBillSystem.Models
{
    [Table("budgets")]
    public class Budget : ISoftDeletable  //预算表
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        public int FamilyId { get; set; }  //所属家庭Id

        [Required]
        public int CategoryId { get; set; }  //分类Id

        [Required]
        [Column(TypeName = "decimal(12,2)")]
        public decimal Amount { get; set; }  //预算金额

        [Required]
        [Column(TypeName = "varchar(50)")]
        public string Period { get; set; } = "monthly";  //预算周期

        [Required]
        public int Year { get; set; }  //年份

        public int? Month { get; set; }  //月份（月度预算时必填）

        [Column(TypeName = "decimal(12,2)")]
        public decimal UsedAmount { get; set; } = 0.00m;  //已使用金额

        [Column(TypeName = "decimal(5,2)")]
        public decimal AlertThreshold { get; set; } = 80.00m;  //预警阈值（百分比）

        [Column(TypeName = "tinyint(1)")]
        public bool IsActive { get; set; } = true;  //是否启用

        [MaxLength(200)]
        [Column(TypeName = "varchar(200)")]
        public string? Description { get; set; }  //描述

        [Required]
        public int CreatedBy { get; set; }  //创建者Id

        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [Column(TypeName = "datetime")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;  //创建时间   
        public DateTime UpdatedAt { get; set; } = DateTime.Now; //更新时间

        [Column(TypeName = "datetime")]
        public DateTime? DeletedAt { get; set; }  //删除时间

        // 计算属性
        [NotMapped]
        public decimal RemainingAmount => Amount - UsedAmount;

        [NotMapped]
        public decimal UsagePercentage => Amount > 0 ? (UsedAmount / Amount) * 100 : 0;

        [NotMapped]
        public bool IsOverBudget => UsedAmount > Amount;

        [NotMapped]
        public bool ShouldAlert => UsagePercentage >= AlertThreshold;

        // 导航属性
        [JsonIgnore]
        public virtual Family Family { get; set; }

        [JsonIgnore]
        public virtual Category Category { get; set; }

        [JsonIgnore]
        public virtual User Creator { get; set; }
    }
}