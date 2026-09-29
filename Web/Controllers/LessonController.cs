using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

using Microsoft.EntityFrameworkCore;

using Web.Models.EF;

namespace Web.Controllers
{
    public class LessonController : Controller
    {
        private readonly FoodContext _context;
        private readonly IWebHostEnvironment _environment;


        // =====================================================
        // CONSTRUCTOR
        // =====================================================

        public LessonController(
            FoodContext context,
            IWebHostEnvironment environment)
        {
            _context = context;
            _environment = environment;
        }


        // =====================================================
        // USER HIỆN TẠI
        // =====================================================

        private async Task<User?> GetCurrentUserAsync()
        {
            // ---------------------------------------------
            // 1. Lấy User theo NameIdentifier
            // ---------------------------------------------

            var nameIdentifier =
                User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (Guid.TryParse(
                    nameIdentifier,
                    out var userId))
            {
                var userById =
                    await _context.Users
                        .FirstOrDefaultAsync(
                            x => x.Id == userId);

                if (userById != null)
                {
                    return userById;
                }
            }


            // ---------------------------------------------
            // 2. Nếu không có Id thì thử theo Name
            // ---------------------------------------------

            var identityName =
                User.Identity?.Name;

            if (string.IsNullOrWhiteSpace(identityName))
            {
                return null;
            }


            var user =
                await _context.Users
                    .FirstOrDefaultAsync(
                        x =>
                            x.Username == identityName ||
                            x.Email == identityName);

            return user;
        }


        // =====================================================
        // KIỂM TRA GIẢNG VIÊN CỦA KHÓA HỌC
        // =====================================================

        private async Task<bool> IsCourseTeacherAsync(
            Course course)
        {
            var currentUser =
                await GetCurrentUserAsync();

            if (currentUser == null)
            {
                return false;
            }


            return string.Equals(
                       currentUser.Role,
                       "Teacher",
                       StringComparison.OrdinalIgnoreCase)
                   &&
                   course.TeacherId == currentUser.Id;
        }


        // =====================================================
        // DANH SÁCH KHÓA HỌC
        // =====================================================

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var courses =
                await _context.Courses
                    .Include(c => c.Teacher)
                    .Include(c => c.Lessons)
                    .Include(c => c.CourseChapters)
                    .OrderByDescending(
                        c => c.CreatedAt)
                    .ToListAsync();

            return View(courses);
        }


        // =====================================================
        // CHI TIẾT KHÓA HỌC
        // =====================================================

        [HttpGet]
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }


            var course = await _context.Courses

        .Include(c => c.Teacher)

        .Include(c => c.LessonSections)

        .Include(c => c.CourseChapters)
            .ThenInclude(ch => ch.Lessons)
                .ThenInclude(l => l.Assignment)

        .Include(c => c.CourseChapters)
            .ThenInclude(ch => ch.Lessons)
                .ThenInclude(l => l.Quiz)
                    .ThenInclude(q => q.Questions)
                        .ThenInclude(qq => qq.Options)


        .Include(c => c.Enrollments)
            .ThenInclude(e => e.Student)

        .Include(c => c.Grades)
            .ThenInclude(g => g.Student)

        .FirstOrDefaultAsync(
            c => c.Id == id.Value);

            if (course == null)
            {
                return NotFound();
            }


            // =================================================
            // KIỂM TRA USER
            // =================================================

            var currentUser =
                await GetCurrentUserAsync();

            if (currentUser == null)
            {
                return RedirectToAction(
                    "Login",
                    "Account");
            }


            // =================================================
            // TỰ ĐỘNG GHI DANH SINH VIÊN
            // =================================================

            if (string.Equals(
                    currentUser.Role,
                    "Student",
                    StringComparison.OrdinalIgnoreCase))
            {
                var existedEnrollment =
                    await _context.Enrollments
                        .AnyAsync(e =>
                            e.CourseId == course.Id &&
                            e.StudentId == currentUser.Id);


                if (!existedEnrollment)
                {
                    var enrollment =
                        new Enrollment
                        {
                            CourseId =
                                course.Id,

                            StudentId =
                                currentUser.Id
                        };


                    _context.Enrollments.Add(
                        enrollment);

                    await _context.SaveChangesAsync();


                    // -----------------------------------------
                    // Load lại dữ liệu sau khi ghi danh
                    // -----------------------------------------

                    course =
                        await _context.Courses

                            .Include(c => c.Teacher)

                            .Include(c => c.LessonSections)

                            .Include(c => c.CourseChapters)
                                .ThenInclude(ch => ch.Lessons)
                                    .ThenInclude(l => l.Assignment)

                            .Include(c => c.Lessons)
                                .ThenInclude(l => l.Assignment)

                            .Include(c => c.Enrollments)
                                .ThenInclude(e => e.Student)

                            .Include(c => c.Grades)
                                .ThenInclude(g => g.Student)

                            .FirstOrDefaultAsync(
                                c => c.Id == id.Value);


                    if (course == null)
                    {
                        return NotFound();
                    }
                }
            }


            // =================================================
            // VIEWBAG
            // =================================================

            ViewBag.IsCourseTeacher =
                await IsCourseTeacherAsync(course);

            ViewBag.CurrentUserId =
                currentUser.Id;

            ViewBag.CurrentUserRole =
                currentUser.Role;


            return View(course);
        }


        // =====================================================
        // =====================================================
        // CHAPTER
        // =====================================================
        // =====================================================


        // =====================================================
        // THÊM CHƯƠNG
        // =====================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateChapter(
            int courseId,
            string name,
            int orderNumber)
        {
            var course =
                await _context.Courses
                    .FirstOrDefaultAsync(
                        c => c.Id == courseId);


            if (course == null)
            {
                TempData["Error"] =
                    "Không tìm thấy khóa học.";

                return RedirectToAction(
                    "Index");
            }


            if (!await IsCourseTeacherAsync(course))
            {
                return Forbid();
            }


            if (string.IsNullOrWhiteSpace(name))
            {
                TempData["Error"] =
                    "Vui lòng nhập tên chương.";

                return RedirectToAction(
                    "Details",
                    new
                    {
                        id = courseId
                    });
            }


            if (orderNumber < 1)
            {
                orderNumber = 1;
            }


            // ---------------------------------------------
            // Không cho trùng số chương
            // ---------------------------------------------

            var duplicateOrder =
                await _context.CourseChapters
                    .AnyAsync(x =>
                        x.CourseId == courseId &&
                        x.OrderNumber == orderNumber);


            if (duplicateOrder)
            {
                TempData["Error"] =
                    "Số thứ tự chương đã tồn tại.";

                return RedirectToAction(
                    "Details",
                    new
                    {
                        id = courseId
                    });
            }


            var chapter =
                new CourseChapter
                {
                    CourseId =
                        courseId,

                    Name =
                        name.Trim(),

                    OrderNumber =
                        orderNumber
                };


            _context.CourseChapters.Add(
                chapter);

            await _context.SaveChangesAsync();


            TempData["Success"] =
                "Đã thêm chương.";


            return RedirectToAction(
                "Details",
                new
                {
                    id = courseId
                });
        }


        // =====================================================
        // SỬA CHƯƠNG
        // =====================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditChapter(
            int id,
            string name,
            int orderNumber)
        {
            var chapter =
                await _context.CourseChapters
                    .Include(c => c.Course)
                    .FirstOrDefaultAsync(
                        c => c.Id == id);


            if (chapter == null)
            {
                return NotFound();
            }


            if (chapter.Course == null)
            {
                return NotFound();
            }


            if (!await IsCourseTeacherAsync(
                    chapter.Course))
            {
                return Forbid();
            }


            if (string.IsNullOrWhiteSpace(name))
            {
                TempData["Error"] =
                    "Vui lòng nhập tên chương.";

                return RedirectToAction(
                    "Details",
                    new
                    {
                        id =
                            chapter.CourseId
                    });
            }


            if (orderNumber < 1)
            {
                orderNumber = 1;
            }


            // ---------------------------------------------
            // Kiểm tra trùng số chương
            // ---------------------------------------------

            var duplicateOrder =
                await _context.CourseChapters
                    .AnyAsync(x =>
                        x.CourseId ==
                            chapter.CourseId
                        &&
                        x.OrderNumber ==
                            orderNumber
                        &&
                        x.Id !=
                            chapter.Id);


            if (duplicateOrder)
            {
                TempData["Error"] =
                    "Số thứ tự chương đã tồn tại.";

                return RedirectToAction(
                    "Details",
                    new
                    {
                        id =
                            chapter.CourseId
                    });
            }


            chapter.Name =
                name.Trim();

            chapter.OrderNumber =
                orderNumber;


            // ---------------------------------------------
            // Đồng bộ dữ liệu cũ trên Lesson
            // ---------------------------------------------

            var lessons =
                await _context.Lessons
                    .Where(l =>
                        l.ChapterId ==
                            chapter.Id)
                    .ToListAsync();


            foreach (var lesson in lessons)
            {
                lesson.ChapterName =
                    chapter.Name;

                lesson.ChapterOrder =
                    chapter.OrderNumber;
            }


            await _context.SaveChangesAsync();


            TempData["Success"] =
                "Đã cập nhật chương.";


            return RedirectToAction(
                "Details",
                new
                {
                    id =
                        chapter.CourseId
                });
        }


        // =====================================================
        // XÓA CHƯƠNG
        // =====================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteChapter(
            int id)
        {
            var chapter =
                await _context.CourseChapters
                    .Include(c => c.Course)
                    .Include(c => c.Lessons)
                    .FirstOrDefaultAsync(
                        c => c.Id == id);


            if (chapter == null)
            {
                return NotFound();
            }


            if (chapter.Course == null)
            {
                return NotFound();
            }


            if (!await IsCourseTeacherAsync(
                    chapter.Course))
            {
                return Forbid();
            }


            // Không cho xóa chương đang có bài
            if (chapter.Lessons != null &&
                chapter.Lessons.Any())
            {
                TempData["Error"] =
                    "Không thể xóa chương đang có bài giảng.";

                return RedirectToAction(
                    "Details",
                    new
                    {
                        id =
                            chapter.CourseId
                    });
            }


            var courseId =
                chapter.CourseId;


            _context.CourseChapters.Remove(
                chapter);

            await _context.SaveChangesAsync();


            TempData["Success"] =
                "Đã xóa chương.";


            return RedirectToAction(
                "Details",
                new
                {
                    id =
                        courseId
                });
        }


        // =====================================================
        // =====================================================
        // LESSON / ACTIVITY
        // =====================================================
        // =====================================================


        // =====================================================
        // TẠO BÀI GIẢNG / HOẠT ĐỘNG
        // =====================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateLesson(
            int courseId,
            int chapterId,
            string title,
            string? content,
            string? lessonType,
            int orderNumber,
            DateTime? dueDate,
            IFormFile? attachment,
            decimal maxScore = 10,
            int? timeLimitMinutes = 30,
            decimal quizMaxScore = 10)
        {
            var course =
                await _context.Courses
                    .FirstOrDefaultAsync(
                        c => c.Id == courseId);


            if (course == null)
            {
                TempData["Error"] =
                    "Không tìm thấy khóa học.";

                return RedirectToAction(
                    "Index");
            }


            if (!await IsCourseTeacherAsync(course))
            {
                return Forbid();
            }


            if (string.IsNullOrWhiteSpace(title))
            {
                TempData["Error"] =
                    "Vui lòng nhập tên hoạt động.";

                return RedirectToAction(
                    "Details",
                    new
                    {
                        id =
                            courseId
                    });
            }


            // ---------------------------------------------
            // Kiểm tra chapter
            // ---------------------------------------------

            var chapter =
                await _context.CourseChapters
                    .FirstOrDefaultAsync(
                        c =>
                            c.Id == chapterId &&
                            c.CourseId == courseId);


            if (chapter == null)
            {
                TempData["Error"] =
                    "Chương không thuộc khóa học này.";

                return RedirectToAction(
                    "Details",
                    new
                    {
                        id =
                            courseId
                    });
            }


            if (orderNumber < 1)
            {
                orderNumber = 1;
            }


            // ---------------------------------------------
            // Chuẩn hóa loại hoạt động
            // ---------------------------------------------

            var normalizedLessonType =
                string.IsNullOrWhiteSpace(
                    lessonType)
                    ? "Lecture / Lý thuyết"
                    : lessonType.Trim();


            // ---------------------------------------------
            // File
            // ---------------------------------------------

            string? attachmentFileName =
                null;

            string? attachmentPath =
                null;


            if (attachment != null &&
                attachment.Length > 0)
            {
                var result =
                    await SaveLessonAttachmentAsync(
                        attachment);


                if (!result.Success)
                {
                    TempData["Error"] =
                        result.ErrorMessage;

                    return RedirectToAction(
                        "Details",
                        new
                        {
                            id =
                                courseId
                        });
                }


                attachmentFileName =
                    result.OriginalFileName;

                attachmentPath =
                    result.PublicPath;
            }


            // ---------------------------------------------
            // Tạo Lesson
            // ---------------------------------------------

            var lesson =
                new Lesson
                {
                    CourseId =
                        courseId,

                    ChapterId =
                        chapter.Id,

                    Title =
                        title.Trim(),

                    Content =
                        content,

                    LessonType =
                        normalizedLessonType,

                    OrderNumber =
                        orderNumber,

                    ChapterName =
                        chapter.Name,

                    ChapterOrder =
                        chapter.OrderNumber,

                    AttachmentFileName =
                        attachmentFileName,

                    AttachmentPath =
                        attachmentPath,

                    CreatedAt =
                        DateTime.Now
                };


            _context.Lessons.Add(
                lesson);

            await _context.SaveChangesAsync();


            // =================================================
            // TẠO ASSIGNMENT NẾU LÀ BÀI TẬP
            // =================================================

            if (string.Equals(
                    normalizedLessonType,
                    "Bài tập",
                    StringComparison.OrdinalIgnoreCase))
            {
                if (maxScore < 0)
                {
                    maxScore = 0;
                }


                if (maxScore > 100)
                {
                    maxScore = 100;
                }


                var assignment =
                    new Assignment
                    {
                        LessonId =
                            lesson.Id,

                        Title =
                            lesson.Title,

                        Instructions =
                            lesson.Content,

                        DueDate =
                            dueDate,

                        MaxScore =
                            maxScore,

                        CreatedAt =
                            DateTime.Now
                    };


                _context.Assignments.Add(
                    assignment);

                await _context.SaveChangesAsync();
            }


            // =================================================
            // TẠO QUIZ NẾU LÀ QUIZ / KIỂM TRA
            // =================================================

            if (
                string.Equals(
                    normalizedLessonType,
                    "Quiz / Kiểm tra",
                    StringComparison.OrdinalIgnoreCase)
                ||
                string.Equals(
                    normalizedLessonType,
                    "Quiz",
                    StringComparison.OrdinalIgnoreCase)
                ||
                string.Equals(
                    normalizedLessonType,
                    "Kiểm tra",
                    StringComparison.OrdinalIgnoreCase)
            )
            {
                if (!timeLimitMinutes.HasValue ||
                    timeLimitMinutes.Value < 1)
                {
                    timeLimitMinutes =
                        30;
                }


                if (quizMaxScore < 0)
                {
                    quizMaxScore = 0;
                }


                if (quizMaxScore > 100)
                {
                    quizMaxScore = 100;
                }


                var quiz =
                    new Quiz
                    {
                        LessonId =
                            lesson.Id,

                        Title =
                            lesson.Title,

                        TimeLimitMinutes =
                            timeLimitMinutes,

                        MaxScore =
                            quizMaxScore,

                        CreatedAt =
                            DateTime.Now
                    };


                _context.Quizzes.Add(
                    quiz);

                await _context.SaveChangesAsync();
            }


            TempData["Success"] =
                "Đã thêm hoạt động.";


            return RedirectToAction(
                "Details",
                new
                {
                    id =
                        courseId
                });
        }
        // =====================================================
        // THÊM CÂU HỎI QUIZ
        // =====================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddQuizQuestion(
            int quizId,
            string questionText,
            string optionA,
            string optionB,
            string optionC,
            string optionD,
            int correctOption,
            decimal score = 1)
        {
            var quiz = await _context.Quizzes
                .Include(q => q.Lesson)
                    .ThenInclude(l => l.Course)
                .FirstOrDefaultAsync(q => q.Id == quizId);

            if (quiz == null)
            {
                return NotFound();
            }

            if (quiz.Lesson == null || quiz.Lesson.Course == null)
            {
                return NotFound();
            }

            if (!await IsCourseTeacherAsync(quiz.Lesson.Course))
            {
                return Forbid();
            }

            if (string.IsNullOrWhiteSpace(questionText))
            {
                TempData["Error"] = "Vui lòng nhập nội dung câu hỏi.";

                return RedirectToAction(
                    "Details",
                    new { id = quiz.Lesson.CourseId });
            }

            if (string.IsNullOrWhiteSpace(optionA) ||
                string.IsNullOrWhiteSpace(optionB) ||
                string.IsNullOrWhiteSpace(optionC) ||
                string.IsNullOrWhiteSpace(optionD))
            {
                TempData["Error"] = "Vui lòng nhập đủ 4 đáp án.";

                return RedirectToAction(
                    "Details",
                    new { id = quiz.Lesson.CourseId });
            }

            if (correctOption < 0 || correctOption > 3)
            {
                correctOption = 0;
            }

            if (score < 0)
            {
                score = 0;
            }

            if (score > 100)
            {
                score = 100;
            }

            var nextOrder =
                await _context.QuizQuestions
                    .Where(q => q.QuizId == quizId)
                    .Select(q => (int?)q.OrderNumber)
                    .MaxAsync() ?? 0;

            nextOrder++;


            var question = new QuizQuestion
            {
                QuizId = quizId,
                QuestionText = questionText.Trim(),
                OrderNumber = nextOrder,
                Score = score
            };

            _context.QuizQuestions.Add(question);

            await _context.SaveChangesAsync();


            var options = new[]
            {
        new QuizOption
        {
            QuizQuestionId = question.Id,
            OptionText = optionA.Trim(),
            IsCorrect = correctOption == 0
        },

        new QuizOption
        {
            QuizQuestionId = question.Id,
            OptionText = optionB.Trim(),
            IsCorrect = correctOption == 1
        },

        new QuizOption
        {
            QuizQuestionId = question.Id,
            OptionText = optionC.Trim(),
            IsCorrect = correctOption == 2
        },

        new QuizOption
        {
            QuizQuestionId = question.Id,
            OptionText = optionD.Trim(),
            IsCorrect = correctOption == 3
        }
    };

            _context.QuizOptions.AddRange(options);

            await _context.SaveChangesAsync();


            TempData["Success"] = "Đã thêm câu hỏi Quiz.";

            return RedirectToAction(
                "Details",
                new { id = quiz.Lesson.CourseId });
        }
        // =====================================================
        // XÓA CÂU HỎI QUIZ
        // =====================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteQuizQuestion(int id)
        {
            var question = await _context.QuizQuestions
                .Include(q => q.Quiz)
                    .ThenInclude(q => q.Lesson)
                        .ThenInclude(l => l.Course)
                .Include(q => q.Options)
                .FirstOrDefaultAsync(q => q.Id == id);

            if (question == null)
            {
                return NotFound();
            }

            if (question.Quiz?.Lesson?.Course == null)
            {
                return NotFound();
            }

            var course = question.Quiz.Lesson.Course;

            if (!await IsCourseTeacherAsync(course))
            {
                return Forbid();
            }


            if (question.Options.Any())
            {
                _context.QuizOptions.RemoveRange(question.Options);
            }

            _context.QuizQuestions.Remove(question);

            await _context.SaveChangesAsync();


            TempData["Success"] = "Đã xóa câu hỏi.";

            return RedirectToAction(
                "Details",
                new { id = course.Id });
        }
        // =====================================================
        // SINH VIÊN NỘP QUIZ
        // =====================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SubmitQuiz(
            int quizId,
            Dictionary<int, int> answers)
        {
            var currentUser = await GetCurrentUserAsync();

            if (currentUser == null)
            {
                return Unauthorized();
            }

            if (!string.Equals(
                    currentUser.Role,
                    "Student",
                    StringComparison.OrdinalIgnoreCase))
            {
                return Forbid();
            }

            var quiz = await _context.Quizzes
                .Include(q => q.Lesson)
                    .ThenInclude(l => l.Course)
                .Include(q => q.Questions)
                    .ThenInclude(q => q.Options)
                .FirstOrDefaultAsync(q => q.Id == quizId);

            if (quiz == null ||
                quiz.Lesson == null ||
                quiz.Lesson.Course == null)
            {
                return NotFound();
            }

            var isEnrolled = await _context.Enrollments
                .AnyAsync(e =>
                    e.CourseId == quiz.Lesson.CourseId &&
                    e.StudentId == currentUser.Id);

            if (!isEnrolled)
            {
                return Forbid();
            }

            decimal totalScore = 0;

            var results = new List<object>();

            foreach (var question in quiz.Questions
                .OrderBy(q => q.OrderNumber)
                .ThenBy(q => q.Id))
            {
                int? selectedOptionId = null;

                if (answers != null &&
                    answers.TryGetValue(
                        question.Id,
                        out var selectedId))
                {
                    selectedOptionId = selectedId;
                }

                var correctOption = question.Options
                    .FirstOrDefault(o => o.IsCorrect);

                var selectedOption =
                    selectedOptionId.HasValue
                        ? question.Options.FirstOrDefault(
                            o => o.Id == selectedOptionId.Value)
                        : null;

                bool isCorrect =
                    selectedOption != null &&
                    selectedOption.IsCorrect;

                if (isCorrect)
                {
                    totalScore += question.Score;
                }

                results.Add(new
                {
                    questionId = question.Id,
                    questionOrder = question.OrderNumber,
                    questionText = question.QuestionText,
                    selectedText = selectedOption?.OptionText,
                    correctText = correctOption?.OptionText,
                    isCorrect = isCorrect,
                    score = question.Score
                });
            }

            var totalQuestionScore =
                quiz.Questions.Sum(q => q.Score);

            return Json(new
            {
                success = true,
                score = totalScore,
                maxScore = totalQuestionScore,
                quizMaxScore = quiz.MaxScore,
                results = results
            });
        }
        // =====================================================
        // POST SỬA BÀI
        // =====================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditLesson(
            int id,
            int chapterId,
            string title,
            string? content,
            string? lessonType,
            int orderNumber,
            IFormFile? attachment,
            bool removeAttachment = false)
        {
            var lesson =
                await _context.Lessons
                    .Include(l => l.Course)
                    .FirstOrDefaultAsync(
                        l => l.Id == id);


            if (lesson == null)
            {
                return NotFound();
            }


            if (lesson.Course == null)
            {
                return NotFound();
            }


            if (!await IsCourseTeacherAsync(
                    lesson.Course))
            {
                return Forbid();
            }


            if (string.IsNullOrWhiteSpace(title))
            {
                TempData["Error"] =
                    "Vui lòng nhập tên bài giảng.";

                return RedirectToAction(
                    "Details",
                    new
                    {
                        id =
                            lesson.CourseId
                    });
            }


            var chapter =
                await _context.CourseChapters
                    .FirstOrDefaultAsync(
                        c =>
                            c.Id == chapterId &&
                            c.CourseId ==
                                lesson.CourseId);


            if (chapter == null)
            {
                TempData["Error"] =
                    "Chương không thuộc khóa học này.";

                return RedirectToAction(
                    "Details",
                    new
                    {
                        id =
                            lesson.CourseId
                    });
            }


            if (orderNumber < 1)
            {
                orderNumber = 1;
            }


            // ---------------------------------------------
            // Xóa file cũ
            // ---------------------------------------------

            if (removeAttachment &&
                !string.IsNullOrWhiteSpace(
                    lesson.AttachmentPath))
            {
                DeleteLessonAttachment(
                    lesson.AttachmentPath);

                lesson.AttachmentFileName =
                    null;

                lesson.AttachmentPath =
                    null;
            }


            // ---------------------------------------------
            // Upload file mới
            // ---------------------------------------------

            if (attachment != null &&
                attachment.Length > 0)
            {
                var result =
                    await SaveLessonAttachmentAsync(
                        attachment);


                if (!result.Success)
                {
                    TempData["Error"] =
                        result.ErrorMessage;

                    return RedirectToAction(
                        "Details",
                        new
                        {
                            id =
                                lesson.CourseId
                        });
                }


                // Xóa file cũ
                if (!string.IsNullOrWhiteSpace(
                        lesson.AttachmentPath))
                {
                    DeleteLessonAttachment(
                        lesson.AttachmentPath);
                }


                lesson.AttachmentFileName =
                    result.OriginalFileName;

                lesson.AttachmentPath =
                    result.PublicPath;
            }


            // ---------------------------------------------
            // Cập nhật bài
            // ---------------------------------------------

            lesson.Title =
                title.Trim();

            lesson.Content =
                content;

            lesson.LessonType =
                string.IsNullOrWhiteSpace(lessonType)
                    ? "Lecture / Lý thuyết"
                    : lessonType.Trim();

            lesson.OrderNumber =
                orderNumber;

            lesson.ChapterId =
                chapter.Id;

            lesson.ChapterName =
                chapter.Name;

            lesson.ChapterOrder =
                chapter.OrderNumber;


            await _context.SaveChangesAsync();


            TempData["Success"] =
                "Đã cập nhật bài giảng.";


            return RedirectToAction(
                "Details",
                new
                {
                    id =
                        lesson.CourseId
                });
        }


        // =====================================================
        // XÓA BÀI
        // =====================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteLesson(
            int id)
        {
            var lesson =
                await _context.Lessons
                    .Include(l => l.Course)
                    .FirstOrDefaultAsync(
                        l => l.Id == id);


            if (lesson == null)
            {
                return NotFound();
            }


            if (lesson.Course == null)
            {
                return NotFound();
            }


            if (!await IsCourseTeacherAsync(
                    lesson.Course))
            {
                return Forbid();
            }


            var courseId =
                lesson.CourseId;


            // ---------------------------------------------
            // Xóa file vật lý
            // ---------------------------------------------

            if (!string.IsNullOrWhiteSpace(
                    lesson.AttachmentPath))
            {
                DeleteLessonAttachment(
                    lesson.AttachmentPath);
            }


            // Assignment và Quiz
            // sẽ được database cascade xóa
            // theo quan hệ Lesson -> Assignment
            // và Lesson -> Quiz.


            _context.Lessons.Remove(
                lesson);

            await _context.SaveChangesAsync();


            TempData["Success"] =
                "Đã xóa bài giảng.";


            return RedirectToAction(
                "Details",
                new
                {
                    id =
                        courseId
                });
        }


        // =====================================================
        // SAVE FILE BÀI GIẢNG
        // =====================================================

        private async Task<FileSaveResult>
            SaveLessonAttachmentAsync(
                IFormFile file)
        {
            const long maxFileSize =
                20 * 1024 * 1024;


            var allowedExtensions =
                new[]
                {
                    ".pdf",
                    ".doc",
                    ".docx",
                    ".ppt",
                    ".pptx",
                    ".xls",
                    ".xlsx",
                    ".zip",
                    ".rar",
                    ".jpg",
                    ".jpeg",
                    ".png"
                };


            if (file.Length > maxFileSize)
            {
                return FileSaveResult.Fail(
                    "File vượt quá dung lượng cho phép 20MB.");
            }


            var extension =
                Path.GetExtension(
                    file.FileName)
                    .ToLowerInvariant();


            if (!allowedExtensions.Contains(
                    extension))
            {
                return FileSaveResult.Fail(
                    "Định dạng file không được hỗ trợ.");
            }


            var uploadFolder =
                Path.Combine(
                    _environment.WebRootPath,
                    "uploads",
                    "lessons");


            Directory.CreateDirectory(
                uploadFolder);


            var newFileName =
                $"{Guid.NewGuid():N}{extension}";


            var physicalPath =
                Path.Combine(
                    uploadFolder,
                    newFileName);


            await using (
                var stream =
                    new FileStream(
                        physicalPath,
                        FileMode.Create))
            {
                await file.CopyToAsync(
                    stream);
            }


            return FileSaveResult.Ok(
                Path.GetFileName(
                    file.FileName),
                $"/uploads/lessons/{newFileName}");
        }


        // =====================================================
        // DELETE FILE BÀI GIẢNG
        // =====================================================

        private void DeleteLessonAttachment(
            string? attachmentPath)
        {
            if (string.IsNullOrWhiteSpace(
                    attachmentPath))
            {
                return;
            }


            const string prefix =
                "/uploads/lessons/";


            if (!attachmentPath.StartsWith(
                    prefix,
                    StringComparison.OrdinalIgnoreCase))
            {
                return;
            }


            var fileName =
                Path.GetFileName(
                    attachmentPath);


            if (string.IsNullOrWhiteSpace(
                    fileName))
            {
                return;
            }


            var physicalPath =
                Path.Combine(
                    _environment.WebRootPath,
                    "uploads",
                    "lessons",
                    fileName);


            if (System.IO.File.Exists(
                    physicalPath))
            {
                System.IO.File.Delete(
                    physicalPath);
            }
        }


        // =====================================================
        // =====================================================
        // LESSON SECTION
        // =====================================================
        // =====================================================


        // Dùng cho:
        // Giới thiệu
        // Thông tin chung học phần
        // Chi tiết học phần
        // CLO, CĐR, Bloom
        // Kiểm tra, đánh giá
        // Chính sách học phần
        //
        // Những nội dung này KHÔNG thuộc CourseChapter.
        // =====================================================


        // =====================================================
        // THÊM LESSON SECTION
        // =====================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateSection(
            int courseId,
            string title,
            string? sectionType,
            string? content,
            int orderNumber,
            IFormFile? attachment)
        {
            var course =
                await _context.Courses
                    .FirstOrDefaultAsync(
                        c => c.Id == courseId);


            if (course == null)
            {
                TempData["Error"] =
                    "Không tìm thấy khóa học.";

                return RedirectToAction(
                    "Index");
            }


            if (!await IsCourseTeacherAsync(course))
            {
                return Forbid();
            }


            if (string.IsNullOrWhiteSpace(title))
            {
                TempData["Error"] =
                    "Vui lòng nhập tên nội dung.";

                return RedirectToAction(
                    "Details",
                    new
                    {
                        id =
                            courseId
                    });
            }


            if (orderNumber < 1)
            {
                orderNumber = 1;
            }


            // ---------------------------------------------
            // Upload file
            // ---------------------------------------------

            string? attachmentFileName =
                null;

            string? attachmentPath =
                null;


            if (attachment != null &&
                attachment.Length > 0)
            {
                var result =
                    await SaveSectionAttachmentAsync(
                        attachment);


                if (!result.Success)
                {
                    TempData["Error"] =
                        result.ErrorMessage;

                    return RedirectToAction(
                        "Details",
                        new
                        {
                            id =
                                courseId
                        });
                }


                attachmentFileName =
                    result.OriginalFileName;

                attachmentPath =
                    result.PublicPath;
            }


            // ---------------------------------------------
            // Tạo LessonSection
            // ---------------------------------------------

            var section =
                new LessonSection
                {
                    CourseId =
                        courseId,

                    Title =
                        title.Trim(),

                    SectionType =
                        string.IsNullOrWhiteSpace(
                            sectionType)
                            ? null
                            : sectionType.Trim(),

                    Content =
                        content,

                    OrderNumber =
                        orderNumber,

                    AttachmentFileName =
                        attachmentFileName,

                    AttachmentPath =
                        attachmentPath,

                    CreatedAt =
                        DateTime.Now
                };


            _context.LessonSections.Add(
                section);

            await _context.SaveChangesAsync();


            TempData["Success"] =
                "Đã thêm nội dung khóa học.";


            return RedirectToAction(
                "Details",
                new
                {
                    id =
                        courseId
                });
        }


        // =====================================================
        // SỬA LESSON SECTION
        // =====================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditSection(
            int id,
            string title,
            string? sectionType,
            string? content,
            int orderNumber,
            IFormFile? attachment,
            bool removeAttachment = false)
        {
            var section =
                await _context.LessonSections
                    .Include(s => s.Course)
                    .FirstOrDefaultAsync(
                        s => s.Id == id);


            if (section == null)
            {
                return NotFound();
            }


            if (section.Course == null)
            {
                return NotFound();
            }


            if (!await IsCourseTeacherAsync(
                    section.Course))
            {
                return Forbid();
            }


            if (string.IsNullOrWhiteSpace(title))
            {
                TempData["Error"] =
                    "Vui lòng nhập tên nội dung.";

                return RedirectToAction(
                    "Details",
                    new
                    {
                        id =
                            section.CourseId
                    });
            }


            if (orderNumber < 1)
            {
                orderNumber = 1;
            }


            // ---------------------------------------------
            // Xóa file hiện tại
            // ---------------------------------------------

            if (removeAttachment &&
                !string.IsNullOrWhiteSpace(
                    section.AttachmentPath))
            {
                DeleteSectionAttachment(
                    section.AttachmentPath);

                section.AttachmentFileName =
                    null;

                section.AttachmentPath =
                    null;
            }


            // ---------------------------------------------
            // Upload file mới
            // ---------------------------------------------

            if (attachment != null &&
                attachment.Length > 0)
            {
                var result =
                    await SaveSectionAttachmentAsync(
                        attachment);


                if (!result.Success)
                {
                    TempData["Error"] =
                        result.ErrorMessage;

                    return RedirectToAction(
                        "Details",
                        new
                        {
                            id =
                                section.CourseId
                        });
                }


                if (!string.IsNullOrWhiteSpace(
                        section.AttachmentPath))
                {
                    DeleteSectionAttachment(
                        section.AttachmentPath);
                }


                section.AttachmentFileName =
                    result.OriginalFileName;

                section.AttachmentPath =
                    result.PublicPath;
            }


            // ---------------------------------------------
            // Cập nhật
            // ---------------------------------------------

            section.Title =
                title.Trim();

            section.SectionType =
                string.IsNullOrWhiteSpace(
                    sectionType)
                    ? null
                    : sectionType.Trim();

            section.Content =
                content;

            section.OrderNumber =
                orderNumber;


            await _context.SaveChangesAsync();


            TempData["Success"] =
                "Đã cập nhật nội dung khóa học.";


            return RedirectToAction(
                "Details",
                new
                {
                    id =
                        section.CourseId
                });
        }


        // =====================================================
        // XÓA LESSON SECTION
        // =====================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteSection(
            int id)
        {
            var section =
                await _context.LessonSections
                    .Include(s => s.Course)
                    .FirstOrDefaultAsync(
                        s => s.Id == id);


            if (section == null)
            {
                return NotFound();
            }


            if (section.Course == null)
            {
                return NotFound();
            }


            if (!await IsCourseTeacherAsync(
                    section.Course))
            {
                return Forbid();
            }


            var courseId =
                section.CourseId;


            // ---------------------------------------------
            // Xóa file vật lý
            // ---------------------------------------------

            if (!string.IsNullOrWhiteSpace(
                    section.AttachmentPath))
            {
                DeleteSectionAttachment(
                    section.AttachmentPath);
            }


            _context.LessonSections.Remove(
                section);

            await _context.SaveChangesAsync();


            TempData["Success"] =
                "Đã xóa nội dung khóa học.";


            return RedirectToAction(
                "Details",
                new
                {
                    id =
                        courseId
                });
        }


        // =====================================================
        // SAVE FILE LESSON SECTION
        // =====================================================

        private async Task<FileSaveResult>
            SaveSectionAttachmentAsync(
                IFormFile file)
        {
            const long maxFileSize =
                20 * 1024 * 1024;


            var allowedExtensions =
                new[]
                {
                    ".pdf",
                    ".doc",
                    ".docx",
                    ".ppt",
                    ".pptx",
                    ".xls",
                    ".xlsx",
                    ".zip",
                    ".rar",
                    ".jpg",
                    ".jpeg",
                    ".png"
                };


            if (file.Length > maxFileSize)
            {
                return FileSaveResult.Fail(
                    "File vượt quá dung lượng cho phép 20MB.");
            }


            var extension =
                Path.GetExtension(
                    file.FileName)
                    .ToLowerInvariant();


            if (!allowedExtensions.Contains(
                    extension))
            {
                return FileSaveResult.Fail(
                    "Định dạng file không được hỗ trợ.");
            }


            var uploadFolder =
                Path.Combine(
                    _environment.WebRootPath,
                    "uploads",
                    "lesson-sections");


            Directory.CreateDirectory(
                uploadFolder);


            var newFileName =
                $"{Guid.NewGuid():N}{extension}";


            var physicalPath =
                Path.Combine(
                    uploadFolder,
                    newFileName);


            await using (
                var stream =
                    new FileStream(
                        physicalPath,
                        FileMode.Create))
            {
                await file.CopyToAsync(
                    stream);
            }


            return FileSaveResult.Ok(
                Path.GetFileName(
                    file.FileName),
                $"/uploads/lesson-sections/{newFileName}");
        }


        // =====================================================
        // DELETE FILE LESSON SECTION
        // =====================================================

        private void DeleteSectionAttachment(
            string? attachmentPath)
        {
            if (string.IsNullOrWhiteSpace(
                    attachmentPath))
            {
                return;
            }


            const string prefix =
                "/uploads/lesson-sections/";


            if (!attachmentPath.StartsWith(
                    prefix,
                    StringComparison.OrdinalIgnoreCase))
            {
                return;
            }


            var fileName =
                Path.GetFileName(
                    attachmentPath);


            if (string.IsNullOrWhiteSpace(
                    fileName))
            {
                return;
            }


            var physicalPath =
                Path.Combine(
                    _environment.WebRootPath,
                    "uploads",
                    "lesson-sections",
                    fileName);


            if (System.IO.File.Exists(
                    physicalPath))
            {
                System.IO.File.Delete(
                    physicalPath);
            }
        }


        // =====================================================
        // KẾT QUẢ LƯU FILE
        // =====================================================

        private sealed class FileSaveResult
        {
            public bool Success
            {
                get;
                private set;
            }


            public string? OriginalFileName
            {
                get;
                private set;
            }


            public string? PublicPath
            {
                get;
                private set;
            }


            public string ErrorMessage
            {
                get;
                private set;
            } = string.Empty;


            public static FileSaveResult Ok(
                string originalFileName,
                string publicPath)
            {
                return new FileSaveResult
                {
                    Success =
                        true,

                    OriginalFileName =
                        originalFileName,

                    PublicPath =
                        publicPath
                };
            }


            public static FileSaveResult Fail(
                string message)
            {
                return new FileSaveResult
                {
                    Success =
                        false,

                    ErrorMessage =
                        message
                };
            }
        }


        // =====================================================
        // XÓA RIÊNG NỘI DUNG CHI TIẾT CỦA LESSON SECTION
        //
        // Không xóa:
        // - Title
        // - SectionType
        // - OrderNumber
        // - AttachmentFileName
        // - AttachmentPath
        // =====================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult>
            ClearSectionContent(
                int id)
        {
            var section =
                await _context.LessonSections
                    .Include(s => s.Course)
                    .FirstOrDefaultAsync(
                        s => s.Id == id);


            if (section == null)
            {
                return NotFound();
            }


            if (section.Course == null)
            {
                return NotFound();
            }


            if (!await IsCourseTeacherAsync(
                    section.Course))
            {
                return Forbid();
            }


            section.Content =
                null;


            await _context.SaveChangesAsync();


            TempData["Success"] =
                "Đã xóa nội dung chi tiết.";


            return RedirectToAction(
                "Details",
                new
                {
                    id =
                        section.CourseId
                });
        }
    }
}