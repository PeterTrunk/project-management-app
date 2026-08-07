namespace ProjectManager.API.Services.EmailService
{
    public class ConsoleEmailService : IEmailService
    {
        public Task SendEmailVerificationAsync(string toEmail, string displayName, string verificationToken)
        {
            Console.WriteLine("-----------------------------------------------------------");
            Console.WriteLine($"[EMAIL] Verification email to: {toEmail}");
            Console.WriteLine($"[EMAIL] Token: {verificationToken}");
            Console.WriteLine("-----------------------------------------------------------");
            return Task.CompletedTask;
        }

        public Task SendPasswordResetAsync(string toEmail, string displayName, string resetToken)
        {
            Console.WriteLine("-----------------------------------------------------------");
            Console.WriteLine($"[EMAIL] Password reset email to: {toEmail}");
            Console.WriteLine($"[EMAIL] Token: {resetToken}");
            Console.WriteLine("-----------------------------------------------------------");
            return Task.CompletedTask;
        }
    }
}
