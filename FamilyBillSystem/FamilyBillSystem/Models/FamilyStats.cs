using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace FamilyBillSystem.Models
{
    [Table("family_stats")]
    public class FamilyStats  //家庭财务统计表
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        public int FamilyId { get; set; }  //所属家庭Id

        [Required]
        [Column(TypeName = "decimal(12,2)")]
        public decimal TotalIncome { get; set; } = 0.00m;  //家庭总收入

        [Required]
        [Column(TypeName = "decimal(12,2)")]
        public decimal TotalExpense { get; set; } = 0.00m;  //总支出        [Column(TypeName = "datetime")]
        public DateTime LastUpdated { get; set; } = DateTime.Now;  //最后更新时间

        //导航属性
        [JsonIgnore]
        public virtual Family Family { get; set; }

        // 获取余额的辅助属性
        [NotMapped]
        public decimal Balance => TotalIncome - TotalExpense;
    }
}