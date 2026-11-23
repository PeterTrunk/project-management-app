namespace ProjectManager.API.Validators.IntegrationValidators
{
    /// <summary>
    /// A saját telepítésű szolgáltató címének ellenőrzése. Két validátor használja (felvétel és
    /// módosítás), ezért külön osztály.
    ///
    /// A szabály azért szigorúbb egy egyszerű „legyen URL"-nél, mert ezt a címet a szerver
    /// FOGJA HÍVNI, méghozzá a tárolt hozzáférési tokennel. Három megfontolás:
    ///
    /// 1. <b>https kötelező.</b> A token a kérés fejlécében utazik; titkosítatlan csatornán
    ///    egy belső hálózat sem elég indok a kiadására.
    /// 2. <b>Nincs felhasználónév a címben.</b> A https://user:pass@host alak hitelesítő adatot
    ///    csempészne a mezőbe, amely - a tokennel ellentétben - titkosítás nélkül tárolódna.
    /// 3. <b>Nincs lekérdezés és töredék.</b> Ezek a végpont útvonalához fűződnének, és a
    ///    kialakuló cím nem az lenne, amit a felhasználó látni hisz.
    ///
    /// Amit ez a szabály NEM old meg: a cím megadója a szerver kimenő kéréseit a belső hálózat
    /// felé irányíthatja. Ezt itt két dolog szorítja kezelhető méretűre: a mezőt csak
    /// projektadminisztrátor állíthatja, és a rendszer kizárólag GET kérést indít fix
    /// útvonalakra, amelyek válaszát commitlistaként értelmezi - a nyers válasz sem a
    /// felületre, sem a naplóba nem kerül ki.
    /// </summary>
    public static class InstanceUrlRule
    {
        public const int MaxLength = 300;

        public const string Message =
            "A kiszolgáló címe https protokollú, felhasználónév, lekérdezés és töredék nélküli cím legyen! (pl. https://git.ceg.hu)";

        public static bool IsValid(string? value)
        {
            //A hiány érvényes: ilyenkor a szolgáltató felhős változatáról van szó
            if (string.IsNullOrWhiteSpace(value)) return true;

            if (!Uri.TryCreate(value.Trim(), UriKind.Absolute, out var uri)) return false;

            if (uri.Scheme != Uri.UriSchemeHttps) return false;
            if (!string.IsNullOrEmpty(uri.UserInfo)) return false;
            if (!string.IsNullOrEmpty(uri.Query)) return false;
            if (!string.IsNullOrEmpty(uri.Fragment)) return false;

            return true;
        }
    }
}
