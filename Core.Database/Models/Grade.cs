namespace Web.Models.EF
{
    public class Grade
    {
        public int Id { get; set; }

        public Guid StudentId { get; set; }

        public int CourseId { get; set; }

        // Điểm chuyên cần - 10%
        public double CC { get; set; }

        // Điểm giữa kỳ - 30%
        public double GK { get; set; }

        // Điểm cuối kỳ - 60%
        public double CK { get; set; }

        // Điểm tổng kết - hệ thống tự tính
        public double Score { get; set; }

        public string? Comment { get; set; }

        // Ngày nhập điểm
        public DateTime GradedAt { get; set; } = DateTime.Now;

        // Ngày cập nhật điểm gần nhất
        public DateTime UpdatedAt { get; set; } = DateTime.Now;

        // Hạn giảng viên được phép sửa điểm
        public DateTime? EditDeadline { get; set; }

        // Khóa điểm
        public bool IsLocked { get; set; } = false;

        public User? Student { get; set; }

        public Course? Course { get; set; }
    }
}