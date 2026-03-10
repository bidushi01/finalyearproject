using finalyearproject.Data.Helper;
using finalyearproject.Data.Repository;
using finalyearproject.Data.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Linq;

namespace finalyearproject.UI.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminController : Controller
    {
        private readonly IUserRepository _userRepository;
        private readonly IEmailService _emailService;

        public AdminController(IUserRepository userRepository, IEmailService emailService)
        {
            _userRepository = userRepository;
            _emailService = emailService;
        }

        // ── Dashboard ────────────────────────────────────────────────────

        [HttpGet]
        public IActionResult Index()
        {
            return View();
        }

        [HttpGet]
        public async Task<IActionResult> GetHelpStatistics()
        {
            var stats = await _userRepository.GetAdminHelpStatisticsAsync();
            return Json(stats);
        }

        [HttpGet]
        public IActionResult AllUsers()
        {
            return View();
        }

        [HttpGet]
        public IActionResult TopHelpers()
        {
            return View();
        }

        [HttpGet]
        public IActionResult HelperPerformance()
        {
            return View();
        }

        [HttpGet]
        public IActionResult RecentChanges()
        {
            return View();
        }

        // ── Pending Users Page ───────────────────────────────────────────

        [HttpGet]
        public IActionResult PendingUsers()
        {
            return View();
        }

        // Returns PENDING users as JSON (sp_GetPendingUsers).
        // Keep this endpoint simple and reliable: pending approvals must always show.

        //[HttpGet]
        //public async Task<IActionResult> GetPendingUsers()
        //{
        //    var users = await _userRepository.GetAllUsersAsync();

        //    var rows = users.Select(u => new
        //    {
        //        userId = (int)u.UserId,
        //        username = (string)(u.Username ?? ""),
        //        email = (string)(u.Email ?? ""),
        //        phoneNumber = (string)(u.PhoneNumber ?? ""),
        //        cvPath = (string)(u.CVPath ?? ""),
        //        portfolioUrl = (string)(u.PortfolioUrl ?? ""),
        //        createdAt = (DateTime?)u.CreatedAt,
        //        isApprovedByAdmin = (bool)u.IsApprovedByAdmin,
        //        isRejected = (bool)u.IsRejected
        //    }).ToList();

        //    return Json(rows);
        //}


        [HttpGet]
        public async Task<IActionResult> GetPendingUsers()
        {
            // This returns ALL email-verified users (pending/approved/rejected) in ONE row per user.
            var users = await _userRepository.GetAllUsersAsync();

            var rows = users.Select(u => new
            {
                userId = (int)u.UserId,
                username = (string)(u.Username ?? ""),
                email = (string)(u.Email ?? ""),
                phoneNumber = (string)(u.PhoneNumber ?? ""),
                cvPath = (string)(u.CVPath ?? ""),
                portfolioUrl = (string)(u.PortfolioUrl ?? ""),
                createdAt = (DateTime?)u.CreatedAt,

                skillCount = (int)u.SkillCount,
                skillsJson = (string)(u.SkillsJson ?? "[]"),

                isApprovedByAdmin = (bool)u.IsApprovedByAdmin,
                isRejected = (bool)u.IsRejected
            }).ToList();

            return Json(rows);
        }

        // ── Recent user skill / CV changes for dashboard ────────────────
        // Group by user: one row per user, with all their changes in the popup.

        [HttpGet]
        public async Task<IActionResult> GetRecentUserChanges()
        {
            var changes = await _userRepository.GetRecentUserSkillChangesAsync(50);
            var byUser = new Dictionary<int, List<object>>();

            if (changes != null)
            {
                foreach (dynamic row in changes)
                {
                    int userId = Convert.ToInt32(row.UserId ?? row.userId ?? 0);
                    if (userId <= 0) continue;

                    int fieldId = Convert.ToInt32(row.FieldId ?? row.fieldId ?? 0);
                    int skillId = Convert.ToInt32(row.SkillId ?? row.skillId ?? 0);
                    int subSkillId = Convert.ToInt32(row.SubSkillId ?? row.subSkillId ?? 0);

                    var changeObj = new
                    {
                        userSkillChangeId = row.UserSkillChangeId ?? row.userSkillChangeId,
                        actionType = (string)(row.ActionType ?? row.actionType ?? ""),
                        changedAt = row.ChangedAt ?? row.changedAt,
                        fieldId = fieldId,
                        skillId = skillId,
                        subSkillId = subSkillId
                    };

                    if (!byUser.ContainsKey(userId))
                        byUser[userId] = new List<object>();
                    byUser[userId].Add(changeObj);
                }
            }

            var enriched = new List<object>();
            foreach (var kv in byUser)
            {
                int userId = kv.Key;
                var userChanges = kv.Value;
                var user = await _userRepository.GetUserByIdAsync(userId);
                string username = user?.Username ?? ("User #" + userId);
                string cvPath = user?.CVPath ?? "";
                string portfolioUrl = user?.PortfolioUrl ?? "";

                DateTime? latest = null;
                foreach (dynamic c in userChanges)
                {
                    var dt = c.changedAt ?? c.ChangedAt;
                    if (dt != null)
                    {
                        DateTime d = Convert.ToDateTime(dt);
                        if (!latest.HasValue || d > latest.Value) latest = d;
                    }
                }

                enriched.Add(new
                {
                    userId = userId,
                    username = username,
                    cvPath = cvPath,
                    portfolioUrl = portfolioUrl,
                    changeCount = userChanges.Count,
                    latestChangedAt = latest,
                    changes = userChanges
                });
            }

            enriched = enriched.OrderByDescending(x =>
            {
                var lt = ((dynamic)x).latestChangedAt;
                return lt != null ? (DateTime)lt : DateTime.MinValue;
            }).ToList();
            return Json(enriched);
        }
        // ── Approve User ─────────────────────────────────────────────────

        [HttpPost]
        public async Task<IActionResult> ApproveUser(int userId)
        {
            Console.WriteLine($"🔍 ApproveUser - UserId: {userId}");

            var (success, message) = await _userRepository.ApproveUserAsync(userId);

            if (success)
            {
                Console.WriteLine($"✅ User {userId} approved");

                // Send approval email so user knows they can log in
                var user = await _userRepository.GetUserByIdAsync(userId);
                if (user != null)
                {
                    await _emailService.SendApprovalEmailAsync(user.Email, user.Username);
                    Console.WriteLine($"✅ Approval email sent to {user.Email}");
                }
            }
            else
            {
                Console.WriteLine($"❌ Approve failed for user {userId}: {message}");
            }

            return Json(new { success, message });
        }

        // ── Reject User ──────────────────────────────────────────────────
        // Reject = delete user from system and send an email with a reason.

        [HttpPost]
        public async Task<IActionResult> RejectUser(int userId, string reason)
        {
            Console.WriteLine($"🔍 RejectUser - UserId: {userId}");

            var user = await _userRepository.GetUserByIdAsync(userId);
            if (user == null)
            {
                return Json(new { success = false, message = "User not found." });
            }

            var (success, message) = await _userRepository.RejectUserAsync(userId);

            if (success)
            {
                Console.WriteLine($"✅ User {userId} rejected / deleted");

                var cleanReason = string.IsNullOrWhiteSpace(reason)
                    ? "Your profile information did not meet our verification requirements."
                    : reason;

                await _emailService.SendRejectionEmailAsync(user.Email, user.Username, cleanReason);
            }
            else
            {
                Console.WriteLine($"❌ Reject failed for user {userId}: {message}");
            }

            return Json(new { success, message });
        }

        // ── Other Admin Pages (add your existing ones below) ─────────────

        [HttpGet]
        public IActionResult About()
        {
            return View();
        }

        [HttpGet]
        public IActionResult OurServices()
        {
            return View();
        }
    }
}
