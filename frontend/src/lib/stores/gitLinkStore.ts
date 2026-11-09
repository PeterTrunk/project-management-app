import { writable } from 'svelte/store';
import type { LinkedCommitResponse, LinkedPrResponse } from '../api/gitApi';

/**
 * A projekt git hivatkozásai: commitok és pull requestek.
 *
 * **Miért LAPOS, és miért a hivatkozás azonosítója szerint.** Egy commit üzenete több task
 * kulcsát is tartalmazhatja, és a webhook ilyenkor TÖBB sort hoz létre - egyet minden
 * illeszkedő taskra, mindegyiket a saját azonosítójával és `taskId`-jével. A tároló ezt az
 * alakot tükrözi, leképezés nélkül. Egy `taskId → hivatkozások` szerinti tároló ugyanazt az
 * adatot egy szűkebb feltevésre fordítaná le, és a hozzárendeletlen sor (ahol a `taskId`
 * `null`) külön esetté válna benne.
 *
 * A taskonkénti nézet ebből származtatott - lásd `linksForTask`.
 *
 * **Miért van saját tárolója.** Korábban ugyanaz az adat KÉT helyen élt: a `taskStore`
 * feladatain (`commitLinks` / `prLinks`) és a `GitView` lokális listáiban. Minden esemény
 * mindkettőt frissítette - a `GitView` úgy, hogy teljes újratöltést indított -, és a kettő
 * elcsúszhatott egymástól. Ez a tároló az egyetlen gyorsítótár.
 *
 * **Miért teljes a lefedés.** A tároló csak akkor maradhat friss események alapján, ha MINDEN
 * változásról jön értesítés. A hozzárendeletlen hivatkozások három esetben némán változtak
 * (keletkezés, megszűnés, állapotváltozás); ezt a webhook oldalán javítottuk, ugyanazon a
 * `CommitLinked` / `PrLinked` eseménynéven, `taskId: null`-lal.
 */

interface GitLinkState {
    commits: LinkedCommitResponse[];
    prs: LinkedPrResponse[];
}

const initialState: GitLinkState = {
    commits: [],
    prs: []
};

export const gitLinkStore = writable<GitLinkState>(initialState);

export function setGitLinks(commits: LinkedCommitResponse[], prs: LinkedPrResponse[]) {
    gitLinkStore.set({ commits, prs });
}

export function clearGitLinks() {
    gitLinkStore.set(initialState);
}

/**
 * Felvétel vagy csere az azonosító szerint.
 *
 * Itt CSERE a helyes művelet, nem kihagyás - ellentétben a létrehozási eseményekkel: egy pull
 * request sora az állapotváltozásaival együtt újra és újra megérkezik (megnyitás, szerkesztés,
 * lezárás, merge), és mindannyiszor a friss értéket hordozza. Kihagyással a merge sosem
 * látszódna.
 */
function upsert<T extends { id: string }>(items: T[], incoming: T): T[] {
    const index = items.findIndex(item => item.id === incoming.id);
    if (index === -1) return [...items, incoming];

    const next = [...items];
    next[index] = incoming;
    return next;
}

export function handleCommitLinked(payload: LinkedCommitResponse) {
    gitLinkStore.update(state => ({
        ...state,
        commits: dropPromotedPlaceholder(
            upsert(state.commits, payload),
            payload,
            (link, incoming) => link.commitSha === incoming.commitSha)
    }));
}

export function handlePrLinked(payload: LinkedPrResponse) {
    gitLinkStore.update(state => ({
        ...state,
        prs: dropPromotedPlaceholder(
            upsert(state.prs, payload),
            payload,
            (link, incoming) => link.prNumber === incoming.prNumber)
    }));
}

/**
 * A helyőrző sor eltávolítása, amikor ugyanaz a commit vagy PR végre illeszkedett egy taskra.
 *
 * **Miért a kliens dolga.** A szerver ilyenkor TÖRLI a hozzárendeletlen sort, és erről nincs
 * külön esemény - szándékosan: a kapcsolt sor kiküldése hordozza a `commitSha`-t, illetve a
 * `prNumber`-t, tehát a párja ebből azonosítható. Egy külön törlési esemény egy negyedik
 * eseménynév lenne ugyanarra az információra.
 *
 * Enélkül ugyanaz a commit egyszerre látszana a hozzárendeletlen listában és a task alatt -
 * pontosan az az állapot, amit a szerveroldali helyőrző-törlés megszüntetett.
 */
function dropPromotedPlaceholder<T extends { id: string; taskId: string | null }>(
    items: T[],
    incoming: T,
    sameSubject: (link: T, incoming: T) => boolean
): T[] {
    //Csak a hozzárendelt sor léptet elő: egy hozzárendeletlen esemény nem vehet ki semmit.
    if (incoming.taskId === null) return items;

    return items.filter(link =>
        link.id === incoming.id || link.taskId !== null || !sameSubject(link, incoming));
}

/**
 * Egy feladat hivatkozásai - a részletnézet ebből dolgozik.
 *
 * Tiszta függvény, hogy a `taskStore`-ból kivezetett `commitLinks` / `prLinks` mezők helyére
 * ugyanaz az adat kerüljön, de egyetlen forrásból.
 */
export function linksForTask<T extends { taskId: string | null }>(links: T[], taskId: string): T[] {
    return links.filter(link => link.taskId === taskId);
}
