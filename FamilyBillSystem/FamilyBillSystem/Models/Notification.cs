using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace FamilyBillSystem.Models
{
    [Table("notifications")]
    public class Notification  //通知表
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        public int? FamilyId { get; set; }  //所属家庭Id

        public int? UserId { get; set; }  //接收用户Id

        [Required]
        [MaxLength(100)]
        [Column(TypeName = "varchar(100)")]
        public string Title { get; set; }  //标题

        [Required]
        [Column(TypeName = "text")]
        public string Message { get; set; }  //内容

        [Required]
        [Column(TypeName = "varchar(50)")]
        public string Status { get; set; } = "unread";  //状态，未读，已读

        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [Column(TypeName = "datetime")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;  //创建时间

        //导航属性
        [JsonIgnore]
        public virtual Family Family { get; set; }

        [JsonIgnore]
        public virtual User User { get; set; }
    }
}