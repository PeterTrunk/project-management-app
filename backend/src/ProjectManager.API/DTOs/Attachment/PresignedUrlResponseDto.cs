namespace ProjectManager.API.DTOs.Attachment
{
    public class PresignedUrlResponseDto
    {
        /// <summary>A cél URL, amire a böngésző az űrlapot POST-olja.</summary>
        public string UploadUrl { get; set; } = string.Empty;

        /// <summary>
        /// Az aláírt policy és a hozzá tartozó mezők. A kliensnek mindet változtatás nélkül
        /// vissza kell küldenie, a fájlt pedig utolsó mezőként csatolnia.
        /// </summary>
        public Dictionary<string, string> Fields { get; set; } = new();

        public string StorageKey { get; set; } = string.Empty;
        public DateTime ExpiresAt { get; set; }
    }
}
