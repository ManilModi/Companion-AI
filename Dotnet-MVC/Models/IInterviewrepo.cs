namespace DotnetMVCApp.Models
{
    public interface IInterviewrepo
    {
        public IEnumerable<Interview> GetAllInterview();
        public Interview GetInterviewById(int id);
        public Interview Add(Interview interview);
        public Interview Update(Interview interview);
        public Interview Delete(int id);

    }
}
