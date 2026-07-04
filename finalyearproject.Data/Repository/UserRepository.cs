using Dapper;
using finalyearproject.Data.DataAccess;
using finalyearproject.Data.Models.Domain;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using System.Data;

namespace finalyearproject.Data.Repository
{
    public class UserRepository : IUserRepository
    {
        private readonly ISqlDataAccess _sqlDataAccess;
        private readonly string _connectionString;

        public UserRepository(ISqlDataAccess sqlDataAccess, IConfiguration configuration)
        {
            _sqlDataAccess = sqlDataAccess;
            // Use the same connection name as SqlDataAccess ("conn") so both paths hit PeerHelpPlatform.
            _connectionString = configuration.GetConnectionString("conn");
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

        // ── User Getters ──────────────────────────────────────────────────

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

        // ── Admin Approval & Rejection ────────────────────────────────────
        // These actions MUST work even if the stored procedure returns no result-set.

        public async Task<(bool Success, string Message)> ApproveUserAsync(int userId)
        {
            using var conn = new SqlConnection(_connectionString);
            await conn.ExecuteAsync(
                "sp_ApproveUser",
                new { UserId = userId },
                commandType: CommandType.StoredProcedure);

            // Verify approval directly from table so login works reliably.
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

            // Enforce delete (SQL-heavy, simple, no extra algorithm).
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
            // Uses stored procedure only – no inline SQL.
            using var conn = new SqlConnection(_connectionString);
            return await conn.QueryAsync<User>(
                "sp_GetPendingUsers",
                commandType: CommandType.StoredProcedure);
        }




        public async Task<IEnumerable<User>> GetAllUsersAsync() // Explicitly return User objects
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

        // ── Master Data ───────────────────────────────────────────────────

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

        // ── User Skills ───────────────────────────────────────────────────

        public async Task<(int Result, string Message)> AddUserSkillAsync(
            int userId, int fieldId, int skillId, int subSkillId,
            int experienceLevel, string availableDays,
            TimeSpan? availableTimeStart, TimeSpan? availableTimeEnd)
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
                AvailableTimeEnd = availableTimeEnd
            };
            var result = await _sqlDataAccess.LoadSingleDataAsync<dynamic, dynamic>("sp_AddUserSkill", parameters);
            return (Convert.ToInt32(result.Result), result.Message?.ToString() ?? "");
        }

        public async Task<IEnumerable<dynamic>> GetUserSkillsDisplayAsync(int userId) =>
            await _sqlDataAccess.LoadDataAsync<dynamic, dynamic>("sp_GetUserSkills", new { UserId = userId });

        public async Task<(int Result, string Message)> UpdateUserSkillAsync(
            int userSkillId,
            int userId,
            int fieldId,
            int skillId,
            int subSkillId,
            int experienceLevel,
            string availableDays,
            TimeSpan? availableTimeStart,
            TimeSpan? availableTimeEnd)
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
                AvailableTimeEnd = availableTimeEnd
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
            // This uses a stored procedure only (no inline SQL) to log that
            // a user added or updated their skills, so admins can be notified.
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

        public async Task<bool> WithdrawHelpRequestAsync(int helpRequestId, int seekerId)
        {
            using var conn = new SqlConnection(_connectionString);
            var p = new Dapper.DynamicParameters();
            p.Add("@HelpRequestId", helpRequestId);
            p.Add("@SeekerId", seekerId);
            p.Add("@Result", dbType: DbType.Int32, direction: ParameterDirection.Output);
            await conn.ExecuteAsync("sp_WithdrawHelpRequest", p, commandType: CommandType.StoredProcedure);
            return p.Get<int>("@Result") == 1;
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

        public async Task<IEnumerable<dynamic>> GetHelpRequestMessagesAsync(int helpRequestId) =>
            await _sqlDataAccess.LoadDataAsync<dynamic, dynamic>("sp_GetHelpRequestMessages", new { HelpRequestId = helpRequestId });

        public async Task SendHelpMessageAsync(int helpRequestId, int senderId, string messageText, string attachmentPath)
        {
            await _sqlDataAccess.LoadSingleDataAsync<dynamic, dynamic>("sp_InsertHelpMessage",
                new
                {
                    HelpRequestId = helpRequestId,
                    SenderId = senderId,
                    MessageText = messageText ?? "",
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

            // Simple points rule: 10 points per successful help as helper
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
            using var conn = new SqlConnection(_connectionString);

            // Normalize status text: lowercase + remove spaces (handles "Not Completed" vs "NotCompleted")
            const string StatusNorm = "REPLACE(LOWER(ISNULL(Status,'')),' ','')";

            var totalHelpers = await conn.ExecuteScalarAsync<int>(
                $"SELECT COUNT(DISTINCT HelperId) FROM dbo.HelpRequests WHERE HelperId IS NOT NULL AND {StatusNorm} IN ('accepted','completed','notcompleted')");

            var totalHelpsGiven = await conn.ExecuteScalarAsync<int>(
                $"SELECT COUNT(1) FROM dbo.HelpRequests WHERE HelperId IS NOT NULL AND {StatusNorm} IN ('accepted','completed','notcompleted')");

            var requestsNotHelped = await conn.ExecuteScalarAsync<int>(
                $"SELECT COUNT(1) FROM dbo.HelpRequests WHERE {StatusNorm} IN ('pending','rejected')");

            var helperBreakdown = (await conn.QueryAsync<dynamic>(
                $@"SELECT u.UserId, u.Username, COUNT(r.HelpRequestId) AS HelpsGiven
                   FROM dbo.Users u
                   INNER JOIN dbo.HelpRequests r ON r.HelperId = u.UserId AND {StatusNorm} IN ('accepted','completed','notcompleted')
                   GROUP BY u.UserId, u.Username
                   ORDER BY HelpsGiven DESC")).ToList();

            // Per-helper performance: how many requests they received and how those requests ended
            var usersWhoDidNotHelp = (await conn.QueryAsync<dynamic>(
                $@"SELECT
                        u.UserId,
                        u.Username,
                        COUNT(r.HelpRequestId) AS RequestsReceived,
                        SUM(CASE WHEN {StatusNorm.Replace("Status", "r.Status")} = 'accepted'     THEN 1 ELSE 0 END) AS Accepted,
                        SUM(CASE WHEN {StatusNorm.Replace("Status", "r.Status")} = 'completed'    THEN 1 ELSE 0 END) AS Completed,
                        SUM(CASE WHEN {StatusNorm.Replace("Status", "r.Status")} = 'notcompleted' THEN 1 ELSE 0 END) AS NotCompleted,
                        SUM(CASE WHEN {StatusNorm.Replace("Status", "r.Status")} = 'rejected'     THEN 1 ELSE 0 END) AS Rejected,
                        SUM(CASE WHEN {StatusNorm.Replace("Status", "r.Status")} = 'withdrawn'    THEN 1 ELSE 0 END) AS Withdrawn,
                        SUM(CASE WHEN {StatusNorm.Replace("Status", "r.Status")} = 'pending'      THEN 1 ELSE 0 END) AS Pending
                    FROM dbo.Users u
                    INNER JOIN dbo.HelpRequests r ON r.HelperId = u.UserId
                    GROUP BY u.UserId, u.Username
                    ORDER BY RequestsReceived DESC")).ToList();

            return new
            {
                TotalHelpers = totalHelpers,
                TotalHelpsGiven = totalHelpsGiven,
                RequestsNotHelped = requestsNotHelped,
                HelperBreakdown = helperBreakdown,
                UsersWhoDidNotHelp = usersWhoDidNotHelp
            };
        }
    }
}
