import { describe, it, expect } from 'vitest';
import {
    expiryFromToken,
    refreshMarginMs,
    refreshDelayMs,
    nextRefreshDelayMs,
    MIN_REFRESH_MARGIN_MS,
    MAX_REFRESH_MARGIN_MS
} from './tokenSchedule';

const MINUTE = 60_000;

function base64url(value: unknown): string {
    const json = JSON.stringify(value);
    const bytes = new TextEncoder().encode(json);
    const binary = Array.from(bytes, b => String.fromCharCode(b)).join('');

    return btoa(binary).replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '');
}

/** A valódi token alakja: három, ponttal elválasztott base64url rész. */
function makeToken(claims: Record<string, unknown>): string {
    return [base64url({ alg: 'HS256', typ: 'JWT' }), base64url(claims), 'nem-ellenorzott-alairas'].join('.');
}

describe('expiryFromToken', () => {
    it('a token saját exp mezőjét olvassa ki, ezredmásodpercben', () => {
        const expSeconds = 1_800_000_000;

        expect(expiryFromToken(makeToken({ exp: expSeconds }))).toBe(expSeconds * 1000);
    });

    it('elbírja a kitöltés nélküli base64url payloadot', () => {
        //Addig bővítjük a payloadot, amíg a base64 kitöltés nélkül marad - ezt az
        //atob önmagában visszautasítaná.
        for (let pad = 0; pad < 4; pad++) {
            const token = makeToken({ exp: 1_800_000_000, pad: 'x'.repeat(pad) });

            expect(token.split('.')[1]).not.toContain('=');
            expect(expiryFromToken(token)).toBe(1_800_000_000_000);
        }
    });

    it('az ékezetes igényneveken sem akad el', () => {
        const token = makeToken({ exp: 1_800_000_000, name: 'Árvíztűrő Tükörfúrógép' });

        expect(expiryFromToken(token)).toBe(1_800_000_000_000);
    });

    //A null ág a tartalék élettartamot kapcsolja be, tehát nem kivételkezelés,
    //hanem viselkedés: nem szabad kivételt dobnia.
    it.each([
        ['üres', ''],
        ['null', null],
        ['nem JWT', 'csak-egy-szoveg'],
        ['hiányos', 'fej.torzs'],
        ['nem base64 törzs', 'fej.!!!.alairas']
    ])('null-t ad %s tokenre', (_cimke, token) => {
        expect(expiryFromToken(token as string | null)).toBeNull();
    });

    it('null-t ad, ha a payloadban nincs exp', () => {
        expect(expiryFromToken(makeToken({ sub: 'valaki' }))).toBeNull();
    });

    it('null-t ad, ha az exp nem szám', () => {
        expect(expiryFromToken(makeToken({ exp: '1800000000' }))).toBeNull();
    });
});

describe('refreshMarginMs', () => {
    it('a hátralévő idő negyede', () => {
        expect(refreshMarginMs(20 * MINUTE)).toBe(5 * MINUTE);
    });

    it('rövid élettartamnál a minimumra áll', () => {
        expect(refreshMarginMs(MINUTE)).toBe(MIN_REFRESH_MARGIN_MS);
    });

    it('hosszú élettartamnál a maximumra áll', () => {
        expect(refreshMarginMs(10 * 60 * MINUTE)).toBe(MAX_REFRESH_MARGIN_MS);
    });

    //Ez a ráhagyás létjogosultsága: 15 percnél a korábbi fix 1 perc nagyjából akkora
    //volt, mint a háttérfülek időzítő-csúszása.
    it('15 perces élettartamnál érdemi mozgásteret ad', () => {
        expect(refreshMarginMs(15 * MINUTE)).toBe(3.75 * MINUTE);
    });
});

describe('refreshDelayMs', () => {
    it('a lejárat előtt a ráhagyással korábbra időzít', () => {
        const now = 1_000_000;

        expect(refreshDelayMs(now + 15 * MINUTE, now)).toBe(11.25 * MINUTE);
    });

    it('hosszú élettartamnál a plafonos ráhagyással számol', () => {
        const now = 1_000_000;

        expect(refreshDelayMs(now + 120 * MINUTE, now)).toBe(115 * MINUTE);
    });

    it('a már lejárt token azonnali megújítást kap, nem negatív időzítőt', () => {
        const now = 1_000_000;

        expect(refreshDelayMs(now - 5 * MINUTE, now)).toBe(0);
    });

    it('a ráhagyásnál kevesebb hátralévő idő is nullára esik, nem mínuszba', () => {
        const now = 1_000_000;

        expect(refreshDelayMs(now + 10_000, now)).toBe(0);
    });
});

describe('nextRefreshDelayMs', () => {
    //A javítás lényege: a két forrás közül a TOKEN dönt. Korábban a beégetett
    //élettartam döntött, és ha az nem egyezett a szerverével, a megújítás a lejárat
    //UTÁNRA került.
    it('a tokenből számol, nem a tartalék élettartamból', () => {
        const now = 1_000_000;
        const token = makeToken({ exp: (now + 15 * MINUTE) / 1000 });

        //A tartalék szándékosan nyolcszorosa a token tényleges élettartamának
        expect(nextRefreshDelayMs(token, 120, now)).toBe(11.25 * MINUTE);
    });

    it('a tartalékra csak akkor esik vissza, ha a token nem elemezhető', () => {
        const now = 1_000_000;

        expect(nextRefreshDelayMs('nem-egy-jwt', 15, now)).toBe(11.25 * MINUTE);
    });

    it('token nélkül is ad használható késleltetést', () => {
        expect(nextRefreshDelayMs(null, 15, 1_000_000)).toBe(11.25 * MINUTE);
    });
});
