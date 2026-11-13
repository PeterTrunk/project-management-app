namespace ProjectManager.API.DTOs.Auth
{
    public class DisableTotpDto
    {
        public string CurrentPassword { get; set; } = string.Empty;

        //Aktív második faktor mellett kötelező. Nullable, mert a kikapcsolás akkor is
        //meghívható, amikor a fiókon nincs bekapcsolva - ilyenkor nincs mit ellenőrizni.
        public string? TotpToken { get; set; }
    }
}
