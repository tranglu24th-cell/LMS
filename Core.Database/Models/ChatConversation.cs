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

        /// <summary>
        /// Ghi chú / nhận xét của giảng viên khi xem lại lịch sử hội thoại AI của sinh viên.
        /// </summary>
        [StringLength(1000)]
        public string? TeacherNote { get; set; }

        public User? User { get; set; }

        public ICollection<ChatMessage> Messages { get; set; }
            = new List<ChatMessage>();
    }
}