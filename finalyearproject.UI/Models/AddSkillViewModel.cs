using System.ComponentModel.DataAnnotations;

namespace finalyearproject.UI.Models
{
    // Step 2: Add Skills
    public class AddSkillViewModel
    {
        public int UserId { get; set; }

        // Used only when editing an existing skill for a logged-in user.
        public int? UserSkillId { get; set; }

        [Required(ErrorMessage = "Field is required")]
        public int FieldId { get; set; }

        [Required(ErrorMessage = "Skill is required")]
        public int SkillId { get; set; }

        [Required(ErrorMessage = "Sub-Skill is required")]
        public int SubSkillId { get; set; }

        [Required(ErrorMessage = "Experience level is required")]
        [Range(1, 4, ErrorMessage = "Please select a valid experience level")]
        public int ExperienceLevel { get; set; }

        [Required(ErrorMessage = "Available days are required")]
        public string AvailableDays { get; set; }

        [Required(ErrorMessage = "Start time is required")]
        public String AvailableTimeStart { get; set; }

        [Required(ErrorMessage = "End time is required")]
        public String AvailableTimeEnd { get; set; }
    }
}