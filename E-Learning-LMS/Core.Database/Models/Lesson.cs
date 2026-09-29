using System.ComponentModel.DataAnnotations;

namespace Web.Models.EF
{
    public class Lesson
    {
        public int Id { get; set; }

        public int CourseId { get; set; }

        [Required]
        [StringLength(200)]
        public string Title { get; set; } = string.Empty;

        public string? Content { get; set; }

        // =========================
        // THÔNG TIN CHƯƠNG
        // =========================

        [StringLength(200)]
        public string? ChapterName { get; set; }

        public int ChapterOrder { get; set; }

        // =========================
        // THÔNG TIN BÀI HỌC
        // =========================

        public int OrderNumber { get; set; }

        [StringLength(100)]
        public string? LessonType { get; set; } = "Lecture / Lý thuyết";

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public Course? Course { get; set; }
    }
}