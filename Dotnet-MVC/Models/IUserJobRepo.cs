namespace DotnetMVCApp.Models
{
    public interface IUserJobRepo
    {
        IJobRepo jobRepo { get; }
        IUserRepo userRepo { get; }

        void save();
    }
}
