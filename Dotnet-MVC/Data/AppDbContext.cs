using DotnetMVCApp.Models;
using HiringAssistance.Models;
using Microsoft.EntityFrameworkCore;

namespace DotnetMVCApp.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<User> AppUsers { get; set; }
        public DbSet<Job> Jobs { get; set; }
        public DbSet<Interview> Interviews { get; set; }
        public DbSet<Feedback> Feedbacks { get; set; }
        public DbSet<UserJob> UserJobs { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Many-to-many: User <-> Job
            modelBuilder.Entity<UserJob>()
                .HasKey(uj => new { uj.UserId, uj.JobId });

            modelBuilder.Entity<UserJob>()
                .HasOne(uj => uj.User)
                .WithMany(u => u.JobsApplied)
                .HasForeignKey(uj => uj.UserId);

            modelBuilder.Entity<UserJob>()
                .HasOne(uj => uj.Job)
                .WithMany(j => j.Applicants)
                .HasForeignKey(uj => uj.JobId);

            // One-to-many: HR -> Jobs Posted
            modelBuilder.Entity<Job>()
                .HasOne(j => j.PostedBy)
                .WithMany(u => u.JobsPosted)
                .HasForeignKey(j => j.PostedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            // One-to-many: User -> Interviews
            modelBuilder.Entity<Interview>()
                .HasOne(i => i.User)
                .WithMany(u => u.Interviews)
                .HasForeignKey(i => i.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // One-to-many: Job -> Interviews
            modelBuilder.Entity<Interview>()
                .HasOne(i => i.Job)
                .WithMany(j => j.Interviews)
                .HasForeignKey(i => i.JobId)
                .OnDelete(DeleteBehavior.Cascade);

            // One-to-many: Candidate -> Feedbacks
            modelBuilder.Entity<Feedback>()
                .HasOne(f => f.User)
                .WithMany(u => u.Feedbacks)
                .HasForeignKey(f => f.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // One-to-many: Job -> Feedbacks
            modelBuilder.Entity<Feedback>()
                .HasOne(f => f.Job)
                .WithMany(j => j.Feedbacks)
                .HasForeignKey(f => f.JobId)
                .OnDelete(DeleteBehavior.Cascade);

            // Ensure JSON columns use jsonb in PostgreSQL (if applicable)
            modelBuilder.Entity<User>()
                .Property(u => u.ExtractedInfo)
                .HasColumnType("jsonb");

            modelBuilder.Entity<Job>()
                .Property(j => j.SkillsRequired)
                .HasColumnType("jsonb");

            modelBuilder.Entity<Job>()
                .Property(j => j.Quiz)
                .HasColumnType("jsonb");

            modelBuilder.Entity<Interview>()
                .Property(i => i.Score)
                .HasColumnType("jsonb");
        }
    }
}
