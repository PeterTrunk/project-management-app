import { describe, it, expect, beforeEach } from 'vitest';
import { get } from 'svelte/store';
import type { CommitBackfillResponse } from '../api/gitBackfillApi';
import {
    backfillStore, setBackfills, clearBackfills, handleBackfillUpdated,
    describeBackfill, describeGap, openBackfillCount, backfillBadgeClass
} from './backfillStore';

/**
 * A hiányos pushok megjelenítése.
 *
 * Azért van itt teszt, mert négy állapot és két kimenet (hibaok, megmaradt hiány) keveredik,
 * és a kettő FÉLREÉRTHETŐ egymás nélkül: egy részleges pótlás nem siker, egy automatikus
 * kudarc pedig nem a felhasználó mulasztása. Ezek a szövegek azok, amelyekből a felhasználó
 * eldönti, nyomja-e újra a gombot.
 */
function row(overrides: Partial<CommitBackfillResponse> = {}): CommitBackfillResponse {
    return {
        id: 'b1',
        integrationId: 'i1',
        provider: 'GitLab',
        repoFullName: 'peter/pma',
        ref: 'refs/heads/main',
        reportedCommitCount: 25,
        receivedCommitCount: 20,
        state: 'Pending',
        attemptCount: 0,
        lastError: null,
        canRun: true,
        lastAttemptWasAutomatic: false,
        createdAt: '2026-11-23T10:00:00Z',
        claimedAt: null,
        completedAt: null,
        ...overrides
    };
}

describe('backfillStore', () => {
    beforeEach(() => clearBackfills());

    it('a lista cserélhető és üríthető', () => {
        setBackfills([row(), row({ id: 'b2' })]);
        expect(get(backfillStore).backfills).toHaveLength(2);

        clearBackfills();
        expect(get(backfillStore).backfills).toHaveLength(0);
    });

    //Ugyanaz a sor újra és újra megérkezik, az állapotváltozásaival együtt
    it('az esemény CSERÉLI a sort, nem duplikálja', () => {
        setBackfills([row({ state: 'Pending' })]);

        handleBackfillUpdated(row({ state: 'Completed', completedAt: '2026-11-23T10:05:00Z' }));

        const rows = get(backfillStore).backfills;
        expect(rows).toHaveLength(1);
        expect(rows[0].state).toBe('Completed');
    });

    /**
     * Egy ismeretlen sor eseménye is látszódjon: a hiányt a webhook észleli, tehát egy új sor
     * akkor keletkezik, amikor a felhasználó már a felületen van - ha ilyenkor kihagynánk,
     * a pótlás csak oldalfrissítés után lenne elérhető.
     */
    it('az ismeretlen sor bekerül, a lista elejére', () => {
        setBackfills([row({ id: 'regi' })]);

        handleBackfillUpdated(row({ id: 'uj' }));

        expect(get(backfillStore).backfills.map(b => b.id)).toEqual(['uj', 'regi']);
    });

    it('a jelölő csak a még nyitott sorokat számolja', () => {
        const rows = [
            row({ id: '1', state: 'Pending' }),
            row({ id: '2', state: 'Failed' }),
            row({ id: '3', state: 'Partial' }),
            row({ id: '4', state: 'Completed' })
        ];

        expect(openBackfillCount(rows)).toBe(3);
    });
});

describe('describeGap', () => {
    it('kiszámolja a hiányt, ha a szolgáltató darabszámot közöl', () => {
        const text = describeGap(row({ reportedCommitCount: 25, receivedCommitCount: 20 }));

        expect(text).toContain('25 commitot jelzett');
        expect(text).toContain('20 érkezett meg');
        expect(text).toContain('5 hiányzik');
    });

    /**
     * A GitHub nem közöl darabszámot: ott csak annyit tudunk, hogy a lista a felső határon
     * állt. Egy kitalált szám rosszabb a bevallott bizonytalanságnál - ezért a két eset
     * szövege is más.
     */
    it('darabszám nélkül a felső határra hivatkozik, és nem talál ki számot', () => {
        const text = describeGap(row({ reportedCommitCount: null, receivedCommitCount: 2048 }));

        expect(text).toContain('felső határt');
        expect(text).toContain('2048');
        expect(text).not.toContain('hiányzik');
    });

    it('nem ír negatív hiányt, ha több érkezett az ígértnél', () => {
        const text = describeGap(row({ reportedCommitCount: 20, receivedCommitCount: 20 }));

        expect(text).not.toContain('hiányzik');
    });
});

describe('describeBackfill', () => {
    it('a pótlásra váró sor gombot kap', () => {
        const view = describeBackfill(row({ state: 'Pending' }));

        expect(view.kind).toBe('pending');
        expect(view.actionLabel).toBe('Pótlás');
        expect(view.canRun).toBe(true);
    });

    //Azonosítók nélkül a szerver nem indíthatónak jelöli: ezt ki is kell írni
    it('a nem indítható sor megmondja, hogy nem pótolható', () => {
        const view = describeBackfill(row({ state: 'Pending', canRun: false }));

        expect(view.canRun).toBe(false);
        expect(view.note).toContain('nem pótolható');
    });

    it('a pótolt sor nem kínál műveletet', () => {
        const view = describeBackfill(row({ state: 'Completed', canRun: false }));

        expect(view.kind).toBe('done');
        expect(view.actionLabel).toBeNull();
        expect(view.note).toBeNull();
    });

    it('a futó sor gombja nincs', () => {
        const view = describeBackfill(row({ state: 'InProgress', canRun: false }));

        expect(view.kind).toBe('running');
        expect(view.actionLabel).toBeNull();
    });

    /**
     * Ez a legfontosabb elágazás: ha a szerver egy futó sort MÉGIS indíthatónak jelöl, akkor a
     * foglalása elévült, vagyis a futás megszakadt (újraindítás, telepítés). Enélkül a
     * felhasználó egy örökre „folyamatban" álló sort látna, és nem tudná, hogy tehet vele valamit.
     */
    it('a megszakadt futás újraindíthatóként jelenik meg', () => {
        const view = describeBackfill(row({
            state: 'InProgress',
            canRun: true,
            claimedAt: '2026-11-23T09:00:00Z'
        }));

        expect(view.kind).toBe('pending');
        expect(view.label).toBe('Megszakadt futás');
        expect(view.actionLabel).toBe('Újraindítás');
        expect(view.note).toContain('nem fejeződött be');
    });

    it('a részleges pótlás nem sikerként látszik, és újraindítható', () => {
        const view = describeBackfill(row({
            state: 'Partial',
            lastError: 'A szolgáltató sem adta meg a teljes intervallumot.'
        }));

        expect(view.kind).toBe('partial');
        expect(view.actionLabel).toBe('Újrapróbálás');
        expect(view.note).toContain('teljes intervallumot');
    });

    it('a részleges pótlás magyarázat nélkül is mond valamit', () => {
        const view = describeBackfill(row({ state: 'Partial', lastError: null }));

        expect(view.note).toContain('nem szűnt meg');
    });

    it('a hibás sor a hibaokot mutatja', () => {
        const view = describeBackfill(row({
            state: 'Failed',
            lastError: 'A szolgáltató elutasította a hitelesítő adatot.'
        }));

        expect(view.kind).toBe('failed');
        expect(view.note).toBe('A szolgáltató elutasította a hitelesítő adatot.');
        expect(view.actionLabel).toBe('Újrapróbálás');
    });

    /**
     * Ha a kísérletet a rendszer indította, azt ki kell írni: a felhasználó különben egy indok
     * nélküli kudarcot lát egy olyan sornál, amelyhez hozzá sem nyúlt, és azt hiszi, ő
     * mulasztott el valamit.
     */
    it('az automatikus kudarc megnevezi, hogy nem ember indította', () => {
        const view = describeBackfill(row({
            state: 'Failed',
            lastAttemptWasAutomatic: true,
            lastError: 'A szolgáltató nem elérhető.'
        }));

        expect(view.note).toContain('automatikus pótlás nem sikerült');
        expect(view.note).toContain('nem elérhető');
    });

    it('hibaok nélkül sem marad üresen a magyarázat', () => {
        const view = describeBackfill(row({ state: 'Failed', lastError: null }));

        expect(view.note).toContain('Ismeretlen hiba');
    });

    it('az ismeretlen állapot nem tünteti el a sort', () => {
        const view = describeBackfill(row({ state: 'ValamiUj' }));

        expect(view.kind).toBe('pending');
        expect(view.gap).toContain('25 commitot jelzett');
    });
});

describe('backfillBadgeClass', () => {
    it('a kimenetek a globális jelölő-osztályokra képződnek', () => {
        expect(backfillBadgeClass('done')).toBe('badge-green');
        expect(backfillBadgeClass('partial')).toBe('badge-yellow');
        expect(backfillBadgeClass('failed')).toBe('badge-red');
        expect(backfillBadgeClass('pending')).toBe('badge-blue');
        expect(backfillBadgeClass('running')).toBe('badge-blue');
    });
});
