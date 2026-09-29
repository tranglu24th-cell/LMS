using System;
using System.ComponentModel.DataAnnotations;

namespace Web.Models.EF
{
    public class Quiz
    {
        public int Id { get; set; }

        [Required]
        public int LessonId { get; set; }

        [Required]
        [StringLength(200)]
        public string Title { get; set; } = string.Empty;

        public int? TimeLimitMinutes { get; set; }

        [Range(0, 100)]
        public decimal MaxScore { get; set; } = 10;

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public Lesson? Lesson { get; set; }

        public ICollection<QuizQuestion> Questions { get; set; }
            = new List<QuizQuestion>();
    }
}