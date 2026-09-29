using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Web.Models.EF;
using Core.Database.Models; // Ép dùng chuẩn model này
using System;
using System.Security.Claims;
using System.Threading.Tasks;
using System.Linq;

namespace Web.Controllers
{
    // Trang "Góp Ý" - nơi Học viên và Giảng viên trao đổi, góp ý với nhau (có thể gắn theo khóa học).
    // Ai cũng xem được, nhưng phải đăng nhập mới gửi/trả lời được.
    public class ContactController : Controller
    {
        private readonly FoodContext _dbContext;
        public ContactController(FoodContext dbContext)
        {
            _dbContext = dbContext;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var list = await _dbContext.Contacts
                .Include(c => c.Course)
                .OrderByDescending(c => c.CreatedAt)
                .ToListAsync();

            // Danh sách khóa học để gắn thẻ khi gửi góp ý (không bắt buộc)
            ViewBag.Courses = await _dbContext.Courses
                .OrderBy(c => c.Title)
                .ToListAsync();

            return View(list);
        }

        // Gửi góp ý mới - bắt buộc đăng nhập (Học viên hoặc Giảng viên)
        [HttpPost]
        public async Task<IActionResult> Index(string Message, int? CourseId)
        {
            if (User.Identity?.IsAuthenticated != true)
            {
                TempData["ContactError"] = "Vui lòng đăng nhập để gửi góp ý.";
                return RedirectToAction("Login", "Account", new { returnUrl = Url.Action("Index", "Contact") });
            }

            if (!string.IsNullOrWhiteSpace(Message))
            {
                var userId = GetCurrentUserId();
                var fullName = User.FindFirst(ClaimTypes.Name)?.Value ?? "Người dùng";
                var email = User.FindFirst(ClaimTypes.Email)?.Value ?? "";
                var role = User.FindFirst(ClaimTypes.Role)?.Value; // "Student" | "Teacher"

                var newContact = new Contact
                {
                    Name = fullName,
                    Email = email,
                    Message = Message.Trim(),
                    CreatedAt = DateTime.Now,
                    SenderId = userId,
                    SenderRole = role,
                    CourseId = CourseId
                };

                _dbContext.Contacts.Add(newContact);
                await _dbContext.SaveChangesAsync();
            }

            return RedirectToAction(nameof(Index));
        }

        // Trả lời một góp ý - Học viên hoặc Giảng viên đã đăng nhập đều có thể trả lời lẫn nhau
        [HttpPost]
        public async Task<IActionResult> Reply(int Id, string ReplyMessage)
        {
            if (User.Identity?.IsAuthenticated != true)
            {
                TempData["ContactError"] = "Vui lòng đăng nhập để trả lời góp ý.";
                return RedirectToAction("Login", "Account", new { returnUrl = Url.Action("Index", "Contact") });
            }

            if (!string.IsNullOrWhiteSpace(ReplyMessage))
            {
                var contact = await _dbContext.Contacts.FirstOrDefaultAsync(c => c.Id == Id);

                if (contact != null)
                {
                    contact.AdminReply = ReplyMessage.Trim();
                    contact.RepliedAt = DateTime.Now;
                    contact.RepliedById = GetCurrentUserId();
                    contact.RepliedByRole = User.FindFirst(ClaimTypes.Role)?.Value;

                    _dbContext.Entry(contact).State = EntityState.Modified;
                    await _dbContext.SaveChangesAsync();
                }
            }

            return RedirectToAction(nameof(Index));
        }

        private Guid? GetCurrentUserId()
        {
            var idString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return Guid.TryParse(idString, out var id) ? id : (Guid?)null;
        }
    }
}