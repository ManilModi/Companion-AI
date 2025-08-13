using DotnetMVCApp.Data;
using DotnetMVCApp.Models;

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

        public void save()
        {
            _context.SaveChanges();
        }
    }
}
