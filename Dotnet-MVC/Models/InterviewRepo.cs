using DotnetMVCApp.Data;
using DotnetMVCApp.Models;
using Microsoft.EntityFrameworkCore;

namespace DotnetMVCApp.Repositories
{
    public class InterviewRepo : IInterviewrepo
    {
        private readonly AppDbContext _context;

        public InterviewRepo(AppDbContext context)
        {
            _context = context;
        }

        public IEnumerable<Interview> GetAllInterview()
        {
            return _context.Interviews
                .Include(i => i.User)
                .Include(i => i.Job)
                .ToList();
        }

        public Interview GetInterviewById(int id)
        {
            return _context.Interviews
                .Include(i => i.User)
                .Include(i => i.Job)
                .FirstOrDefault(i => i.InterviewId == id);
        }

        public Interview Add(Interview interview)
        {
            _context.Interviews.Add(interview);
            _context.SaveChanges();
            return interview;
        }

        public Interview Update(Interview interview)
        {
            _context.Interviews.Update(interview);
            _context.SaveChanges();
            return interview;
        }

        public Interview Delete(int id)
        {
            var interview = _context.Interviews.Find(id);
            if (interview != null)
            {
                _context.Interviews.Remove(interview);
                _context.SaveChanges();
            }
            return interview;
        }

        public IEnumerable<Interview> GetByUser(int userId)
        {
            return _context.Interviews
                .Include(i => i.Job)
                .Where(i => i.UserId == userId)
                .OrderByDescending(i => i.CreatedAt)
                .ToList();
        }




    }
}
