using System.ComponentModel.DataAnnotations;

namespace Web.Models.EF
{
    /// <summary>
    /// Tài liệu cá nhân do sinh viên tự tải lên (ghi chú, bài làm, tài liệu tham khảo...).
    /// Chỉ chủ sở hữu (StudentId) mới xem/xóa được tài liệu của mình.
    /// </summary>
    public class PersonalDocument
    {
        public int Id { get; set; }

        public Guid StudentId { get; set; }

        [Required]
        [StringLength(255)]
        public string FileName { get; set; } = string.Empty;

        [Required]
        [StringLength(500)]
        public string FilePath { get; set; } = string.Empty;

        public long FileSize { get; set; }

        [StringLength(300)]
        public string? Description { get; set; }

        public DateTime UploadedAt { get; set; } = DateTime.Now;

        public User? Student { get; set; }
    }
}
