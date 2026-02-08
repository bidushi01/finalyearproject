namespace finalyearproject.Data.Models.Domain
{
    public class PasswordResetToken
    {
        public int TokenId { get; set; }
        public int UserId { get; set; }
        public string Token { get; set; }
        public DateTime ExpiresAt { get; set; }
        public bool IsUsed { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
