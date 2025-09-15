namespace DotnetMVCApp.Models
{
    public interface IFeedbackrepo
    {
        public IEnumerable<Feedback> GetAllFeedbacks();
        public Feedback GetFeedbackById(int id);
        IEnumerable<Feedback> GetByJob(int jobId);
        IEnumerable<Feedback> GetByUser(int userId);

        Feedback? GetByUserAndJob(int userId, int jobId);
        public Feedback Add(Feedback feedback);
        public Feedback Update(Feedback feedback);
        public Feedback Delete(int id);
    }
}
