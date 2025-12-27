using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;
using System.Text.Json.Serialization;
using FamilyBillSystem.Interfaces;

namespace FamilyBillSystem.Models
{
    [Table("bills")]
    public class Bill : ISoftDeletable  //账单表
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        public int FamilyId { get; set; }  //所属家庭Id

        [Required]
        public int UserId { get; set; }  //操作人Id

        public int? CategoryId { get; set; }  //所属分类Id

        [Required]
        [Column(TypeName = "varchar(50)")]
        public string Type { get; set; }  //收支类型

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        [Range(0.01, double.MaxValue, ErrorMessage = "金额必须大于0")]
        public decimal Amount { get; set; }  //总金额

        [MaxLength(255)]
        [Column(TypeName = "varchar(255)")]
        public string? Description { get; set; }  //备注描述

        [MaxLength(50)]
        [Column(TypeName = "varchar(50)")]
        public string PaymentMethod { get; set; }  //支付方式,微信、支付宝、现金、信用卡、其他

        [Column(TypeName = "text")]
        public string? Remark { get; set; }  //详细说明

        [Required]
        [Column(TypeName = "date")]
        public DateTime BillDate { get; set; } = DateTime.Today;  //收支日期



        [Required]
        [Column(TypeName = "varchar(50)")]
        public string Status { get; set; } = "confirmed"; //状态，已确认，已删除

        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [Column(TypeName = "datetime")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;  //创建时间        [Column(TypeName = "datetime")]
        public DateTime UpdatedAt { get; set; } = DateTime.Now;  //更新时间

        [Column(TypeName = "datetime")]
        public DateTime? DeletedAt { get; set; }  //删除时间

        // 导航属性
        [JsonIgnore]
        public virtual Family Family { get; set; }

        [JsonIgnore]
        public virtual User User { get; set; }

        [JsonIgnore]
        public virtual Category Category { get; set; }


    }
}