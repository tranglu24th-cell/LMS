using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Linq.Dynamic.Core;
using Web.Areas.Admin.Models;
using Web.Models.EF;

namespace Web.Areas.Admin.Controllers
{
    [Area("Admin")]
    public class LessonController : Controller
    {
        private readonly FoodContext _dbContext;

        public LessonController(FoodContext dbContext)
        {
            _dbContext = dbContext;
        }

        // ==========================================
        // DANH SÁCH KHÓA HỌC
        // /Admin/Lesson
        // ==========================================
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var courses = await _dbContext.Courses
                .Include(c => c.Teacher)
                .OrderByDescending(c => c.CreatedAt)
                .ToListAsync();

            return View(courses);
        }

        // ==========================================
        // LẤY DANH SÁCH GIẢNG VIÊN
        // Dùng cho form Thêm / Cập nhật khóa học
        // ==========================================
        [HttpGet]
        public async Task<IActionResult> GetTeachers()
        {
            var teachers = await _dbContext.Users
                .Where(u =>
                    u.Role == "Teacher" &&
                    u.IsActive)
                .OrderBy(u => u.FullName)
                .Select(u => new
                {
                    id = u.Id,
                    fullName = u.FullName,
                    username = u.Username
                })
                .ToListAsync();

            return Ok(teachers);
        }

        // ==========================================
        // LẤY THÔNG TIN MỘT KHÓA HỌC
        // Dùng khi bấm Cập nhật
        // ==========================================
        [HttpGet]
        public async Task<IActionResult> GetCourse(int id)
        {
            var course = await _dbContext.Courses
                .Include(c => c.Teacher)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (course == null)
                return NotFound();

            return Ok(new
            {
                id = course.Id,
                title = course.Title,
                description = course.Description,
                image = course.Image,
                teacherId = course.TeacherId,
                teacherName = course.Teacher != null
                    ? course.Teacher.FullName
                    : "",
                scheduleDayOfWeek = course.ScheduleDayOfWeek,
                scheduleStartTime = course.ScheduleStartTime.HasValue
                    ? course.ScheduleStartTime.Value.ToString(@"hh\:mm")
                    : null,
                scheduleEndTime = course.ScheduleEndTime.HasValue
                    ? course.ScheduleEndTime.Value.ToString(@"hh\:mm")
                    : null,
                room = course.Room
            });
        }

        // ==========================================
        // THÊM / CẬP NHẬT KHÓA HỌC
        // ==========================================
        [HttpPost]
        public async Task<IActionResult> SaveCourse(CourseViewModel model)
        {
            try
            {
                // Kiểm tra tên khóa học
                if (string.IsNullOrWhiteSpace(model.Title))
                {
                    return BadRequest(new
                    {
                        message = "Tên khóa học không được để trống."
                    });
                }

                // Kiểm tra giảng viên
                if (!model.TeacherId.HasValue)
                {
                    return BadRequest(new
                    {
                        message = "Vui lòng chọn giảng viên."
                    });
                }

                // Kiểm tra giảng viên tồn tại
                var teacher = await _dbContext.Users
                    .FirstOrDefaultAsync(u =>
                        u.Id == model.TeacherId.Value &&
                        u.Role == "Teacher" &&
                        u.IsActive);

                if (teacher == null)
                {
                    return BadRequest(new
                    {
                        message = "Giảng viên không hợp lệ."
                    });
                }

                Course course;

                // ============================
                // THÊM KHÓA HỌC
                // ============================
                if (!model.Id.HasValue || model.Id.Value == 0)
                {
                    course = new Course
                    {
                        Title = model.Title.Trim(),
                        Description = model.Description,
                        TeacherId = model.TeacherId.Value,
                        CreatedAt = DateTime.Now,
                        ScheduleDayOfWeek = model.ScheduleDayOfWeek,
                        ScheduleStartTime = ParseScheduleTime(model.ScheduleStartTime),
                        ScheduleEndTime = ParseScheduleTime(model.ScheduleEndTime),
                        Room = string.IsNullOrWhiteSpace(model.Room) ? null : model.Room.Trim()
                    };

                    await _dbContext.Courses.AddAsync(course);
                }
                else
                {
                    // ============================
                    // CẬP NHẬT KHÓA HỌC
                    // ============================
                    course = await _dbContext.Courses
                        .FirstOrDefaultAsync(c => c.Id == model.Id.Value);

                    if (course == null)
                        return NotFound();

                    course.Title = model.Title.Trim();
                    course.Description = model.Description;
                    course.TeacherId = model.TeacherId.Value;
                    course.ScheduleDayOfWeek = model.ScheduleDayOfWeek;
                    course.ScheduleStartTime = ParseScheduleTime(model.ScheduleStartTime);
                    course.ScheduleEndTime = ParseScheduleTime(model.ScheduleEndTime);
                    course.Room = string.IsNullOrWhiteSpace(model.Room) ? null : model.Room.Trim();
                }

                // ============================
                // UPLOAD ẢNH
                // ============================
                if (model.ImageFile != null &&
                    model.ImageFile.Length > 0)
                {
                    var allowedExtensions = new[]
                    {
                ".jpg",
                ".jpeg",
                ".png",
                ".gif",
                ".webp"
            };

                    var extension = Path
                        .GetExtension(model.ImageFile.FileName)
                        .ToLowerInvariant();

                    if (!allowedExtensions.Contains(extension))
                    {
                        return BadRequest(new
                        {
                            message = "Chỉ được chọn ảnh JPG, JPEG, PNG, GIF hoặc WEBP."
                        });
                    }

                    // Thư mục lưu ảnh
                    var folderPath = Path.Combine(
                        Directory.GetCurrentDirectory(),
                        "wwwroot",
                        "uploads",
                        "courses"
                    );

                    if (!Directory.Exists(folderPath))
                    {
                        Directory.CreateDirectory(folderPath);
                    }

                    // Tên file mới
                    var fileName =
                        Guid.NewGuid().ToString() + extension;

                    var filePath =
                        Path.Combine(folderPath, fileName);

                    // Lưu file
                    using (var stream = new FileStream(
                        filePath,
                        FileMode.Create))
                    {
                        await model.ImageFile.CopyToAsync(stream);
                    }

                    // Lưu đường dẫn vào DB
                    course.Image =
                        "/uploads/courses/" + fileName;
                }

                await _dbContext.SaveChangesAsync();

                return Ok(new
                {
                    success = true,
                    message =
                        model.Id.HasValue && model.Id.Value > 0
                            ? "Cập nhật khóa học thành công."
                            : "Thêm khóa học thành công."
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new
                {
                    success = false,
                    message = ex.Message
                });
            }
        }

        // ==========================================
        // XÓA KHÓA HỌC
        // ==========================================
        [HttpPost]
        public async Task<IActionResult> DeleteCourse(int id)
        {
            try
            {
                // ==========================================
                // TÌM KHÓA HỌC
                // ==========================================
                var course = await _dbContext.Courses
                    .FirstOrDefaultAsync(c => c.Id == id);

                if (course == null)
                {
                    return NotFound(new
                    {
                        message = "Không tìm thấy khóa học."
                    });
                }

                // ==========================================
                // XÓA CÁC BÀI HỌC
                // ==========================================
                var lessons = await _dbContext.Lessons
                    .Where(l => l.CourseId == id)
                    .ToListAsync();

                if (lessons.Any())
                {
                    _dbContext.Lessons.RemoveRange(lessons);
                }

                // ==========================================
                // XÓA CÁC ĐĂNG KÝ KHÓA HỌC
                // ==========================================
                var enrollments = await _dbContext.Enrollments
                    .Where(e => e.CourseId == id)
                    .ToListAsync();

                if (enrollments.Any())
                {
                    _dbContext.Enrollments.RemoveRange(enrollments);
                }

                // ==========================================
                // XÓA KHÓA HỌC
                // ==========================================
                _dbContext.Courses.Remove(course);

                await _dbContext.SaveChangesAsync();

                return Ok(new
                {
                    success = true,
                    message = "Xóa khóa học thành công."
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new
                {
                    success = false,
                    message = ex.Message
                });
            }
        }

        // ==========================================
        // DANH SÁCH BÀI HỌC CỦA MỘT KHÓA HỌC
        // /Admin/Lesson/Lessons?courseId=1
        // ==========================================
        [HttpGet]
        public async Task<IActionResult> Lessons(int courseId)
        {
            var course = await _dbContext.Courses
                .Include(c => c.Teacher)
                .FirstOrDefaultAsync(c => c.Id == courseId);

            if (course == null)
                return NotFound();

            ViewBag.Course = course;

            return View(courseId);
        }

        // ==========================================
        // LẤY DANH SÁCH BÀI HỌC CHO DATATABLE
        // ==========================================
        [HttpPost]
        public async Task<IActionResult> getList(
      jDatatable model,
      int courseId)
        {
            var items = _dbContext.Lessons
                .Where(l => l.CourseId == courseId);


            // =========================
            // TÌM KIẾM
            // =========================

            if (!string.IsNullOrEmpty(model.search.value))
            {
                items = items.Where(i =>
                    i.Title.Contains(model.search.value) ||
                    (i.ChapterName != null &&
                     i.ChapterName.Contains(model.search.value)));
            }


            // =========================
            // SẮP XẾP
            // =========================

            items = items
                .OrderBy(l => l.ChapterOrder)
                .ThenBy(l => l.OrderNumber);


            int recordsTotal =
                await items.CountAsync();


            // =========================
            // DATA
            // =========================

            var data = await items

                .Select(i => new
                {
                    i.Id,

                    chapterName =
                        i.ChapterName,

                    chapterOrder =
                        i.ChapterOrder,

                    lessonType =
                        i.LessonType,

                    title =
                        i.Title,

                    orderNumber =
                        i.OrderNumber,

                    createdAt =
                        i.CreatedAt.ToString("dd/MM/yyyy")
                })

                .Skip(model.start)

                .Take(model.length)

                .ToListAsync();


            return Ok(new
            {
                draw = model.draw,

                recordsFiltered =
                    recordsTotal,

                recordsTotal =
                    recordsTotal,

                data = data
            });
        }

        // ==========================================
        // LẤY THÔNG TIN MỘT BÀI HỌC
        // ==========================================
        [HttpGet]
        public async Task<IActionResult> getItem(int id)
        {
            var item = await _dbContext.Lessons
                .FindAsync(id);

            if (item == null)
                return NotFound();

            return Ok(item);
        }

        // ==========================================
        // THÊM / CẬP NHẬT BÀI HỌC
        // ==========================================
        [HttpPost]
        public async Task<IActionResult> Save(LessonViewModel model)
        {
            try
            {
                // ==========================================
                // KIỂM TRA KHÓA HỌC
                // ==========================================
                var course = await _dbContext.Courses
                    .FirstOrDefaultAsync(c => c.Id == model.CourseId);

                if (course == null)
                {
                    return NotFound(new
                    {
                        message = "Không tìm thấy khóa học."
                    });
                }

                // ==========================================
                // KIỂM TRA TÊN BÀI HỌC
                // ==========================================
                if (string.IsNullOrWhiteSpace(model.Title))
                {
                    return BadRequest(new
                    {
                        message = "Tên bài học không được để trống."
                    });
                }

                Lesson item;

                // ==========================================
                // THÊM BÀI HỌC MỚI
                // ==========================================
                if (!model.Id.HasValue)
                {
                    item = new Lesson
                    {
                        CourseId = model.CourseId,
                        CreatedAt = DateTime.Now
                    };

                    await _dbContext.Lessons.AddAsync(item);
                }
                else
                {
                    // ==========================================
                    // CẬP NHẬT BÀI HỌC
                    // ==========================================
                    item = await _dbContext.Lessons
                        .FirstOrDefaultAsync(l =>
                            l.Id == model.Id.Value);

                    if (item == null)
                    {
                        return NotFound(new
                        {
                            message = "Không tìm thấy bài học."
                        });
                    }

                    // Không cho sửa bài sang khóa học khác
                    if (item.CourseId != model.CourseId)
                    {
                        return BadRequest(new
                        {
                            message = "Bài học không thuộc khóa học này."
                        });
                    }
                }

                // ==========================================
                // THÔNG TIN CHƯƠNG
                // ==========================================
                item.ChapterName =
                    string.IsNullOrWhiteSpace(model.ChapterName)
                        ? null
                        : model.ChapterName.Trim();

                item.ChapterOrder =
                    model.ChapterOrder > 0
                        ? model.ChapterOrder
                        : 1;

                // ==========================================
                // THÔNG TIN BÀI HỌC
                // ==========================================
                item.LessonType =
                    string.IsNullOrWhiteSpace(model.LessonType)
                        ? "Lecture / Lý thuyết"
                        : model.LessonType.Trim();

                item.Title = model.Title.Trim();

                item.Content = model.Content;

                item.OrderNumber =
                    model.OrderNumber > 0
                        ? model.OrderNumber
                        : 1;

                // ==========================================
                // LƯU DATABASE
                // ==========================================
                await _dbContext.SaveChangesAsync();

                return Ok(new
                {
                    success = true,
                    message = model.Id.HasValue
                        ? "Cập nhật bài học thành công."
                        : "Thêm bài học thành công.",
                    id = item.Id
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new
                {
                    success = false,
                    message = ex.Message
                });
            }
        }

        // ==========================================
        // XÓA BÀI HỌC
        // ==========================================
        [HttpGet]
        public async Task<IActionResult> Delete(int id)
        {
            var item = await _dbContext.Lessons
                .FindAsync(id);

            if (item == null)
                return Ok(false);

            _dbContext.Lessons.Remove(item);

            await _dbContext.SaveChangesAsync();

            return Ok(true);
        }

        // ==========================================
        // CHUYỂN CHUỖI "HH:mm" THÀNH TimeSpan?
        // ==========================================
        private static TimeSpan? ParseScheduleTime(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;

            return TimeSpan.TryParse(value, out var result) ? result : null;
        }
    }
}