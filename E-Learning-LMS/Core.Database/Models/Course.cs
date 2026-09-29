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

        // ===== Lịch học (tùy chọn, do giáo viên/quản trị thiết lập) =====

        /// <summary>0 = Chủ nhật ... 6 = Thứ bảy (giống System.DayOfWeek)</summary>
        public int? ScheduleDayOfWeek { get; set; }

        public TimeSpan? ScheduleStartTime { get; set; }

        public TimeSpan? ScheduleEndTime { get; set; }

        [StringLength(50)]
        public string? Room { get; set; }

        public User? Teacher { get; set; }

        public ICollection<Lesson> Lessons { get; set; }
            = new List<Lesson>();

        public ICollection<Enrollment> Enrollments { get; set; }
            = new List<Enrollment>();

        public ICollection<Grade> Grades { get; set; }
            = new List<Grade>();
    }
}