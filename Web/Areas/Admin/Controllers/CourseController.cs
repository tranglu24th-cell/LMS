using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Web.Models.EF;

namespace Web.Areas.Admin.Controllers
{
    [Area("Admin")]
    public class CourseController : Controller
    {
        private readonly FoodContext _dbContext;

        public CourseController(FoodContext dbContext)
        {
            _dbContext = dbContext;
        }

        // =========================
        // DANH SÁCH GIẢNG VIÊN
        // =========================
        public async Task<IActionResult> Index()
        {
            var teachers = await _dbContext.Users
                .Where(u => u.Role == "Teacher")
                .OrderBy(u => u.Username)
                .ToListAsync();

            var courseCounts = await _dbContext.Courses
                .GroupBy(c => c.TeacherId)
                .Select(g => new
                {
                    TeacherId = g.Key,
                    Count = g.Count()
                })
                .ToListAsync();

            ViewBag.CourseCounts = courseCounts
                .ToDictionary(x => x.TeacherId, x => x.Count);

            return View(teachers);
        }

        // =========================
        // LẤY THÔNG TIN GIẢNG VIÊN
        // =========================
        [HttpGet]
        public async Task<IActionResult> GetItem(Guid id)
        {
            var teacher = await _dbContext.Users
                .FirstOrDefaultAsync(u =>
                    u.Id == id &&
                    u.Role == "Teacher");

            if (teacher == null)
                return NotFound();

            return Ok(teacher);
        }

        // =========================
        // XEM CHI TIẾT GIẢNG VIÊN
        // =========================
        [HttpGet]
        public async Task<IActionResult> GetTeacher(Guid id)
        {
            var teacher = await _dbContext.Users
                .FirstOrDefaultAsync(u =>
                    u.Id == id &&
                    u.Role == "Teacher");

            if (teacher == null)
                return NotFound();

            var courses = await _dbContext.Courses
                .Where(c => c.TeacherId == id)
                .OrderBy(c => c.CreatedAt)
                .Select(c => new
                {
                    c.Id,
                    c.Title,
                    c.CreatedAt
                })
                .ToListAsync();

            return Ok(new
            {
                id = teacher.Id,
                fullName = teacher.FullName,
                username = teacher.Username,
                role = teacher.Role,
                courseCount = courses.Count,
                courses = courses
            });
        }

        // =========================
        // THÊM GIẢNG VIÊN
        // =========================
        [HttpPost]
        public async Task<IActionResult> Create(User model)
        {
            if (string.IsNullOrWhiteSpace(model.Username))
                return BadRequest("Tên đăng nhập không được để trống.");

            var exists = await _dbContext.Users
                .AnyAsync(u => u.Username == model.Username);

            if (exists)
                return BadRequest("Tài khoản này đã tồn tại.");

            var teacher = new User
            {
                Id = Guid.NewGuid(),

                MSSV = string.Empty,

                FullName = model.FullName ?? string.Empty,

                Email = model.Email ?? string.Empty,

                // Gmail cũng chính là tên đăng nhập
                Username = model.Email ?? string.Empty,

                Password = model.Password,

                Role = "Teacher",

                IsActive = true,

                IsOnline = false,

                CreatedAt = DateTime.Now
            };
            await _dbContext.Users.AddAsync(teacher);
            await _dbContext.SaveChangesAsync();

            return Ok(teacher);
        }

        // =========================
        // CẬP NHẬT GIẢNG VIÊN
        // =========================
        [HttpPost]
        public async Task<IActionResult> Edit(User model)
        {
            var teacher = await _dbContext.Users
                .FirstOrDefaultAsync(u =>
                    u.Id == model.Id &&
                    u.Role == "Teacher");

            if (teacher == null)
                return NotFound();

            if (string.IsNullOrWhiteSpace(model.Username))
                return BadRequest("Tên đăng nhập không được để trống.");

            teacher.FullName = model.FullName ?? string.Empty;
            teacher.Username = model.Username;

            if (!string.IsNullOrWhiteSpace(model.Password))
            {
                teacher.Password = model.Password;
            }

            await _dbContext.SaveChangesAsync();

            return Ok(teacher);
        }
        // =========================
        // KHÓA / MỞ KHÓA GIẢNG VIÊN
        // =========================
        [HttpPost]
        public async Task<IActionResult> ToggleActive(Guid id)
        {
            var teacher = await _dbContext.Users
                .FirstOrDefaultAsync(u =>
                    u.Id == id &&
                    u.Role == "Teacher");

            if (teacher == null)
                return NotFound();

            teacher.IsActive = !teacher.IsActive;

            await _dbContext.SaveChangesAsync();

            return Ok(new
            {
                success = true,
                isActive = teacher.IsActive
            });
        }
        // =========================
        // XÓA GIẢNG VIÊN
        // =========================
        [HttpPost]
        public async Task<IActionResult> Delete(Guid id)
        {
            var teacher = await _dbContext.Users
                .FirstOrDefaultAsync(u =>
                    u.Id == id &&
                    u.Role == "Teacher");

            if (teacher == null)
                return NotFound();

            // Không cho xóa giảng viên đang có khóa học
            var hasCourses = await _dbContext.Courses
                .AnyAsync(c => c.TeacherId == id);

            if (hasCourses)
            {
                return BadRequest(
                    "Không thể xóa giảng viên đang có khóa học."
                );
            }

            _dbContext.Users.Remove(teacher);

            await _dbContext.SaveChangesAsync();

            return Ok(true);
        }
    }
}