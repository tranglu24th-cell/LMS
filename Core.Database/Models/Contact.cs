using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Web.Models.EF;

namespace Core.Database.Models
{
    /// <summary>
    /// Góp ý / trao đổi giữa học viên và giảng viên.
    /// Ai cũng có thể xem, nhưng chỉ tài khoản đã đăng nhập (Student/Teacher) mới được gửi và trả lời.
    /// </summary>
    public class Contact
    {
        [Key]
        public int Id { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập tên của bạn")]
        [StringLength(100)]
        public string? Name { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập email")]
        [EmailAddress(ErrorMessage = "Email không đúng định dạng")]
        public string? Email { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập tin nhắn")]
        public string? Message { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
  
        public string? AdminReply { get; set; }

        public DateTime? RepliedAt { get; set; }

        // ================= Bổ sung cho góp ý giữa Học viên <-> Giảng viên =================

        /// <summary>Id tài khoản (User) đã gửi góp ý. Null nếu là khách chưa đăng nhập (dữ liệu cũ).</summary>
        public Guid? SenderId { get; set; }

        /// <summary>"Student" | "Teacher" | null (khách).</summary>
        [StringLength(20)]
        public string? SenderRole { get; set; }

        /// <summary>Góp ý có thể gắn với 1 khóa học cụ thể (không bắt buộc).</summary>
        public int? CourseId { get; set; }

        [ForeignKey(nameof(CourseId))]
        public Course? Course { get; set; }

        /// <summary>Id tài khoản đã trả lời (giảng viên, học viên khác, hoặc admin).</summary>
        public Guid? RepliedById { get; set; }

        /// <summary>"Student" | "Teacher" | "Admin".</summary>
        [StringLength(20)]
        public string? RepliedByRole { get; set; }
    }
}