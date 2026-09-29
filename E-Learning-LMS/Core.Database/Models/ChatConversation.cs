using System.ComponentModel.DataAnnotations;

namespace Web.Models.EF
{
    public class ChatConversation
    {
        public int Id { get; set; }

        public Guid UserId { get; set; }

        [StringLength(200)]
        public string? Title { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public User? User { get; set; }

        public ICollection<ChatMessage> Messages { get; set; }
            = new List<ChatMessage>();
    }
}