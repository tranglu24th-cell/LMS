namespace Web.Areas.Admin.Models
{
    public class LessonViewModel
    {
        public int? Id { get; set; }

        public int CourseId { get; set; }

        // =========================
        // THÔNG TIN CHƯƠNG
        // =========================

        public string? ChapterName { get; set; }

        public int ChapterOrder { get; set; }

        // =========================
        // THÔNG TIN BÀI HỌC
        // =========================

        public string? LessonType { get; set; }

        public string? Title { get; set; }

        public string? Content { get; set; }

        public int OrderNumber { get; set; }
    }
}