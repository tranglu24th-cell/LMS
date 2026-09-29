using System;
using System.ComponentModel.DataAnnotations;

namespace Web.Models.EF
{
    public class Lesson
    {
        public int Id { get; set; }

        public int CourseId { get; set; }

        // ==============================
        // CHƯƠNG
        // ==============================

        public int? ChapterId { get; set; }

        public CourseChapter? Chapter { get; set; }

        // ==============================
        // THÔNG TIN BÀI HỌC
        // ==============================

        [Required]
        [StringLength(200)]
        public string Title { get; set; } = string.Empty;

        public string? Content { get; set; }

        // ==============================
        // FILE ĐÍNH KÈM
        // ==============================

        [StringLength(255)]
        public string? AttachmentFileName { get; set; }

        [StringLength(500)]
        public string? AttachmentPath { get; set; }

        // ==============================
        // TẠM GIỮ DỮ LIỆU CŨ
        // Sau này sẽ bỏ khi chuyển hoàn toàn
        // sang CourseChapter
        // ==============================

        [StringLength(200)]
        public string? ChapterName { get; set; }

        public int ChapterOrder { get; set; }

        // ==============================
        // LOẠI BÀI
        // ==============================

        public int OrderNumber { get; set; }

        [StringLength(100)]
        public string? LessonType { get; set; }
            = "Lecture / Lý thuyết";

        public DateTime CreatedAt { get; set; }
            = DateTime.Now;

        // ==============================
        // KHÓA HỌC
        // ==============================

        public Course? Course { get; set; }
        public Assignment? Assignment { get; set; }
        public Quiz? Quiz { get; set; }
    }
}