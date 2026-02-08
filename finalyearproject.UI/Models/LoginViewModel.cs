using System.ComponentModel.DataAnnotations;

namespace finalyearproject.UI.Models
{
    public class LoginViewModel
    {
        [Required(ErrorMessage = "Email or Username is required")]
        [Display(Name = "Email / Username")]
        public string EmailOrUsername { get; set; }

        [Required(ErrorMessage = "Password is required")]
        [DataType(DataType.Password)]
        public string Password { get; set; }

        [Display(Name = "Remember me")]
        public bool RememberMe { get; set; }
    }
}
