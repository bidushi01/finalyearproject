using System.Net;
using System.Net.Mail;
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
                Body = $"Your OTP code is: {otpCode}\n\nThis code expires in 10 minutes.",
                IsBodyHtml = false
            };

            mailMessage.To.Add(toEmail);
            await smtpClient.SendMailAsync(mailMessage);
            smtpClient.Dispose();
            return true;
        }

        public async Task<bool> SendApprovalEmailAsync(string toEmail, string username)
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
                Subject = "Account Approved",
                Body = $"Hello {username}, your account has been approved. You can now log in.",
                IsBodyHtml = false
            };

            mailMessage.To.Add(toEmail);
            await smtpClient.SendMailAsync(mailMessage);
            smtpClient.Dispose();
            return true;
        }

        public async Task<bool> SendRejectionEmailAsync(string toEmail, string username, string reason)
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
                Subject = "Account Registration Status",
                Body = $"Hello {username}, your account registration could not be approved.\n\nReason: {reason}",
                IsBodyHtml = false
            };

            mailMessage.To.Add(toEmail);
            await smtpClient.SendMailAsync(mailMessage);
            smtpClient.Dispose();
            return true;
        }

        public async Task<bool> SendSkillApprovedEmailAsync(string toEmail, string username, string skillSummary)
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
                Subject = "Your skill was approved",
                Body = $"Hello {username},\n\nThe skill you added is now approved by the admin:\n{skillSummary}\n\nYou are now eligible to receive help requests for this skill on PeerAssist.",
                IsBodyHtml = false
            };

            mailMessage.To.Add(toEmail);
            await smtpClient.SendMailAsync(mailMessage);
            smtpClient.Dispose();
            return true;
        }

        public async Task<bool> SendSkillRejectedEmailAsync(string toEmail, string username, string skillSummary, string reason)
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
                Subject = "Your skill was not approved",
                Body = $"Hello {username},\n\nThe skill you submitted was reviewed and not approved:\n{skillSummary}\n\nReason: {reason}\n\nYou may update your CV and submit again from your profile.",
                IsBodyHtml = false
            };

            mailMessage.To.Add(toEmail);
            await smtpClient.SendMailAsync(mailMessage);
            smtpClient.Dispose();
            return true;
        }

        public async Task<bool> SendPasswordResetEmailAsync(string toEmail, string resetLink)
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
                Subject = "Password Reset",
                Body = $"Click the link below to reset your password:\n\n{resetLink}\n\nThis link expires in 1 hour.",
                IsBodyHtml = false
            };

            mailMessage.To.Add(toEmail);
            await smtpClient.SendMailAsync(mailMessage);
            smtpClient.Dispose();
            return true;
        }
    }
}