import { writable } from 'svelte/store';
import type { CommitBackfillResponse } from '../api/gitBackfillApi';

/**
 * A projekt hiányosan érkezett pushai.
 *
 * Ugyanaz a szerkezet, mint a `gitLinkStore`-nál: a lista a projekt betöltésével jön, utána a
 * `BackfillUpdated` esemény tartja frissen. Ezért nincs újratöltés a pótlás után sem - a
 * szerver a művelet végén kiküldi a sort, és azt ugyanaz a kezelő veszi át, amelyik egy másik
 * felhasználó indítását is.
 *
 * A sorok a sikeres pótlás után NEM tűnnek el: a nyilvántartás annak a nyoma, hogy egy push
 * hiányosan érkezett, és utólag kellett helyreállítani.
 */
interface BackfillState {
    backfills: CommitBackfillResponse[];
}

const initialState: BackfillState = {
    backfills: []
};

export const backfillStore = writable<BackfillState>(initialState);

export function setBackfills(backfills: CommitBackfillResponse[]) {
    backfillStore.set({ backfills });
}

export function clearBackfills() {
    backfillStore.set(initialState);
}

/**
 * Felvétel vagy csere azonosító szerint.
 *
 * Itt a CSERE a helyes művelet: ugyanaz a sor az állapotváltozásaival együtt újra megérkezik
 * (foglalás, siker, részleges, hiba), és mindannyiszor a friss értéket hordozza. Kihagyással a
 * felhasználó a saját indításának az eredményét sem látná.
 */
export function handleBackfillUpdated(payload: CommitBackfillResponse) {
    backfillStore.update(state => {
        const index = state.backfills.findIndex(b => b.id === payload.id);
        if (index === -1) return { backfills: [payload, ...state.backfills] };

        const next = [...state.backfills];
        next[index] = payload;
        return { backfills: next };
    });
}

export type BackfillKind = 'pending' | 'running' | 'done' | 'partial' | 'failed';

export interface BackfillView {
    kind: BackfillKind;

    /** Az állapot jelölője a soron. */
    label: string;

    /** A hiány mértéke, emberi nyelven. */
    gap: string;

    /** Hibaok vagy a megmaradt hiány magyarázata; null, ha nincs mit mondani. */
    note: string | null;

    /** Indítható-e most. A szerver dönti el, a kliens csak megjeleníti. */
    canRun: boolean;

    /** A gomb szövege. Null, ha nincs gomb. */
    actionLabel: string | null;
}

/**
 * Egy sor megjelenítendő alakja.
 *
 * Tiszta függvény, és ez nem formalitás: négy állapot és két kimenet (hibaok, megmaradt hiány)
 * keveredik benne, az automatikus kísérlet pedig más szöveget kíván, mint a kézi. Tesztelhető
 * függvényként ezek a kombinációk állításokká válnak, nem sablonba írt feltételekké.
 */
export function describeBackfill(row: CommitBackfillResponse): BackfillView {
    const gap = describeGap(row);

    switch (row.state) {
        case 'Completed':
            return {
                kind: 'done', label: 'Pótolva', gap, note: null,
                canRun: false, actionLabel: null
            };

        case 'InProgress':
            //A szerver akkor is indíthatónak jelöli, ha a FOGLALÁS elévült - vagyis a futás
            //megszakadt (újraindítás, telepítés). Ezt külön mondjuk ki, különben a felhasználó
            //egy örökre "folyamatban" álló sort látna, és nem tudná, hogy tehet vele valamit.
            return row.canRun
                ? {
                    kind: 'pending', label: 'Megszakadt futás', gap,
                    note: 'Az előző futás nem fejeződött be, újraindítható.',
                    canRun: true, actionLabel: 'Újraindítás'
                }
                : {
                    kind: 'running', label: 'Folyamatban', gap, note: null,
                    canRun: false, actionLabel: null
                };

        case 'Partial':
            return {
                kind: 'partial', label: 'Részben pótolva', gap,
                note: row.lastError ?? 'A hiány csökkent, de nem szűnt meg.',
                canRun: row.canRun, actionLabel: 'Újrapróbálás'
            };

        case 'Failed':
            return {
                kind: 'failed', label: 'Sikertelen', gap,
                note: failureNote(row),
                canRun: row.canRun, actionLabel: 'Újrapróbálás'
            };

        default:
            return {
                kind: 'pending', label: 'Pótlásra vár', gap,
                note: row.canRun ? null : 'Ez a push nem pótolható.',
                canRun: row.canRun, actionLabel: 'Pótlás'
            };
    }
}

/**
 * A hibaok. Ha a kísérletet a RENDSZER indította, azt ki kell írni: enélkül a felhasználó egy
 * indok nélküli kudarcot lát, és azt hiszi, ő mulasztott el valamit.
 */
function failureNote(row: CommitBackfillResponse): string {
    const reason = row.lastError ?? 'Ismeretlen hiba.';

    return row.lastAttemptWasAutomatic
        ? `Az automatikus pótlás nem sikerült: ${reason}`
        : reason;
}

/**
 * A hiány mértéke.
 *
 * A GitLab megmondja a push valódi darabszámát, tehát a hiány kiszámolható. A GitHub NEM közöl
 * darabszámot - ott csak annyit tudunk, hogy a lista a felső határon állt, a hiány mértéke
 * ismeretlen. A két esetet nem mossuk össze: egy kitalált szám rosszabb a bevallott
 * bizonytalanságnál.
 */
export function describeGap(row: CommitBackfillResponse): string {
    if (row.reportedCommitCount === null) {
        return `a payload elérte a szolgáltatói felső határt (${row.receivedCommitCount} commit érkezett meg)`;
    }

    const missing = row.reportedCommitCount - row.receivedCommitCount;

    return `a szolgáltató ${row.reportedCommitCount} commitot jelzett, ${row.receivedCommitCount} érkezett meg`
        + (missing > 0 ? ` — ${missing} hiányzik` : '');
}

/**
 * Az állapot jelölőjének színe. A projekt globális jelölő-osztályait használja, hogy a
 * színkészlet egy helyen maradjon - itt csak a MEGFELELTETÉS van, és az tesztelhető.
 */
export function backfillBadgeClass(kind: BackfillKind): string {
    switch (kind) {
        case 'done': return 'badge-green';
        case 'partial': return 'badge-yellow';
        case 'failed': return 'badge-red';
        default: return 'badge-blue';
    }
}

/**
 * Ahány sor még figyelmet kér. A jelölőhöz: a pótolt sorok megmaradnak a listában, de már nincs
 * velük dolga senkinek.
 */
export function openBackfillCount(rows: CommitBackfillResponse[]): number {
    return rows.filter(row => row.state !== 'Completed').length;
}
