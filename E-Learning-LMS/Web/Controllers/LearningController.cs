using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using Web.Models.EF;

namespace Web.Controllers
{
    /// <summary>
    /// Controller phụ trách các chức năng học tập của sinh viên:
    /// "Khóa học của tôi", "Kết quả học tập", "Lịch học",
    /// "Tài liệu cá nhân", "Báo cáo học tập" và "Tra cứu điểm".
    ///
    /// Đăng nhập/đăng xuất hiện do AccountController (cookie) đảm nhiệm,
    /// controller này chỉ đọc thông tin sinh viên đang đăng nhập từ Claims.
    /// </summary>
    public class LearningController : Controller
    {
        private readonly FoodContext _context;

        public LearningController(FoodContext context)
        {
            _context = context;
        }

        // ================= KHÓA HỌC CỦA TÔI =================

        // GET: /Learning/MyCourses
        [HttpGet]
        public async Task<IActionResult> MyCourses()
        {
            var studentId = GetCurrentStudentId();
            if (studentId == null)
                return RedirectToAction("Login", "Account", new { returnUrl = Url.Action(nameof(MyCourses)) });

            var student = await _context.Users.FirstOrDefaultAsync(u => u.Id == studentId);
            if (student == null)
            {
                return RedirectToAction("Login", "Account");
            }

            var enrollments = await _context.Enrollments
                .Include(e => e.Course).ThenInclude(c => c!.Teacher)
                .Include(e => e.Course).ThenInclude(c => c!.Lessons)
                .Where(e => e.StudentId == studentId)
                .OrderByDescending(e => e.EnrolledAt)
                .ToListAsync();

            ViewBag.Student = student;
            return View(enrollments);
        }

        // ================= KẾT QUẢ HỌC TẬP =================

        // GET: /Learning/Results
        [HttpGet]
        public async Task<IActionResult> Results()
        {
            var studentId = GetCurrentStudentId();
            if (studentId == null)
                return RedirectToAction("Login", "Account", new { returnUrl = Url.Action(nameof(Results)) });

            var student = await _context.Users.FirstOrDefaultAsync(u => u.Id == studentId);
            if (student == null)
            {
                return RedirectToAction("Login", "Account");
            }

            var enrollments = await _context.Enrollments
                .Include(e => e.Course).ThenInclude(c => c!.Lessons)
                .Where(e => e.StudentId == studentId)
                .ToListAsync();

            var grades = await _context.Grades
                .Include(g => g.Course)
                .Where(g => g.StudentId == studentId)
                .OrderByDescending(g => g.GradedAt)
                .ToListAsync();

            ViewBag.Student = student;
            ViewBag.Enrollments = enrollments;
            ViewBag.AverageScore = grades.Count > 0 ? grades.Average(g => g.Score) : (double?)null;

            return View(grades);
        }

        // ================= LỊCH HỌC =================

        // GET: /Learning/Schedule
        [HttpGet]
        public async Task<IActionResult> Schedule()
        {
            var studentId = GetCurrentStudentId();
            if (studentId == null)
                return RedirectToAction("Login", "Account", new { returnUrl = Url.Action(nameof(Schedule)) });

            var student = await _context.Users.FirstOrDefaultAsync(u => u.Id == studentId);
            if (student == null)
                return RedirectToAction("Login", "Account");

            var courses = await _context.Enrollments
                .Include(e => e.Course)
                .Where(e => e.StudentId == studentId && e.Course != null)
                .Select(e => e.Course!)
                .ToListAsync();

            ViewBag.Student = student;

            // Sắp xếp: có lịch (theo thứ trong tuần) trước, chưa có lịch để cuối
            var ordered = courses
                .OrderBy(c => c.ScheduleDayOfWeek.HasValue ? c.ScheduleDayOfWeek.Value : 8)
                .ThenBy(c => c.ScheduleStartTime ?? TimeSpan.Zero)
                .ToList();

            return View(ordered);
        }

        // ================= TÀI LIỆU CÁ NHÂN =================

        // GET: /Learning/Documents
        [HttpGet]
        public async Task<IActionResult> Documents()
        {
            var studentId = GetCurrentStudentId();
            if (studentId == null)
                return RedirectToAction("Login", "Account", new { returnUrl = Url.Action(nameof(Documents)) });

            var student = await _context.Users.FirstOrDefaultAsync(u => u.Id == studentId);
            if (student == null)
                return RedirectToAction("Login", "Account");

            var documents = await _context.PersonalDocuments
                .Where(d => d.StudentId == studentId)
                .OrderByDescending(d => d.UploadedAt)
                .ToListAsync();

            ViewBag.Student = student;
            return View(documents);
        }

        // POST: /Learning/UploadDocument
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UploadDocument(IFormFile file, string? description)
        {
            var studentId = GetCurrentStudentId();
            if (studentId == null)
                return RedirectToAction("Login", "Account");

            if (file == null || file.Length == 0)
            {
                TempData["Error"] = "Vui lòng chọn tệp để tải lên.";
                return RedirectToAction(nameof(Documents));
            }

            const long maxSize = 20 * 1024 * 1024; // 20MB
            if (file.Length > maxSize)
            {
                TempData["Error"] = "Tệp vượt quá dung lượng cho phép (20MB).";
                return RedirectToAction(nameof(Documents));
            }

            var allowedExtensions = new[]
            {
                ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx",
                ".txt", ".zip", ".rar", ".jpg", ".jpeg", ".png", ".gif"
            };

            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!allowedExtensions.Contains(extension))
            {
                TempData["Error"] = "Định dạng tệp không được hỗ trợ.";
                return RedirectToAction(nameof(Documents));
            }

            var folderPath = Path.Combine(
                Directory.GetCurrentDirectory(), "wwwroot", "uploads", "documents", studentId.Value.ToString());

            if (!Directory.Exists(folderPath))
                Directory.CreateDirectory(folderPath);

            var storedFileName = Guid.NewGuid().ToString() + extension;
            var filePath = Path.Combine(folderPath, storedFileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            var document = new PersonalDocument
            {
                StudentId = studentId.Value,
                FileName = file.FileName,
                FilePath = "/uploads/documents/" + studentId.Value + "/" + storedFileName,
                FileSize = file.Length,
                Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
                UploadedAt = DateTime.Now
            };

            await _context.PersonalDocuments.AddAsync(document);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Tải tài liệu lên thành công.";
            return RedirectToAction(nameof(Documents));
        }

        // GET: /Learning/DeleteDocument/5
        [HttpGet]
        public async Task<IActionResult> DeleteDocument(int id)
        {
            var studentId = GetCurrentStudentId();
            if (studentId == null)
                return RedirectToAction("Login", "Account");

            var document = await _context.PersonalDocuments
                .FirstOrDefaultAsync(d => d.Id == id && d.StudentId == studentId);

            if (document != null)
            {
                var fullPath = Path.Combine(
                    Directory.GetCurrentDirectory(), "wwwroot",
                    document.FilePath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));

                if (System.IO.File.Exists(fullPath))
                {
                    System.IO.File.Delete(fullPath);
                }

                _context.PersonalDocuments.Remove(document);
                await _context.SaveChangesAsync();

                TempData["Success"] = "Đã xóa tài liệu.";
            }

            return RedirectToAction(nameof(Documents));
        }

        // ================= BÁO CÁO HỌC TẬP =================

        // GET: /Learning/Report
        [HttpGet]
        public async Task<IActionResult> Report()
        {
            var studentId = GetCurrentStudentId();
            if (studentId == null)
                return RedirectToAction("Login", "Account", new { returnUrl = Url.Action(nameof(Report)) });

            var student = await _context.Users.FirstOrDefaultAsync(u => u.Id == studentId);
            if (student == null)
                return RedirectToAction("Login", "Account");

            var enrollments = await _context.Enrollments
                .Include(e => e.Course).ThenInclude(c => c!.Lessons)
                .Include(e => e.Course).ThenInclude(c => c!.Teacher)
                .Where(e => e.StudentId == studentId)
                .ToListAsync();

            var grades = await _context.Grades
                .Include(g => g.Course)
                .Where(g => g.StudentId == studentId)
                .OrderByDescending(g => g.GradedAt)
                .ToListAsync();

            ViewBag.Student = student;
            ViewBag.Enrollments = enrollments;
            ViewBag.TotalLessons = enrollments.Sum(e => e.Course?.Lessons.Count ?? 0);
            ViewBag.AverageScore = grades.Count > 0 ? grades.Average(g => g.Score) : (double?)null;
            ViewBag.PassedCount = grades.Count(g => g.Score >= 5);
            ViewBag.FailedCount = grades.Count(g => g.Score < 5);
            ViewBag.GeneratedAt = DateTime.Now;

            return View(grades);
        }

        // ================= TRA CỨU ĐIỂM (công khai, theo MSSV) =================

        // GET: /Learning/LookupScore
        [HttpGet]
        public IActionResult LookupScore()
        {
            return View();
        }

        // POST: /Learning/LookupScore
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> LookupScore(string mssv, string fullName)
        {
            mssv = (mssv ?? string.Empty).Trim();
            fullName = (fullName ?? string.Empty).Trim();

            if (string.IsNullOrEmpty(mssv) || string.IsNullOrEmpty(fullName))
            {
                ViewBag.Error = "Vui lòng nhập đầy đủ MSSV và Họ tên.";
                return View();
            }

            var student = await _context.Users
                .Where(u => u.MSSV == mssv)
                .ToListAsync();

            var match = student.FirstOrDefault(u =>
                string.Equals(u.FullName?.Trim(), fullName, StringComparison.OrdinalIgnoreCase));

            if (match == null)
            {
                ViewBag.Error = "Không tìm thấy sinh viên phù hợp với MSSV và Họ tên đã nhập.";
                return View();
            }

            var grades = await _context.Grades
                .Include(g => g.Course)
                .Where(g => g.StudentId == match.Id)
                .OrderByDescending(g => g.GradedAt)
                .ToListAsync();

            ViewBag.Student = match;
            ViewBag.AverageScore = grades.Count > 0 ? grades.Average(g => g.Score) : (double?)null;

            return View(grades);
        }

        private Guid? GetCurrentStudentId()
        {
            var idString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(idString))
                return null;

            return Guid.TryParse(idString, out var id) ? id : null;
        }
    }
}
