using MailKit.Net.Smtp;
using MimeKit;
using Microsoft.Extensions.Options;
using System.Threading.Tasks;

namespace DotnetMVCApp.Attributes
{
    // Email settings class (used with appsettings.json)
    public class EmailSettings
    {
        public string SmtpServer { get; set; }
        public int Port { get; set; }
        public string SenderName { get; set; }
        public string SenderEmail { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }
    }

    // Email service
    public class EmailService
    {
        private readonly EmailSettings _emailSettings;

        public EmailService(IOptions<EmailSettings> emailSettings)
        {
            _emailSettings = emailSettings.Value;
        }

        // Send a general email
        public async Task SendEmailAsync(string toEmail, string subject, string htmlBody)
        {
            var email = new MimeMessage();
            email.Sender = MailboxAddress.Parse(_emailSettings.SenderEmail);
            email.From.Add(new MailboxAddress(_emailSettings.SenderName, _emailSettings.SenderEmail));
            email.To.Add(MailboxAddress.Parse(toEmail));
            email.Subject = subject;

            var builder = new BodyBuilder
            {
                HtmlBody = htmlBody
            };
            email.Body = builder.ToMessageBody();

            using var smtp = new SmtpClient();
            await smtp.ConnectAsync(_emailSettings.SmtpServer, _emailSettings.Port, MailKit.Security.SecureSocketOptions.StartTls);
            await smtp.AuthenticateAsync(_emailSettings.Username, _emailSettings.Password);
            await smtp.SendAsync(email);
            await smtp.DisconnectAsync(true);
        }

        // Generate and send OTP email
        public async Task<string> SendOtpEmailAsync(string toEmail)
        {
            var otp = new Random().Next(100000, 999999).ToString(); // 6-digit OTP
            var subject = "Your OTP Code";
            var body = $"<p>Your OTP code is: <strong>{otp}</strong></p><p>This code will expire in 10 minutes.</p>";

            await SendEmailAsync(toEmail, subject, body);

            return otp; // return OTP for verification logic
        }
    }
}
