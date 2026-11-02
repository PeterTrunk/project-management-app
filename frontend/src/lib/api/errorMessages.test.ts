import { describe, it, expect } from 'vitest';
import { friendlyMessageFor, normalizeResponseData } from './errorMessages';

// A teszt oka egy valodi hiba: egy 400-as validacios valasz "[object Object]"-kent jelent meg
// a felhasznalonak, mert a hivok a valasz torzset kozvetlenul szovegkent olvassak. A
// normalizalo dolga, hogy ez SOHA ne fordulhasson elo - barmilyen valaszalak eseten.
describe('normalizeResponseData', () => {
    it('a sajat {error} alakbol a szoveget adja', () => {
        expect(normalizeResponseData({ error: 'A task időközben módosult!' }))
            .toBe('A task időközben módosult!');
    });

    it('a mar kicsomagolt szoveget valtozatlanul adja vissza', () => {
        expect(normalizeResponseData('Hibás email vagy jelszó!')).toBe('Hibás email vagy jelszó!');
    });

    it('az ASP.NET validacios alakbol a mezonevet ES az uzenetet adja', () => {
        const body = {
            type: 'https://tools.ietf.org/html/rfc9110',
            title: 'One or more validation errors occurred.',
            status: 400,
            errors: { RowVersion: ['Érvénytelen RowVersion!'] }
        };

        expect(normalizeResponseData(body)).toBe('RowVersion: Érvénytelen RowVersion!');
    });

    it('tobb validacios hibat osszefuz', () => {
        const body = {
            errors: {
                Title: ['A task címe maximum 200 karakter lehet!'],
                Priority: ['Érvénytelen prioritás!']
            }
        };

        const message = normalizeResponseData(body)!;

        expect(message).toContain('Title:');
        expect(message).toContain('Priority:');
    });

    it('a ProblemDetails detail/title mezojere is visszaesik', () => {
        expect(normalizeResponseData({ detail: 'Részletes ok' })).toBe('Részletes ok');
        expect(normalizeResponseData({ title: 'Cím' })).toBe('Cím');
    });

    it('HTML valaszra null-t ad, hogy a hivo altalanos uzenetet tegyen helyette', () => {
        expect(normalizeResponseData('<!DOCTYPE html><html>502</html>')).toBeNull();
    });

    it('ISMERETLEN objektumalakra null-t ad - ez a lenyegi allitas', () => {
        // Enelkul pontosan ez lett "[object Object]".
        expect(normalizeResponseData({ valamiVaratlan: { melyen: true } })).toBeNull();
    });

    it('ures valaszra null-t ad', () => {
        expect(normalizeResponseData(null)).toBeNull();
        expect(normalizeResponseData(undefined)).toBeNull();
    });

    it('SOHA nem ad vissza olyan erteket, ami "[object Object]"-kent jelenne meg', () => {
        const alakok: unknown[] = [
            { error: 'ok' },
            { errors: { A: ['x'] } },
            { detail: 'd' },
            { title: 't' },
            { message: 'm' },
            { ismeretlen: 1 },
            {},
            [],
            null,
            undefined,
            'szoveg',
            42
        ];

        for (const alak of alakok) {
            const eredmeny = normalizeResponseData(alak);
            expect(eredmeny === null || typeof eredmeny === 'string').toBe(true);
            expect(String(eredmeny)).not.toContain('[object Object]');
        }
    });
});

describe('friendlyMessageFor', () => {
    it('a megszakitott kerest NEM tekinti hibanak', () => {
        expect(friendlyMessageFor({ code: 'ERR_CANCELED' })).toBeNull();
    });

    it('idotullepesre olvashato uzenetet ad', () => {
        expect(friendlyMessageFor({ code: 'ETIMEDOUT' })).toContain('nem válaszolt időben');
    });

    it('valasz nelkuli hibara a halozati uzenetet adja', () => {
        expect(friendlyMessageFor({ message: 'Network Error' }))
            .toContain('Nem sikerült elérni a szervert');
    });

    it('502/503/504-re atmeneti hibat jelez', () => {
        for (const status of [502, 503, 504]) {
            expect(friendlyMessageFor({ response: { status } })).toContain('nem elérhető');
        }
    });

    it('egyeb statuszra null-t ad, hogy a szerver uzenete menjen ki', () => {
        expect(friendlyMessageFor({ response: { status: 409 } })).toBeNull();
        expect(friendlyMessageFor({ response: { status: 400 } })).toBeNull();
    });
});
