using DotnetMVCApp.Models;

public interface IUnitOfWork : IDisposable
{
    IUserJobRepo UserJobs { get; }
    IJobRepo Jobs { get; }
    IUserRepo Users { get; }
    IFeedbackrepo Feedbacks { get; }


    int Save(); // Commit transaction
}
