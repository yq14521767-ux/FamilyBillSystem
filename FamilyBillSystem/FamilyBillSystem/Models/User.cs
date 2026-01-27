using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;
using System.Text.Json.Serialization;
using FamilyBillSystem.Services;

namespace FamilyBillSystem.Models
{
    public class User : ISoftDeletable  //用户表
    {
        public int Id { get; set; }


        [MaxLength(64)]
        [Column(TypeName = "varchar(64)")]
        public string? OpenId { get; set; }  //微信登录标识

        [MaxLength(50)]
        [Column(TypeName = "varchar(50)")]
        public string Nickname { get; set; }  //昵称

        [NotMapped]//不保存到数据库
        public string Password { get; set; }

        [Required]
        [ValidateNever]//不验证该属性
        public byte[] PasswordHash { get; set; } = Array.Empty<byte>();

        [Required]
        [ValidateNever]
        public byte[] PasswordSalt { get; set; } = Array.Empty<byte>();

        [MaxLength(500)]
        [Column(TypeName = "varchar(500)")]
        public string AvatarUrl { get; set; }  //头像链接（七牛云CDN链接）

        [MaxLength(11)]
        [Column(TypeName = "varchar(11)")]
        public string Phone { get; set; }  //手机号

        [MaxLength(100)]
        [Column(TypeName = "varchar(100)")]
        public string? Email { get; set; }  //电子邮箱

        [Column(TypeName = "integer")]
        public byte? Gender { get; set; } // 0-保密,1-男,2-女，null-未设置

        [Column(TypeName = "datetime")]
        public DateTime? LastLoginAt { get; set; }  //最后登录时间

        [Column(TypeName = "int")]
        public int LoginCount { get; set; } = 0;  //登录次数

        [Required]
        [Column(TypeName = "varchar(50)")]
        public string Status { get; set; } = "active";  //账户状态，active-活跃，frozen-冻结

        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [Column(TypeName = "datetime")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;  //用户创建时间        [Column(TypeName = "datetime")]
        public DateTime UpdatedAt { get; set; } = DateTime.Now;  //用户最近一次更新

        [Column(TypeName = "datetime")]
        public DateTime? DeletedAt { get; set; }  //删除时间

        public string? RefreshToken { get; set; } // 当前有效的 Refresh Token（随机字符串）

        [Column(TypeName = "datetime")]
        public DateTime? RefreshTokenExpireAt { get; set; } // UTC 时间

        //导航属性
        [JsonIgnore]
        public virtual ICollection<Family> CreatedFamilies { get; set; } = new List<Family>();
        
        [JsonIgnore]
        public virtual ICollection<FamilyMember> FamilyMembers { get; set; } = new List<FamilyMember>();
        
        [JsonIgnore]
        public virtual ICollection<Bill> Bills { get; set; } = new List<Bill>();
        
        [JsonIgnore]
        public virtual ICollection<Notification> Notifications { get; set; } = new List<Notification>();

    }
}
