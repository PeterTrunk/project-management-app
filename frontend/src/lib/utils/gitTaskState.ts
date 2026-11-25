import type { LinkedCommitResponse, LinkedPrResponse } from '../api/gitApi';

/**
 * Egy feladat git állapota a kártyán.
 *
 * Miért tiszta függvények, és miért itt. A kártyán megjelenő jelölés és az ellentmondás-jelzés
 * nem új ADAT, hanem a már meglévő hivatkozásokból levont következtetés - és a következtetés az,
 * ami tévedhet. Tiszta függvényként a szabályok állításokká válnak (lásd a teszteket), nem
 * sablonba írt feltételekké, amelyeket csak kézzel, a felületen lehetne végigpróbálni.
 */

/** A pull request állapotai, ahogy a szerver tárolja (GitPrStates). */
export const PR_OPEN = 'open';
export const PR_MERGED = 'merged';
export const PR_CLOSED = 'closed';

export interface TaskPrSummary {
    number: number;
    state: string;
}

export interface TaskGitSummary {
    commitCount: number;

    /** A feladat „fő" pull requestje, vagy null. */
    pr: TaskPrSummary | null;

    /** Ahány pull request tartozik a feladathoz - a kártyán a többlet jelzéséhez. */
    prCount: number;
}

/**
 * A kártyán megjelenő összefoglaló.
 *
 * Több pull request közül azt emeljük ki, amelyik a legtöbbet mondja a feladat állapotáról:
 * az összeolvadt a legerősebb (az a munka bekerült), utána a nyitott (az még folyamatban), és
 * csak végül a lezárt. Holtversenyben a nagyobb sorszám, vagyis a frissebb.
 */
export function summarizeTaskGit(
    commits: LinkedCommitResponse[],
    prs: LinkedPrResponse[]
): TaskGitSummary {
    const rank = (state: string): number =>
        state === PR_MERGED ? 3 : state === PR_OPEN ? 2 : 1;

    const main = prs.reduce<LinkedPrResponse | null>((best, pr) => {
        if (best === null) return pr;
        if (rank(pr.state) !== rank(best.state)) return rank(pr.state) > rank(best.state) ? pr : best;
        return pr.prNumber > best.prNumber ? pr : best;
    }, null);

    return {
        commitCount: commits.length,
        pr: main === null ? null : { number: main.prNumber, state: main.state },
        prCount: prs.length
    };
}

/** Van-e egyáltalán mit kiírni. Enélkül a kártyák többsége egy üres sort kapna. */
export function hasGitActivity(summary: TaskGitSummary): boolean {
    return summary.commitCount > 0 || summary.pr !== null;
}

/**
 * A jelölés szövege: „3 commit · PR #42 merged".
 *
 * Null, ha nincs git tevékenység - a kártya ilyenkor nem jelenít meg semmit, mert a legtöbb
 * feladatnál ez a helyzet, és egy üres jelölő csak zajt adna.
 */
export function formatGitSummary(summary: TaskGitSummary): string | null {
    if (!hasGitActivity(summary)) return null;

    const parts: string[] = [];

    if (summary.commitCount > 0) {
        parts.push(`${summary.commitCount} commit`);
    }

    if (summary.pr !== null) {
        const extra = summary.prCount > 1 ? ` +${summary.prCount - 1}` : '';
        parts.push(`PR #${summary.pr.number} ${summary.pr.state}${extra}`);
    }

    return parts.join(' · ');
}

/** A jelölő színe a pull request állapotából. A projekt globális jelölő-osztályai. */
export function gitSummaryBadgeClass(summary: TaskGitSummary): string {
    if (summary.pr === null) return 'badge-blue';

    switch (summary.pr.state) {
        case PR_MERGED: return 'badge-green';
        case PR_OPEN: return 'badge-yellow';
        default: return 'badge-blue';
    }
}

export type GitContradictionCode =
    | 'openPrOnDoneTask'
    | 'doneWithoutCommits'
    | 'mergedPrOnOpenTask';

export interface GitContradiction {
    code: GitContradictionCode;

    /** Rövid megnevezés a felületre. */
    label: string;

    /** Mit érdemes tenni, vagy miért lehet ez mégis rendben. */
    hint: string;

    /**
     * Igaz, ha a jelzés TÉVES is lehet. Ilyenkor halkabban kell megjeleníteni: egy biztos jelzés
     * és egy bizonytalan ugyanúgy kiírva azt tanítaná meg a felhasználónak, hogy egyiket se
     * vegye komolyan.
     */
    uncertain: boolean;
}

export interface GitContradictionInput {
    /**
     * A feladat <b>kész</b>: az aktív sprint utolsó oszlopába került (`completedAt`). Ez a
     * kártya „Kész" jelölésének a feltétele is.
     */
    isCompleted: boolean;

    /**
     * A feladat <b>lezárult</b> a sprint lezárásával (`closedAt`). A kettő NEM ugyanaz: a
     * „kész" a munka állapota, a „lezárt" a sprinté - és a lezárt sprint feladatán már nincs
     * mit tenni, tehát a figyelmeztetés sem segít, csak zajt ad.
     */
    isClosed: boolean;

    commits: LinkedCommitResponse[];
    prs: LinkedPrResponse[];

    /**
     * A projekten van olyan hiányos push, amelynek a pótlása még nem fejeződött be sikerrel.
     * Ilyenkor a „nincs commit" jelzés téves riasztás is lehet, mert a szolgáltató levágta a
     * commitlistát - lásd a commit pótlást.
     */
    hasOpenBackfill?: boolean;
}

/**
 * Az ellentmondások a feladat állapota és a git adat között.
 *
 * Mindhárom szabály a `completedAt`-ra épül, nem az oszlop nevére: a `MapsToStatus` szabad
 * szöveg, tehát a „készre" következtetés abból projektenként más lenne. A `completedAt` viszont
 * a szerver által írt tény, és a kártya is ezt mutatja.
 *
 * A `closedAt` ettől külön kérdés: az a SPRINT lezárását jelenti, és a lezárt sprint feladatán
 * már nincs mit eldönteni - ott a jelzés elmarad.
 */
export function detectGitContradictions(input: GitContradictionInput): GitContradiction[] {
    const { isCompleted, commits, prs } = input;

    //Lezárt sprint feladatán nincs mit eldönteni: a jelzés ott már csak a múltat kritizálná.
    //Ezért nem is finomítjuk, hanem elhagyjuk - a jelölés (commitok, PR) viszont megmarad,
    //mert az tény, nem figyelmeztetés.
    if (input.isClosed) return [];

    const contradictions: GitContradiction[] = [];

    if (isCompleted && prs.some(pr => pr.state === PR_OPEN)) {
        contradictions.push({
            code: 'openPrOnDoneTask',
            label: 'Kész feladat, nyitott PR',
            hint: 'A feladat a kész oszlopban van, de tartozik hozzá nyitott pull request.',
            uncertain: false
        });
    }

    if (isCompleted && commits.length === 0) {
        contradictions.push({
            code: 'doneWithoutCommits',
            label: 'Kész feladat commit nélkül',
            hint: input.hasOpenBackfill === true
                ? 'Nincs hozzá kapcsolt commit — de a projekt egyik pushja hiányosan érkezett, ezért ez téves jelzés is lehet. Pótlás a git nézetben.'
                : 'Nincs hozzá kapcsolt commit. Lehet, hogy a commit üzenetéből kimaradt a feladat kulcsa.',

            //A pótlásra váró push pontosan ezt a jelzést tudja hamissá tenni
            uncertain: input.hasOpenBackfill === true
        });
    }

    if (!isCompleted && prs.some(pr => pr.state === PR_MERGED)) {
        contradictions.push({
            code: 'mergedPrOnOpenTask',
            label: 'Összeolvadt PR, a feladat nyitott',
            hint: 'A pull request bekerült, de a feladat még nincs készen.',
            uncertain: false
        });
    }

    return contradictions;
}
