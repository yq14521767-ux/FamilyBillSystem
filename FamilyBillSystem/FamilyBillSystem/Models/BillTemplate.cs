using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace FamilyBillSystem.Models
{
    [Table("bill_templates")]
    public class BillTemplate  //账单模板表
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        public int FamilyId { get; set; }  //所属家庭Id

        [Required]
        public int UserId { get; set; }  //创建者Id

        public int? CategoryId { get; set; }  //分类Id

        [Required]
        [MaxLength(100)]
        [Column(TypeName = "varchar(100)")]
        public string Name { get; set; }  //模板名称

        [Required]
        [Column(TypeName = "varchar(50)")]
        public string Type { get; set; } = "expense";  //类型

        [Column(TypeName = "decimal(12,2)")]
        public decimal? DefaultAmount { get; set; }  //默认金额

        [MaxLength(200)]
        [Column(TypeName = "varchar(200)")]
        public string? DefaultDescription { get; set; }  //默认描述

        [MaxLength(50)]
        [Column(TypeName = "varchar(50)")]
        public string? DefaultPaymentMethod { get; set; }  //默认支付方式

        [Column(TypeName = "text")]
        public string? DefaultRemark { get; set; }  //默认说明

        [Column(TypeName = "tinyint(1)")]
        public bool IsActive { get; set; } = true;  //是否启用

        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [Column(TypeName = "datetime")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;  //创建时间        [Column(TypeName = "datetime")]
        public DateTime UpdatedAt { get; set; } = DateTime.Now; //更新时间

        [Column(TypeName = "datetime")]
        public DateTime? DeletedAt { get; set; }  //删除时间

        // 导航属性
        [JsonIgnore]
        public virtual Family Family { get; set; }

        [JsonIgnore]
        public virtual User User { get; set; }

        [JsonIgnore]
        public virtual Category? Category { get; set; }
    }
}