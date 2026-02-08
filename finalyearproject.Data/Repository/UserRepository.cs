using Dapper;
using finalyearproject.Data.DataAccess;
using finalyearproject.Data.Models.Domain;

namespace finalyearproject.Data.Repository
{
    public class UserRepository : IUserRepository
    {
        private readonly ISqlDataAccess _sqlDataAccess;

        public UserRepository(ISqlDataAccess sqlDataAccess)
        {
            _sqlDataAccess = sqlDataAccess;
        }

        public async Task<(int Result, string Message)> RegisterUserAsync(User user)
        {
            var parameters = new
            {
                Username = user.Username,
                Email = user.Email,
                PasswordHash = user.PasswordHash,
                PasswordSalt = user.PasswordSalt,
                PhoneNumber = user.PhoneNumber,
                CVPath = user.CVPath,
                PortfolioUrl = user.PortfolioUrl
            };

            var result = await _sqlDataAccess.LoadSingleDataAsync<dynamic, dynamic>(
                "sp_RegisterUser",
                parameters
            );

            return (result.Result, result.Message);
        }

        public async Task<bool> StoreOTPAsync(string email, string otpCode, DateTime expiresAt)
        {
            var parameters = new { Email = email, OTPCode = otpCode, ExpiresAt = expiresAt };
            var result = await _sqlDataAccess.LoadSingleDataAsync<int, dynamic>("sp_StoreOTP", parameters);
            return result == 1;
        }

        public async Task<bool> VerifyOTPAsync(string email, string otpCode)
        {
            var parameters = new { Email = email, OTPCode = otpCode };
            var result = await _sqlDataAccess.LoadSingleDataAsync<dynamic, dynamic>("sp_VerifyOTP", parameters);
            return result.IsValid == 1;
        }

        public async Task<User> GetUserByEmailAsync(string email)
        {
            var parameters = new { Email = email };
            return await _sqlDataAccess.LoadSingleDataAsync<User, dynamic>("sp_GetUserByEmail", parameters);
        }

        public async Task<User> GetUserByUsernameAsync(string username)
        {
            var parameters = new { Username = username };
            return await _sqlDataAccess.LoadSingleDataAsync<User, dynamic>("sp_GetUserByUsername", parameters);
        }

        public async Task<AdminLogin> GetAdminByUsernameAsync(string username)
        {
            var parameters = new { Username = username };
            return await _sqlDataAccess.LoadSingleDataAsync<AdminLogin, dynamic>("sp_AdminLogin", parameters);
        }

        public async Task<bool> ApproveUserAsync(int userId)
        {
            var parameters = new { UserId = userId };
            var result = await _sqlDataAccess.LoadSingleDataAsync<dynamic, dynamic>("sp_ApproveUser", parameters);
            return result.Result == 1;
        }

        public async Task<IEnumerable<User>> GetPendingUsersAsync()
        {
            return await _sqlDataAccess.LoadDataAsync<User, dynamic>("sp_GetPendingUsers", new { });
        }
    }
}
