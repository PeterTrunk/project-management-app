using Microsoft.Extensions.Options;
using ProjectManager.API.Common.Options;
using Resend;

namespace ProjectManager.API.Services.EmailService
{
    public class ResendEmailService : IEmailService
    {
        private readonly IResend _resend;
        private readonly string _fromEmail;
        private readonly string _frontendUrl;

        public ResendEmailService(IResend resend, IOptions<EmailOptions> options)
        {
            _resend = resend;
            _fromEmail = options.Value.EmailFrom;
            _frontendUrl = options.Value.FrontendUrl;
        }

        public async Task SendEmailVerificationAsync(string toEmail, string displayName, string verificationToken)
        {
            var verificationUrl = $"{_frontendUrl}/#/verify-email?token={verificationToken}";

            var message = new EmailMessage
            {
                From = _fromEmail,
                To = { toEmail },
                Subject = "Erősítsd meg az email címed - ProjectManager",
                HtmlBody = $"""
                    <h2>Üdvözlünk, {displayName}!</h2>
                    <p>Kattints az alábbi gombra az email cím megerősítéséhez:</p>
                    <a href="{verificationUrl}" 
                       style="background:#3b82f6;color:white;padding:12px 24px;border-radius:6px;text-decoration:none;display:inline-block;">
                        Email megerősítése
                    </a>
                    <p>A link 24 óráig érvényes.</p>
                    <p>Ha nem te regisztráltál, hagyd figyelmen kívül ezt az emailt.</p>
                """
            };

            await _resend.EmailSendAsync(message);
        }

        public async Task SendPasswordResetAsync(string toEmail, string displayName, string resetToken)
        {
            var resetUrl = $"{_frontendUrl}/#/reset-password?token={resetToken}";

            var message = new EmailMessage
            {
                From = _fromEmail,
                To = { toEmail },
                Subject = "Jelszó visszaállítás - ProjectManager",
                HtmlBody = $"""
                    <h2>Helló, {displayName}!</h2>
                    <p>Jelszó visszaállítási kérelmet kaptunk a fiókodhoz.</p>
                    <a href="{resetUrl}"
                       style="background:#3b82f6;color:white;padding:12px 24px;border-radius:6px;text-decoration:none;display:inline-block;">
                        Jelszó visszaállítása
                    </a>
                    <p>A link 1 óráig érvényes.</p>
                    <p>Ha nem te kérted, hagyd figyelmen kívül ezt az emailt.</p>
                """
            };

            await _resend.EmailSendAsync(message);
        }
    }
}
