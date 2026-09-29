namespace Web.Areas.Admin.Models
{
    public class GradeViewModel
    {
        public int? Id { get; set; }
        public Guid StudentId { get; set; }
        public int CourseId { get; set; }
        public double Score { get; set; }
        public string? Comment { get; set; }
    }
}
