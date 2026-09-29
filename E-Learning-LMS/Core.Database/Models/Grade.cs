namespace Web.Models.EF
{
    public class Grade
    {
        public int Id { get; set; }

        public Guid StudentId { get; set; }

        public int CourseId { get; set; }

        public double Score { get; set; }

        public string? Comment { get; set; }

        public DateTime GradedAt { get; set; } = DateTime.Now;

        public User? Student { get; set; }

        public Course? Course { get; set; }
    }
}
