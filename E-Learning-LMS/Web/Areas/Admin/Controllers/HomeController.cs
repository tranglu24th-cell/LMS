using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Web.Models.EF;

namespace Web.Areas.Admin.Controllers
{
    [Area("Admin")]
    public class HomeController : Controller
    {
        private readonly FoodContext _dbContext;

        public HomeController(FoodContext dbContext)
        {
            _dbContext = dbContext;
        }

        /// <summary>
        /// Trang chủ Admin: hiển thị thống kê thật của hệ thống LMS
        /// (số khóa học, sinh viên, giảng viên, hội thoại với Trợ giảng AI).
        /// </summary>
        public async Task<IActionResult> Index()
        {
            ViewBag.CourseCount = await _dbContext.Courses.CountAsync();

            ViewBag.StudentCount = await _dbContext.Users
                .CountAsync(u => u.Role == "Student");

            ViewBag.TeacherCount = await _dbContext.Users
                .CountAsync(u => u.Role == "Teacher");

            ViewBag.ChatConversationCount = await _dbContext.ChatConversations.CountAsync();

            return View();
        }
    }
}
