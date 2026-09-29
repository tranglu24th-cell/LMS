using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Web.Areas.Admin.Models;
using Web.Models.EF;

namespace Web.Areas.Admin.Controllers
{
    [Area("Admin")]
    public class StudentController : Controller
    {
        private readonly FoodContext _dbContext;

        public StudentController(FoodContext dbContext)
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
            var items = _dbContext.Users
             .Where(i => i.Role == "Student")
             .AsQueryable();

            if (!string.IsNullOrEmpty(model.search.value))
            {
                items = items.Where(i =>
                    i.MSSV.Contains(model.search.value) ||
                    i.FullName.Contains(model.search.value) ||
                    i.Email.Contains(model.search.value) ||
                    i.Username.Contains(model.search.value));
            }

            int recordsTotal = await items.CountAsync();

            var data = await items
                .OrderBy(i => i.FullName)
                .Select(i => new
                {
                    i.Id,
                    i.MSSV,
                    i.FullName,
                    i.Email,
                    i.Username,
                    i.ClassName,
                    i.IsActive,
                    i.IsOnline
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

        [HttpGet]
        public async Task<IActionResult> GetItem(Guid id)
        {
            var student = await _dbContext.Users
                .FirstOrDefaultAsync(i => i.Id == id);

            if (student == null)
                return NotFound();

            return Ok(student);
        }

        [HttpPost]
        public async Task<IActionResult> Save(User model)
        {
            try
            {
                User? student;

                if (model.Id == Guid.Empty)
                {
                    student = new User
                    {
                        Id = Guid.NewGuid(),
                        MSSV = model.MSSV,
                        FullName = model.FullName,
                        Email = model.Email,
                        Username = model.Username,
                        Password = model.Password,
                        ClassName = model.ClassName,
                        IsActive = model.IsActive,
                        IsOnline = false
                    };

                    await _dbContext.Users.AddAsync(student);
                }
                else
                {
                    student = await _dbContext.Users
                        .FirstOrDefaultAsync(i => i.Id == model.Id);

                    if (student == null)
                        return NotFound();

                    student.MSSV = model.MSSV;
                    student.FullName = model.FullName;
                    student.Email = model.Email;
                    student.Username = model.Username;
                    student.ClassName = model.ClassName;
                    student.IsActive = model.IsActive;

                    if (!string.IsNullOrEmpty(model.Password))
                    {
                        student.Password = model.Password;
                    }
                }

                await _dbContext.SaveChangesAsync();

                return Ok(student);
            }
            catch (Exception ex)
            {
                return BadRequest(new
                {
                    message = ex.Message
                });
            }
        }

        [HttpGet]
        public async Task<IActionResult> Delete(Guid id)
        {
            try
            {
                var student = await _dbContext.Users
                    .FirstOrDefaultAsync(i => i.Id == id);

                if (student == null)
                    return NotFound();

                _dbContext.Users.Remove(student);

                await _dbContext.SaveChangesAsync();

                return Ok(true);
            }
            catch
            {
                return Ok(false);
            }
        }
    }
}