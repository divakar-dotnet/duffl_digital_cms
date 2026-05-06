using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace DufflDigital.Web.Services
{
    public interface IEmailService
    {
        Task SendOtpEmailAsync(string toEmail, string otp);
    }

    public class EmailService : IEmailService
    {
        private readonly IConfiguration _config;

        public EmailService(IConfiguration config)
        {
            _config = config;
        }

        public async Task SendOtpEmailAsync(string toEmail, string otp)
        {
            var fromEmail = _config["Email:From"];
            var fromName = _config["Email:DisplayName"];
            var smtpHost = _config["Email:SmtpHost"];
            var smtpPort = int.Parse(_config["Email:SmtpPort"] ?? "587");
            var username = _config["Email:Username"];
            var password = (_config["Email:Password"] ?? "").Replace(" ", "");

            // Build the email message using MimeKit
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(fromName, fromEmail));
            message.To.Add(new MailboxAddress("", toEmail));
            message.Subject = "Your Duffl Admin Password Reset OTP";

            message.Body = new TextPart("html")
            {
                Text = $@"
                    <div style='font-family:sans-serif; max-width:480px; margin:auto;'>
                        <h2 style='color:#FFB300;'>Duffl Digital</h2>
                        <p>You requested a password reset. Use the OTP below:</p>
                        <div style='font-size:36px; font-weight:bold; letter-spacing:10px;
                                    color:#212529; background:#f8f9fa; padding:24px;
                                    text-align:center; border-radius:8px; margin:20px 0;'>
                            {otp}
                        </div>
                        <p style='color:#888;'>
                            This OTP expires in <strong>10 minutes</strong>.<br/>
                            If you did not request this, please ignore this email.
                        </p>
                        <hr style='border:none; border-top:1px solid #eee;'/>
                        <p style='color:#ccc; font-size:12px;'>Duffl Digital Admin System</p>
                    </div>"
            };

            // MailKit SMTP client — works reliably with Gmail App Passwords
            using var smtp = new SmtpClient();

            // Connect using STARTTLS on port 587
            await smtp.ConnectAsync(smtpHost, smtpPort, SecureSocketOptions.StartTls);

            // Authenticate with Gmail App Password
            await smtp.AuthenticateAsync(username, password);

            await smtp.SendAsync(message);
            await smtp.DisconnectAsync(true);
        }
    }
}