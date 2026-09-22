using System.Globalization;
using System.Text.Json;

namespace ProjectManager.API.Services.GitWebhookService.Payloads
{
    /// <summary>
    /// Védekező olvasás a webhook payloadból.
    ///
    /// A payload IDEGEN bemenet: 
    /// Egy szolgáltató verziófrissítése bármikor elhagyhat vagy átnevezhet mezőket.
    /// A JsonElement.GetProperty ilyenkor kivételt dob, amiből a végpont 500-at ad,
    /// a szolgáltató pedig a webhookot hibásnak jelöli, és idővel kikapcsolhatja.
    /// Ezért itt minden olvasás nemlétező és rossz típusú mezőre is választ ad.
    ///
    /// A TryGetProperty önmagában nem elég: JSON null értékre is igazat ad, 
    /// a GetString() pedig nem string típusú elemen kivételt dob. Ezért mindenhol a ValueKind a döntő.
    /// </summary>
    internal static class JsonPayloadReader
    {
        public static bool TryReadString(this JsonElement parent, string propertyName, out string value)
        {
            value = string.Empty;

            if (parent.ValueKind != JsonValueKind.Object) return false;
            if (!parent.TryGetProperty(propertyName, out var property)) return false;
            if (property.ValueKind != JsonValueKind.String) return false;

            value = property.GetString() ?? string.Empty;
            return true;
        }

        /// <summary>
        /// String olvasása, ahol a hiány megengedett. 
        /// A csupa szóköz értéket is null-nak tekinti:
        /// Egy üres html_url ugyanúgy "nincs adat", mint a hiányzó mező.
        /// </summary>
        public static string? ReadOptionalString(this JsonElement parent, string propertyName)
        {
            if (!parent.TryReadString(propertyName, out var value)) return null;
            return string.IsNullOrWhiteSpace(value) ? null : value;
        }

        public static bool TryReadInt(this JsonElement parent, string propertyName, out int value)
        {
            value = 0;

            if (parent.ValueKind != JsonValueKind.Object) return false;
            if (!parent.TryGetProperty(propertyName, out var property)) return false;

            return property.ValueKind == JsonValueKind.Number && property.TryGetInt32(out value);
        }

        /// <summary>
        /// Igaz csak akkor, ha a mező létezik ÉS JSON true. Minden más hamis.
        /// </summary>
        public static bool ReadBool(this JsonElement parent, string propertyName)
        {
            if (parent.ValueKind != JsonValueKind.Object) return false;
            if (!parent.TryGetProperty(propertyName, out var property)) return false;

            return property.ValueKind == JsonValueKind.True;
        }

        public static bool TryReadObject(this JsonElement parent, string propertyName, out JsonElement value)
        {
            value = default;

            if (parent.ValueKind != JsonValueKind.Object) return false;
            if (!parent.TryGetProperty(propertyName, out var property)) return false;
            if (property.ValueKind != JsonValueKind.Object) return false;

            value = property;
            return true;
        }

        public static bool TryReadArray(this JsonElement parent, string propertyName, out JsonElement value)
        {
            value = default;

            if (parent.ValueKind != JsonValueKind.Object) return false;
            if (!parent.TryGetProperty(propertyName, out var property)) return false;
            if (property.ValueKind != JsonValueKind.Array) return false;

            value = property;
            return true;
        }

        /// <summary>
        /// A GitLab merge request dátumai nem ISO 8601 alakúak, hanem így néznek ki: 2026-09-16 12:05:00 UTC.
        /// Ezt az általános elemző NEM fogadja el (leellenőrizve),
        /// ezért kell külön formátum. A commitok időbélyege ellenben mindkét szolgáltatónál szabályos ISO 8601.
        /// </summary>
        private static readonly string[] NonIsoFormats = ["yyyy-MM-dd HH:mm:ss 'UTC'"];

        /// <summary>
        /// Időbélyeg olvasása UTC-ben.
        ///
        /// Szándékosan <see cref="DateTimeOffset"/>: 
        /// A szolgáltatók eltolásos ISO 8601-et küldenek (2026-09-16T14:27:31+02:00),  és a DateTimeOffset.UtcDateTime
        /// egyetlen lépésben ad DateTimeKind.Utc értéket, ezt várja a timestamp with time zone oszlop.
        /// </summary>
        public static DateTime? ReadTimestamp(this JsonElement parent, string propertyName)
        {
            if (!parent.TryReadString(propertyName, out var raw)) return null;

            if (DateTimeOffset.TryParse(
                    raw, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
                return parsed.UtcDateTime;

            return DateTimeOffset.TryParseExact(
                raw, NonIsoFormats, CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var exact)
                ? exact.UtcDateTime
                : null;
        }
    }
}
