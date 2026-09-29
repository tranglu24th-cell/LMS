using System.ComponentModel.DataAnnotations;

namespace Web.Models.EF
{
    public class Course
    {
        public int Id { get; set; }

        [Required]
        [StringLength(200)]
        public string Title { get; set; } = string.Empty;

        public string? Description { get; set; }

        public string? Image { get; set; }

        public Guid TeacherId { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // ===== Lịch học =====

        /// <summary>
        /// 0 = Chủ nhật ... 6 = Thứ bảy
        /// </summary>
        public int? ScheduleDayOfWeek { get; set; }

        public TimeSpan? ScheduleStartTime { get; set; }

        public TimeSpan? ScheduleEndTime { get; set; }

        [StringLength(50)]
        public string? Room { get; set; }

        public User? Teacher { get; set; }

        // ===== Nội dung khóa học =====

        public ICollection<LessonSection> LessonSections { get; set; }
            = new List<LessonSection>();

        // ===== Chương giảng dạy =====

        public ICollection<CourseChapter> CourseChapters { get; set; }
            = new List<CourseChapter>();

        // ===== Bài giảng =====

        public ICollection<Lesson> Lessons { get; set; }
            = new List<Lesson>();

        // ===== Sinh viên =====

        public ICollection<Enrollment> Enrollments { get; set; }
            = new List<Enrollment>();

        // ===== Điểm =====

        public ICollection<Grade> Grades { get; set; }
            = new List<Grade>();
    }
}