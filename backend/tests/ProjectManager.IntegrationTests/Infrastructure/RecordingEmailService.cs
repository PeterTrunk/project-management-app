using ProjectManager.API.Services.EmailService;

namespace ProjectManager.IntegrationTests.Infrastructure
{
    /// <summary>Egy rögzített kimenő levél.</summary>
    /// <param name="Kind">Milyen levél: <c>verification</c> vagy <c>password-reset</c>.</param>
    /// <param name="ToEmail">A címzett.</param>
    /// <param name="Token">A levélbe került token - a tesztek ezzel folytatják a folyamatot.</param>
    public sealed record SentEmail(string Kind, string ToEmail, string Token);

    /// <summary>
    /// Az e-mail küldés az egyetlen külső hatás az AuthService-ben, amit teszt közben nem
    /// engedhetünk meg. A dupla listába gyűjti a leveleket, így az állítás egy sima LINQ
    /// kifejezés - és a tesztek a valódi tokent olvassák ki, nem az adatbázisból másolják.
    ///
    /// Ez utóbbi nem kényelmi kérdés: a levélbe kerülő és a tárolt érték eltérhet
    /// (például ha a tárolás egyszer hash-re váltana), és ezt a különbséget csak így lehet mérni.
    /// </summary>
    public sealed class RecordingEmailService : IEmailService
    {
        private readonly List<SentEmail> _sent = new();

        public IReadOnlyList<SentEmail> Sent => _sent;

        public SentEmail? LastOfKind(string kind) =>
            _sent.LastOrDefault(e => e.Kind == kind);

        public void Clear() => _sent.Clear();

        public Task SendEmailVerificationAsync(string toEmail, string displayName, string verificationToken)
        {
            _sent.Add(new SentEmail("verification", toEmail, verificationToken));
            return Task.CompletedTask;
        }

        public Task SendPasswordResetAsync(string toEmail, string displayName, string resetToken)
        {
            _sent.Add(new SentEmail("password-reset", toEmail, resetToken));
            return Task.CompletedTask;
        }
    }
}
