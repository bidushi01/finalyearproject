using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using System.Security.Claims;
using finalyearproject.UI.Models;
using finalyearproject.Data.Repository;
using finalyearproject.Data.Services;
using finalyearproject.Data.Helper;
using finalyearproject.Data.Models.Domain;

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

        // =============================
        // REGISTER - GET
        // =============================
        [HttpGet]
        public IActionResult Register()
        {
            return View();
        }

        // =============================
        // REGISTER - POST
        // =============================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(RegisterViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            try
            {
                // Upload CV
                string cvPath = await SaveFileAsync(model.CV, "uploads");

                // Hash password
                var (hash, salt) = _passwordService.HashPassword(model.Password);

                // Create user object
                var user = new User
                {
                    Username = model.Username,
                    Email = model.Email,
                    PasswordHash = hash,
                    PasswordSalt = salt,
                    PhoneNumber = model.PhoneNumber,
                    CVPath = cvPath,
                    PortfolioUrl = model.PortfolioUrl
                };

                // Register user
                var (result, message) = await _userRepository.RegisterUserAsync(user);

                if (result > 0)
                {
                    // Generate OTP
                    string otpCode = GenerateOTP();
                    DateTime expiresAt = DateTime.Now.AddMinutes(10);

                    // Store OTP
                    await _userRepository.StoreOTPAsync(model.Email, otpCode, expiresAt);

                    // Send OTP email
                    await _emailService.SendOTPEmailAsync(model.Email, otpCode);

                    // Redirect to OTP verification
                    TempData["Email"] = model.Email;
                    TempData["SuccessMessage"] = "Registration successful! Please check your email for OTP.";
                    return RedirectToAction("VerifyOTP");
                }
                else
                {
                    ModelState.AddModelError("", message);
                    return View(model);
                }
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", $"Error: {ex.Message}");
                return View(model);
            }
        }

        // =============================
        // VERIFY OTP - GET
        // =============================
        [HttpGet]
        public IActionResult VerifyOTP()
        {
            var model = new VerifyOTPViewModel
            {
                Email = TempData["Email"]?.ToString()
            };

            if (string.IsNullOrEmpty(model.Email))
                return RedirectToAction("Register");

            return View(model);
        }

        // =============================
        // VERIFY OTP - POST
        // =============================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> VerifyOTP(VerifyOTPViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            try
            {
                bool isValid = await _userRepository.VerifyOTPAsync(model.Email, model.OTPCode);

                if (isValid)
                {
                    TempData["SuccessMessage"] = "✅ Email verified! Your account is pending admin approval. You will receive an email once approved.";
                    return RedirectToAction("Login");
                }
                else
                {
                    ModelState.AddModelError("", "Invalid or expired OTP code.");
                    return View(model);
                }
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", $"Error: {ex.Message}");
                return View(model);
            }
        }

        // =============================
        // RESEND OTP
        // =============================
        [HttpPost]
        public async Task<IActionResult> ResendOTP(string email)
        {
            try
            {
                string otpCode = GenerateOTP();
                DateTime expiresAt = DateTime.Now.AddMinutes(10);

                await _userRepository.StoreOTPAsync(email, otpCode, expiresAt);
                await _emailService.SendOTPEmailAsync(email, otpCode);

                return Json(new { success = true, message = "OTP resent successfully!" });
            }
            catch
            {
                return Json(new { success = false, message = "Failed to resend OTP." });
            }
        }

        // =============================
        // LOGIN - GET
        // =============================
        [HttpGet]
        public IActionResult Login()
        {
            return View();
        }

        // =============================
        // LOGIN - POST
        // =============================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            try
            {
                // Check if it's admin login first
                var admin = await _userRepository.GetAdminByUsernameAsync(model.EmailOrUsername);

                if (admin != null)
                {
                    // Verify admin password
                    bool isAdminPasswordValid = _passwordService.VerifyPassword(
                        model.Password,
                        admin.PasswordHash,
                        admin.PasswordSalt
                    );

                    if (isAdminPasswordValid)
                    {
                        // Create admin claims
                        var adminClaims = new List<Claim>
                        {
                            new Claim(ClaimTypes.NameIdentifier, admin.AdminId.ToString()),
                            new Claim(ClaimTypes.Name, admin.Username),
                            new Claim(ClaimTypes.Email, admin.Email),
                            new Claim(ClaimTypes.Role, "Admin")
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
                            }
                        );

                        TempData["SuccessMessage"] = $"Welcome back, {admin.Username}!";
                        return RedirectToAction("Index", "Admin");
                    }
                }

                // Try user login
                User user = null;

                // Check if input is email
                if (model.EmailOrUsername.Contains("@"))
                {
                    user = await _userRepository.GetUserByEmailAsync(model.EmailOrUsername);
                }
                else
                {
                    user = await _userRepository.GetUserByUsernameAsync(model.EmailOrUsername);
                }

                if (user == null)
                {
                    ModelState.AddModelError("", "Invalid email/username or password.");
                    return View(model);
                }

                // Verify user password
                bool isPasswordValid = _passwordService.VerifyPassword(
                    model.Password,
                    user.PasswordHash,
                    user.PasswordSalt
                );

                if (!isPasswordValid)
                {
                    ModelState.AddModelError("", "Invalid email/username or password.");
                    return View(model);
                }

                // Check email verification
                if (!user.IsEmailVerified)
                {
                    ModelState.AddModelError("", "Please verify your email first.");
                    return View(model);
                }

                // Check admin approval
                if (!user.IsApprovedByAdmin)
                {
                    ModelState.AddModelError("", "⏳ Your account is pending admin approval. You will receive an email once approved.");
                    return View(model);
                }

                // Create user claims
                var userClaims = new List<Claim>
                {
                    new Claim(ClaimTypes.NameIdentifier, user.UserId.ToString()),
                    new Claim(ClaimTypes.Name, user.Username),
                    new Claim(ClaimTypes.Email, user.Email),
                    new Claim(ClaimTypes.Role, "User")
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
                    }
                );

                TempData["SuccessMessage"] = $"Welcome back, {user.Username}!";
                return RedirectToAction("Index", "User");
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", $"Error: {ex.Message}");
                return View(model);
            }
        }

        // =============================
        // LOGOUT
        // =============================
        [HttpPost]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            TempData["SuccessMessage"] = "You have been logged out successfully.";
            return RedirectToAction("Index", "Home");
        }

        // =============================
        // HELPER METHODS
        // =============================
        private string GenerateOTP()
        {
            Random random = new Random();
            return random.Next(100000, 999999).ToString();
        }

        private async Task<string> SaveFileAsync(IFormFile file, string folderName)
        {
            string uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", folderName);

            if (!Directory.Exists(uploadsFolder))
                Directory.CreateDirectory(uploadsFolder);

            string uniqueFileName = Guid.NewGuid().ToString() + "_" + file.FileName;
            string filePath = Path.Combine(uploadsFolder, uniqueFileName);

            using (var fileStream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(fileStream);
            }

            return $"/{folderName}/{uniqueFileName}";
        }
    }
}