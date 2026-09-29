using Microsoft.AspNetCore.Http;

namespace Web.Areas.Admin.Models
{
    public class CourseViewModel
    {
        public int? Id { get; set; }

        public string? Title { get; set; }

        public string? Description { get; set; }

        public string? Image { get; set; }

        public Guid? TeacherId { get; set; }

        public IFormFile? ImageFile { get; set; }

        // ===== Lịch học =====
        public int? ScheduleDayOfWeek { get; set; }

        public string? ScheduleStartTime { get; set; }

        public string? ScheduleEndTime { get; set; }

        public string? Room { get; set; }
    }
}