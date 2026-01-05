namespace BusSystem.Identity.API.Services;

public interface IEmailService
{
    /// <summary>
    /// Sends a password reset email to the specified email address
    /// </summary>
    /// <param name="toEmail">Recipient email address</param>
    /// <param name="userName">User's username</param>
    /// <param name="resetLink">Password reset link</param>
    /// <returns>True if email was sent successfully, false otherwise</returns>
    Task<bool> SendPasswordResetEmailAsync(string toEmail, string userName, string resetLink);
}

