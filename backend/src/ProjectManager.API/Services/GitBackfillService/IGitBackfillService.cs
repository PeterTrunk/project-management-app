using ProjectManager.API.DTOs.Git;

namespace ProjectManager.API.Services.GitBackfillService
{
    public interface IGitBackfillService
    {
        /// <summary>
        /// A projekt hiányosan érkezett pushai, a legutóbbival kezdve. A már pótolt sorok is
        /// benne vannak: a nyilvántartás nem törlődik, mert ez a nyoma annak, hogy egy push
        /// hiányos volt, és utólag kellett helyreállítani.
        /// </summary>
        Task<List<CommitBackfillResponseDto>> GetBackfillsAsync(Guid projectId);

        /// <summary>
        /// A kimaradt commitok pótlása: lefoglalja a sort, lekéri a hiányzó commitokat a
        /// szolgáltatótól, és ugyanazon az illesztő úton vezeti át őket, mint a webhook.
        ///
        /// Nem dob kivételt a szolgáltató hibájára: az a visszatérő sor állapotába kerül,
        /// hogy a felhasználó lássa az okot, és újrapróbálhassa. Kivétel csak akkor van, ha a
        /// kérés eleve nem teljesíthető (nincs ilyen sor, már folyamatban van, vagy nem
        /// indítható).
        /// </summary>
        Task<CommitBackfillResponseDto> RunBackfillAsync(Guid projectId, Guid backfillId);

        /// <summary>
        /// Az automatikus kísérlet, a háttérfutásból. Pontosan egyszer fut le soronként, és
        /// csak akkor, ha az integráción be van kapcsolva.
        ///
        /// Nem dob kivételt, és nem is ad vissza semmit: itt nincs kinek jelezni. Minden
        /// kimenet a nyilvántartási sorba és a naplóba kerül, a felületet pedig a valós idejű
        /// esemény frissíti. Ha a kísérlet nem indulhat vagy hibára fut, a sort ezután a
        /// felhasználó indítja újra - automatikus ismétlés NINCS.
        /// </summary>
        Task RunAutomaticAsync(Guid backfillId);
    }
}
