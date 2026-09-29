using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using Web.Models.EF;

namespace Web.Controllers
{
    public class CourseController : Controller
    {
        private readonly FoodContext _context;

        public CourseController(FoodContext context)
        {
            _context = context;
        }

        // Danh sách khóa học
        public async Task<IActionResult> Index()
        {
            var courses = await _context.Courses
             .Include(c => c.Teacher)
             .Include(c => c.Lessons)
             .OrderByDescending(c => c.CreatedAt)
             .ToListAsync();

            return View(courses);
        }

        // Chi tiết khóa học
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
                return NotFound();

            // Nếu chưa đăng nhập thì chuyển sang trang đăng nhập
            if (User.Identity?.IsAuthenticated != true)
            {
                return RedirectToAction(
                    "Login",
                    "Account",
                    new
                    {
                        returnUrl = Url.Action(
                            "Details",
                            "Course",
                            new { id = id.Value }
                        )
                    }
                );
            }

            // Lấy khóa học và danh sách sinh viên
            var course = await _context.Courses
                .Include(c => c.Teacher)
                .Include(c => c.Lessons)
                .Include(c => c.Enrollments)
                    .ThenInclude(e => e.Student)
                .Include(c => c.Grades)
                    .ThenInclude(g => g.Student)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (course == null)
                return NotFound();

            // Lấy ID sinh viên đang đăng nhập
            var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (Guid.TryParse(userIdString, out Guid userId))
            {
                // Kiểm tra sinh viên đã đăng ký khóa học chưa
                var enrollment = await _context.Enrollments
                    .FirstOrDefaultAsync(e =>
                        e.CourseId == course.Id &&
                        e.StudentId == userId);

                // Nếu chưa có thì tự động đăng ký
                if (enrollment == null)
                {
                    var newEnrollment = new Enrollment
                    {
                        StudentId = userId,
                        CourseId = course.Id,
                        EnrolledAt = DateTime.Now
                    };

                    _context.Enrollments.Add(newEnrollment);

                    await _context.SaveChangesAsync();

                    // Tải lại khóa học để Participants có sinh viên mới
                    course = await _context.Courses
                        .Include(c => c.Teacher)
                        .Include(c => c.Lessons)
                        .Include(c => c.Enrollments)
                            .ThenInclude(e => e.Student)
                         .Include(c => c.Grades)
                            .ThenInclude(g => g.Student)
                        .FirstOrDefaultAsync(c => c.Id == id);
                }
            }

            return View(course);
        }
    }
}