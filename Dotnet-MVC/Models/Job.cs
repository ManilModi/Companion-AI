using DotnetMVCApp.Models;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

public class Job
{
    [Key]
    public int JobId { get; set; }

    [Required]
    [MaxLength(200)]
    public string JobTitle { get; set; }

    [Required]
    [Column(TypeName = "text")]
    public string? JobDescription { get; set; }

    public string? TechStacks { get; set; }

    [Column(TypeName = "jsonb")]
    public string? SkillsRequired { get; set; }

    public DateTime OpenTime { get; set; } = DateTime.UtcNow;
    public DateTime CloseTime { get; set; } = DateTime.UtcNow;

    [Column(TypeName = "jsonb")]
    public string? Quiz { get; set; }

    // ✅ New fields
    [Required]
    [MaxLength(150)]
    public string Company { get; set; } = "Unknown Company";

    [MaxLength(150)]
    public string Location { get; set; } = "Not Specified";

    [MaxLength(50)]
    public string JobType { get; set; } = "Full Time";  // Full Time, Part Time, Remote, Internship etc.

    [MaxLength(100)]
    public string SalaryRange { get; set; } = "Not Disclosed";

    public ICollection<UserJob>? Applicants { get; set; }

    public int PostedByUserId { get; set; }
    public User? PostedBy { get; set; }

    public ICollection<Interview>? Interviews { get; set; }
    public ICollection<Feedback>? Feedbacks { get; set; }
}
