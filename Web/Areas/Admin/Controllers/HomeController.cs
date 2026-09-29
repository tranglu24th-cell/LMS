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

        public async Task<IActionResult> Index()
        {
            // Tổng số khóa học đang có trên hệ thống
            ViewBag.TotalCourses = await _dbContext.Courses.CountAsync();

            // Tổng số sinh viên / học viên
            ViewBag.TotalStudents = await _dbContext.Users
                .Where(u => u.Role == "Student")
                .CountAsync();

            // Tổng số giảng viên
            ViewBag.TotalTeachers = await _dbContext.Users
                .Where(u => u.Role == "Teacher")
                .CountAsync();

            // Tổng số phản hồi & liên hệ chưa/đã xử lý
            ViewBag.TotalContacts = await _dbContext.Contacts.CountAsync();

            return View();
        }
    }
}
