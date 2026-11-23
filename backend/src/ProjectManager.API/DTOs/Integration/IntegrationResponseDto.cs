namespace ProjectManager.API.DTOs.Integration
{
    public class IntegrationResponseDto
    {
        public Guid Id { get; set; }
        public string Provider { get; set; } = string.Empty;
        public string RepoFullName { get; set; } = string.Empty;
        public string WebhookToken { get; set; } = string.Empty;
        public string WebhookUrl { get; set; } = string.Empty;
        public bool IsEnabled { get; set; }
        public bool IsVerified { get; set; }

        /// <summary>
        /// A saját telepítésű szolgáltató címe, vagy null a felhős változatnál. Ez nem titok:
        /// a felületnek meg kell tudnia jeleníteni, melyik kiszolgálóval dolgozik az integráció.
        /// </summary>
        public string? InstanceUrl { get; set; }

        /// <summary>
        /// Van-e beállított hozzáférési token. Maga a token NEM kerül ki, ahogy a webhook titok
        /// sem: a felületnek csak azt kell tudnia, hogy beállított állapotot írjon ki, és hogy
        /// a pótlás egyáltalán megkísérelhető-e privát tárháznál.
        /// </summary>
        public bool HasAccessToken { get; set; }

        public bool AutoBackfillEnabled { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
