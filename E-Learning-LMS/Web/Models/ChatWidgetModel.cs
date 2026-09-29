namespace Web.Models
{
    /// <summary>
    /// Ngữ cảnh truyền từ trang LMS vào khung chat trợ giảng.
    /// Trang nào không có khóa học/bài học cụ thể thì để trống.
    /// </summary>
    public class ChatWidgetModel
    {
        /// <summary>Khóa học sinh viên đang xem (nếu có).</summary>
        public int? CourseId { get; set; }

        /// <summary>Bài học sinh viên đang xem (nếu có).</summary>
        public int? LessonId { get; set; }

        /// <summary>Dòng mô tả nhỏ hiển thị dưới tiêu đề khung chat.</summary>
        public string? ContextLabel { get; set; }
    }
}
