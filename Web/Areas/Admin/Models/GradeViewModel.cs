namespace Web.Areas.Admin.Models
{
    public class GradeViewModel
    {
        public int? Id { get; set; }

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
    }
}