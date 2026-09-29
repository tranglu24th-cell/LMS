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
                .OrderBy(u => u.FullName)
                .Select(u => new { u.Id, u.MSSV, u.FullName })
                .ToListAsync();

            ViewBag.Courses = await _dbContext.Courses
                .OrderBy(c => c.Title)
                .Select(c => new { c.Id, c.Title })
                .ToListAsync();

            return View();
        }

        // ==========================================
        // LẤY DANH SÁCH ĐIỂM CHO DATATABLE
        // ==========================================
        [HttpPost]
        public async Task<IActionResult> getList(jDatatable model, int? courseId = null, string? mssv = null)
        {
            var items = _dbContext.Grades
                .Include(g => g.Student)
                .Include(g => g.Course)
                .AsQueryable();

            if (courseId.HasValue && courseId.Value > 0)
            {
                items = items.Where(g => g.CourseId == courseId.Value);
            }

            if (!string.IsNullOrEmpty(mssv))
            {
                items = items.Where(g => g.Student != null && g.Student.MSSV.Contains(mssv));
            }

            if (!string.IsNullOrEmpty(model.search.value))
            {
                var keyword = model.search.value;
                items = items.Where(g =>
                    (g.Student != null && (g.Student.FullName.Contains(keyword) || g.Student.MSSV.Contains(keyword))) ||
                    (g.Course != null && g.Course.Title.Contains(keyword)));
            }

            int recordsTotal = await items.CountAsync();

            var data = await items
                .OrderByDescending(g => g.GradedAt)
                .Select(g => new
                {
                    g.Id,
                    studentId = g.StudentId,
                    mssv = g.Student != null ? g.Student.MSSV : "",
                    fullName = g.Student != null ? g.Student.FullName : "(Đã xóa)",
                    courseId = g.CourseId,
                    courseTitle = g.Course != null ? g.Course.Title : "(Đã xóa)",
                    g.Score,
                    g.Comment,
                    gradedAt = g.GradedAt.ToString("dd/MM/yyyy")
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
            var item = await _dbContext.Grades.FindAsync(id);

            if (item == null)
                return NotFound();

            return Ok(item);
        }

        // ==========================================
        // THÊM / CẬP NHẬT ĐIỂM
        // ==========================================
        [HttpPost]
        public async Task<IActionResult> Save(GradeViewModel model)
        {
            if (model.StudentId == Guid.Empty)
                return BadRequest("Vui lòng chọn sinh viên.");

            if (model.CourseId <= 0)
                return BadRequest("Vui lòng chọn khóa học.");

            if (model.Score < 0 || model.Score > 10)
                return BadRequest("Điểm phải nằm trong khoảng từ 0 đến 10.");

            var studentExists = await _dbContext.Users.AnyAsync(u => u.Id == model.StudentId);
            if (!studentExists)
                return BadRequest("Sinh viên không tồn tại.");

            var courseExists = await _dbContext.Courses.AnyAsync(c => c.Id == model.CourseId);
            if (!courseExists)
                return BadRequest("Khóa học không tồn tại.");

            Grade item;

            if (model.Id == null)
            {
                // Không cho tạo trùng một sinh viên có nhiều điểm cho cùng một khóa học
                var duplicated = await _dbContext.Grades.AnyAsync(g =>
                    g.StudentId == model.StudentId && g.CourseId == model.CourseId);

                if (duplicated)
                    return BadRequest("Sinh viên này đã có điểm cho khóa học này. Vui lòng chỉnh sửa thay vì thêm mới.");

                item = new Grade
                {
                    StudentId = model.StudentId,
                    CourseId = model.CourseId,
                    GradedAt = DateTime.Now
                };

                await _dbContext.Grades.AddAsync(item);
            }
            else
            {
                var existing = await _dbContext.Grades.FindAsync(model.Id);

                if (existing == null)
                    return NotFound();

                item = existing;
                item.StudentId = model.StudentId;
                item.CourseId = model.CourseId;
                item.GradedAt = DateTime.Now;
            }

            item.Score = model.Score;
            item.Comment = model.Comment;

            await _dbContext.SaveChangesAsync();

            return Ok(item);
        }

        // ==========================================
        // XÓA ĐIỂM
        // ==========================================
        [HttpGet]
        public async Task<IActionResult> Delete(int id)
        {
            var item = await _dbContext.Grades.FindAsync(id);

            if (item == null)
                return Ok(false);

            _dbContext.Grades.Remove(item);

            await _dbContext.SaveChangesAsync();

            return Ok(true);
        }
    }
}
