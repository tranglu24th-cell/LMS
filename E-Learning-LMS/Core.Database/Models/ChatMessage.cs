using System.ComponentModel.DataAnnotations;

namespace Web.Models.EF
{
    public class ChatMessage
    {
        public int Id { get; set; }

        public int ConversationId { get; set; }

        [Required]
        [StringLength(20)]
        public string Role { get; set; } = string.Empty;

        [Required]
        public string Content { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public ChatConversation? Conversation { get; set; }
    }
}