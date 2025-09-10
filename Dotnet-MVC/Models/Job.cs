using DotnetMVCApp.Models;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

public class Job
{
    [Key]
    public int JobId { get; set; }

    [Required]
    [MaxLength(200)]   // ✅ new field for title
    public string JobTitle { get; set; }

    [Required]
    [Column(TypeName = "text")]   // ✅ large text instead of varchar
    public string? JobDescription { get; set; }

    public string? TechStacks { get; set; }

    [Column(TypeName = "jsonb")]
    public string? SkillsRequired { get; set; }

    public DateTime OpenTime { get; set; } = DateTime.UtcNow;
    public DateTime CloseTime { get; set; } = DateTime.UtcNow;

    [Column(TypeName = "jsonb")]
    public string? Quiz { get; set; }

    public ICollection<UserJob>? Applicants { get; set; }

    public int PostedByUserId { get; set; }
    public User? PostedBy { get; set; }

    public ICollection<Interview>? Interviews { get; set; }
    public ICollection<Feedback>? Feedbacks { get; set; }
}
