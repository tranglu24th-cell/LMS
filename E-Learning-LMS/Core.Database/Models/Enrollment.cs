namespace Web.Models.EF
{
    public class Enrollment
    {
        public int Id { get; set; }

        public Guid StudentId { get; set; }

        public int CourseId { get; set; }

        public DateTime EnrolledAt { get; set; } = DateTime.Now;

        public User? Student { get; set; }

        public Course? Course { get; set; }
    }
}