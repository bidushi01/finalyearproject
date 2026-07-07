using Dapper;
using finalyearproject.Data.DataAccess;
using finalyearproject.Data.Models.Domain;
using finalyearproject.Data.Security;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using System.Data;

namespace finalyearproject.Data.Repository
{
    public class UserRepository : IUserRepository
    {
        private readonly ISqlDataAccess _sqlDataAccess;
        private readonly string _connectionString;
        private readonly ChatMessageProtector _chatProtector;
        private readonly string? _sqlLegacyPassphrase;

        public UserRepository(ISqlDataAccess sqlDataAccess, IConfiguration configuration, ChatMessageProtector chatMessageProtector)
        {
            _sqlDataAccess = sqlDataAccess;
            _chatProtector = chatMessageProtector;
            _connectionString = configuration.GetConnectionString("conn");
            var leg = configuration["ChatMessages:SqlLegacyPassphrase"]?.Trim();
            _sqlLegacyPassphrase = string.IsNullOrEmpty(leg) ? null : leg;
        }

        public async Task<(int Result, string Message)> RegisterUserAsync(User user)
        {
            var parameters = new
            {
                user.Username,
                user.Email,
                user.PasswordHash,
                user.PasswordSalt,
                user.PhoneNumber,
                user.CVPath,
                user.PortfolioUrl
            };
            var result = await _sqlDataAccess.LoadSingleDataAsync<dynamic, dynamic>("sp_RegisterUser", parameters);
            return (Convert.ToInt32(result.Result), result.Message?.ToString() ?? "");
        }

        public async Task<User> GetUserByEmailAsync(string email) =>
            await _sqlDataAccess.LoadSingleDataAsync<User, dynamic>("sp_GetUserByEmail", new { Email = email });

        public async Task<User> GetUserByUsernameAsync(string username) =>
            await _sqlDataAccess.LoadSingleDataAsync<User, dynamic>("sp_GetUserByUsername", new { Username = username });

        public async Task<User> GetUserByIdAsync(int userId) =>
            await _sqlDataAccess.LoadSingleDataAsync<User, dynamic>("sp_GetUserById", new { UserId = userId });

        public async Task<AdminLogin> GetAdminByUsernameAsync(string username) =>
            await _sqlDataAccess.LoadSingleDataAsync<AdminLogin, dynamic>("sp_AdminLogin", new { Username = username });

        public async Task<AdminLogin> GetAdminByEmailAsync(string email) =>
            await _sqlDataAccess.LoadSingleDataAsync<AdminLogin, dynamic>("sp_GetAdminByEmail", new { Email = email });

        public async Task<(bool Success, string Message)> ApproveUserAsync(int userId)
        {
            using var conn = new SqlConnection(_connectionString);
            await conn.ExecuteAsync(
                "sp_ApproveUser",
                new { UserId = userId },
                commandType: CommandType.StoredProcedure);

            var approvedCount = await conn.ExecuteScalarAsync<int>(
                "SELECT COUNT(1) FROM dbo.Users WHERE UserId = @UserId AND IsApprovedByAdmin = 1",
                new { UserId = userId });

            return approvedCount == 1
                ? (true, "User approved successfully.")
                : (false, "Approve failed. User is still not approved in database.");
        }

        public async Task<(bool Success, string Message)> RejectUserAsync(int userId)
        {
            using var conn = new SqlConnection(_connectionString);
            await conn.ExecuteAsync(
                "sp_RejectUser",
                new { UserId = userId },
                commandType: CommandType.StoredProcedure);

            var exists = await conn.ExecuteScalarAsync<int>(
                "SELECT COUNT(1) FROM dbo.Users WHERE UserId = @UserId",
                new { UserId = userId });

            if (exists == 0)
                return (true, "User rejected and deleted.");

            await conn.ExecuteAsync("DELETE FROM dbo.UserSkills WHERE UserId = @UserId", new { UserId = userId });
            await conn.ExecuteAsync("DELETE FROM dbo.PasswordResetTokens WHERE UserId = @UserId", new { UserId = userId });
            await conn.ExecuteAsync("DELETE FROM dbo.Users WHERE UserId = @UserId", new { UserId = userId });

            var existsAfter = await conn.ExecuteScalarAsync<int>(
                "SELECT COUNT(1) FROM dbo.Users WHERE UserId = @UserId",
                new { UserId = userId });

            return existsAfter == 0
                ? (true, "User rejected and deleted.")
                : (false, "Reject failed. User still exists in database.");
        }

        public async Task<(bool IsEmailVerified, bool IsApprovedByAdmin)> GetUserAuthFlagsAsync(int userId)
        {
            using var conn = new SqlConnection(_connectionString);

            var flags = await conn.QueryFirstOrDefaultAsync<dynamic>(
                "SELECT IsEmailVerified, IsApprovedByAdmin FROM dbo.Users WHERE UserId = @UserId",
                new { UserId = userId });

            if (flags == null) return (false, false);

            bool emailVerified = Convert.ToInt32(flags.IsEmailVerified) == 1;
            bool approved = Convert.ToInt32(flags.IsApprovedByAdmin) == 1;
            return (emailVerified, approved);
        }

        public async Task<IEnumerable<User>> GetPendingUsersAsync()
        {
            using var conn = new SqlConnection(_connectionString);
            return await conn.QueryAsync<User>(
                "sp_GetPendingUsers",
                commandType: CommandType.StoredProcedure);
        }

        public async Task<IEnumerable<User>> GetAllUsersAsync()
        {
            using var conn = new SqlConnection(_connectionString);
            return await conn.QueryAsync<User>("sp_GetAllUsers", commandType: CommandType.StoredProcedure);
        }

        public async Task<bool> StoreOTPAsync(string email, string otpCode, DateTime expiresAt)
        {
            var result = await _sqlDataAccess.LoadSingleDataAsync<dynamic, dynamic>(
                "sp_StoreOTP",
                new { Email = email, OTPCode = otpCode, ExpiresAt = expiresAt });
            return Convert.ToInt32(result.Result) == 1;
        }

        public async Task<bool> VerifyOTPAsync(string email, string otpCode)
        {
            var result = await _sqlDataAccess.LoadSingleDataAsync<dynamic, dynamic>(
                "sp_VerifyOTP",
                new { Email = email, OTPCode = otpCode });
            return Convert.ToInt32(result.IsValid) == 1;
        }

        public async Task<dynamic> CheckOTPValidityAsync(string email, string otpCode) =>
            await _sqlDataAccess.LoadSingleDataAsync<dynamic, dynamic>(
                "sp_CheckOTPValidity",
                new { Email = email, OTPCode = otpCode });

        public async Task MarkOTPAsUsedAsync(string email, string otpCode) =>
            await _sqlDataAccess.LoadSingleDataAsync<dynamic, dynamic>(
                "sp_MarkOTPAsUsed",
                new { Email = email, OTPCode = otpCode });

        public async Task MarkEmailAsVerifiedAsync(int userId) =>
            await _sqlDataAccess.LoadSingleDataAsync<dynamic, dynamic>(
                "sp_MarkEmailAsVerified",
                new { UserId = userId });

        public async Task<bool> StorePasswordResetTokenAsync(int userId, string token, DateTime expiresAt)
        {
            var result = await _sqlDataAccess.LoadSingleDataAsync<dynamic, dynamic>(
                "sp_StorePasswordResetToken",
                new { UserId = userId, Token = token, ExpiresAt = expiresAt });
            return Convert.ToInt32(result.Result) == 1;
        }

        public async Task<dynamic> VerifyPasswordResetTokenAsync(string token) =>
            await _sqlDataAccess.LoadSingleDataAsync<dynamic, dynamic>(
                "sp_VerifyPasswordResetToken",
                new { Token = token });

        public async Task<bool> ResetPasswordAsync(int userId, string passwordHash, string passwordSalt, string token)
        {
            var result = await _sqlDataAccess.LoadSingleDataAsync<dynamic, dynamic>(
                "sp_ResetPassword",
                new { UserId = userId, PasswordHash = passwordHash, PasswordSalt = passwordSalt, Token = token });
            return Convert.ToInt32(result.Result) == 1;
        }

        public async Task<bool> StoreAdminPasswordResetTokenAsync(int adminId, string token, DateTime expiresAt)
        {
            var result = await _sqlDataAccess.LoadSingleDataAsync<dynamic, dynamic>(
                "sp_StoreAdminPasswordResetToken",
                new { AdminId = adminId, Token = token, ExpiresAt = expiresAt });
            return Convert.ToInt32(result.Result) == 1;
        }

        public async Task<dynamic> VerifyAdminPasswordResetTokenAsync(string token) =>
            await _sqlDataAccess.LoadSingleDataAsync<dynamic, dynamic>(
                "sp_AdminVerifyPasswordResetToken",
                new { Token = token });

        public async Task<bool> ResetAdminPasswordAsync(int adminId, string passwordHash, string passwordSalt, string token)
        {
            var result = await _sqlDataAccess.LoadSingleDataAsync<dynamic, dynamic>(
                "sp_ResetAdminPassword",
                new { AdminId = adminId, PasswordHash = passwordHash, PasswordSalt = passwordSalt, Token = token });
            return Convert.ToInt32(result.Result) == 1;
        }

        public async Task<(int Result, string Message)> AddFieldAsync(string fieldName)
        {
            var result = await _sqlDataAccess.LoadSingleDataAsync<dynamic, dynamic>(
                "sp_AddField",
                new { FieldName = fieldName });
            return (Convert.ToInt32(result.Result), result.Message?.ToString() ?? "");
        }

        public async Task<(int Result, string Message)> AddSkillAsync(int fieldId, string skillName)
        {
            var result = await _sqlDataAccess.LoadSingleDataAsync<dynamic, dynamic>(
                "sp_AddSkill",
                new { FieldId = fieldId, SkillName = skillName });
            return (Convert.ToInt32(result.Result), result.Message?.ToString() ?? "");
        }

        public async Task<(int Result, string Message)> AddSubSkillAsync(int skillId, string subSkillName)
        {
            var result = await _sqlDataAccess.LoadSingleDataAsync<dynamic, dynamic>(
                "sp_AddSubSkill",
                new { SkillId = skillId, SubSkillName = subSkillName });
            return (Convert.ToInt32(result.Result), result.Message?.ToString() ?? "");
        }

        public async Task<IEnumerable<dynamic>> GetAllFieldsAsync() =>
            await _sqlDataAccess.LoadDataAsync<dynamic, dynamic>("sp_GetAllFields", new { });

        public async Task<IEnumerable<dynamic>> GetSkillsByFieldAsync(int fieldId) =>
            await _sqlDataAccess.LoadDataAsync<dynamic, dynamic>("sp_GetSkillsByField", new { FieldId = fieldId });

        public async Task<IEnumerable<dynamic>> GetSubSkillsBySkillAsync(int skillId) =>
            await _sqlDataAccess.LoadDataAsync<dynamic, dynamic>("sp_GetSubSkillsBySkill", new { SkillId = skillId });

        public async Task<(int Result, string Message)> AddUserSkillAsync(
            int userId, int fieldId, int skillId, int subSkillId,
            int experienceLevel, string availableDays,
            TimeSpan? availableTimeStart, TimeSpan? availableTimeEnd,
            string? availableTimeSlots = null)
        {
            var parameters = new
            {
                UserId = userId,
                FieldId = fieldId,
                SkillId = skillId,
                SubSkillId = subSkillId,
                ExperienceLevel = experienceLevel,
                AvailableDays = availableDays,
                AvailableTimeStart = availableTimeStart,
                AvailableTimeEnd = availableTimeEnd,
                AvailableTimeSlots = availableTimeSlots
            };
            var result = await _sqlDataAccess.LoadSingleDataAsync<dynamic, dynamic>("sp_AddUserSkill", parameters);
            return (Convert.ToInt32(result.Result), result.Message?.ToString() ?? "");
        }

        public async Task<IEnumerable<dynamic>> GetUserSkillsDisplayAsync(int userId, bool approvedOnly = false) =>
            await _sqlDataAccess.LoadDataAsync<dynamic, dynamic>("sp_GetUserSkills", new { UserId = userId, ApprovedOnly = approvedOnly });

        public async Task FinalizeRegistrationAutoApproveAsync(int userId)
        {
            await _sqlDataAccess.LoadSingleDataAsync<dynamic, dynamic>(
                "sp_FinalizeRegistrationAutoApprove",
                new { UserId = userId });
        }

        private const string PendingUserSkillsQuery = @"
SELECT
    us.UserSkillId,
    us.UserId,
    u.Username,
    u.Email,
    u.CVPath,
    u.PortfolioUrl,
    f.FieldName,
    sk.SkillName,
    ss.SubSkillName,
    us.ExperienceLevel,
    us.AvailableDays,
    us.AvailableTimeStart,
    us.AvailableTimeEnd,
    us.SkillSubmittedAt
FROM dbo.UserSkills us
INNER JOIN dbo.Users u ON u.UserId = us.UserId
INNER JOIN dbo.MasterField f ON f.FieldId = us.FieldId
INNER JOIN dbo.MasterSkill sk ON sk.SkillId = us.SkillId
INNER JOIN dbo.MasterSubSkill ss ON ss.SubSkillId = us.SubSkillId
WHERE ISNULL(us.IsSkillApproved, 0) = 0
  AND (us.SkillRejectionReason IS NULL OR LEN(RTRIM(ISNULL(us.SkillRejectionReason, ''))) = 0)
ORDER BY us.SkillSubmittedAt ASC, us.UserSkillId ASC;";

        public async Task<IEnumerable<dynamic>> GetPendingUserSkillsAsync()
        {
            try
            {
                return await _sqlDataAccess.LoadDataAsync<dynamic, dynamic>("sp_GetPendingUserSkills", new { });
            }
            catch (SqlException)
            {
                using var conn = new SqlConnection(_connectionString);
                return await conn.QueryAsync<dynamic>(PendingUserSkillsQuery);
            }
        }


        public async Task<dynamic?> GetSkillApprovalStatsAsync() =>
            await _sqlDataAccess.LoadSingleDataAsync<dynamic, dynamic>("sp_GetSkillApprovalStats", new { });

        public async Task<(bool Success, string Message, string? Email, string? Username, string? SkillSummary)> ApproveUserSkillAsync(int userSkillId)
        {
            var row = await _sqlDataAccess.LoadSingleDataAsync<dynamic, dynamic>(
                "sp_ApproveUserSkill",
                new { UserSkillId = userSkillId });
            if (row == null) return (false, "Skill not found.", null, null, null);
            if (Convert.ToInt32(row.Result) != 1)
                return (false, row.Message?.ToString() ?? "", null, null, null);
            return (true, row.Message?.ToString() ?? "", row.Email?.ToString(), row.Username?.ToString(), BuildSkillSummary(row));
        }

        public async Task<(bool Success, string Message, string? Email, string? Username, string? SkillSummary, string? RejectionReason)> RejectUserSkillAsync(int userSkillId, string reason)
        {
            var row = await _sqlDataAccess.LoadSingleDataAsync<dynamic, dynamic>(
                "sp_RejectUserSkill",
                new { UserSkillId = userSkillId, Reason = reason ?? "" });
            if (row == null) return (false, "Skill not found.", null, null, null, null);
            if (Convert.ToInt32(row.Result) != 1)
                return (false, row.Message?.ToString() ?? "", null, null, null, null);
            return (true, row.Message?.ToString() ?? "", row.Email?.ToString(), row.Username?.ToString(), BuildSkillSummary(row), row.SkillRejectionReason?.ToString());
        }

        private static string BuildSkillSummary(dynamic row)
        {
            var field = row.FieldName?.ToString() ?? "";
            var skill = row.SkillName?.ToString() ?? "";
            var sub = row.SubSkillName?.ToString() ?? "";
            return $"{field} > {skill} > {sub}";
        }

        public async Task<bool> UserOwnsUserSkillAsync(int userId, int userSkillId)
        {
            using var conn = new SqlConnection(_connectionString);
            var count = await conn.ExecuteScalarAsync<int>(
                "SELECT COUNT(1) FROM dbo.UserSkills WHERE UserSkillId = @UserSkillId AND UserId = @UserId",
                new { UserSkillId = userSkillId, UserId = userId });
            return count > 0;
        }

        public async Task<bool> UserHasNonRejectedSkillAsync(int userId, int fieldId, int skillId, int subSkillId)
        {
            using var conn = new SqlConnection(_connectionString);
            var count = await conn.ExecuteScalarAsync<int>(
                @"SELECT COUNT(1) FROM dbo.UserSkills
                  WHERE UserId = @UserId AND FieldId = @FieldId AND SkillId = @SkillId AND SubSkillId = @SubSkillId
                    AND (SkillRejectionReason IS NULL OR LEN(RTRIM(SkillRejectionReason)) = 0)",
                new { UserId = userId, FieldId = fieldId, SkillId = skillId, SubSkillId = subSkillId });
            return count > 0;
        }

        public async Task<(int Result, string Message)> UpdateUserSkillAsync(
            int userSkillId,
            int userId,
            int fieldId,
            int skillId,
            int subSkillId,
            int experienceLevel,
            string availableDays,
            TimeSpan? availableTimeStart,
            TimeSpan? availableTimeEnd,
            string? availableTimeSlots = null)
        {
            var parameters = new
            {
                UserSkillId = userSkillId,
                UserId = userId,
                FieldId = fieldId,
                SkillId = skillId,
                SubSkillId = subSkillId,
                ExperienceLevel = experienceLevel,
                AvailableDays = availableDays,
                AvailableTimeStart = availableTimeStart,
                AvailableTimeEnd = availableTimeEnd,
                AvailableTimeSlots = availableTimeSlots
            };

            var result = await _sqlDataAccess.LoadSingleDataAsync<dynamic, dynamic>(
                "sp_UpdateUserSkill",
                parameters);

            return (Convert.ToInt32(result.Result), result.Message?.ToString() ?? "");
        }

        public async Task DeleteUserSkillAsync(int userSkillId)
        {
            await _sqlDataAccess.LoadSingleDataAsync<dynamic, dynamic>(
                "sp_DeleteUserSkill",
                new { UserSkillId = userSkillId });
        }

        public async Task LogUserSkillChangeAsync(
            int userId,
            string actionType,
            int fieldId,
            int skillId,
            int subSkillId)
        {
            await _sqlDataAccess.LoadSingleDataAsync<dynamic, dynamic>(
                "sp_LogUserSkillChange",
                new
                {
                    UserId = userId,
                    ActionType = actionType,
                    FieldId = fieldId,
                    SkillId = skillId,
                    SubSkillId = subSkillId
                });
        }

        public async Task<IEnumerable<dynamic>> GetRecentUserSkillChangesAsync(int top)
        {
            return await _sqlDataAccess.LoadDataAsync<dynamic, dynamic>(
                "sp_GetRecentUserSkillChanges",
                new { Top = top });
        }

        public async Task<(int Result, string Message)> UpdateUserDocumentsAsync(
            int userId,
            string cvPath,
            string portfolioUrl)
        {
            var result = await _sqlDataAccess.LoadSingleDataAsync<dynamic, dynamic>(
                "sp_UpdateUserDocuments",
                new
                {
                    UserId = userId,
                    CVPath = cvPath,
                    PortfolioUrl = portfolioUrl
                });

            return (Convert.ToInt32(result.Result), result.Message?.ToString() ?? "");
        }

        public async Task<bool> CanRequestHelpAsync(int seekerId)
        {
            using var conn = new SqlConnection(_connectionString);
            var p = new Dapper.DynamicParameters();
            p.Add("@SeekerId", seekerId);
            p.Add("@CanRequest", dbType: DbType.Int32, direction: ParameterDirection.Output);
            await conn.ExecuteAsync("sp_CanRequestHelp", p, commandType: CommandType.StoredProcedure);
            return p.Get<int>("@CanRequest") == 1;
        }

        public async Task<bool> SeekerHasPendingHelpRequestAsync(int seekerId)
        {
            using var conn = new SqlConnection(_connectionString);
            var count = await conn.ExecuteScalarAsync<int>(
                @"SELECT COUNT(1) FROM dbo.HelpRequests
                  WHERE SeekerId = @SeekerId
                    AND REPLACE(LOWER(LTRIM(RTRIM(ISNULL(Status, '')))), ' ', '') = 'pending'",
                new { SeekerId = seekerId });
            return count > 0;
        }

        public async Task<string?> GetAskHelpBusyReasonAsync(int userId)
        {
            var canRequest = await CanRequestHelpAsync(userId);
            var seekerPending = await SeekerHasPendingHelpRequestAsync(userId);
            if (canRequest && !seekerPending)
                return null;

            const string norm = "REPLACE(LOWER(LTRIM(RTRIM(ISNULL(Status, '')))), ' ', '')";

            using var conn = new SqlConnection(_connectionString);
            var helpingAccepted = await conn.ExecuteScalarAsync<int>(
                $@"SELECT COUNT(1) FROM dbo.HelpRequests
                   WHERE HelperId = @UserId AND {norm} = 'accepted'",
                new { UserId = userId });
            if (helpingAccepted > 0)
                return "helperActive";

            if (seekerPending)
                return "seekerPending";

            var inSessionAsSeeker = await conn.ExecuteScalarAsync<int>(
                $@"SELECT COUNT(1) FROM dbo.HelpRequests
                   WHERE SeekerId = @UserId AND {norm} = 'accepted'",
                new { UserId = userId });
            if (inSessionAsSeeker > 0)
                return "seekerActive";

            return "other";
        }

        public async Task<(int HelpRequestId, int Result, string Message)> CreateHelpRequestAsync(
            int seekerId, int helperId, int fieldId, int skillId, int subSkillId,
            TimeSpan? timeStart, TimeSpan? timeEnd, string availableDay, string description)
        {
            using var conn = new SqlConnection(_connectionString);
            var p = new Dapper.DynamicParameters();
            p.Add("@SeekerId", seekerId);
            p.Add("@HelperId", helperId);
            p.Add("@FieldId", fieldId);
            p.Add("@SkillId", skillId);
            p.Add("@SubSkillId", subSkillId);
            p.Add("@TimeStart", timeStart);
            p.Add("@TimeEnd", timeEnd);
            p.Add("@AvailableDay", availableDay ?? "");
            p.Add("@Description", description ?? "");
            p.Add("@HelpRequestId", dbType: DbType.Int32, direction: ParameterDirection.Output);
            p.Add("@Result", dbType: DbType.Int32, direction: ParameterDirection.Output);
            p.Add("@Message", dbType: DbType.String, size: 200, direction: ParameterDirection.Output);
            await conn.ExecuteAsync("sp_CreateHelpRequest", p, commandType: CommandType.StoredProcedure);
            return (p.Get<int>("@HelpRequestId"), p.Get<int>("@Result"), p.Get<string>("@Message") ?? "");
        }

        public async Task<IEnumerable<dynamic>> GetRankedHelpersAsync(
            int seekerId, int fieldId, int skillId, int subSkillId,
            TimeSpan? timeStart, TimeSpan? timeEnd, string availableDay)
        {
            return await _sqlDataAccess.LoadDataAsync<dynamic, dynamic>(
                "sp_GetRankedHelpers",
                new
                {
                    SeekerId = seekerId,
                    FieldId = fieldId,
                    SkillId = skillId,
                    SubSkillId = subSkillId,
                    TimeStart = timeStart,
                    TimeEnd = timeEnd,
                    AvailableDay = availableDay ?? ""
                });
        }

        public async Task<bool> AcceptHelpRequestAsync(int helpRequestId)
        {
            using var conn = new SqlConnection(_connectionString);
            var p = new Dapper.DynamicParameters();
            p.Add("@HelpRequestId", helpRequestId);
            p.Add("@Result", dbType: DbType.Int32, direction: ParameterDirection.Output);
            await conn.ExecuteAsync("sp_AcceptHelpRequest", p, commandType: CommandType.StoredProcedure);
            return p.Get<int>("@Result") == 1;
        }

        public async Task<bool> RejectHelpRequestAsync(int helpRequestId)
        {
            using var conn = new SqlConnection(_connectionString);
            var p = new Dapper.DynamicParameters();
            p.Add("@HelpRequestId", helpRequestId);
            p.Add("@Result", dbType: DbType.Int32, direction: ParameterDirection.Output);
            await conn.ExecuteAsync("sp_RejectHelpRequest", p, commandType: CommandType.StoredProcedure);
            return p.Get<int>("@Result") == 1;
        }

        public async Task<bool> WithdrawHelpRequestAsync(int helpRequestId, int seekerId, bool isTimeout = false)
        {
            using var conn = new SqlConnection(_connectionString);
            var p = new Dapper.DynamicParameters();
            p.Add("@HelpRequestId", helpRequestId);
            p.Add("@SeekerId", seekerId);
            p.Add("@IsTimeout", isTimeout);
            p.Add("@Result", dbType: DbType.Int32, direction: ParameterDirection.Output);
            await conn.ExecuteAsync("sp_WithdrawHelpRequest", p, commandType: CommandType.StoredProcedure);
            return p.Get<int>("@Result") == 1;
        }

        public async Task RecordHelperNoResponseAsync(int helperId)
        {
            if (helperId <= 0) return;
            using var conn = new SqlConnection(_connectionString);
            await conn.ExecuteAsync(
                "sp_RecordHelperNoResponse",
                new { HelperId = helperId },
                commandType: CommandType.StoredProcedure);
        }

        public async Task<bool> EndSessionAsync(int helpRequestId, bool isSuccessful)
        {
            using var conn = new SqlConnection(_connectionString);
            var p = new Dapper.DynamicParameters();
            p.Add("@HelpRequestId", helpRequestId);
            p.Add("@IsSuccessful", isSuccessful);
            p.Add("@Result", dbType: DbType.Int32, direction: ParameterDirection.Output);
            await conn.ExecuteAsync("sp_EndSession", p, commandType: CommandType.StoredProcedure);
            return p.Get<int>("@Result") == 1;
        }

        public async Task UpdateHelperRatingAsync(int helperId, int fieldId, int skillId, int subSkillId, decimal newRatingValue)
        {
            await _sqlDataAccess.LoadSingleDataAsync<dynamic, dynamic>(
                "sp_UpdateHelperRating",
                new
                {
                    HelperId = helperId,
                    FieldId = fieldId,
                    SkillId = skillId,
                    SubSkillId = subSkillId,
                    NewRatingValue = newRatingValue
                });
        }

        public async Task<IEnumerable<dynamic>> GetHelpRequestsBySeekerAsync(int seekerId) =>
            await _sqlDataAccess.LoadDataAsync<dynamic, dynamic>("sp_GetHelpRequestsBySeeker", new { SeekerId = seekerId });

        public async Task<IEnumerable<dynamic>> GetHelpRequestsByHelperAsync(int helperId) =>
            await _sqlDataAccess.LoadDataAsync<dynamic, dynamic>("sp_GetHelpRequestsByHelper", new { HelperId = helperId });

        public async Task<IEnumerable<dynamic>> GetHelpRequestMessagesAsync(int helpRequestId)
        {
            var rows = (await _sqlDataAccess.LoadDataAsync<dynamic, dynamic>("sp_GetHelpRequestMessages", new { HelpRequestId = helpRequestId })).ToList();
            foreach (var row in rows)
            {
                if (row is not IDictionary<string, object> dict)
                    continue;
                if (!dict.TryGetValue("MessageText", out var v) || v is not string s)
                    continue;
                dict["MessageText"] = await ResolveChatMessageTextAsync(s).ConfigureAwait(false);
            }

            return rows;
        }

        private const string SqlLegacyChatPrefix = "SQLPP|V1|";

        private async Task<string> ResolveChatMessageTextAsync(string stored)
        {
            if (string.IsNullOrEmpty(stored))
                return "";

            if (stored.StartsWith(SqlLegacyChatPrefix, StringComparison.Ordinal))
                return await DecryptSqlLegacyChatAsync(stored).ConfigureAwait(false);

            return _chatProtector.Unprotect(stored);
        }

        private async Task<string> DecryptSqlLegacyChatAsync(string stored)
        {
            if (_sqlLegacyPassphrase == null || string.IsNullOrEmpty(_connectionString))
                return "[Legacy chat: set ChatMessages:SqlLegacyPassphrase to match PeerAssist.ChatMessages.EncryptLegacy.sql]";

            var hex = stored[SqlLegacyChatPrefix.Length..];
            byte[] bytes;
            try
            {
                bytes = Convert.FromHexString(hex);
            }
            catch
            {
                return "[Invalid legacy chat data]";
            }

            try
            {
                await using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync().ConfigureAwait(false);
                await using var cmd = new SqlCommand(
                    "SELECT CONVERT(NVARCHAR(MAX), DecryptByPassPhrase(@p, @b))",
                    conn);
                cmd.Parameters.AddWithValue("@p", _sqlLegacyPassphrase);
                var pb = cmd.Parameters.Add("@b", SqlDbType.VarBinary, -1);
                pb.Value = bytes;

                var scalar = await cmd.ExecuteScalarAsync().ConfigureAwait(false);
                if (scalar == null || scalar == DBNull.Value)
                    return "[Legacy message could not be decrypted — check passphrase]";
                return scalar.ToString() ?? "";
            }
            catch
            {
                return "[Legacy message could not be decrypted]";
            }
        }

        public async Task SendHelpMessageAsync(int helpRequestId, int senderId, string messageText, string attachmentPath)
        {
            var storedText = _chatProtector.Protect(messageText ?? "");
            await _sqlDataAccess.LoadSingleDataAsync<dynamic, dynamic>("sp_InsertHelpMessage",
                new
                {
                    HelpRequestId = helpRequestId,
                    SenderId = senderId,
                    MessageText = storedText,
                    AttachmentPath = attachmentPath
                });
        }

        private sealed class HelpRequestParties
        {
            public int SeekerId { get; set; }
            public int HelperId { get; set; }
            public int FieldId { get; set; }
            public int SkillId { get; set; }
            public int SubSkillId { get; set; }
        }

        public async Task<(int SeekerId, int HelperId, int FieldId, int SkillId, int SubSkillId)> GetHelpRequestPartiesAsync(int helpRequestId)
        {
            using var conn = new SqlConnection(_connectionString);
            var row = await conn.QuerySingleOrDefaultAsync<HelpRequestParties>(
                "SELECT SeekerId, HelperId, FieldId, SkillId, SubSkillId FROM dbo.HelpRequests WHERE HelpRequestId = @HelpRequestId",
                new { HelpRequestId = helpRequestId });

            if (row == null) return (0, 0, 0, 0, 0);
            return (row.SeekerId, row.HelperId, row.FieldId, row.SkillId, row.SubSkillId);
        }

        public async Task<string> GetHelpRequestStatusAsync(int helpRequestId)
        {
            using var conn = new SqlConnection(_connectionString);
            var status = await conn.ExecuteScalarAsync<string>(
                "SELECT Status FROM dbo.HelpRequests WHERE HelpRequestId = @HelpRequestId",
                new { HelpRequestId = helpRequestId });
            return status ?? "";
        }

        public async Task<int> GetUserPendingHelpRequestCountAsync(int userId)
        {
            using var conn = new SqlConnection(_connectionString);
            var count = await conn.ExecuteScalarAsync<int>(
                "SELECT COUNT(1) FROM dbo.HelpRequests WHERE HelperId = @UserId AND LOWER(Status) = 'pending'",
                new { UserId = userId });
            return count;
        }

        public async Task<int> GetUserActiveSentRequestCountAsync(int userId)
        {
            using var conn = new SqlConnection(_connectionString);
            var count = await conn.ExecuteScalarAsync<int>(
                "SELECT COUNT(1) FROM dbo.HelpRequests WHERE SeekerId = @UserId AND LOWER(Status) IN ('pending','accepted')",
                new { UserId = userId });
            return count;
        }

        public async Task<dynamic> GetUserProfileStatsAsync(int userId)
        {
            using var conn = new SqlConnection(_connectionString);

            var sessionsCompleted = await conn.ExecuteScalarAsync<int>(
                "SELECT COUNT(1) FROM dbo.HelpRequests WHERE (SeekerId = @UserId OR HelperId = @UserId) AND LOWER(Status) IN ('completed','notcompleted')",
                new { UserId = userId });

            var successfulHelpsAsHelper = await conn.ExecuteScalarAsync<int>(
                "SELECT COUNT(1) FROM dbo.HelpRequests WHERE HelperId = @UserId AND LOWER(Status) = 'completed'",
                new { UserId = userId });

            var avgRating = await conn.ExecuteScalarAsync<decimal?>(
                @"SELECT CAST(
                        CASE WHEN SUM(ISNULL(SkillReviewCount,0)) = 0 THEN 0
                             ELSE SUM(ISNULL(SkillRating,0) * ISNULL(SkillReviewCount,0)) / NULLIF(SUM(ISNULL(SkillReviewCount,0)),0)
                        END AS DECIMAL(3,2))
                  FROM dbo.UserSkills
                  WHERE UserId = @UserId",
                new { UserId = userId }) ?? 0m;

            var totalReviews = await conn.ExecuteScalarAsync<int>(
                "SELECT ISNULL(SUM(SkillReviewCount),0) FROM dbo.UserSkills WHERE UserId = @UserId",
                new { UserId = userId });

            int pointsEarned = successfulHelpsAsHelper * 10;

            return new
            {
                SessionsCompleted = sessionsCompleted,
                SuccessfulHelpsAsHelper = successfulHelpsAsHelper,
                AverageRating = avgRating,
                TotalReviews = totalReviews,
                PointsEarned = pointsEarned
            };
        }

        public async Task<dynamic> GetAdminHelpStatisticsAsync()
        {
            var totals = await _sqlDataAccess.LoadSingleDataAsync<dynamic, dynamic>(
                "sp_GetAdminHelpStatsTotals",
                new { });

            var helperBreakdown = (await _sqlDataAccess.LoadDataAsync<dynamic, dynamic>(
                "sp_GetAdminTopHelpers",
                new { })).ToList();

            var usersWhoDidNotHelp = (await _sqlDataAccess.LoadDataAsync<dynamic, dynamic>(
                "sp_GetAdminUsersWhoDidNotHelp",
                new { })).ToList();

            return new
            {
                TotalHelpers = (int)(totals?.TotalHelpers ?? 0),
                TotalHelpsGiven = (int)(totals?.TotalHelpsGiven ?? 0),
                RequestsNotHelped = (int)(totals?.RequestsNotHelped ?? 0),
                HelperBreakdown = helperBreakdown,
                UsersWhoDidNotHelp = usersWhoDidNotHelp
            };
        }
    }
}