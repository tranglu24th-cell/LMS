using System;
using System.ComponentModel.DataAnnotations;

namespace Web.Models.EF
{
    public class User
    {
        [Key]
        public Guid Id { get; set; }

        [Required]
        [StringLength(20)]
        public string MSSV { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        public string FullName { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        [StringLength(150)]
        public string Email { get; set; } = string.Empty;

        [Required]
        [StringLength(50)]
        public string Username { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        public string Password { get; set; } = string.Empty;

        [StringLength(50)]
        public string? ClassName { get; set; }
        [Required]
        [StringLength(20)]
        public string Role { get; set; } = "Student";
        public bool IsActive { get; set; } = true;

        public bool IsOnline { get; set; } = false;
        public DateTime CreatedAt { get; set; } = DateTime.Now;

    }
}