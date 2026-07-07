using System.ComponentModel.DataAnnotations;

namespace finalyearproject.UI.Models
{
    public class VerifyOTPViewModel
    {
        [Required]
        public string Email { get; set; }

        [Required(ErrorMessage = "OTP code is required")]
        [RegularExpression(@"^\d{6}$", ErrorMessage = "OTP must be exactly 6 digits")]
        [Display(Name = "Enter OTP Code")]
        public string OTPCode { get; set; }

        
    }
}