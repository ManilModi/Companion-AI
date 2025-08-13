namespace DotnetMVCApp.Models
{
    public interface IFeedbackrepo
    {
        public IEnumerable<Feedback> GetAllFeedbacks();
        public Feedback GetFeedbackById(int id);
        public Feedback Add(Feedback feedback);
        public Feedback Update(Feedback feedback);
        public Feedback Delete(int id);
    }
}
