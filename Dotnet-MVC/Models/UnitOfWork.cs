using DotnetMVCApp.Data;
using DotnetMVCApp.Models;

public class UnitOfWork : IUnitOfWork
{
    private readonly AppDbContext _context;
    public IUserJobRepo UserJobs { get; }
    public IJobRepo Jobs { get; }
    public IUserRepo Users { get; }

    public UnitOfWork(AppDbContext context, IUserJobRepo userJobRepo, IJobRepo jobRepo, IUserRepo userRepo)
    {
        _context = context;
        UserJobs = userJobRepo;
        Jobs = jobRepo;
        Users = userRepo;
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
