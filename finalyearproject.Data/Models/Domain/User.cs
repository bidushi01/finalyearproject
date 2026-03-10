namespace finalyearproject.Data.Models.Domain
{
    public class User
    {
        public int UserId { get; set; }
        public string Username { get; set; }
        public string Email { get; set; }
        public string PasswordHash { get; set; }
        public string PasswordSalt { get; set; }
        public string PhoneNumber { get; set; }
        public bool IsEmailVerified { get; set; }
        public bool IsApprovedByAdmin { get; set; }
        public bool IsRejected { get; set; }
        public string CVPath { get; set; }
        public string PortfolioUrl { get; set; }
        public DateTime CreatedAt { get; set; }
        public int RegistrationStep { get; set; }  // ⬅️ ADD THIS 
        
        // Returned by sp_GetAllUsers (ONE row per user)
        public int SkillCount { get; set; }
        public string? SkillsJson { get; set; }
        
    }
}
