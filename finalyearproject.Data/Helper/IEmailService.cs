namespace finalyearproject.Data.Helper
{
    public interface IEmailService
    {
        Task<bool> SendOTPEmailAsync(string toEmail, string otpCode);
        Task<bool> SendApprovalEmailAsync(string toEmail, string username, bool isApproved);
    }
}