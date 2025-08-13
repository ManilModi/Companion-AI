using DotnetMVCApp.Data;
using DotnetMVCApp.Models;
using Microsoft.EntityFrameworkCore;

namespace DotnetMVCApp.Repositories
{
    public class UserRepo : IUserRepo
    {
        private readonly AppDbContext _context;

        public UserRepo(AppDbContext context)
        {
            _context = context;
        }

        public IEnumerable<User> GetAllUser()
        {
            return _context.AppUsers
                .Include(u => u.JobsApplied)
                .Include(u => u.JobsPosted)
                .Include(u => u.Interviews)
                .Include(u => u.Feedbacks)
                .ToList();
        }

        public User GetUserById(int id)
        {
            return _context.AppUsers
                .Include(u => u.JobsApplied)
                .Include(u => u.JobsPosted)
                .Include(u => u.Interviews)
                .Include(u => u.Feedbacks)
                .FirstOrDefault(u => u.UserId == id);
        }

        public User Add(User user)
        {
            _context.AppUsers.Add(user);
            _context.SaveChanges();
            return user;
        }

        public User Update(User userChanges)
        {
            _context.AppUsers.Update(userChanges);
            _context.SaveChanges();
            return userChanges;
        }

        public void Delete(int id)
        {
            var user = _context.AppUsers.Find(id);
            if (user != null)
            {
                _context.AppUsers.Remove(user);
                _context.SaveChanges();
            }
        }
    }
}
