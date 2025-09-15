using DotnetMVCApp.Data;
using DotnetMVCApp.Models;

public class UnitOfWork : IUnitOfWork
{
    private readonly AppDbContext _context;
    public IUserJobRepo UserJobs { get; }
    public IJobRepo Jobs { get; }
    public IUserRepo Users { get; }
    public IFeedbackrepo Feedbacks { get; }

    public UnitOfWork(AppDbContext context, IUserJobRepo userJobRepo, IJobRepo jobRepo, IUserRepo userRepo, IFeedbackrepo feedbacks)
    {
        _context = context;
        UserJobs = userJobRepo;
        Jobs = jobRepo;
        Users = userRepo;
        Feedbacks = feedbacks;
    }

    public int Save()
    {
        return _context.SaveChanges();
    }

    public void Dispose()
    {
        _context.Dispose();
    }
}
