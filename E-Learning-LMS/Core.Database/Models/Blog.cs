using System;
using System.ComponentModel.DataAnnotations;

namespace Core.Database.Models
{
    public class Blog
    {
        [Key]
        public Guid Id { get; set; }

        public string? Title { get; set; }

        public string? Picture { get; set; }

        public string? Content { get; set; }

        public DateTime CreatedOn { get; set; }
    }
}