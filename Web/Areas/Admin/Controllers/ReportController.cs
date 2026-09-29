using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Web.Models.EF;
using System.Linq.Dynamic.Core;

namespace Web.Areas.Admin.Controllers
{
    [Area("Admin")]
    public class ReportController : Controller
    {
        private readonly FoodContext _dbContext;
        public ReportController(FoodContext dbContext)
        {
            _dbContext = dbContext;
        }
        public IActionResult EnrollmentByMonth()
        {
            return View();
        }
        [HttpGet]
        public async Task<IActionResult> getEnrollmentByMonth(int year)
        {
            var items = from e in _dbContext.Enrollments.Where(i => i.EnrolledAt.Year == year)
                        group e by e.EnrolledAt.Month into g
                        select new
                        {
                            Months = g.Key,
                            SoLuong = g.Count()
                        };
            return Ok(await items.OrderBy(p => p.Months).ToListAsync());
        }
        [HttpGet]
        public IActionResult DiemTrungBinhTheoKhoa()
        {
            var thongKeList = (from g in _dbContext.Grades
                               join c in _dbContext.Courses on g.CourseId equals c.Id
                               select new
                               {
                                   CourseId = c.Id,
                                   CourseTitle = c.Title,
                                   Score = g.Score
                               })
                               .GroupBy(x => new { x.CourseId, x.CourseTitle })
                               .Select(g => new
                               {
                                   TenKhoa = g.Key.CourseTitle,
                                   SoLuongSinhVien = g.Count(),
                                   DiemTrungBinh = Math.Round(g.Average(x => x.Score), 2)
                               })
                               .OrderByDescending(x => x.DiemTrungBinh)
                               .ToList();

            return View(thongKeList);
        }
    }
}
