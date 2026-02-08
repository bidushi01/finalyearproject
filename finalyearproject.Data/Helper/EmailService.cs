using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using finalyearproject.Data.Models.Domain;

namespace finalyearproject.Data.Helper
{
    public class EmailService : IEmailService
    {
        private readonly EmailSettings _emailSettings;

        public EmailService(IOptions<EmailSettings> settings)
        {
            _emailSettings = settings.Value;
        }

        public async Task<bool> SendOTPEmailAsync(string toEmail, string otpCode)
        {
            var smtpClient = new SmtpClient(_emailSettings.SmtpServer)
            {
                Port = _emailSettings.Port,
                Credentials = new NetworkCredential(_emailSettings.Username, _emailSettings.Password),
                EnableSsl = true
            };

            var mailMessage = new MailMessage
            {
                From = new MailAddress(_emailSettings.From, "PeerHelp Platform"),
                Subject = "Email Verification - OTP Code",
                Body = $@"
                    <html>
                    <body style='font-family: Arial, sans-serif;'>
                        <div style='max-width: 600px; margin: 0 auto; padding: 20px; border: 1px solid #ddd;'>
                            <h2 style='color: #17a2b8;'>Email Verification</h2>
                            <p>Your OTP code is:</p>
                            <div style='background: #f8f9fa; padding: 15px; text-align: center; font-size: 24px; font-weight: bold; color: #17a2b8;'>
                                {otpCode}
                            </div>
                            <p><strong>This code will expire in 10 minutes.</strong></p>
                        </div>
                    </body>
                    </html>
                ",
                IsBodyHtml = true
            };

            mailMessage.To.Add(toEmail);
            await smtpClient.SendMailAsync(mailMessage);
            smtpClient.Dispose();
            return true;
        }

        public async Task<bool> SendApprovalEmailAsync(string toEmail, string username, bool isApproved)
        {
            var smtpClient = new SmtpClient(_emailSettings.SmtpServer)
            {
                Port = _emailSettings.Port,
                Credentials = new NetworkCredential(_emailSettings.Username, _emailSettings.Password),
                EnableSsl = true
            };

            string subject = isApproved ? "Account Approved!" : "Account Registration Update";
            string body = isApproved
                ? $@"
                    <html>
                    <body style='font-family: Arial, sans-serif;'>
                        <div style='max-width: 600px; margin: 0 auto; padding: 20px; border: 1px solid #28a745;'>
                            <h2 style='color: #28a745;'>Account Approved!</h2>
                            <p>Hello <strong>{username}</strong>,</p>
                            <p>Your account has been approved. You can now log in.</p>
                        </div>
                    </body>
                    </html>
                "
                : $@"
                    <html>
                    <body style='font-family: Arial, sans-serif;'>
                        <div style='max-width: 600px; margin: 0 auto; padding: 20px; border: 1px solid #dc3545;'>
                            <h2 style='color: #dc3545;'>Account Registration Update</h2>
                            <p>Hello <strong>{username}</strong>,</p>
                            <p>Your account could not be approved at this time.</p>
                        </div>
                    </body>
                    </html>
                ";

            var mailMessage = new MailMessage
            {
                From = new MailAddress(_emailSettings.From, "PeerHelp Platform"),
                Subject = subject,
                Body = body,
                IsBodyHtml = true
            };

            mailMessage.To.Add(toEmail);
            await smtpClient.SendMailAsync(mailMessage);
            smtpClient.Dispose();
            return true;
        }
    }
}