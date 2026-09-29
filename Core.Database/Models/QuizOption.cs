using System.ComponentModel.DataAnnotations;

namespace Web.Models.EF
{
    public class QuizOption
    {
        public int Id { get; set; }

        [Required]
        public int QuizQuestionId { get; set; }

        [Required]
        [StringLength(500)]
        public string OptionText { get; set; } = string.Empty;

        public bool IsCorrect { get; set; }

        public QuizQuestion? QuizQuestion { get; set; }
    }
}