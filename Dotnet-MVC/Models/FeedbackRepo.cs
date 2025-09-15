using DotnetMVCApp.Data;
using DotnetMVCApp.Models;
using Microsoft.EntityFrameworkCore;
using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using DotnetMVCApp.Models;
using DotnetMVCApp.Repositories;
using DotnetMVCApp.ViewModels.Feedback;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System.Text;

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

        public IEnumerable<Feedback> GetByJob(int jobId)
        {
            return _context.Feedbacks
                .Include(f => f.User)
                .Include(f => f.Job)
                .Where(f => f.JobId == jobId)
                .ToList();
        }

        public IEnumerable<Feedback> GetByUser(int userId)
        {
            return _context.Feedbacks
                .Include(f => f.User)
                .Include(f => f.Job)
                .Where(f => f.UserId == userId)
                .ToList();
        }

        public Feedback? GetByUserAndJob(int userId, int jobId)
        {
            return _context.Feedbacks
                .Include(f => f.User)
                .Include(f => f.Job)
                .FirstOrDefault(f => f.UserId == userId && f.JobId == jobId);
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
