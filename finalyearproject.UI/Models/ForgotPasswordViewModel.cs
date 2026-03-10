using System.ComponentModel.DataAnnotations;

namespace finalyearproject.UI.Models
{
    public class ForgotPasswordViewModel
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; }
    }
}

