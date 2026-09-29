using System;
using System.ComponentModel.DataAnnotations;

namespace Web.Models.EF
{
    public class LessonSection
    {
        public int Id { get; set; }

        public int CourseId { get; set; }

        [Required]
        [StringLength(200)]
        public string Title { get; set; } = string.Empty;

        public string? Content { get; set; }

        [StringLength(255)]
        public string? AttachmentFileName { get; set; }

        [StringLength(500)]
        public string? AttachmentPath { get; set; }

        public int OrderNumber { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public Course? Course { get; set; }
        [StringLength(100)]
        public string? SectionType { get; set; }
    }
}