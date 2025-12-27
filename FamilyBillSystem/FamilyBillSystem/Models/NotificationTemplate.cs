using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace FamilyBillSystem.Models
{
    [Table("notification_templates")]
    public class NotificationTemplate  //通知模板表
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        [MaxLength(50)]
        [Column(TypeName = "varchar(50)")]
        public string Code { get; set; } // 模板编码

        [Required]
        [MaxLength(100)]
        [Column(TypeName = "varchar(100)")]
        public string Title { get; set; }  //通知标题

        [Required]
        [Column(TypeName = "text")]
        public string Content { get; set; } // 通知内容，可含 {{user_name}}, {{family_name}}, {{amount}}

        [Required]
        [Column(TypeName = "varchar(50)")]
        public string Type { get; set; } = "system"; // 模板类型，系统内置，用户自定义

        public int? CreatedBy { get; set; }  //创建人Id

        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [Column(TypeName = "datetime")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;  //创建时间        [Column(TypeName = "datetime")]
        public DateTime UpdatedAt { get; set; } = DateTime.Now; //更新时间

        //导航属性
        [JsonIgnore]
        public virtual User Creator { get; set; }

        [JsonIgnore]
        public virtual ICollection<Notification> Notifications { get; set; } = new List<Notification>();
    }
}