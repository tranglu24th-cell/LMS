using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Web.Models.EF;

namespace Web.Controllers
{
    /// <summary>
    /// Trợ giảng AI của hệ thống E-Learning.
    ///
    /// Khác với bản cũ (chatbot tư vấn bánh, lịch sử hội thoại giữ tạm trong JS),
    /// controller này:
    ///   1. Dùng system prompt sư phạm: giải thích khái niệm cho sinh viên.
    ///   2. Lấy ngữ cảnh học tập THẲNG TỪ CSDL (khóa học, bài học, lớp đang theo học).
    ///   3. Lưu mọi lượt hỏi/đáp xuống bảng ChatConversations / ChatMessages
    ///      và nạp lại lịch sử từ CSDL ở mỗi request.
    /// </summary>
    public class ChatController : Controller
    {
        /// <summary>Số message gần nhất nạp lại từ CSDL làm ngữ cảnh cho model.</summary>
        private const int ContextMessageLimit = 20;

        /// <summary>Số message trả về cho giao diện khi mở lại khung chat.</summary>
        private const int HistoryMessageLimit = 50;

        private readonly FoodContext _db;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _config;

        public ChatController(
            FoodContext db,
            IHttpClientFactory httpClientFactory,
            IConfiguration config)
        {
            _db = db;
            _httpClientFactory = httpClientFactory;
            _config = config;
        }

        // =====================================================================
        // DTO
        // =====================================================================

        public class ChatRequest
        {
            public string Message { get; set; } = "";

            /// <summary>Hội thoại đang mở. Null = tạo hội thoại mới.</summary>
            public int? ConversationId { get; set; }

            /// <summary>Khóa học sinh viên đang xem (nếu có) - dùng làm ngữ cảnh.</summary>
            public int? CourseId { get; set; }

            /// <summary>Bài học sinh viên đang xem (nếu có) - dùng làm ngữ cảnh.</summary>
            public int? LessonId { get; set; }
        }

        public class ChatTurn
        {
            public string Role { get; set; } = "";
            public string Content { get; set; } = "";
        }

        // =====================================================================
        // GET /Chat/History
        // Nạp lại hội thoại từ CSDL khi người dùng mở khung chat.
        // Không truyền conversationId -> lấy hội thoại gần nhất của sinh viên.
        // =====================================================================

        [HttpGet]
        public async Task<IActionResult> History(int? conversationId)
        {
            var userId = GetCurrentUserId();
            if (userId == null)
            {
                return Json(new
                {
                    success = true,
                    signedIn = false,
                    conversationId = (int?)null,
                    messages = Array.Empty<object>()
                });
            }

            var conversation = conversationId.HasValue
                ? await _db.ChatConversations
                    .FirstOrDefaultAsync(c => c.Id == conversationId.Value && c.UserId == userId.Value)
                : await _db.ChatConversations
                    .Where(c => c.UserId == userId.Value)
                    .OrderByDescending(c => c.Id)
                    .FirstOrDefaultAsync();

            if (conversation == null)
            {
                return Json(new
                {
                    success = true,
                    signedIn = true,
                    conversationId = (int?)null,
                    messages = Array.Empty<object>()
                });
            }

            // Lấy N message mới nhất rồi đảo lại cho đúng thứ tự thời gian.
            var messages = await _db.ChatMessages
                .Where(m => m.ConversationId == conversation.Id)
                .OrderByDescending(m => m.Id)
                .Take(HistoryMessageLimit)
                .Select(m => new { m.Role, m.Content, m.CreatedAt })
                .ToListAsync();

            messages.Reverse();

            return Json(new
            {
                success = true,
                signedIn = true,
                conversationId = conversation.Id,
                title = conversation.Title,
                messages
            });
        }

        // =====================================================================
        // POST /Chat/NewConversation
        // Bắt đầu một hội thoại mới (nút "Cuộc trò chuyện mới").
        // =====================================================================

        [HttpPost]
        public IActionResult NewConversation()
        {
            // Không tạo bản ghi rỗng trong CSDL: chỉ cần client quên conversationId,
            // hội thoại mới sẽ được tạo ở lần gửi tin nhắn kế tiếp.
            return Json(new { success = true, conversationId = (int?)null });
        }

        // =====================================================================
        // POST /Chat/SendMessage
        // =====================================================================

        [HttpPost]
        public async Task<IActionResult> SendMessage([FromBody] ChatRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Message))
            {
                return Json(new { success = false, reply = "Bạn chưa nhập nội dung câu hỏi." });
            }

            var userMessage = request.Message.Trim();

            try
            {
                var userId = GetCurrentUserId();

                // -------------------------------------------------------------
                // 1. Xác định / tạo hội thoại và LƯU câu hỏi của sinh viên
                // -------------------------------------------------------------
                ChatConversation? conversation = null;

                if (userId != null)
                {
                    conversation = await ResolveConversationAsync(userId.Value, request.ConversationId, userMessage);

                    _db.ChatMessages.Add(new ChatMessage
                    {
                        ConversationId = conversation.Id,
                        Role = "user",
                        Content = userMessage,
                        CreatedAt = DateTime.Now
                    });

                    await _db.SaveChangesAsync();
                }

                // -------------------------------------------------------------
                // 2. Nạp lịch sử hội thoại TỪ CSDL (không lấy từ JS nữa)
                // -------------------------------------------------------------
                var history = conversation == null
                    ? new List<ChatTurn>()
                    : await LoadContextFromDatabaseAsync(conversation.Id, excludeLastUserMessage: true);

                // -------------------------------------------------------------
                // 3. Dựng ngữ cảnh học tập từ CSDL + system prompt trợ giảng
                // -------------------------------------------------------------
                var learningContext = await BuildLearningContextAsync(userId, request.CourseId, request.LessonId);
                var systemPrompt = BuildTutorSystemPrompt(learningContext);

                // -------------------------------------------------------------
                // 4. Gọi model
                // -------------------------------------------------------------
                var messages = new List<object>
                {
                    new { role = "system", content = systemPrompt }
                };

                foreach (var turn in history)
                {
                    messages.Add(new { role = NormalizeRole(turn.Role), content = turn.Content });
                }

                messages.Add(new { role = "user", content = userMessage });

                var (ok, replyText) = await CallModelAsync(messages);

                if (!ok)
                {
                    // Vẫn giữ câu hỏi đã lưu, không lưu câu trả lời lỗi vào lịch sử.
                    return Json(new
                    {
                        success = false,
                        reply = replyText,
                        conversationId = conversation?.Id
                    });
                }

                // -------------------------------------------------------------
                // 5. LƯU câu trả lời của trợ giảng
                // -------------------------------------------------------------
                if (conversation != null)
                {
                    _db.ChatMessages.Add(new ChatMessage
                    {
                        ConversationId = conversation.Id,
                        Role = "assistant",
                        Content = replyText,
                        CreatedAt = DateTime.Now
                    });

                    await _db.SaveChangesAsync();
                }

                return Json(new
                {
                    success = true,
                    reply = replyText,
                    conversationId = conversation?.Id,
                    persisted = conversation != null
                });
            }
            catch (Exception ex)
            {
                return Json(new
                {
                    success = false,
                    reply = "Trợ giảng đang gặp sự cố kỹ thuật. Bạn thử lại sau ít phút nhé. "
                          + $"(Chi tiết: {ex.Message})"
                });
            }
        }

        // =====================================================================
        // HỘI THOẠI
        // =====================================================================

        /// <summary>
        /// Lấy hội thoại đang mở của sinh viên, hoặc tạo mới nếu chưa có.
        /// Luôn kiểm tra hội thoại thuộc đúng sinh viên đang đăng nhập.
        /// </summary>
        private async Task<ChatConversation> ResolveConversationAsync(
            Guid userId, int? conversationId, string firstMessage)
        {
            if (conversationId.HasValue)
            {
                var existing = await _db.ChatConversations
                    .FirstOrDefaultAsync(c => c.Id == conversationId.Value && c.UserId == userId);

                if (existing != null)
                    return existing;
            }

            var conversation = new ChatConversation
            {
                UserId = userId,
                Title = Truncate(firstMessage, 200),
                CreatedAt = DateTime.Now
            };

            _db.ChatConversations.Add(conversation);
            await _db.SaveChangesAsync();

            return conversation;
        }

        /// <summary>
        /// Nạp ngữ cảnh hội thoại từ bảng ChatMessages.
        /// </summary>
        /// <param name="excludeLastUserMessage">
        /// Bỏ message "user" cuối cùng vì nó sẽ được gửi riêng ở cuối danh sách.
        /// </param>
        private async Task<List<ChatTurn>> LoadContextFromDatabaseAsync(
            int conversationId, bool excludeLastUserMessage)
        {
            var recent = await _db.ChatMessages
                .Where(m => m.ConversationId == conversationId)
                .OrderByDescending(m => m.Id)
                .Take(ContextMessageLimit + 1)
                .Select(m => new ChatTurn { Role = m.Role, Content = m.Content })
                .ToListAsync();

            // Đang ở thứ tự mới -> cũ. Bỏ phần tử đầu (chính là câu hỏi vừa lưu).
            if (excludeLastUserMessage && recent.Count > 0
                && string.Equals(recent[0].Role, "user", StringComparison.OrdinalIgnoreCase))
            {
                recent.RemoveAt(0);
            }

            if (recent.Count > ContextMessageLimit)
                recent = recent.Take(ContextMessageLimit).ToList();

            recent.Reverse();
            return recent;
        }

        // =====================================================================
        // NGỮ CẢNH HỌC TẬP LẤY TỪ CSDL
        // =====================================================================

        /// <summary>
        /// Gom dữ liệu thật từ CSDL: sinh viên là ai, đang xem bài nào,
        /// khóa học đó có những bài gì, sinh viên đang theo học những khóa nào.
        /// </summary>
        private async Task<string> BuildLearningContextAsync(Guid? userId, int? courseId, int? lessonId)
        {
            var sb = new StringBuilder();

            // ---- Sinh viên ----
            if (userId != null)
            {
                var student = await _db.Users
                    .Where(u => u.Id == userId.Value)
                    .Select(u => new { u.FullName, u.MSSV, u.ClassName, u.Role })
                    .FirstOrDefaultAsync();

                if (student != null)
                {
                    sb.AppendLine("### Người học đang trò chuyện");
                    sb.AppendLine($"- Họ tên: {student.FullName}");
                    if (!string.IsNullOrWhiteSpace(student.MSSV))
                        sb.AppendLine($"- MSSV: {student.MSSV}");
                    if (!string.IsNullOrWhiteSpace(student.ClassName))
                        sb.AppendLine($"- Lớp: {student.ClassName}");
                    sb.AppendLine();
                }
            }

            // ---- Bài học đang mở ----
            if (lessonId.HasValue)
            {
                var lesson = await _db.Lessons
                    .Include(l => l.Course)
                    .FirstOrDefaultAsync(l => l.Id == lessonId.Value);

                if (lesson != null)
                {
                    sb.AppendLine("### Bài học sinh viên đang mở");
                    sb.AppendLine($"- Tên bài: {lesson.Title}");
                    if (!string.IsNullOrWhiteSpace(lesson.ChapterName))
                        sb.AppendLine($"- Thuộc chương: {lesson.ChapterName}");
                    if (!string.IsNullOrWhiteSpace(lesson.LessonType))
                        sb.AppendLine($"- Loại bài: {lesson.LessonType}");
                    if (lesson.Course != null)
                        sb.AppendLine($"- Khóa học: {lesson.Course.Title}");

                    if (!string.IsNullOrWhiteSpace(lesson.Content))
                    {
                        sb.AppendLine("- Nội dung bài học (trích):");
                        sb.AppendLine(Truncate(StripHtml(lesson.Content), 2500));
                    }

                    sb.AppendLine();

                    // Nếu chưa biết courseId thì suy ra từ bài học.
                    courseId ??= lesson.CourseId;
                }
            }

            // ---- Khóa học đang xem ----
            if (courseId.HasValue)
            {
                var course = await _db.Courses
                    .Include(c => c.Teacher)
                    .FirstOrDefaultAsync(c => c.Id == courseId.Value);

                if (course != null)
                {
                    sb.AppendLine("### Khóa học liên quan");
                    sb.AppendLine($"- Tên khóa học: {course.Title}");
                    if (course.Teacher != null)
                        sb.AppendLine($"- Giảng viên: {course.Teacher.FullName}");
                    if (!string.IsNullOrWhiteSpace(course.Description))
                        sb.AppendLine($"- Mô tả: {Truncate(StripHtml(course.Description), 600)}");
                    if (!string.IsNullOrWhiteSpace(course.Room))
                        sb.AppendLine($"- Phòng học: {course.Room}");
                    if (course.ScheduleDayOfWeek.HasValue)
                    {
                        var day = VietnameseDayName(course.ScheduleDayOfWeek.Value);
                        var time = course.ScheduleStartTime.HasValue && course.ScheduleEndTime.HasValue
                            ? $" ({course.ScheduleStartTime:hh\\:mm} - {course.ScheduleEndTime:hh\\:mm})"
                            : "";
                        sb.AppendLine($"- Lịch học: {day}{time}");
                    }

                    var outline = await _db.Lessons
                        .Where(l => l.CourseId == course.Id)
                        .OrderBy(l => l.ChapterOrder).ThenBy(l => l.OrderNumber)
                        .Select(l => new { l.ChapterName, l.Title })
                        .Take(40)
                        .ToListAsync();

                    if (outline.Count > 0)
                    {
                        sb.AppendLine("- Đề cương các bài học:");
                        foreach (var l in outline)
                        {
                            var chapter = string.IsNullOrWhiteSpace(l.ChapterName) ? "" : $"[{l.ChapterName}] ";
                            sb.AppendLine($"  + {chapter}{l.Title}");
                        }
                    }

                    sb.AppendLine();
                }
            }

            // ---- Các khóa sinh viên đang theo học ----
            if (userId != null)
            {
                var enrolled = await _db.Enrollments
                    .Where(e => e.StudentId == userId.Value)
                    .Include(e => e.Course)
                    .OrderByDescending(e => e.EnrolledAt)
                    .Select(e => e.Course!.Title)
                    .Take(15)
                    .ToListAsync();

                if (enrolled.Count > 0)
                {
                    sb.AppendLine("### Các khóa học sinh viên đang theo học");
                    foreach (var title in enrolled)
                        sb.AppendLine($"- {title}");
                    sb.AppendLine();
                }
            }

            return sb.Length == 0
                ? "(Chưa có dữ liệu khóa học nào gắn với ngữ cảnh hiện tại.)"
                : sb.ToString();
        }

        /// <summary>
        /// System prompt của trợ giảng: giải thích khái niệm, không giải bài hộ.
        /// </summary>
        private static string BuildTutorSystemPrompt(string learningContext)
        {
            return $@"
Bạn là ""Trợ giảng AI"" của hệ thống E-Learning LMS.
Nhiệm vụ chính: GIẢI THÍCH KHÁI NIỆM, thuật ngữ và nội dung bài học cho sinh viên.

PHONG CÁCH
- Luôn trả lời bằng tiếng Việt, xưng ""mình"" và gọi người học là ""bạn"".
- Thân thiện, kiên nhẫn, khích lệ; không phán xét khi sinh viên hỏi câu cơ bản.
- Độ dài vừa phải: 4-8 câu. Dùng gạch đầu dòng khi liệt kê nhiều ý.
- Thuật ngữ tiếng Anh thì giữ nguyên kèm giải nghĩa tiếng Việt trong ngoặc.

C�CH GIẢI THÍCH MỘT KHÁI NIỆM (theo thứ tự)
1. Định nghĩa ngắn gọn bằng ngôn ngữ đời thường, tránh định nghĩa vòng vo.
2. Một ví dụ minh họa cụ thể, gần gũi với sinh viên.
3. Phân biệt với khái niệm dễ nhầm lẫn, hoặc nêu lỗi sai thường gặp.
4. Kết bằng một câu hỏi gợi mở để sinh viên tự kiểm tra hiểu bài.

NGUYÊN TẮC SƯ PHẠM (bắt buộc)
- Với bài tập, đề kiểm tra hay câu hỏi ""đáp án là gì"": KHÔNG đưa đáp án cuối cùng.
  Thay vào đó hãy gợi ý hướng làm, nhắc lại kiến thức cần dùng và đặt câu hỏi dẫn dắt
  để sinh viên tự tìm ra kết quả. Chỉ xác nhận đúng/sai khi sinh viên đã tự trình bày lời giải.
- KHÔNG bịa điểm số, lịch học, tên giảng viên, tài liệu hay nội dung bài học.
  Nếu dữ liệu bên dưới không có, hãy nói rõ là chưa có trong hệ thống
  và hướng dẫn sinh viên xem mục tương ứng trên LMS hoặc hỏi giảng viên.
- Nếu câu hỏi mơ hồ, hãy hỏi lại MỘT câu ngắn để làm rõ trước khi giải thích.
- Nếu câu hỏi nằm ngoài phạm vi học tập, từ chối lịch sự và mời sinh viên quay lại nội dung bài học.

DỮ LIỆU THẬT LẤY TỪ CƠ SỞ DỮ LIỆU CỦA LMS
Hãy ưu tiên bám sát phần dữ liệu này khi câu hỏi liên quan đến khóa học hoặc bài học.

{learningContext}
".Trim();
        }

        // =====================================================================
        // GỌI MODEL
        // =====================================================================

        private async Task<(bool ok, string reply)> CallModelAsync(List<object> messages)
        {
            var apiKey = _config["AIChatBot:ApiKey"];
            var model = _config["AIChatBot:Model"];
            var apiUrl = _config["AIChatBot:ApiUrl"];

            if (string.IsNullOrWhiteSpace(apiKey))
                return (false, "Chưa cấu hình API key cho trợ giảng AI (AIChatBot:ApiKey trong appsettings.json).");

            if (string.IsNullOrWhiteSpace(model))
                model = "gemini-2.5-flash";

            if (string.IsNullOrWhiteSpace(apiUrl))
                apiUrl = "https://generativelanguage.googleapis.com/v1beta/openai/chat/completions";

            var payload = new
            {
                model,
                max_tokens = 700,
                messages
            };

            var client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(60);

            var httpRequest = new HttpRequestMessage(HttpMethod.Post, apiUrl);
            httpRequest.Headers.Add("Authorization", $"Bearer {apiKey}");
            httpRequest.Content = new StringContent(
                JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

            var response = await client.SendAsync(httpRequest);
            var responseBody = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
                return (false, $"Trợ giảng chưa trả lời được (mã {(int)response.StatusCode}). Bạn thử lại giúp mình nhé.");

            using var doc = JsonDocument.Parse(responseBody);

            if (!doc.RootElement.TryGetProperty("choices", out var choices) || choices.GetArrayLength() == 0)
                return (false, "Trợ giảng chưa trả lời được. Bạn thử diễn đạt lại câu hỏi nhé.");

            var replyText = choices[0].GetProperty("message").GetProperty("content").GetString();

            return string.IsNullOrWhiteSpace(replyText)
                ? (false, "Trợ giảng chưa trả lời được. Bạn thử diễn đạt lại câu hỏi nhé.")
                : (true, replyText.Trim());
        }

        // =====================================================================
        // TIỆN ÍCH
        // =====================================================================

        private Guid? GetCurrentUserId()
        {
            var idString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(idString))
                return null;

            return Guid.TryParse(idString, out var id) ? id : null;
        }

        private static string NormalizeRole(string role)
        {
            return role?.ToLowerInvariant() switch
            {
                "assistant" or "model" or "bot" => "assistant",
                "system" => "system",
                _ => "user"
            };
        }

        private static string Truncate(string? text, int max)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;
            text = text.Trim();
            return text.Length <= max ? text : text.Substring(0, max) + "...";
        }

        private static string StripHtml(string html)
        {
            if (string.IsNullOrEmpty(html)) return string.Empty;
            return System.Text.RegularExpressions.Regex
                .Replace(html, "<.*?>", " ")
                .Replace("&nbsp;", " ")
                .Replace("&amp;", "&")
                .Replace("&lt;", "<")
                .Replace("&gt;", ">");
        }

        private static string VietnameseDayName(int dayOfWeek)
        {
            return dayOfWeek switch
            {
                0 => "Chủ nhật",
                1 => "Thứ hai",
                2 => "Thứ ba",
                3 => "Thứ tư",
                4 => "Thứ năm",
                5 => "Thứ sáu",
                6 => "Thứ bảy",
                _ => "Chưa rõ"
            };
        }
    }
}
