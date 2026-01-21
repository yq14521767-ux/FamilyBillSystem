using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;
using FamilyBillSystem.Services;

namespace FamilyBillSystem.Models
{
    [Table("categories")]
    public class Category : ISoftDeletable  //收支分类表
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        [MaxLength(50)]
        [Column(TypeName = "varchar(50)")]
        public string Name { get; set; }  //名称

        [Required]
        [Column(TypeName = "varchar(50)")]
        public string Type { get; set; }  //类型

        public int? ParentId { get; set; }  //父分类Id

        [MaxLength(50)]
        [Column(TypeName = "varchar(50)")]
        public string Icon { get; set; }  //图标标识

        [MaxLength(10)]
        [Column(TypeName = "varchar(10)")]
        public string Color { get; set; } //颜色

        [Column(TypeName = "int")]
        public int SortOrder { get; set; } = 0;  //排序序号

        // [Column(TypeName = "tinyint(1)")]
        // public bool IsSystem { get; set; } = false;  //是否系统默认分类

        public int? FamilyId { get; set; } //所属家庭Id

        [Required]
        [Column(TypeName = "varchar(50)")]
        public string Status { get; set; } = "active";  //状态，启用，停用

        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [Column(TypeName = "datetime")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;  //创建时间

        [Column(TypeName = "datetime")]
        public DateTime UpdatedAt { get; set; } = DateTime.Now; //更新时间

        [Column(TypeName = "datetime")]
        public DateTime? DeletedAt { get; set; }  //删除时间

        // 导航属性
        [JsonIgnore]
        public virtual Category Parent { get; set; }

        [JsonIgnore]
        public virtual ICollection<Category> Children { get; set; } = new List<Category>();

        [JsonIgnore]
        public virtual Family Family { get; set; }

        [JsonIgnore]
        public virtual ICollection<Bill> Bills { get; set; } = new List<Bill>();
    }
}