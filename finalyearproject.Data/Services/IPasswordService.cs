namespace finalyearproject.Data.Services
{
    public interface IPasswordService
    {
        (string Hash, string Salt) HashPassword(string password);
        bool VerifyPassword(string password, string storedHash, string storedSalt);
    }
}