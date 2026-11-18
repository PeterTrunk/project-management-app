using ProjectManager.API.Common.Constants;

namespace ProjectManager.API.Model
{
    /// <summary>
    /// Egy hiányosan megérkezett push nyilvántartása.
    ///
    /// A git szolgáltatók nem küldik el minden push minden commitját: a GitLab 20 commitnál,
    /// a GitHub 2048-nál levágja a payload commits tömbjét. Enélkül a sor nélkül ez NÉMA
    /// adatvesztés: ha a task kulcsa épp egy levágott commit üzenetében van, a kapcsolat soha
    /// nem jön létre, és semmi nem jelzi.
    ///
    /// A sor a sikeres pótlás után sem törlődik: ez marad a nyoma annak, hogy egy push
    /// hiányosan érkezett, és utólag kellett helyreállítani.
    /// </summary>
    public class CommitBackfill
    {
        public Guid Id { get; set; }
        public Guid IntegrationId { get; set; }

        /// <summary>Az ág, amelyre a push érkezett (pl. refs/heads/main). A pótló híváshoz és a felülethez.</summary>
        public string? Ref { get; set; }

        /// <summary>
        /// A push előtti és utáni állapot azonosítója. Ez a két SHA a pótló hívás paramétere
        /// (compare API), ezért kell eltárolni: a gomb később, egy másik kérésben indul,
        /// amikor a payload már nincs meg.
        /// </summary>
        public string? BeforeSha { get; set; }
        public string? AfterSha { get; set; }

        /// <summary>
        /// Amennyit a szolgáltató a pushra ígért. GitLabnál a total_commits_count; GitHubnál
        /// nincs ilyen mező, ott a tömb felső határa a jelzés, tehát az érték null marad.
        /// </summary>
        public int? ReportedCommitCount { get; set; }

        /// <summary>Amennyi ténylegesen megérkezett a payloadban, az értelmezhetetlenekkel együtt.</summary>
        public int ReceivedCommitCount { get; set; }

        public string State { get; set; } = BackfillStates.Pending;

        /// <summary>
        /// A foglalás: enélkül két egyidejű indítás ugyanarra az intervallumra két lekérést
        /// futtatna. A ClaimedById üres értéke AUTOMATIKUS futást jelent, nem hiányzó adatot.
        /// </summary>
        public DateTime? ClaimedAt { get; set; }
        public Guid? ClaimedById { get; set; }

        /// <summary>Hányadik kísérletnél tartunk, és mi volt az utolsó hibaok.</summary>
        public int AttemptCount { get; set; }
        public string? LastError { get; set; }

        /// <summary>Mikor derült ki a hiány, és mikor pótoltuk.</summary>
        public DateTime CreatedAt { get; set; }
        public DateTime? CompletedAt { get; set; }

        public Integration Integration { get; set; } = null!;
    }
}
