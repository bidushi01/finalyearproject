//namespace finalyearproject.Data.Helper
//{
//    public interface IEmailService
//    {
//        Task<bool> SendOTPEmailAsync(string toEmail, string otpCode);
//        Task<bool> SendApprovalEmailAsync(string toEmail, string username, bool isApproved);
//        Task<bool> SendPasswordResetEmailAsync(string toEmail, string resetLink);


//    }
//}

namespace finalyearproject.Data.Helper
{
    public interface IEmailService
    {
        Task<bool> SendOTPEmailAsync(string toEmail, string otpCode);
        Task<bool> SendApprovalEmailAsync(string toEmail, string username);
        Task<bool> SendRejectionEmailAsync(string toEmail, string username, string reason);
        Task<bool> SendPasswordResetEmailAsync(string toEmail, string resetLink);
    }
}