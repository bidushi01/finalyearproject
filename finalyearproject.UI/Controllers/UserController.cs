using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using finalyearproject.Data.Repository;
using Microsoft.AspNetCore.SignalR;
using finalyearproject.UI.Hubs;

namespace finalyearproject.UI.Controllers
{
    public class UserController : Controller
    {
        private readonly IUserRepository _userRepository;
        private readonly IHubContext<HelpHub> _helpHub;

        public UserController(IUserRepository userRepository, IHubContext<HelpHub> helpHub)
        {
            _userRepository = userRepository;
            _helpHub = helpHub;
        }

        // ── On login, redirect straight to Profile ──────────────────────
        [HttpGet]
        public IActionResult Index()
        {
            return RedirectToAction("Profile");
        }

        // ── Profile page ────────────────────────────────────────────────
        [HttpGet]
        public IActionResult Profile()
        {
            return View();
        }

        // ── Help Inbox: requests sent TO me as helper ─────────────────────
        [HttpGet]
        [Authorize(Roles = "User")]
        public IActionResult MyHelpInbox()
        {
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            ViewBag.UserId = userIdClaim ?? "";
            return View();
        }

        // ── Ask for Help page ───────────────────────────────────────────
        [HttpGet]
        [Authorize(Roles = "User")]
        public IActionResult AskHelp()
        {
            return View();
        }

        // ── Return current user's basic info (for Profile page header) ──
        [HttpGet]
        public async Task<IActionResult> GetUserInfo()
        {
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdClaim)) return Unauthorized();

            int userId = int.Parse(userIdClaim);
            var user = await _userRepository.GetUserByIdAsync(userId);
            if (user == null) return NotFound();

            return Json(new
            {
                email = user.Email,
                phoneNumber = user.PhoneNumber,
                joinedDate = user.CreatedAt,
                cvPath = user.CVPath,
                portfolioUrl = user.PortfolioUrl
            });
        }

        // ── Return current user's skills as JSON (for Profile page) ────
        [HttpGet]
        public async Task<IActionResult> GetUserSkillsJson()
        {
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdClaim)) return Unauthorized();

            int userId = int.Parse(userIdClaim);
            var skills = await _userRepository.GetUserSkillsDisplayAsync(userId);
            return Json(skills);
        }

        // ── PEERASSIST: Ranked helpers (Phases 4–7). Optional excludeHelperId for "next best" after timeout ──
        [HttpGet]
        [Authorize(Roles = "User")]
        public async Task<IActionResult> GetRankedHelpers(int fieldId, int skillId, int subSkillId, string timeStart, string timeEnd, string availableDay, int excludeHelperId = 0)
        {
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdClaim)) return Unauthorized();

            int seekerId = int.Parse(userIdClaim);
            TimeSpan? ts = ParseTime(timeStart);
            TimeSpan? te = ParseTime(timeEnd);

            var rows = await _userRepository.GetRankedHelpersAsync(seekerId, fieldId, skillId, subSkillId, ts, te, availableDay);
            var list = new List<object>();
            if (rows != null)
            {
                foreach (var r in rows)
                {
                    int uid = (int)(r.UserId ?? r.userId ?? 0);
                    if (excludeHelperId > 0 && uid == excludeHelperId) continue;
                    var user = await _userRepository.GetUserByIdAsync(uid);
                    list.Add(new
                    {
                        userId = uid,
                        username = user?.Username ?? ("User #" + uid),
                        hes = r.HES ?? r.hes
                    });
                }
            }
            return Json(list);
        }

        // ── Phase 3: Send help request (CRH check, set RP_s=1, create request) ─
        [HttpPost]
        [Authorize(Roles = "User")]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> SendHelpRequest(int helperId, int fieldId, int skillId, int subSkillId, string timeStart, string timeEnd, string availableDay, string description)
        {
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdClaim)) return Json(new { success = false, message = "Not logged in." });

            int seekerId = int.Parse(userIdClaim);
            if (!await _userRepository.CanRequestHelpAsync(seekerId))
                return Json(new { success = false, message = "You already have an active request or session. Complete or cancel it first." });

            TimeSpan? ts = ParseTime(timeStart);
            TimeSpan? te = ParseTime(timeEnd);
            var (helpRequestId, result, message) = await _userRepository.CreateHelpRequestAsync(
                seekerId, helperId, fieldId, skillId, subSkillId, ts, te, availableDay ?? "", description ?? "");

            if (result == 1 && helperId > 0)
            {
                var seeker = await _userRepository.GetUserByIdAsync(seekerId);
                var seekerName = seeker?.Username ?? "Someone";
                await _helpHub.Clients.Group("user-" + helperId)
                    .SendAsync("HelpRequestReceived", seekerName);
            }

            return Json(new { success = result == 1, message, helpRequestId });
        }

        [HttpGet]
        [Authorize(Roles = "User")]
        public IActionResult MyHelpRequests()
        {
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            ViewBag.UserId = userIdClaim ?? "";
            return View();
        }

        [HttpGet]
        [Authorize(Roles = "User")]
        public async Task<IActionResult> GetMyHelpRequests()
        {
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdClaim)) return Unauthorized();
            int userId = int.Parse(userIdClaim);

            var asSeeker = await _userRepository.GetHelpRequestsBySeekerAsync(userId);
            var asHelper = await _userRepository.GetHelpRequestsByHelperAsync(userId);
            return Json(new { asSeeker = asSeeker ?? Array.Empty<object>(), asHelper = asHelper ?? Array.Empty<object>() });
        }

        // ── Helper-only and seeker-only lists (used by separate pages) ───

        [HttpGet]
        [Authorize(Roles = "User")]
        public async Task<IActionResult> GetRequestsAsHelper()
        {
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdClaim)) return Unauthorized();

            int userId = int.Parse(userIdClaim);
            var list = await _userRepository.GetHelpRequestsByHelperAsync(userId);
            return Json(list ?? Array.Empty<object>());
        }

        [HttpGet]
        [Authorize(Roles = "User")]
        public async Task<IActionResult> GetRequestsAsSeeker()
        {
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdClaim)) return Unauthorized();

            int userId = int.Parse(userIdClaim);
            var list = await _userRepository.GetHelpRequestsBySeekerAsync(userId);
            return Json(list ?? Array.Empty<object>());
        }

        // ── Navbar notification snapshot (pending requests counts) ────────

        [HttpGet]
        [Authorize(Roles = "User")]
        public async Task<IActionResult> GetNotificationSnapshot()
        {
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdClaim)) return Unauthorized();

            int userId = int.Parse(userIdClaim);
            int inboxPending = await _userRepository.GetUserPendingHelpRequestCountAsync(userId);
            int myActive = await _userRepository.GetUserActiveSentRequestCountAsync(userId);

            return Json(new
            {
                inboxPending,
                myActive
            });
        }

        [HttpPost]
        [Authorize(Roles = "User")]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> AcceptRequest(int helpRequestId)
        {
            var ok = await _userRepository.AcceptHelpRequestAsync(helpRequestId);
            return Json(new { success = ok });
        }

        [HttpPost]
        [Authorize(Roles = "User")]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> RejectRequest(int helpRequestId)
        {
            var ok = await _userRepository.RejectHelpRequestAsync(helpRequestId);
            return Json(new { success = ok });
        }

        [HttpPost]
        [Authorize(Roles = "User")]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> WithdrawRequest(int helpRequestId)
        {
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdClaim)) return Json(new { success = false });
            int userId = int.Parse(userIdClaim);
            var ok = await _userRepository.WithdrawHelpRequestAsync(helpRequestId, userId);
            return Json(new { success = ok });
        }

        [HttpGet]
        [Authorize(Roles = "User")]
        public async Task<IActionResult> GetChatMessages(int helpRequestId)
        {
            var messages = await _userRepository.GetHelpRequestMessagesAsync(helpRequestId);
            return Json(messages ?? Array.Empty<object>());
        }

        [HttpPost]
        [Authorize(Roles = "User")]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> SendChatMessage(int helpRequestId, string messageText, IFormFile attachment)
        {
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdClaim)) return Json(new { success = false });
            int userId = int.Parse(userIdClaim);
            string attachmentPath = null;

            var status = (await _userRepository.GetHelpRequestStatusAsync(helpRequestId))?.ToLowerInvariant() ?? "";
            if (status == "completed" || status == "notcompleted")
                return Json(new { success = false, message = "This session is closed. Messaging is disabled." });

            if (attachment != null && attachment.Length > 0)
            {
                // Limit to ~2MB to save disk space
                if (attachment.Length > 2 * 1024 * 1024)
                    return Json(new { success = false, message = "File too large (max 2 MB)." });

                var ext = Path.GetExtension(attachment.FileName)?.ToLowerInvariant() ?? "";
                if (ext != ".png" && ext != ".jpg" && ext != ".jpeg" && ext != ".gif" && ext != ".pdf")
                    return Json(new { success = false, message = "Only images (png,jpg,gif) and PDF files are allowed." });

                var root = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "chat-attachments");
                if (!Directory.Exists(root))
                    Directory.CreateDirectory(root);

                var fileName = Guid.NewGuid().ToString("N") + ext;
                var fullPath = Path.Combine(root, fileName);
                using (var stream = new FileStream(fullPath, FileMode.Create))
                {
                    await attachment.CopyToAsync(stream);
                }

                attachmentPath = "/chat-attachments/" + fileName;
            }

            await _userRepository.SendHelpMessageAsync(helpRequestId, userId, messageText ?? "", attachmentPath);

            var parties = await _userRepository.GetHelpRequestPartiesAsync(helpRequestId);
            var receiverId = userId == parties.SeekerId ? parties.HelperId : parties.SeekerId;
            if (receiverId > 0)
            {
                var me = await _userRepository.GetUserByIdAsync(userId);
                var fromName = me?.Username ?? "Someone";
                await _helpHub.Clients.Group("user-" + receiverId)
                    .SendAsync("MessageReceived", fromName);
            }

            return Json(new { success = true });
        }

        [HttpPost]
        [Authorize(Roles = "User")]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> CompleteHelpRequest(int helpRequestId, bool isSuccessful, int rating)
        {
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdClaim)) return Json(new { success = false, message = "Not logged in." });
            int currentUserId = int.Parse(userIdClaim);

            if (rating < 1 || rating > 5)
                return Json(new { success = false, message = "Rating is required (1–5)." });

            var parties = await _userRepository.GetHelpRequestPartiesAsync(helpRequestId);
            if (parties.SeekerId == 0 || parties.HelperId == 0)
                return Json(new { success = false, message = "Help request not found." });

            // Only the help seeker is allowed to complete and rate
            if (parties.SeekerId != currentUserId)
                return Json(new { success = false, message = "Only the help seeker can complete this request." });

            var ok = await _userRepository.EndSessionAsync(helpRequestId, isSuccessful);
            if (!ok)
                return Json(new { success = false, message = "Could not update session. It may already be closed." });

            if (parties.HelperId > 0)
            {
                await _userRepository.UpdateHelperRatingAsync(
                    parties.HelperId,
                    parties.FieldId,
                    parties.SkillId,
                    parties.SubSkillId,
                    rating);
            }

            return Json(new { success = true });
        }

        // ── Profile stats (sessions completed, rating, points) ────────────

        [HttpGet]
        [Authorize(Roles = "User")]
        public async Task<IActionResult> GetProfileStats()
        {
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdClaim)) return Unauthorized();

            int userId = int.Parse(userIdClaim);
            var stats = await _userRepository.GetUserProfileStatsAsync(userId);
            return Json(stats);
        }

        private static TimeSpan? ParseTime(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return null;
            if (TimeSpan.TryParse(s, out var t)) return t;
            return null;
        }
    }
}