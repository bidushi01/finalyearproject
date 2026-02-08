using finalyearproject.Data.DataAccess;
using finalyearproject.Data.Models.Domain;

namespace finalyearproject.Data.Repository
{
    public interface IUserRepository
    {
        Task<(int Result, string Message)> RegisterUserAsync(User user);
        Task<bool> StoreOTPAsync(string email, string otpCode, DateTime expiresAt);
        Task<bool> VerifyOTPAsync(string email, string otpCode);
        Task<User> GetUserByEmailAsync(string email);
        Task<User> GetUserByUsernameAsync(string username);
        Task<AdminLogin> GetAdminByUsernameAsync(string username);
        Task<bool> ApproveUserAsync(int userId);
        Task<IEnumerable<User>> GetPendingUsersAsync();
    }
}
