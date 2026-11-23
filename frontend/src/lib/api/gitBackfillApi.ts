import apiClient from './client';

/**
 * Egy hiányosan érkezett push.
 *
 * A szolgáltatók nem küldik el minden push minden commitját (a GitLab 20, a GitHub 2048
 * commitnál vágja le a webhook payload listáját). A szerver ezt észleli és feljegyzi; ez a sor
 * az a feljegyzés, amiből a felhasználó elindíthatja a pótlást.
 *
 * A `canRun` szándékosan a SZERVERTŐL jön, és nem itt számoljuk: a futás közbeni foglalás
 * lejárata szerveroldali szabály, és egy kliensoldali másolat második igazságforrás lenne -
 * ugyanaz a hiba, amit a munkamenet-megújításnál már egyszer megszüntettünk.
 */
export interface CommitBackfillResponse {
    id: string;
    integrationId: string;
    provider: string;
    repoFullName: string;
    ref: string | null;

    /** Amennyit a szolgáltató ígért. Null, ha nem közöl darabszámot (GitHub). */
    reportedCommitCount: number | null;
    receivedCommitCount: number;

    state: string;
    attemptCount: number;
    lastError: string | null;
    canRun: boolean;

    /** A legutóbbi kísérletet a rendszer indította, nem ember. */
    lastAttemptWasAutomatic: boolean;

    createdAt: string;
    claimedAt: string | null;
    completedAt: string | null;
}

export async function getBackfillsAsync(projectId: string): Promise<CommitBackfillResponse[]> {
    const response = await apiClient.get(`/projects/${projectId}/git/backfills`);
    return response.data;
}

/**
 * A pótlás indítása. A szolgáltató hibája NEM kivétel: a visszatérő sor állapotában jön meg,
 * hogy a felhasználó lássa az okot, és újrapróbálhassa. Kivétel csak akkor keletkezik, ha a
 * kérés eleve nem teljesíthető (már fut, nem indítható, vagy elérte a kérésszám-korlátot).
 */
export async function runBackfillAsync(
    projectId: string, backfillId: string
): Promise<CommitBackfillResponse> {
    const response = await apiClient.post(`/projects/${projectId}/git/backfills/${backfillId}/run`);
    return response.data;
}
