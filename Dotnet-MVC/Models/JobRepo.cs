using DotnetMVCApp.Data;
using DotnetMVCApp.Models;
using Microsoft.EntityFrameworkCore;

namespace DotnetMVCApp.Repositories
{
    public class JobRepo : IJobRepo
    {
        private readonly AppDbContext _context;

        public JobRepo(AppDbContext context)
        {
            _context = context;
        }

        public IEnumerable<Job> GetAllJobs()
        {
            return _context.Jobs
                .Include(j => j.PostedBy)
                .Include(j => j.Applicants)
                .Include(j => j.Interviews)
                .Include(j => j.Feedbacks)
                .ToList();
        }

        public Job GetJobById(int id)
        {
            return _context.Jobs
                .Include(j => j.PostedBy)
                .Include(j => j.Applicants)
                .Include(j => j.Interviews)
                .Include(j => j.Feedbacks)
                .FirstOrDefault(j => j.JobId == id);
        }

        public Job Add(Job job)
        {
            _context.Jobs.Add(job);
            _context.SaveChanges();
            return job;
        }

        public Job Update(Job job)
        {
            _context.Jobs.Update(job);
            _context.SaveChanges();
            return job;
        }

        public Job Delete(int id)
        {
            var job = _context.Jobs.Find(id);
            if (job != null)
            {
                _context.Jobs.Remove(job);
                _context.SaveChanges();
            }
            return job;
        }
    }
}
