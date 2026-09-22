using System.Text.Json;

namespace ProjectManager.API.Services.GitWebhookService.Payloads
{
    /// <summary>
    /// A push esemény commit tömbjének olvasása.
    /// Mindkét szolgáltató ugyanazokat a mezőneveket használja itt, ezért közös a kód.
    /// (id, message, url, timestamp, author.name, author.email)
    /// Amely Gutlab / Gubhub-nál is működött, 
    /// mert itt nem volt eltérés abban hogy hogyan kezelik a ezeket a payload-okat.
    /// </summary>
    internal static class CommitArrayReader
    {
        public static GitPushEvent Read(JsonElement commits)
        {
            var parsed = new List<GitCommitInfo>();
            var skipped = 0;

            foreach (var commit in commits.EnumerateArray())
            {
                if (TryReadCommit(commit, out var info))
                    parsed.Add(info);
                else
                    skipped++;
            }

            return new GitPushEvent(parsed, skipped);
        }

        private static bool TryReadCommit(JsonElement commit, out GitCommitInfo info)
        {
            info = null!;

            //A sha és az üzenet kötelező: sha nélkül nincs mit azonosítani,
            //üzenet nélkül nincs miben task kulcsot keresni
            if (!commit.TryReadString("id", out var sha) || sha.Length == 0) return false;
            if (!commit.TryReadString("message", out var message)) return false;

            var author = commit.TryReadObject("author", out var authorElement)
                ? authorElement
                : default;

            info = new GitCommitInfo(
                Sha: sha,
                Message: message,
                Url: commit.ReadOptionalString("url"),
                AuthorName: author.ReadOptionalString("name") ?? string.Empty,
                AuthorEmail: author.ReadOptionalString("email") ?? string.Empty,
                CommittedAt: commit.ReadTimestamp("timestamp") ?? DateTime.UtcNow); 
                //Legalább egy közelítő idő, amiatt ne vesszen el ha véletlen nincs rendes timestamp.

            return true;
        }
    }
}
