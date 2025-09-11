using DotnetMVCApp.Data;
using DotnetMVCApp.Models;
using System.Linq;

namespace DotnetMVCApp.Repositories
{
    public class UserJobRepo : IUserJobRepo
    {
        private readonly AppDbContext _context;
        public IJobRepo jobRepo { get; }
        public IUserRepo userRepo { get; }

        public UserJobRepo(AppDbContext context, IJobRepo jobRepo, IUserRepo userRepo)
        {
            _context = context;
            this.jobRepo = jobRepo;
            this.userRepo = userRepo;
        }

        public void ApplyToJob(int userId, int jobId, string? scoreJson = null)
        {
            if (HasApplied(userId, jobId))
                return;

            var userJob = new UserJob
            {
                UserId = userId,
                JobId = jobId,
                Score = scoreJson
            };

            _context.UserJobs.Add(userJob);
            save();
        }

        public bool HasApplied(int userId, int jobId)
        {
            return _context.UserJobs.Any(uj => uj.UserId == userId && uj.JobId == jobId);
        }

        public bool Exists(int userId, int jobId)
        {
            return _context.UserJobs
                .Any(uj => uj.UserId == userId && uj.JobId == jobId);
        }

        public IEnumerable<UserJob> GetJobsByUser(int userId)
        {
            return _context.UserJobs.Where(uj => uj.UserId == userId).ToList();
        }

        public UserJob? GetByUserAndJob(int userId, int jobId)
        {
            return _context.UserJobs.FirstOrDefault(uj => uj.UserId == userId && uj.JobId == jobId);
        }

        public void Delete(UserJob userJob)
        {
            _context.UserJobs.Remove(userJob);
        }


        public void save()
        {
            _context.SaveChanges();
        }
    }
}
