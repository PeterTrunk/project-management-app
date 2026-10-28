namespace ProjectManager.API.Services.FileStorageService
{
    /// <summary>
    /// Egy aláírt feltöltési engedély: a cél URL és a hozzá tartozó űrlapmezők.
    ///
    /// A mezők között ott van az aláírt policy és a szignatúra is, ezért a kliensnek
    /// MINDET változtatás nélkül vissza kell küldenie - különben az aláírás nem egyezik.
    /// A fájl mezője (file) kötelezően az utolsó, ezt a kliens állítja össze.
    /// </summary>
    public class PresignedUpload
    {
        public string Url { get; set; } = string.Empty;
        public Dictionary<string, string> Fields { get; set; } = new();
    }
}
