namespace ProjectManager.API.DTOs.Auth
{
    public class DeleteAccountDto
    {
        /// <summary>
        /// Ismételt hitelesítés: egy ellopott access token önmagában ne tudjon fiókot törölni.
        /// </summary>
        public string CurrentPassword { get; set; } = string.Empty;

        /// <summary>
        /// Kétfaktoros hitelesítés esetén kötelező. Null, ha a fiókon nincs bekapcsolva.
        /// </summary>
        public string? TotpToken { get; set; }
    }
}
