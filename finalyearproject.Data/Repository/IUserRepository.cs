using finalyearproject.Data.Models.Domain;

namespace finalyearproject.Data.Repository
{
    public interface IUserRepository
    {
        // ── User Registration & Authentication ──────────────────────────
        Task<(int Result, string Message)> RegisterUserAsync(User user);
        Task<User> GetUserByEmailAsync(string email);
        Task<User> GetUserByUsernameAsync(string username);
        Task<User> GetUserByIdAsync(int userId);
        Task<AdminLogin> GetAdminByUsernameAsync(string username);
        Task<AdminLogin> GetAdminByEmailAsync(string email);

        // ── OTP Verification ────────────────────────────────────────────
        Task<bool> StoreOTPAsync(string email, string otpCode, DateTime expiresAt);
        Task<bool> VerifyOTPAsync(string email, string otpCode);
        Task<dynamic> CheckOTPValidityAsync(string email, string otpCode);
        Task MarkOTPAsUsedAsync(string email, string otpCode);
        Task MarkEmailAsVerifiedAsync(int userId);

        // ── Auth Flags (read directly from Users table) ─────────────────
        // Used to prevent login issues when stored procedures do not select these columns.
        Task<(bool IsEmailVerified, bool IsApprovedByAdmin)> GetUserAuthFlagsAsync(int userId);

        // ── Admin Approval ──────────────────────────────────────────────
        // Returns (success, message) — used by AdminController
        Task<(bool Success, string Message)> ApproveUserAsync(int userId);
        Task<(bool Success, string Message)> RejectUserAsync(int userId);
        Task<IEnumerable<User>> GetPendingUsersAsync();
        Task<IEnumerable<User>> GetAllUsersAsync();  // full list for pending users page

        // ── Password Reset (User) ───────────────────────────────────────
        Task<bool> StorePasswordResetTokenAsync(int userId, string token, DateTime expiresAt);
        Task<dynamic> VerifyPasswordResetTokenAsync(string token);
        Task<bool> ResetPasswordAsync(int userId, string passwordHash, string passwordSalt, string token);

        // ── Password Reset (Admin) ──────────────────────────────────────
        Task<bool> StoreAdminPasswordResetTokenAsync(int adminId, string token, DateTime expiresAt);
        Task<dynamic> VerifyAdminPasswordResetTokenAsync(string token);
        Task<bool> ResetAdminPasswordAsync(int adminId, string passwordHash, string passwordSalt, string token);

        // ── Master Data ─────────────────────────────────────────────────
        Task<(int Result, string Message)> AddFieldAsync(string fieldName);
        Task<(int Result, string Message)> AddSkillAsync(int fieldId, string skillName);
        Task<(int Result, string Message)> AddSubSkillAsync(int skillId, string subSkillName);
        Task<IEnumerable<dynamic>> GetAllFieldsAsync();
        Task<IEnumerable<dynamic>> GetSkillsByFieldAsync(int fieldId);
        Task<IEnumerable<dynamic>> GetSubSkillsBySkillAsync(int skillId);

        // ── User Skills ─────────────────────────────────────────────────
        Task<(int Result, string Message)> AddUserSkillAsync(
            int userId,
            int fieldId,
            int skillId,
            int subSkillId,
            int experienceLevel,
            string availableDays,
            TimeSpan? availableTimeStart,
            TimeSpan? availableTimeEnd);
        Task<IEnumerable<dynamic>> GetUserSkillsDisplayAsync(int userId);

        Task<(int Result, string Message)> UpdateUserSkillAsync(
            int userSkillId,
            int userId,
            int fieldId,
            int skillId,
            int subSkillId,
            int experienceLevel,
            string availableDays,
            TimeSpan? availableTimeStart,
            TimeSpan? availableTimeEnd);

        Task DeleteUserSkillAsync(int userSkillId);

        // Admin notification when a logged-in user adds or updates skills.
        Task LogUserSkillChangeAsync(
            int userId,
            string actionType,
            int fieldId,
            int skillId,
            int subSkillId);

        // Recent user changes (skills / documents) for admin dashboard
        Task<IEnumerable<dynamic>> GetRecentUserSkillChangesAsync(int top);

        // Update CV path + portfolio URL for a logged-in user
        Task<(int Result, string Message)> UpdateUserDocumentsAsync(
            int userId,
            string cvPath,
            string portfolioUrl);

        // ── PEERASSIST Algorithm (Phases 1, 3, 4–9) ─────────────────────
        Task<bool> CanRequestHelpAsync(int seekerId);
        Task<(int HelpRequestId, int Result, string Message)> CreateHelpRequestAsync(
            int seekerId, int helperId, int fieldId, int skillId, int subSkillId,
            TimeSpan? timeStart, TimeSpan? timeEnd, string availableDay, string description);
        Task<IEnumerable<dynamic>> GetRankedHelpersAsync(
            int seekerId, int fieldId, int skillId, int subSkillId,
            TimeSpan? timeStart, TimeSpan? timeEnd, string availableDay);
        Task<bool> AcceptHelpRequestAsync(int helpRequestId);
        Task<bool> RejectHelpRequestAsync(int helpRequestId);
        Task<bool> WithdrawHelpRequestAsync(int helpRequestId, int seekerId);
        Task<bool> EndSessionAsync(int helpRequestId, bool isSuccessful);
        Task UpdateHelperRatingAsync(int helperId, int fieldId, int skillId, int subSkillId, decimal newRatingValue);
        Task<IEnumerable<dynamic>> GetHelpRequestsBySeekerAsync(int seekerId);
        Task<IEnumerable<dynamic>> GetHelpRequestsByHelperAsync(int helperId);
        Task<IEnumerable<dynamic>> GetHelpRequestMessagesAsync(int helpRequestId);
        Task SendHelpMessageAsync(int helpRequestId, int senderId, string messageText, string attachmentPath);
        Task<(int SeekerId, int HelperId, int FieldId, int SkillId, int SubSkillId)> GetHelpRequestPartiesAsync(int helpRequestId);
        Task<string> GetHelpRequestStatusAsync(int helpRequestId);
        Task<int> GetUserPendingHelpRequestCountAsync(int userId);
        Task<int> GetUserActiveSentRequestCountAsync(int userId);

        // User-facing statistics for profile header
        Task<dynamic> GetUserProfileStatsAsync(int userId);

        // Admin: Help statistics for monitoring
        Task<dynamic> GetAdminHelpStatisticsAsync();
    }
}
