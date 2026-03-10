using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
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

        public AccountController(
            IUserRepository userRepository,
            IPasswordService passwordService,
            IEmailService emailService)
        {
            _userRepository = userRepository;
            _passwordService = passwordService;
            _emailService = emailService;
        }

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

            TempData["SuccessMessage"] = "✅ Basic registration successful! Now add your skills and upload your CV.";
            return RedirectToAction("AddSkills");
        }

        // ═══════════════════════════════════════════════════
        // ADD SKILLS
        // ═══════════════════════════════════════════════════

        [HttpGet]
        public async Task<IActionResult> AddSkills()
        {
            // Allow logged-in users to add skills to their profile
            if (User.Identity?.IsAuthenticated == true)
            {
                var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (!string.IsNullOrEmpty(userIdClaim))
                {
                    var user = await _userRepository.GetUserByIdAsync(int.Parse(userIdClaim));
                    if (user != null)
                    {
                        ViewBag.Username = user.Username;
                        ViewBag.Email = user.Email;
                        ViewBag.IsLoggedInUser = true;
                        ViewBag.UserId = user.UserId;
                        return View();
                    }
                }
            }

            var username = HttpContext.Session.GetString("Username");
            var email = HttpContext.Session.GetString("Email");

            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(email))
                return RedirectToAction("Register");

            ViewBag.Username = username;
            ViewBag.Email = email;
            ViewBag.IsLoggedInUser = false;
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

        // Logged-in users: load skills directly from database
        [HttpGet]
        [Authorize(Roles = "User")]
        public async Task<IActionResult> GetCurrentUserSkills()
        {
            var idStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(idStr))
                return Unauthorized();

            int userId = int.Parse(idStr);
            var skills = await _userRepository.GetUserSkillsDisplayAsync(userId);
            return Json(skills);
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
                { "AvailableTimeEnd",   model.AvailableTimeEnd ?? "" }
            };

            skills.Add(tempSkill);
            HttpContext.Session.SetString("UserSkills", JsonConvert.SerializeObject(skills));
            return Json(new { success = true, message = "Skill added successfully", skill = tempSkill });
        }

        // Logged-in users: write skill directly to UserSkills table (no OTP, no registration session)
        [HttpPost]
        [Authorize(Roles = "User")]
        public async Task<IActionResult> AddUserSkillForLoggedIn([FromBody] AddSkillViewModel model)
        {
            if (model == null)
                return Json(new { success = false, message = "Invalid data received" });

            var idStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(idStr))
                return Json(new { success = false, message = "User is not logged in." });

            int userId = int.Parse(idStr);

            TimeSpan? startTime = null;
            TimeSpan? endTime = null;

            if (!string.IsNullOrEmpty(model.AvailableTimeStart) &&
                TimeSpan.TryParse(model.AvailableTimeStart, out var parsedStart))
                startTime = parsedStart;

            if (!string.IsNullOrEmpty(model.AvailableTimeEnd) &&
                TimeSpan.TryParse(model.AvailableTimeEnd, out var parsedEnd))
                endTime = parsedEnd;

            var (result, message) = await _userRepository.AddUserSkillAsync(
                userId,
                model.FieldId,
                model.SkillId,
                model.SubSkillId,
                model.ExperienceLevel,
                model.AvailableDays,
                startTime,
                endTime);

            if (result > 0)
            {
                // Let the database/stored procedure decide how to notify or log this change.
                await _userRepository.LogUserSkillChangeAsync(
                    userId,
                    "ADD_OR_UPDATE_SKILL",
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

            TimeSpan? startTime = null;
            TimeSpan? endTime = null;

            if (!string.IsNullOrEmpty(model.AvailableTimeStart) &&
                TimeSpan.TryParse(model.AvailableTimeStart, out var parsedStart))
                startTime = parsedStart;

            if (!string.IsNullOrEmpty(model.AvailableTimeEnd) &&
                TimeSpan.TryParse(model.AvailableTimeEnd, out var parsedEnd))
                endTime = parsedEnd;

            var (result, message) = await _userRepository.UpdateUserSkillAsync(
                model.UserSkillId.Value,
                userId,
                model.FieldId,
                model.SkillId,
                model.SubSkillId,
                model.ExperienceLevel,
                model.AvailableDays,
                startTime,
                endTime);

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

            string cvPath = null;
            if (cv != null && cv.Length > 0)
            {
                cvPath = await SaveFileAsync(cv, "uploads");
            }

            var (result, message) = await _userRepository.UpdateUserDocumentsAsync(
                userId,
                cvPath,
                portfolioUrl ?? string.Empty);

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
        public async Task<IActionResult> UploadCVAndProceed(IFormFile cv, string portfolioUrl)
        {
            var username = HttpContext.Session.GetString("Username");
            var email = HttpContext.Session.GetString("Email");
            var skillsJson = HttpContext.Session.GetString("UserSkills") ?? "[]";
            var skills = JsonConvert.DeserializeObject<List<Dictionary<string, object>>>(skillsJson);

            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(email))
                return Json(new { success = false, message = "Session expired. Please start registration again." });

            if (skills.Count == 0)
                return Json(new { success = false, message = "Please add at least one skill before proceeding" });

            if (cv == null || cv.Length == 0)
                return Json(new { success = false, message = "Please upload your CV" });

            string cvPath = await SaveFileAsync(cv, "uploads");
            HttpContext.Session.SetString("CVPath", cvPath);
            HttpContext.Session.SetString("PortfolioUrl", portfolioUrl ?? "");

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
            var cvPath = HttpContext.Session.GetString("CVPath");
            var portfolioUrl = HttpContext.Session.GetString("PortfolioUrl");
            var skillsJson = HttpContext.Session.GetString("UserSkills") ?? "[]";
            var skills = JsonConvert.DeserializeObject<List<Dictionary<string, object>>>(skillsJson);

            var user = new User
            {
                Username = username,
                Email = email,
                PasswordHash = passwordHash,
                PasswordSalt = passwordSalt,
                PhoneNumber = phoneNumber,
                CVPath = cvPath,
                PortfolioUrl = portfolioUrl
            };

            var (result, message) = await _userRepository.RegisterUserAsync(user);

            if (result > 0)
            {
                Console.WriteLine($"✅ User created with UserId: {result}");
                await _userRepository.MarkOTPAsUsedAsync(model.Email, model.OTPCode);

                foreach (var skill in skills)
                {
                    TimeSpan? startTime = null;
                    TimeSpan? endTime = null;

                    var startStr = skill.ContainsKey("AvailableTimeStart") ? skill["AvailableTimeStart"]?.ToString() : null;
                    var endStr = skill.ContainsKey("AvailableTimeEnd") ? skill["AvailableTimeEnd"]?.ToString() : null;

                    if (!string.IsNullOrEmpty(startStr) && TimeSpan.TryParse(startStr, out var ps)) startTime = ps;
                    if (!string.IsNullOrEmpty(endStr) && TimeSpan.TryParse(endStr, out var pe)) endTime = pe;

                    await _userRepository.AddUserSkillAsync(
                        result,
                        Convert.ToInt32(skill["FieldId"]),
                        Convert.ToInt32(skill["SkillId"]),
                        Convert.ToInt32(skill["SubSkillId"]),
                        Convert.ToInt32(skill["ExperienceLevel"]),
                        skill["AvailableDays"].ToString(),
                        startTime,
                        endTime
                    );
                }

                Console.WriteLine("✅ All skills added successfully");
                HttpContext.Session.Clear();
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