using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Web.Models.EF;

namespace Web.Controllers
{
    public class LessonController : Controller
    {
        private readonly FoodContext _context;

        public LessonController(FoodContext context)
        {
            _context = context;
        }

        // ==========================================
        // XEM CHI TIẾT BÀI HỌC
        // /Lesson/Details/1
        // ==========================================
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
                return NotFound();

            var lesson = await _context.Lessons
                .Include(l => l.Course)
                    .ThenInclude(c => c!.Teacher)
                .Include(l => l.Course)
                    .ThenInclude(c => c!.Lessons)
                .FirstOrDefaultAsync(l => l.Id == id);

            if (lesson == null)
                return NotFound();

            return View(lesson);
        }
    }
}