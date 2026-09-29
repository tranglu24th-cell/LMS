using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using Web.Models.EF;

namespace Web.Areas.Admin.Controllers
{
    /// <summary>
    /// Trang Admin/Giảng viên xem lại lịch sử hội thoại giữa sinh viên và Trợ giảng AI.
    /// Chuyển thể từ BookingController cũ: giữ khung card-list + ô ghi chú của giảng viên,
    /// chỉ đổi nguồn dữ liệu từ Booking sang ChatConversation/ChatMessage.
    /// </summary>
    [Area("Admin")]
    [Route("Admin/[controller]")]
    public class ChatHistoryController : Controller
    {
        private readonly FoodContext _dbContext;

        public ChatHistoryController(FoodContext dbContext)
        {
            _dbContext = dbContext;
        }

        // URL tải trang: /Admin/ChatHistory hoặc /Admin/ChatHistory/Index
        [HttpGet("")]
        [HttpGet("Index")]
        public IActionResult Index()
        {
            var list = _dbContext.ChatConversations
                .Include(c => c.User)
                .Include(c => c.Messages)
                .OrderByDescending(c => c.CreatedAt)
                .ToList();

            // Sắp lại tin nhắn theo thứ tự thời gian tăng dần cho từng hội thoại
            foreach (var conv in list)
            {
                conv.Messages = conv.Messages.OrderBy(m => m.CreatedAt).ToList();
            }

            return View(list);
        }

        // URL lưu ghi chú của giảng viên: /Admin/ChatHistory/SaveNote
        [HttpPost("SaveNote")]
        [ValidateAntiForgeryToken]
        public IActionResult SaveNote(int conversationId, string teacherNote)
        {
            var conversation = _dbContext.ChatConversations.FirstOrDefault(c => c.Id == conversationId);
            if (conversation != null)
            {
                conversation.TeacherNote = teacherNote;
                _dbContext.SaveChanges();
                TempData["Message"] = "Đã lưu ghi chú của giảng viên!";
            }
            return RedirectToAction("Index");
        }
    }
}
