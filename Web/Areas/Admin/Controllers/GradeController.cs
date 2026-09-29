using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Linq.Dynamic.Core;
using Web.Areas.Admin.Models;
using Web.Models.EF;

namespace Web.Areas.Admin.Controllers
{
    [Area("Admin")]
    public class GradeController : Controller
    {
        private readonly FoodContext _dbContext;

        public GradeController(FoodContext dbContext)
        {
            _dbContext = dbContext;
        }

        // ==========================================
        // DANH SÁCH ĐIỂM
        // /Admin/Grade
        // ==========================================
        public async Task<IActionResult> Index()
        {
            ViewBag.Students = await _dbContext.Users
                .Where(u => u.Role == "Student")
                .OrderBy(u => u.FullName)
                .Select(u => new
                {
                    u.Id,
                    u.MSSV,
                    u.FullName
                })
                .ToListAsync();

            ViewBag.Courses = await _dbContext.Courses
                .OrderBy(c => c.Title)
                .Select(c => new
                {
                    c.Id,
                    c.Title
                })
                .ToListAsync();

            return View();
        }

        // ==========================================
        // LẤY DANH SÁCH ĐIỂM CHO DATATABLE
        // ==========================================
        [HttpPost]
        public async Task<IActionResult> getList(
            jDatatable model,
            int? courseId = null,
            string? mssv = null)
        {
            var items = _dbContext.Grades
                .Include(g => g.Student)
                .Include(g => g.Course)
                .AsQueryable();

            // Lọc theo khóa học
            if (courseId.HasValue && courseId.Value > 0)
            {
                items = items.Where(g =>
                    g.CourseId == courseId.Value);
            }

            // Lọc theo MSSV
            if (!string.IsNullOrWhiteSpace(mssv))
            {
                items = items.Where(g =>
                    g.Student != null &&
                    g.Student.MSSV.Contains(mssv));
            }

            // Tìm kiếm
            if (!string.IsNullOrWhiteSpace(model.search.value))
            {
                var keyword = model.search.value.Trim();

                items = items.Where(g =>
                    (g.Student != null &&
                     (g.Student.FullName.Contains(keyword) ||
                      g.Student.MSSV.Contains(keyword)))
                    ||
                    (g.Course != null &&
                     g.Course.Title.Contains(keyword)));
            }

            int recordsTotal = await items.CountAsync();

            var data = await items
                .OrderByDescending(g => g.GradedAt)
                .Select(g => new
                {
                    g.Id,

                    studentId = g.StudentId,

                    mssv = g.Student != null
                        ? g.Student.MSSV
                        : "",

                    fullName = g.Student != null
                        ? g.Student.FullName
                        : "(Đã xóa)",

                    courseId = g.CourseId,

                    courseTitle = g.Course != null
                        ? g.Course.Title
                        : "(Đã xóa)",

                    // Điểm thành phần
                    g.CC,
                    g.GK,
                    g.CK,

                    // Tính điểm tổng kết
                    TB =
                        (g.CC * 0.10) +
                        (g.GK * 0.30) +
                        (g.CK * 0.60),

                    g.Comment,

                    gradedAt = g.GradedAt.ToString("dd/MM/yyyy"),

                    g.IsLocked,
                    g.EditDeadline
                })
                .Skip(model.start)
                .Take(model.length)
                .ToListAsync();

            return Ok(new
            {
                draw = model.draw,
                recordsFiltered = recordsTotal,
                recordsTotal = recordsTotal,
                data = data
            });
        }

        // ==========================================
        // LẤY THÔNG TIN MỘT BẢN GHI ĐIỂM
        // ==========================================
        [HttpGet]
        public async Task<IActionResult> getItem(int id)
        {
            var item = await _dbContext.Grades
                .Include(g => g.Student)
                .Include(g => g.Course)
                .FirstOrDefaultAsync(g => g.Id == id);

            if (item == null)
                return NotFound();

            return Ok(new
            {
                item.Id,
                item.StudentId,
                item.CourseId,

                item.CC,
                item.GK,
                item.CK,

                TB =
                    (item.CC * 0.10) +
                    (item.GK * 0.30) +
                    (item.CK * 0.60),

                item.Comment,
                item.GradedAt,
                item.UpdatedAt,
                item.EditDeadline,
                item.IsLocked
            });
        }

        // ==========================================
        // THÊM / CẬP NHẬT ĐIỂM
        // ADMIN CÓ QUYỀN SỬA
        // ==========================================
        [HttpPost]
        public async Task<IActionResult> Save(GradeViewModel model)
        {
            if (model.StudentId == Guid.Empty)
                return BadRequest("Vui lòng chọn sinh viên.");

            if (model.CourseId <= 0)
                return BadRequest("Vui lòng chọn khóa học.");

            var studentExists = await _dbContext.Users
                .AnyAsync(u =>
                    u.Id == model.StudentId &&
                    u.Role == "Student");

            if (!studentExists)
                return BadRequest("Sinh viên không tồn tại.");

            var courseExists = await _dbContext.Courses
                .AnyAsync(c => c.Id == model.CourseId);

            if (!courseExists)
                return BadRequest("Khóa học không tồn tại.");

            // Kiểm tra điểm
            if (model.CC < 0 || model.CC > 10)
                return BadRequest("Điểm CC phải từ 0 đến 10.");

            if (model.GK < 0 || model.GK > 10)
                return BadRequest("Điểm GK phải từ 0 đến 10.");

            if (model.CK < 0 || model.CK > 10)
                return BadRequest("Điểm CK phải từ 0 đến 10.");

            Grade item;

            // ==========================================
            // THÊM MỚI
            // ==========================================
            if (model.Id == null)
            {
                var duplicated = await _dbContext.Grades
                    .AnyAsync(g =>
                        g.StudentId == model.StudentId &&
                        g.CourseId == model.CourseId);

                if (duplicated)
                {
                    return BadRequest(
                        "Sinh viên này đã có điểm cho khóa học này. Vui lòng chỉnh sửa thay vì thêm mới."
                    );
                }

                item = new Grade
                {
                    StudentId = model.StudentId,
                    CourseId = model.CourseId,

                    CC = model.CC,
                    GK = model.GK,
                    CK = model.CK,

                    GradedAt = DateTime.Now,
                    UpdatedAt = DateTime.Now,

                    IsLocked = false
                };

                // Tính TB
                item.Score =
                    item.CC * 0.10 +
                    item.GK * 0.30 +
                    item.CK * 0.60;

                await _dbContext.Grades.AddAsync(item);
            }
            // ==========================================
            // CẬP NHẬT
            // ==========================================
            else
            {
                var existing = await _dbContext.Grades
                    .FirstOrDefaultAsync(g => g.Id == model.Id);

                if (existing == null)
                    return NotFound();

                // ADMIN được sửa kể cả khi:
                // - IsLocked = true
                // - quá EditDeadline
                existing.StudentId = model.StudentId;
                existing.CourseId = model.CourseId;

                existing.CC = model.CC;
                existing.GK = model.GK;
                existing.CK = model.CK;

                existing.Score =
                    existing.CC * 0.10 +
                    existing.GK * 0.30 +
                    existing.CK * 0.60;

                existing.Comment = model.Comment;

                existing.UpdatedAt = DateTime.Now;

                item = existing;
            }

            item.Comment = model.Comment;

            await _dbContext.SaveChangesAsync();

            return Ok(new
            {
                success = true,
                message = "Lưu điểm thành công.",
                id = item.Id
            });
        }

        // ==========================================
        // XÓA ĐIỂM
        // ==========================================
        [HttpGet]
        public async Task<IActionResult> Delete(int id)
        {
            var item = await _dbContext.Grades
                .FirstOrDefaultAsync(g => g.Id == id);

            if (item == null)
                return Ok(false);

            _dbContext.Grades.Remove(item);

            await _dbContext.SaveChangesAsync();

            return Ok(true);
        }
    }
}