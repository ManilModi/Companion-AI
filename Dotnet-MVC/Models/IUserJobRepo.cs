namespace DotnetMVCApp.Models
{
    public interface IUserJobRepo
    {
        IJobRepo jobRepo { get; }
        IUserRepo userRepo { get; }

        void ApplyToJob(int userId, int jobId, string? scoreJson = null);
        bool HasApplied(int userId, int jobId);

        bool Exists(int userId, int jobId);

        public UserJob? GetByUserAndJob(int userId, int jobId);

        public void Delete(UserJob userJob);

        public IEnumerable<UserJob> GetJobsByUser(int userId);

        void save();
    }
}
