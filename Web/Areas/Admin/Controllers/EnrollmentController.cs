using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Web.Areas.Admin.Models;
using Web.Models.EF;

namespace Web.Areas.Admin.Controllers
{
    [Area("Admin")]
    public class EnrollmentController : Controller
    {
        private readonly FoodContext _dbContext;

        public EnrollmentController(FoodContext dbContext)
        {
            _dbContext = dbContext;
        }

        public IActionResult Index()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> getList(jDatatable model)
        {
            var items = _dbContext.Enrollments
                .Include(e => e.Student)
                .Include(e => e.Course)
                .AsQueryable();

            if (!string.IsNullOrEmpty(model.search.value))
            {
                items = items.Where(e =>
                    e.Student.FullName.Contains(model.search.value) ||
                    e.Student.MSSV.Contains(model.search.value) ||
                    e.Course.Title.Contains(model.search.value));
            }

            int recordsTotal = await items.CountAsync();

            var data = await items
                .OrderByDescending(e => e.EnrolledAt)
                .Select(e => new
                {
                    e.Id,
                    studentName = e.Student.FullName,
                    studentMSSV = e.Student.MSSV,
                    courseTitle = e.Course.Title,
                    e.EnrolledAt
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

        /// <summary>
        /// Hủy ghi danh: xóa bản ghi Enrollment (sinh viên rời khỏi khóa học).
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> Delete(int id)
        {
            var item = await _dbContext.Enrollments.FindAsync(id);
            if (item == null)
                return Ok(false);

            _dbContext.Enrollments.Remove(item);
            await _dbContext.SaveChangesAsync();
            return Ok(true);
        }
    }
}
