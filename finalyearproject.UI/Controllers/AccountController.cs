using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using System.Linq;
using System.Security.Claims;
using finalyearproject.UI.Models;
using finalyearproject.Data.Repository;
using finalyearproject.Data.Services;
using finalyearproject.Data.Helper;
using finalyearproject.Data.Models.Domain;
using Newtonsoft.Json;

namespace finalyearproject.UI.Controllers
{
    public class AccountController : Controller
    {
        private readonly IUserRepository _userRepository;
        private readonly IPasswordService _passwordService;
        private readonly IEmailService _emailService;

        private const string SkillDraftSessionKey = "SkillDraft";

        public AccountController(
            IUserRepository userRepository,
            IPasswordService passwordService,
            IEmailService emailService)
        {
            _userRepository = userRepository;
            _passwordService = passwordService;
            _emailService = emailService;
        }

        private List<Dictionary<string, object>> GetSkillDraft()
        {
            var json = HttpContext.Session.GetString(SkillDraftSessionKey) ?? "[]";
            return JsonConvert.DeserializeObject<List<Dictionary<string, object>>>(json) ?? new();
        }

        private void SetSkillDraft(List<Dictionary<string, object>> skills) =>
            HttpContext.Session.SetString(SkillDraftSessionKey, JsonConvert.SerializeObject(skills));

        // ═══════════════════════════════════════════════════
        // REGISTER
        // ═══════════════════════════════════════════════════

        [HttpGet]
        public IActionResult Register()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(RegisterViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            // ✅ Clear, field-specific duplicate messages
            var existingUserByEmail = await _userRepository.GetUserByEmailAsync(model.Email);
            if (existingUserByEmail != null)
            {
                ModelState.AddModelError(nameof(model.Email), "⚠️ This email is already registered. Please use a different email or log in.");
                return View(model);
            }

            var existingUserByUsername = await _userRepository.GetUserByUsernameAsync(model.Username);
            if (existingUserByUsername != null)
            {
                ModelState.AddModelError(nameof(model.Username), "⚠️ This username is already taken. Please choose another.");
                return View(model);
            }

            var (hash, salt) = _passwordService.HashPassword(model.Password);

            HttpContext.Session.SetString("Username", model.Username);
            HttpContext.Session.SetString("Email", model.Email);
            HttpContext.Session.SetString("PasswordHash", hash);
            HttpContext.Session.SetString("PasswordSalt", salt);
            HttpContext.Session.SetString("PhoneNumber", model.PhoneNumber);
            HttpContext.Session.SetString("UserSkills", "[]");
            HttpContext.Session.SetString("CVPath", "");
            HttpContext.Session.SetString("PortfolioUrl", "");

            string otpCode = GenerateOTP();
            DateTime expiresAt = DateTime.Now.AddMinutes(10);
            await _userRepository.StoreOTPAsync(model.Email, otpCode, expiresAt);
            await _emailService.SendOTPEmailAsync(model.Email, otpCode);

            TempData["SuccessMessage"] = "Check your email for the verification code.";
            return RedirectToAction("VerifyOTP");
        }

        // ═══════════════════════════════════════════════════
        // ADD SKILLS (logged-in helpers only)
        // ═══════════════════════════════════════════════════

        [HttpGet]
        [Authorize(Roles = "User")]
        public async Task<IActionResult> AddSkills()
        {
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdClaim))
                return RedirectToAction("Login");

            var user = await _userRepository.GetUserByIdAsync(int.Parse(userIdClaim));
            if (user == null)
                return RedirectToAction("Login");

            ViewBag.Username = user.Username;
            ViewBag.Email = user.Email;
            ViewBag.IsLoggedInUser = true;
            ViewBag.UserId = user.UserId;

            if (HttpContext.Session.GetString(SkillDraftSessionKey) == null)
                SetSkillDraft(new List<Dictionary<string, object>>());

            return View();
        }

        [HttpGet]
        public async Task<IActionResult> GetFields()
        {
            var fields = await _userRepository.GetAllFieldsAsync();
            return Json(fields);
        }

        [HttpGet]
        public async Task<IActionResult> GetSkillsByField(int fieldId)
        {
            var skills = await _userRepository.GetSkillsByFieldAsync(fieldId);
            return Json(skills);
        }

        [HttpGet]
        public async Task<IActionResult> GetSubSkillsBySkill(int skillId)
        {
            var subSkills = await _userRepository.GetSubSkillsBySkillAsync(skillId);
            return Json(subSkills);
        }

        [HttpGet]
        public IActionResult GetUserSkills()
        {
            // Registration-time, session-based skills (step 2 of registration)
            var skillsJson = HttpContext.Session.GetString("UserSkills") ?? "[]";
            var skills = JsonConvert.DeserializeObject<List<dynamic>>(skillsJson);
            return Json(skills);
        }

        // Logged-in users: DB skills (approved / pending review / rejected) + session drafts
        [HttpGet]
        [Authorize(Roles = "User")]
        public async Task<IActionResult> GetCurrentUserSkills()
        {
            var idStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(idStr))
                return Unauthorized();

            int userId = int.Parse(idStr);
            var dbSkills = await _userRepository.GetUserSkillsDisplayAsync(userId, approvedOnly: false);
            var combined = new List<object>();
            foreach (var s in dbSkills)
                combined.Add(s);

            foreach (var d in GetSkillDraft())
            {
                var copy = new Dictionary<string, object>(d);
                copy["ApprovalStatus"] = "Draft";
                combined.Add(copy);
            }

            return Json(combined);
        }

        [HttpGet]
        [Authorize(Roles = "User")]
        public IActionResult GetSkillDraftCount()
        {
            return Json(new { count = GetSkillDraft().Count });
        }

        [HttpPost]
        public async Task<IActionResult> AddNewField(string fieldName)
        {
            var (result, message) = await _userRepository.AddFieldAsync(fieldName);
            if (result > 0)
            {
                var fields = await _userRepository.GetAllFieldsAsync();
                var newField = ((IEnumerable<dynamic>)fields).FirstOrDefault(f => f.FieldId == result);
                return Json(new { success = true, fieldId = result, fieldName = newField?.FieldName ?? fieldName, message });
            }
            return Json(new { success = false, fieldId = result, message });
        }

        [HttpPost]
        public async Task<IActionResult> AddNewSkill(int fieldId, string skillName)
        {
            var (result, message) = await _userRepository.AddSkillAsync(fieldId, skillName);
            if (result > 0)
            {
                var skills = await _userRepository.GetSkillsByFieldAsync(fieldId);
                var newSkill = ((IEnumerable<dynamic>)skills).FirstOrDefault(s => s.SkillId == result);
                return Json(new { success = true, skillId = result, skillName = newSkill?.SkillName ?? skillName, message });
            }
            return Json(new { success = false, skillId = result, message });
        }

        [HttpPost]
        public async Task<IActionResult> AddNewSubSkill(int skillId, string subSkillName)
        {
            var (result, message) = await _userRepository.AddSubSkillAsync(skillId, subSkillName);
            if (result > 0)
            {
                var subSkills = await _userRepository.GetSubSkillsBySkillAsync(skillId);
                var newSubSkill = ((IEnumerable<dynamic>)subSkills).FirstOrDefault(ss => ss.SubSkillId == result);
                return Json(new { success = true, subSkillId = result, subSkillName = newSubSkill?.SubSkillName ?? subSkillName, message });
            }
            return Json(new { success = false, subSkillId = result, message });
        }

        [HttpPost]
        public async Task<IActionResult> AddUserSkill([FromBody] AddSkillViewModel model)
        {
            // Registration flow: keep skills in session until OTP verifies and user row is created
            if (model == null)
                return Json(new { success = false, message = "Invalid data received" });

            var timeErr = ValidateAvailabilityTimes(model.AvailableTimeStart, model.AvailableTimeEnd, model.AvailableTimeSlots);
            if (timeErr != null)
                return Json(new { success = false, message = timeErr });

            var skillsJson = HttpContext.Session.GetString("UserSkills") ?? "[]";
            var skills = JsonConvert.DeserializeObject<List<Dictionary<string, object>>>(skillsJson);

            var fields = await _userRepository.GetAllFieldsAsync();
            var skillsList = await _userRepository.GetSkillsByFieldAsync(model.FieldId);
            var subSkills = await _userRepository.GetSubSkillsBySkillAsync(model.SkillId);

            var field = ((IEnumerable<dynamic>)fields).FirstOrDefault(f => f.FieldId == model.FieldId);
            var skill = ((IEnumerable<dynamic>)skillsList).FirstOrDefault(s => s.SkillId == model.SkillId);
            var subSkill = ((IEnumerable<dynamic>)subSkills).FirstOrDefault(ss => ss.SubSkillId == model.SubSkillId);

            var tempSkill = new Dictionary<string, object>
            {
                { "TempId",             Guid.NewGuid().ToString() },
                { "FieldId",            model.FieldId },
                { "FieldName",          field?.FieldName ?? "" },
                { "SkillId",            model.SkillId },
                { "SkillName",          skill?.SkillName ?? "" },
                { "SubSkillId",         model.SubSkillId },
                { "SubSkillName",       subSkill?.SubSkillName ?? "" },
                { "ExperienceLevel",    model.ExperienceLevel },
                { "AvailableDays",      model.AvailableDays },
                { "AvailableTimeStart", model.AvailableTimeStart ?? "" },
                { "AvailableTimeEnd",   model.AvailableTimeEnd ?? "" },
                { "AvailableTimeSlots", model.AvailableTimeSlots ?? "" }
            };

            skills.Add(tempSkill);
            HttpContext.Session.SetString("UserSkills", JsonConvert.SerializeObject(skills));
            return Json(new { success = true, message = "Skill added successfully", skill = tempSkill });
        }

        // Logged-in users: keep new skills in session until SubmitSkillsForAdminReview
        [HttpPost]
        [Authorize(Roles = "User")]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> AddUserSkillForLoggedIn([FromBody] AddSkillViewModel model)
        {
            try
            {
            if (model == null)
                return Json(new { success = false, message = "Invalid data received" });

            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).Where(m => !string.IsNullOrEmpty(m));
                return Json(new { success = false, message = string.Join(" ", errors) });
            }

            var idStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(idStr))
                return Json(new { success = false, message = "User is not logged in." });

            int userId = int.Parse(idStr);

            if (await SkillSubSkillAlreadyExistsAsync(userId, model.FieldId, model.SkillId, model.SubSkillId, model.TempId))
                return Json(new { success = false, message = "You already have this sub-skill (or it is in your draft list)." });

            var timeErr = ValidateAvailabilityTimes(model.AvailableTimeStart, model.AvailableTimeEnd, model.AvailableTimeSlots);
            if (timeErr != null)
                return Json(new { success = false, message = timeErr });

            var fields = await _userRepository.GetAllFieldsAsync();
            var skillsList = await _userRepository.GetSkillsByFieldAsync(model.FieldId);
            var subSkills = await _userRepository.GetSubSkillsBySkillAsync(model.SkillId);

            var field = ((IEnumerable<dynamic>)fields).FirstOrDefault(f => DynInt(f, "FieldId") == model.FieldId);
            var skill = ((IEnumerable<dynamic>)skillsList).FirstOrDefault(s => DynInt(s, "SkillId") == model.SkillId);
            var subSkill = ((IEnumerable<dynamic>)subSkills).FirstOrDefault(ss => DynInt(ss, "SubSkillId") == model.SubSkillId);

            var draft = GetSkillDraft();
            var entry = new Dictionary<string, object>
            {
                { "TempId", string.IsNullOrEmpty(model.TempId) ? Guid.NewGuid().ToString() : model.TempId },
                { "FieldId", model.FieldId },
                { "FieldName", field?.FieldName ?? "" },
                { "SkillId", model.SkillId },
                { "SkillName", skill?.SkillName ?? "" },
                { "SubSkillId", model.SubSkillId },
                { "SubSkillName", subSkill?.SubSkillName ?? "" },
                { "ExperienceLevel", model.ExperienceLevel },
                { "AvailableDays", model.AvailableDays },
                { "AvailableTimeStart", model.AvailableTimeStart ?? "" },
                { "AvailableTimeEnd", model.AvailableTimeEnd ?? "" },
                { "AvailableTimeSlots", model.AvailableTimeSlots ?? "" }
            };

            if (!string.IsNullOrEmpty(model.TempId))
                draft.RemoveAll(s => s.ContainsKey("TempId") && s["TempId"]?.ToString() == model.TempId);

            draft.Add(entry);
            SetSkillDraft(draft);

            return Json(new
            {
                success = true,
                message = "Skill added to your list. Upload your CV, then click Submit for admin verification.",
                skill = entry,
                isDraft = true
            });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Could not add skill: " + ex.Message });
            }
        }

        private static int DynInt(dynamic row, string name)
        {
            if (row == null) return 0;
            try
            {
                var camel = char.ToLowerInvariant(name[0]) + name[1..];
                if (row is IDictionary<string, object> dict)
                {
                    if (dict.TryGetValue(name, out var v) || dict.TryGetValue(camel, out v))
                        return Convert.ToInt32(v ?? 0);
                }
                var prop = row.GetType().GetProperty(name) ?? row.GetType().GetProperty(camel);
                if (prop != null)
                    return Convert.ToInt32(prop.GetValue(row) ?? 0);
            }
            catch { /* ignore */ }
            return 0;
        }

        [HttpPost]
        [Authorize(Roles = "User")]
        [IgnoreAntiforgeryToken]
        public IActionResult DeleteDraftSkill(string tempId)
        {
            if (string.IsNullOrEmpty(tempId))
                return Json(new { success = false, message = "Invalid skill." });

            var draft = GetSkillDraft();
            draft.RemoveAll(s => s.ContainsKey("TempId") && s["TempId"]?.ToString() == tempId);
            SetSkillDraft(draft);
            return Json(new { success = true, message = "Draft skill removed." });
        }

        [HttpPost]
        [Authorize(Roles = "User")]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> SubmitSkillsForAdminReview(IFormFile cv)
        {
            var idStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(idStr))
                return Json(new { success = false, message = "User is not logged in." });

            int userId = int.Parse(idStr);
            var user = await _userRepository.GetUserByIdAsync(userId);
            if (user == null)
                return Json(new { success = false, message = "User not found." });

            if (cv != null && cv.Length > 0)
            {
                var newCvPath = await SaveFileAsync(cv, "uploads");
                await _userRepository.UpdateUserDocumentsAsync(userId, newCvPath, user.PortfolioUrl ?? string.Empty);
                user = await _userRepository.GetUserByIdAsync(userId);
            }

            if (string.IsNullOrWhiteSpace(user?.CVPath))
                return Json(new { success = false, message = "Please upload and save your CV before submitting skills for admin verification." });

            var draft = GetSkillDraft();
            if (draft.Count == 0)
                return Json(new { success = false, message = "Add at least one skill to your list before submitting." });

            int added = 0;
            var errors = new List<string>();

            foreach (var skill in draft)
            {
                int fieldId = Convert.ToInt32(skill["FieldId"]);
                int skillId = Convert.ToInt32(skill["SkillId"]);
                int subSkillId = Convert.ToInt32(skill["SubSkillId"]);
                int experienceLevel = Convert.ToInt32(skill["ExperienceLevel"]);
                var availableDays = skill["AvailableDays"]?.ToString() ?? "";

                TimeSpan? startTime = null;
                TimeSpan? endTime = null;
                var startStr = skill["AvailableTimeStart"]?.ToString();
                var endStr = skill["AvailableTimeEnd"]?.ToString();
                var slotsJson = skill.ContainsKey("AvailableTimeSlots") ? skill["AvailableTimeSlots"]?.ToString() : null;
                if (!string.IsNullOrEmpty(startStr) && TimeSpan.TryParse(startStr, out var ps))
                    startTime = ps;
                if (!string.IsNullOrEmpty(endStr) && TimeSpan.TryParse(endStr, out var pe))
                    endTime = pe;

                var slotErr = ValidateAvailabilityTimes(startStr, endStr, slotsJson);
                if (slotErr != null)
                {
                    errors.Add(slotErr);
                    continue;
                }

                var (result, message) = await _userRepository.AddUserSkillAsync(
                    userId, fieldId, skillId, subSkillId, experienceLevel, availableDays, startTime, endTime, slotsJson);

                if (result > 0)
                {
                    added++;
                    await _userRepository.LogUserSkillChangeAsync(userId, "SUBMIT_SKILL_FOR_REVIEW", fieldId, skillId, subSkillId);
                }
                else if (!string.IsNullOrEmpty(message))
                    errors.Add(message);
            }

            if (added == 0)
                return Json(new { success = false, message = errors.FirstOrDefault() ?? "Could not submit skills." });

            SetSkillDraft(new List<Dictionary<string, object>>());

            var msg = added == 1
                ? "1 skill sent for admin verification. You will receive email when admin approves or rejects it."
                : $"{added} skills sent for admin verification. You will receive email when admin approves or rejects each one.";

            if (errors.Count > 0)
                msg += " Some skills were skipped: " + string.Join("; ", errors);

            return Json(new { success = true, message = msg, submittedCount = added });
        }

        private async Task<bool> SkillSubSkillAlreadyExistsAsync(
            int userId, int fieldId, int skillId, int subSkillId, string? excludeTempId)
        {
            foreach (var d in GetSkillDraft())
            {
                if (!string.IsNullOrEmpty(excludeTempId) &&
                    d.ContainsKey("TempId") && d["TempId"]?.ToString() == excludeTempId)
                    continue;

                if (Convert.ToInt32(d["FieldId"]) == fieldId &&
                    Convert.ToInt32(d["SkillId"]) == skillId &&
                    Convert.ToInt32(d["SubSkillId"]) == subSkillId)
                    return true;
            }

            if (await _userRepository.UserHasNonRejectedSkillAsync(userId, fieldId, skillId, subSkillId))
                return true;

            return false;
        }

        private static string? ValidateAvailabilityTimes(string? startStr, string? endStr, string? slotsJson)
        {
            if (!string.IsNullOrWhiteSpace(slotsJson))
            {
                try
                {
                    var slots = JsonConvert.DeserializeObject<List<Dictionary<string, string>>>(slotsJson);
                    if (slots != null && slots.Count > 0)
                    {
                        var parsed = new List<(TimeSpan Start, TimeSpan End)>();
                        foreach (var slot in slots)
                        {
                            slot.TryGetValue("start", out var sRaw);
                            slot.TryGetValue("end", out var eRaw);
                            if (!TimeSpan.TryParse(sRaw, out var s) || !TimeSpan.TryParse(eRaw, out var e))
                                return "Invalid time slot.";
                            if (e <= s)
                                return "Each time slot must end later on the same day (cannot continue into the next day).";
                            parsed.Add((s, e));
                        }

                        for (var i = 0; i < parsed.Count; i++)
                        {
                            for (var j = i + 1; j < parsed.Count; j++)
                            {
                                if (parsed[i].Start < parsed[j].End && parsed[j].Start < parsed[i].End)
                                    return "Time slots on the same day cannot overlap.";
                            }
                        }

                        return null;
                    }
                }
                catch
                {
                    return "Invalid time slots data.";
                }
            }

            if (TimeSpan.TryParse(startStr, out var ps) && TimeSpan.TryParse(endStr, out var pe) && pe <= ps)
                return "End time must be later on the same day (cannot continue into the next day).";

            return null;
        }

        private static (TimeSpan? Start, TimeSpan? End, string? SlotsJson, string? Error) ParseAvailability(AddSkillViewModel model)
        {
            var err = ValidateAvailabilityTimes(model.AvailableTimeStart, model.AvailableTimeEnd, model.AvailableTimeSlots);
            if (err != null) return (null, null, null, err);

            TimeSpan? start = null;
            TimeSpan? end = null;
            if (!string.IsNullOrEmpty(model.AvailableTimeStart) && TimeSpan.TryParse(model.AvailableTimeStart, out var ps))
                start = ps;
            if (!string.IsNullOrEmpty(model.AvailableTimeEnd) && TimeSpan.TryParse(model.AvailableTimeEnd, out var pe))
                end = pe;

            return (start, end, model.AvailableTimeSlots, null);
        }

        [HttpPost]
        [Authorize(Roles = "User")]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> DeleteUserSkillForLoggedIn(int userSkillId)
        {
            var idStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(idStr)) return Unauthorized();
            int userId = int.Parse(idStr);

            if (!await _userRepository.UserOwnsUserSkillAsync(userId, userSkillId))
                return Json(new { success = false, message = "Skill not found." });

            await _userRepository.DeleteUserSkillAsync(userSkillId);
            return Json(new { success = true, message = "Skill removed." });
        }

        // Logged-in users: update an existing skill row
        [HttpPost]
        [Authorize(Roles = "User")]
        public async Task<IActionResult> UpdateUserSkillForLoggedIn([FromBody] AddSkillViewModel model)
        {
            if (model == null || model.UserSkillId == null)
                return Json(new { success = false, message = "Invalid data received" });

            var idStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(idStr))
                return Json(new { success = false, message = "User is not logged in." });

            int userId = int.Parse(idStr);

            var (startTime, endTime, slotsJson, timeErr) = ParseAvailability(model);
            if (timeErr != null)
                return Json(new { success = false, message = timeErr });

            var (result, message) = await _userRepository.UpdateUserSkillAsync(
                model.UserSkillId.Value,
                userId,
                model.FieldId,
                model.SkillId,
                model.SubSkillId,
                model.ExperienceLevel,
                model.AvailableDays,
                startTime,
                endTime,
                slotsJson);

            if (result > 0)
            {
                await _userRepository.LogUserSkillChangeAsync(
                    userId,
                    "UPDATE_SKILL",
                    model.FieldId,
                    model.SkillId,
                    model.SubSkillId);
            }

            return Json(new
            {
                success = result > 0,
                message
            });
        }

        // Logged-in users: update CV + portfolio
        [HttpPost]
        [Authorize(Roles = "User")]
        public async Task<IActionResult> UpdateDocumentsForLoggedIn(IFormFile cv, string portfolioUrl)
        {
            var idStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(idStr))
                return Json(new { success = false, message = "User is not logged in." });

            int userId = int.Parse(idStr);
            var user = await _userRepository.GetUserByIdAsync(userId);

            string cvPathToSave = user?.CVPath;
            if (cv != null && cv.Length > 0)
                cvPathToSave = await SaveFileAsync(cv, "uploads");

            if (string.IsNullOrWhiteSpace(cvPathToSave))
                return Json(new { success = false, message = "Please choose a CV file to upload." });

            var (result, message) = await _userRepository.UpdateUserDocumentsAsync(
                userId,
                cvPathToSave,
                portfolioUrl ?? user?.PortfolioUrl ?? string.Empty);

            if (result == 1)
            {
                await _userRepository.LogUserSkillChangeAsync(
                    userId,
                    "UPDATE_DOCUMENTS",
                    0,
                    0,
                    0);
            }

            return Json(new { success = result == 1, message });
        }

        [HttpPost]
        public IActionResult DeleteUserSkill(string tempId)
        {
            var skillsJson = HttpContext.Session.GetString("UserSkills") ?? "[]";
            var skills = JsonConvert.DeserializeObject<List<Dictionary<string, object>>>(skillsJson);
            skills.RemoveAll(s => s.ContainsKey("TempId") && s["TempId"].ToString() == tempId);
            HttpContext.Session.SetString("UserSkills", JsonConvert.SerializeObject(skills));
            return Json(new { success = true, message = "Skill deleted successfully" });
        }

        [HttpPost]
        public async Task<IActionResult> UploadCVAndProceed(IFormFile cv, string portfolioUrl, bool skipSkills = false)
        {
            var username = HttpContext.Session.GetString("Username");
            var email = HttpContext.Session.GetString("Email");
            var skillsJson = HttpContext.Session.GetString("UserSkills") ?? "[]";
            var skills = JsonConvert.DeserializeObject<List<Dictionary<string, object>>>(skillsJson);

            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(email))
                return Json(new { success = false, message = "Session expired. Please start registration again." });

            if (!skipSkills && skills.Count == 0)
                return Json(new { success = false, message = "Please add at least one skill before proceeding, or use Skip skills." });

            if (!skipSkills && (cv == null || cv.Length == 0))
                return Json(new { success = false, message = "Please upload your CV" });

            string cvPath = "";
            if (cv != null && cv.Length > 0)
                cvPath = await SaveFileAsync(cv, "uploads");

            HttpContext.Session.SetString("CVPath", cvPath);
            HttpContext.Session.SetString("PortfolioUrl", portfolioUrl ?? "");
            if (skipSkills)
                HttpContext.Session.SetString("UserSkills", "[]");

            string otpCode = GenerateOTP();
            DateTime expiresAt = DateTime.Now.AddMinutes(10);

            await _userRepository.StoreOTPAsync(email, otpCode, expiresAt);
            await _emailService.SendOTPEmailAsync(email, otpCode);

            Console.WriteLine($"✅ OTP SENT TO {email}: {otpCode}");
            return Json(new { success = true, message = "OTP sent to your email", redirectUrl = Url.Action("VerifyOTP") });
        }

        // ═══════════════════════════════════════════════════
        // VERIFY OTP
        // ═══════════════════════════════════════════════════

        [HttpGet]
        public IActionResult VerifyOTP()
        {
            var email = HttpContext.Session.GetString("Email");
            if (string.IsNullOrEmpty(email))
                return RedirectToAction("Register");

            return View(new VerifyOTPViewModel { Email = email });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> VerifyOTP(VerifyOTPViewModel model)
        {
            Console.WriteLine($"🔍 VERIFY OTP - Email: {model.Email}, Code: {model.OTPCode}");

            if (!ModelState.IsValid)
                return View(model);

            var username = HttpContext.Session.GetString("Username");
            var email = HttpContext.Session.GetString("Email");

            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(email))
            {
                ModelState.AddModelError("", "Session expired. Please start registration again.");
                return View(model);
            }

            // ✅ FIX: Safe null guard before accessing dynamic property
            var otpResult = await _userRepository.CheckOTPValidityAsync(model.Email, model.OTPCode);

            bool isValid = false;
            if (otpResult != null)
            {
                var rawValue = otpResult.IsValid;
                if (rawValue is bool boolVal)
                    isValid = boolVal;
                else if (rawValue != null)
                    isValid = Convert.ToInt32(rawValue) == 1;
            }
            else
            {
                Console.WriteLine("❌ otpResult is NULL — OTP not found or already used");
            }

            Console.WriteLine($"✅ Final isValid = {isValid}");

            if (!isValid)
            {
                ModelState.AddModelError("", "❌ Invalid or expired OTP code. Please check and try again.");
                return View(model);
            }

            var passwordHash = HttpContext.Session.GetString("PasswordHash");
            var passwordSalt = HttpContext.Session.GetString("PasswordSalt");
            var phoneNumber = HttpContext.Session.GetString("PhoneNumber");

            var user = new User
            {
                Username = username,
                Email = email,
                PasswordHash = passwordHash,
                PasswordSalt = passwordSalt,
                PhoneNumber = phoneNumber,
                CVPath = "",
                PortfolioUrl = ""
            };

            var (result, message) = await _userRepository.RegisterUserAsync(user);

            if (result > 0)
            {
                await _userRepository.MarkOTPAsUsedAsync(model.Email, model.OTPCode);
                await _userRepository.MarkEmailAsVerifiedAsync(result);
                await _userRepository.FinalizeRegistrationAutoApproveAsync(result);

                HttpContext.Session.Clear();
                TempData["SuccessMessage"] = "Registration complete! You can log in now and use Ask Help. Add skills from your profile when you want to help others.";
                return RedirectToAction("RegistrationComplete");
            }

            Console.WriteLine($"❌ Registration failed: {message}");
            ModelState.AddModelError("", message);
            return View(model);
        }

        [HttpPost]
        public async Task<IActionResult> ResendOTP(string email)
        {
            string otpCode = GenerateOTP();
            DateTime expiresAt = DateTime.Now.AddMinutes(10);
            await _userRepository.StoreOTPAsync(email, otpCode, expiresAt);
            await _emailService.SendOTPEmailAsync(email, otpCode);
            Console.WriteLine($"✅ OTP RESENT TO {email}: {otpCode}");
            return Json(new { success = true, message = "OTP resent successfully!" });
        }

        [HttpGet]
        public IActionResult RegistrationComplete()
        {
            return View();
        }

        // ═══════════════════════════════════════════════════
        // LOGIN
        // ═══════════════════════════════════════════════════

        [HttpGet]
        public IActionResult Login()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            // ── Check admin by username ──
            var admin = await _userRepository.GetAdminByUsernameAsync(model.EmailOrUsername);
            if (admin != null)
            {
                bool isAdminPasswordValid = _passwordService.VerifyPassword(
                    model.Password, admin.PasswordHash, admin.PasswordSalt);

                if (!isAdminPasswordValid)
                {
                    // ✅ FIX: Show error instead of silently falling through to user check
                    ModelState.AddModelError("", "❌ Incorrect password. Please try again.");
                    return View(model);
                }

                var adminClaims = new List<Claim>
                {
                    new Claim(ClaimTypes.NameIdentifier, admin.AdminId.ToString()),
                    new Claim(ClaimTypes.Name,           admin.Username),
                    new Claim(ClaimTypes.Email,          admin.Email),
                    new Claim(ClaimTypes.Role,           "Admin")
                };

                var adminIdentity = new ClaimsIdentity(adminClaims, CookieAuthenticationDefaults.AuthenticationScheme);
                var adminPrincipal = new ClaimsPrincipal(adminIdentity);

                await HttpContext.SignInAsync(
                    CookieAuthenticationDefaults.AuthenticationScheme,
                    adminPrincipal,
                    new AuthenticationProperties
                    {
                        IsPersistent = model.RememberMe,
                        ExpiresUtc = model.RememberMe ? DateTime.UtcNow.AddDays(30) : DateTime.UtcNow.AddHours(1)
                    });

                TempData["SuccessMessage"] = $"Welcome back, {admin.Username}!";
                return RedirectToAction("Index", "Admin");
            }

            // ── Check regular user ──
            User user = null;
            if (model.EmailOrUsername.Contains("@"))
                user = await _userRepository.GetUserByEmailAsync(model.EmailOrUsername);
            else
                user = await _userRepository.GetUserByUsernameAsync(model.EmailOrUsername);

            if (user == null)
            {
                // ✅ FIX: Friendly not-found message
                ModelState.AddModelError("", "❌ No account found with that email or username.");
                return View(model);
            }

            bool isPasswordValid = _passwordService.VerifyPassword(
                model.Password, user.PasswordHash, user.PasswordSalt);

            if (!isPasswordValid)
            {
                // ✅ FIX: Specific wrong password message
                ModelState.AddModelError("", "❌ Incorrect password. Please try again.");
                return View(model);
            }

            // Read auth flags directly from Users table to avoid login being blocked
            // when stored procedures don't include these fields in their SELECT.
            var (isEmailVerified, isApprovedByAdmin) = await _userRepository.GetUserAuthFlagsAsync(user.UserId);

            if (!isEmailVerified)
            {
                ModelState.AddModelError("", "⚠️ Your email has not been verified. Please complete the OTP verification step.");
                return View(model);
            }

            if (!isApprovedByAdmin)
            {
                ModelState.AddModelError("", "⏳ Your account is pending admin approval. You will receive an email once your account is approved.");
                return View(model);
            }

            var userClaims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.UserId.ToString()),
                new Claim(ClaimTypes.Name,           user.Username),
                new Claim(ClaimTypes.Email,          user.Email),
                new Claim(ClaimTypes.Role,           "User")
            };

            var userIdentity = new ClaimsIdentity(userClaims, CookieAuthenticationDefaults.AuthenticationScheme);
            var userPrincipal = new ClaimsPrincipal(userIdentity);

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                userPrincipal,
                new AuthenticationProperties
                {
                    IsPersistent = model.RememberMe,
                    ExpiresUtc = model.RememberMe ? DateTime.UtcNow.AddDays(30) : DateTime.UtcNow.AddHours(1)
                });

            var pendingCount = await _userRepository.GetUserPendingHelpRequestCountAsync(user.UserId);
            var welcomeMsg = $"Welcome back, {user.Username}!";
            if (pendingCount > 0)
                welcomeMsg += $" You have {pendingCount} pending help request(s). Check My Requests.";
            TempData["SuccessMessage"] = welcomeMsg;
            return RedirectToAction("Index", "User");
        }

        [HttpPost]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            TempData["SuccessMessage"] = "You have been logged out successfully.";
            return RedirectToAction("Index", "Home");
        }

        // ═══════════════════════════════════════════════════
        // ✅ CHANGE PASSWORD (for logged-in users & admins)
        //    Route: /Account/ChangePassword
        //    The navbar "Change Password" button links here
        // ═══════════════════════════════════════════════════

        [HttpGet]
        [Authorize]
        public IActionResult ChangePassword()
        {
            return View();
        }

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangePassword(ChangePasswordViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var role = User.FindFirst(ClaimTypes.Role)?.Value;
            var idStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (string.IsNullOrEmpty(idStr))
            {
                ModelState.AddModelError("", "Unable to identify your account. Please log in again.");
                return View(model);
            }

            if (role == "Admin")
            {
                // ── Admin changing their own password ──
                var admin = await _userRepository.GetAdminByUsernameAsync(User.Identity.Name);
                if (admin == null)
                {
                    ModelState.AddModelError("", "Admin account not found.");
                    return View(model);
                }

                bool currentValid = _passwordService.VerifyPassword(
                    model.CurrentPassword, admin.PasswordHash, admin.PasswordSalt);

                if (!currentValid)
                {
                    ModelState.AddModelError(nameof(model.CurrentPassword), "❌ Current password is incorrect.");
                    return View(model);
                }

                var (hash, salt) = _passwordService.HashPassword(model.NewPassword);
                // Empty string token = direct change, not a reset-link flow
                await _userRepository.ResetAdminPasswordAsync(admin.AdminId, hash, salt, "");

                TempData["SuccessMessage"] = "✅ Password changed successfully!";
                return RedirectToAction("Index", "Admin");
            }
            else
            {
                // ── Regular user changing their own password ──
                int userId = int.Parse(idStr);
                var user = await _userRepository.GetUserByIdAsync(userId);

                if (user == null)
                {
                    ModelState.AddModelError("", "User account not found.");
                    return View(model);
                }

                bool currentValid = _passwordService.VerifyPassword(
                    model.CurrentPassword, user.PasswordHash, user.PasswordSalt);

                if (!currentValid)
                {
                    ModelState.AddModelError(nameof(model.CurrentPassword), "❌ Current password is incorrect.");
                    return View(model);
                }

                var (hash, salt) = _passwordService.HashPassword(model.NewPassword);
                // Empty string token = direct change, not a reset-link flow
                await _userRepository.ResetPasswordAsync(userId, hash, salt, "");

                TempData["SuccessMessage"] = "✅ Password changed successfully!";
                return RedirectToAction("Index", "User");
            }
        }

        // ═══════════════════════════════════════════════════
        // FORGOT PASSWORD
        // ═══════════════════════════════════════════════════

        [HttpGet]
        public IActionResult ForgotPassword()
        {
            return View(new ForgotPasswordViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ForgotPassword(ForgotPasswordViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            Console.WriteLine($"🔍 Forgot Password request for: {model.Email}");

            var user = await _userRepository.GetUserByEmailAsync(model.Email);
            if (user != null)
            {
                var token = Guid.NewGuid().ToString("N");
                var expiresAt = DateTime.UtcNow.AddHours(24);
                var stored = await _userRepository.StorePasswordResetTokenAsync(user.UserId, token, expiresAt);
                if (stored)
                {
                    var resetLink = Url.Action("ResetPassword", "Account",
                        new { token = token },
                        protocol: Request.Scheme, host: Request.Host.Value);
                    Console.WriteLine($"🔗 USER reset link: {resetLink}");
                    await _emailService.SendPasswordResetEmailAsync(user.Email, resetLink);
                }
            }

            var admin = await _userRepository.GetAdminByEmailAsync(model.Email);
            if (admin != null)
            {
                var token = Guid.NewGuid().ToString("N");
                var expiresAt = DateTime.UtcNow.AddHours(24);
                var stored = await _userRepository.StoreAdminPasswordResetTokenAsync(admin.AdminId, token, expiresAt);
                if (stored)
                {
                    var resetLink = Url.Action("ResetAdminPassword", "Account",
                        new { token = token },
                        protocol: Request.Scheme, host: Request.Host.Value);
                    Console.WriteLine($"🔗 ADMIN reset link: {resetLink}");
                    await _emailService.SendPasswordResetEmailAsync(admin.Email, resetLink);
                }
            }

            ViewBag.Message = "If the email exists in our system, a reset link has been sent. Please check your inbox.";
            return View(new ForgotPasswordViewModel());
        }

        // ✅ FIXED: ViewBag.IsAdminReset = false → shared view posts to ResetPassword
        [HttpGet]
        public async Task<IActionResult> ResetPassword(string token)
        {
            Console.WriteLine($"🔍 ResetPassword GET - Token: {token ?? "NULL"}");

            if (string.IsNullOrEmpty(token))
            {
                TempData["ErrorMessage"] = "Invalid password reset link.";
                return RedirectToAction("Login");
            }

            var tokenData = await _userRepository.VerifyPasswordResetTokenAsync(token);
            if (tokenData == null)
            {
                TempData["ErrorMessage"] = "Invalid or expired password reset link. Please request a new one.";
                return RedirectToAction("ForgotPassword");
            }

            Console.WriteLine($"✅ User token valid - UserId: {tokenData.UserId}");
            ViewBag.IsAdminReset = false;
            return View(new ResetPasswordViewModel { Token = token });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model)
        {
            Console.WriteLine($"🔍 ResetPassword POST - Token: {model.Token}");

            if (!ModelState.IsValid)
            {
                ViewBag.IsAdminReset = false;
                return View(model);
            }

            var tokenData = await _userRepository.VerifyPasswordResetTokenAsync(model.Token);
            if (tokenData == null)
            {
                ModelState.AddModelError("", "Invalid or expired password reset link. Please request a new one.");
                ViewBag.IsAdminReset = false;
                return View(model);
            }

            var (hash, salt) = _passwordService.HashPassword(model.NewPassword);
            var success = await _userRepository.ResetPasswordAsync(tokenData.UserId, hash, salt, model.Token);

            if (success)
            {
                TempData["SuccessMessage"] = "✅ Password reset successfully! You can now log in.";
                return RedirectToAction("Login");
            }

            ModelState.AddModelError("", "Failed to reset password. Please try again.");
            ViewBag.IsAdminReset = false;
            return View(model);
        }

        // ✅ FIXED: ViewBag.IsAdminReset = true → shared view posts to ResetAdminPassword
        [HttpGet]
        public async Task<IActionResult> ResetAdminPassword(string token)
        {
            Console.WriteLine($"🔍 ResetAdminPassword GET - Token: {token ?? "NULL"}");

            if (string.IsNullOrEmpty(token))
            {
                TempData["ErrorMessage"] = "Invalid password reset link.";
                return RedirectToAction("Login");
            }

            var tokenData = await _userRepository.VerifyAdminPasswordResetTokenAsync(token);
            if (tokenData == null)
            {
                TempData["ErrorMessage"] = "Invalid or expired password reset link. Please request a new one.";
                return RedirectToAction("ForgotPassword");
            }

            Console.WriteLine($"✅ Admin token valid - AdminId: {tokenData.AdminId}");
            ViewBag.IsAdminReset = true;
            return View("ResetPassword", new ResetPasswordViewModel { Token = token });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetAdminPassword(ResetPasswordViewModel model)
        {
            Console.WriteLine($"🔍 ResetAdminPassword POST - Token: {model.Token}");

            if (!ModelState.IsValid)
            {
                ViewBag.IsAdminReset = true;
                return View("ResetPassword", model);
            }

            var tokenData = await _userRepository.VerifyAdminPasswordResetTokenAsync(model.Token);
            if (tokenData == null)
            {
                ModelState.AddModelError("", "Invalid or expired password reset link. Please request a new one.");
                ViewBag.IsAdminReset = true;
                return View("ResetPassword", model);
            }

            var (hash, salt) = _passwordService.HashPassword(model.NewPassword);
            var success = await _userRepository.ResetAdminPasswordAsync(tokenData.AdminId, hash, salt, model.Token);

            if (success)
            {
                TempData["SuccessMessage"] = "✅ Admin password reset successfully! You can now log in.";
                return RedirectToAction("Login");
            }

            ModelState.AddModelError("", "Failed to reset password. Please try again.");
            ViewBag.IsAdminReset = true;
            return View("ResetPassword", model);
        }

        // ═══════════════════════════════════════════════════
        // HELPERS
        // ═══════════════════════════════════════════════════

        private string GenerateOTP()
        {
            var random = new Random();
            return random.Next(100000, 1000000).ToString("D6");
        }

        private async Task<string> SaveFileAsync(IFormFile file, string folderName)
        {
            string uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", folderName);
            if (!Directory.Exists(uploadsFolder))
                Directory.CreateDirectory(uploadsFolder);

            string uniqueFileName = Guid.NewGuid().ToString() + "_" + file.FileName;
            string filePath = Path.Combine(uploadsFolder, uniqueFileName);

            using (var fileStream = new FileStream(filePath, FileMode.Create))
                await file.CopyToAsync(fileStream);

            return $"/{folderName}/{uniqueFileName}";


            
        }
    }
}