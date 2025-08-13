namespace DotnetMVCApp.Models
{
    public interface IJobRepo
    {
        public IEnumerable<Job> GetAllJobs();
        public Job GetJobById(int id);
        public Job Add(Job job);
        public Job Update(Job job);
        public Job Delete(int id);

    }
}
