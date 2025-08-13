using DotnetMVCApp.Data;
using DotnetMVCApp.Models;
using Microsoft.EntityFrameworkCore;

namespace DotnetMVCApp.Repositories
{
    public class FeedbackRepo : IFeedbackrepo
    {
        private readonly AppDbContext _context;

        public FeedbackRepo(AppDbContext context)
        {
            _context = context;
        }

        public IEnumerable<Feedback> GetAllFeedbacks()
        {
            return _context.Feedbacks
                .Include(f => f.User)
                .Include(f => f.Job)
                .ToList();
        }

        public Feedback GetFeedbackById(int id)
        {
            return _context.Feedbacks
                .Include(f => f.User)
                .Include(f => f.Job)
                .FirstOrDefault(f => f.FeedbackId == id);
        }

        public Feedback Add(Feedback feedback)
        {
            _context.Feedbacks.Add(feedback);
            _context.SaveChanges();
            return feedback;
        }

        public Feedback Update(Feedback feedback)
        {
            _context.Feedbacks.Update(feedback);
            _context.SaveChanges();
            return feedback;
        }

        public Feedback Delete(int id)
        {
            var feedback = _context.Feedbacks.Find(id);
            if (feedback != null)
            {
                _context.Feedbacks.Remove(feedback);
                _context.SaveChanges();
            }
            return feedback;
        }
    }
}
