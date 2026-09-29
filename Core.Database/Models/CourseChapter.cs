using System.ComponentModel.DataAnnotations;

namespace Web.Models.EF
{
    public class CourseChapter
    {
        public int Id { get; set; }

        public int CourseId { get; set; }

        [Required]
        [StringLength(200)]
        public string Name { get; set; } = string.Empty;

        public int OrderNumber { get; set; }

        public Course? Course { get; set; }

        public ICollection<Lesson> Lessons { get; set; }
            = new List<Lesson>();
    }
}