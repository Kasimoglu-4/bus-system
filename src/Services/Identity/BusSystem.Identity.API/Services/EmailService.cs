using System.Net;
using System.Net.Mail;

namespace BusSystem.Identity.API.Services;

public class EmailService : IEmailService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<EmailService> _logger;

    public EmailService(IConfiguration configuration, ILogger<EmailService> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<bool> SendPasswordResetEmailAsync(string toEmail, string userName, string resetLink)
    {
        try
        {
            var smtpHost = _configuration["Email:SmtpHost"];
            var smtpPort = int.Parse(_configuration["Email:SmtpPort"] ?? "587");
            var smtpUsername = _configuration["Email:SmtpUsername"];
            var smtpPassword = _configuration["Email:SmtpPassword"];
            var fromEmail = _configuration["Email:FromEmail"];
            var fromName = _configuration["Email:FromName"] ?? "Bus System";

            // If SMTP is not configured, log the reset link instead (for development)
            if (string.IsNullOrEmpty(smtpHost) || string.IsNullOrEmpty(smtpUsername))
            {
                _logger.LogWarning("SMTP not configured. Password reset link for {Email}: {ResetLink}", toEmail, resetLink);
                return true; // Return true for development purposes
            }

            using var smtpClient = new SmtpClient(smtpHost, smtpPort)
            {
                EnableSsl = true,
                Credentials = new NetworkCredential(smtpUsername, smtpPassword),
                Timeout = 10000
            };

            var mailMessage = new MailMessage
            {
                From = new MailAddress(fromEmail ?? smtpUsername, fromName),
                Subject = "Password Reset Request - Bus System",
                Body = GetPasswordResetEmailBody(userName, resetLink),
                IsBodyHtml = true
            };

            mailMessage.To.Add(toEmail);

            await smtpClient.SendMailAsync(mailMessage);
            
            _logger.LogInformation("Password reset email sent successfully to {Email}", toEmail);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send password reset email to {Email}", toEmail);
            return false;
        }
    }

    private string GetPasswordResetEmailBody(string userName, string resetLink)
    {
        return $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <style>
        body {{
            font-family: Arial, sans-serif;
            line-height: 1.6;
            color: #333;
            max-width: 600px;
            margin: 0 auto;
            padding: 20px;
        }}
        .container {{
            background-color: #f9f9f9;
            border-radius: 8px;
            padding: 30px;
            box-shadow: 0 2px 4px rgba(0,0,0,0.1);
        }}
        .header {{
            text-align: center;
            margin-bottom: 30px;
        }}
        .header h1 {{
            color: #2c3e50;
            margin: 0;
        }}
        .content {{
            background-color: white;
            padding: 25px;
            border-radius: 6px;
        }}
        .button {{
            display: inline-block;
            padding: 12px 30px;
            background-color: #3498db;
            color: white;
            text-decoration: none;
            border-radius: 5px;
            margin: 20px 0;
            font-weight: bold;
        }}
        .button:hover {{
            background-color: #2980b9;
        }}
        .warning {{
            background-color: #fff3cd;
            border-left: 4px solid #ffc107;
            padding: 12px;
            margin: 20px 0;
        }}
        .footer {{
            text-align: center;
            margin-top: 30px;
            font-size: 12px;
            color: #666;
        }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h1>🚌 Bus System</h1>
        </div>
        <div class='content'>
            <h2>Password Reset Request</h2>
            <p>Hello <strong>{userName}</strong>,</p>
            <p>We received a request to reset your password. Click the button below to create a new password:</p>
            <div style='text-align: center;'>
                <a href='{resetLink}' class='button'>Reset Password</a>
            </div>
            <p>Or copy and paste this link into your browser:</p>
            <p style='word-break: break-all; background-color: #f5f5f5; padding: 10px; border-radius: 4px;'>
                {resetLink}
            </p>
            <div class='warning'>
                <strong>⚠️ Important:</strong> This link will expire in 1 hour for security reasons.
            </div>
            <p>If you didn't request a password reset, you can safely ignore this email. Your password will remain unchanged.</p>
        </div>
        <div class='footer'>
            <p>This is an automated message from Bus System. Please do not reply to this email.</p>
        </div>
    </div>
</body>
</html>";
    }
}

