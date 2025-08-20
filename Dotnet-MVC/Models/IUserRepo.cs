namespace DotnetMVCApp.Models
{
    public interface IUserRepo
    {
        public IEnumerable<User> GetAllUser();
        public User GetUserById(int id);
        public User GetUserByEmail(string email);
        public User Add(User user);
        public User Update(User userChanges);
        public void Delete(int id);

    }
}
