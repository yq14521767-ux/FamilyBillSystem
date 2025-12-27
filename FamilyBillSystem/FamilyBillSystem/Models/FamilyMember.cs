using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FamilyBillSystem.Models
{
    [Table("family_members")]
    public class FamilyMember  //家庭成员表
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        public int FamilyId { get; set; }  //所属家庭Id

        [Required]
        public int UserId { get; set; }  //创建者Id

        [Required]
        [Column(TypeName = "varchar(50)")]
        public string Role { get; set; } = "member"; //角色，管理员/成员

        [Column(TypeName = "text")]
        public string Permissions { get; set; } = "{}";  //权限

        [MaxLength(50)]
        [Column(TypeName = "varchar(50)")]
        public string Nickname { get; set; }  //家庭中的昵称

        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [Column(TypeName = "datetime")]
        public DateTime JoinedAt { get; set; } = DateTime.Now; //加入时间

        [Column(TypeName = "datetime")]
        public DateTime? LeftAt { get; set; } //退出时间

        [Required]
        [Column(TypeName = "varchar(50)")]
        public string Status { get; set; } = "active";  //成员状态，正常，已退出

        // 导航属性
        [JsonIgnore]
        public virtual Family Family { get; set; }

        [JsonIgnore]
        public virtual User User { get; set; }

        // 处理权限 JSON 的辅助方法
        [NotMapped]
        public JsonDocument PermissionsJson
        {
            get => JsonDocument.Parse(string.IsNullOrEmpty(Permissions) ? "{}" : Permissions);
            set => Permissions = value != null ? JsonSerializer.Serialize(value) : "{}";
        }
    }
}
