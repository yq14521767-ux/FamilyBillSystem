using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;
using System.Text.Json.Serialization;
using FamilyBillSystem.Interfaces;

namespace FamilyBillSystem.Models
{
    public class Family : ISoftDeletable  //家庭表
    {
        public int Id { get; set; }

        [Required]
        [MaxLength(100)]
        [Column(TypeName = "varchar(100)")]
        public string Name { get; set; }  //组名称

        [Column(TypeName = "text")]
        public string Description { get; set; }  //描述

        [Required]
        [MaxLength(20)]
        [Column(TypeName = "varchar(20)")]
        public string InviteCode { get; set; }  //邀请码

        [Required]
        public int CreatorId { get; set; }  //创建者Id

        [Required]
        [Column(TypeName = "varchar(50)")]
        public string BudgetCycle { get; set; } = "monthly";  //预算周期，月，季，年

        [Column(TypeName = "int")]
        public int? MemberLimit { get; set; } = 10;  //成员数量

        [Column(TypeName = "text")]
        public string Settings { get; set; } = "{}";  //家庭设置

        [Required]
        [Column(TypeName = "varchar(50)")]
        public string Status { get; set; } = "active";  //状态，active-活跃，inactive-解散

        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [Column(TypeName = "datetime")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;  //创建时间        [Column(TypeName = "datetime")]
        public DateTime UpdatedAt { get; set; } = DateTime.Now;  //更新time

        [Column(TypeName = "datetime")]
        public DateTime? DeletedAt { get; set; }  //删除time

        // 导航属性
        [JsonIgnore]
        public virtual User Creator { get; set; }
        
        [JsonIgnore]
        public virtual ICollection<FamilyMember> Members { get; set; } = new List<FamilyMember>();
        
        [JsonIgnore]
        public virtual ICollection<Category> Categories { get; set; } = new List<Category>();
        
        [JsonIgnore]
        public virtual ICollection<Bill> Bills { get; set; } = new List<Bill>();
        
        [JsonIgnore]
        public virtual ICollection<Notification> Notifications { get; set; } = new List<Notification>();
        
        public virtual FamilyStats Stats { get; set; }

        // 处理设置 JSON 的辅助方法
        [NotMapped]
        public JsonDocument SettingsJson
        {
            get => JsonDocument.Parse(string.IsNullOrEmpty(Settings) ? "{}" : Settings);
            set => Settings = value != null ? JsonSerializer.Serialize(value) : "{}";
        }
    }
}
