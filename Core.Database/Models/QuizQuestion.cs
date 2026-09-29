using System.ComponentModel.DataAnnotations;
using Microsoft.VisualBasic.FileIO;

namespace Web.Models.EF
{
    public class QuizQuestion
    {
        public int Id { get; set; }

        [Required]
        public int QuizId { get; set; }

        [Required]
        public string QuestionText { get; set; } = string.Empty;

        public int OrderNumber { get; set; }

        public decimal Score { get; set; } = 1;
        public Quiz? Quiz { get; set; }

        public ICollection<QuizOption> Options { get; set; }
            = new List<QuizOption>();
    }
}