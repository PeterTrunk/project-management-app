import { describe, it, expect } from 'vitest';
import { validateInstanceUrl } from './validators';

/**
 * A saját telepítésű git szolgáltató címe.
 *
 * A szabály szigorúbb egy egyszerű „legyen URL"-nél, és a kliensoldali ellenőrzés itt nem
 * kozmetika: ezt a címet a SZERVER hívja meg, a tárolt hozzáférési tokennel a fejlécben.
 * A kikényszerítés a szerveren van (az API közvetlenül is hívható), ez a réteg a felhasználót
 * segíti - ezért a szabályoknak egyezniük KELL a szerveriekkel.
 */
describe('validateInstanceUrl', () => {
    //Az üres érték a szolgáltató felhős változata
    it.each([null, undefined, '', '   '])('az üres érték érvényes (%s)', value => {
        expect(validateInstanceUrl(value)).toBeNull();
    });

    it.each([
        'https://git.ceg.hu',
        'https://git.ceg.hu/',
        'https://git.ceg.hu:8443',
        'https://ceg.hu/gitlab',
        'https://git.ceg.hu/api/v4'
    ])('elfogadja a kiszolgáló címét (%s)', url => {
        expect(validateInstanceUrl(url)).toBeNull();
    });

    it('elutasítja a titkosítatlan címet, és megmondja, miért', () => {
        const error = validateInstanceUrl('http://git.ceg.hu');

        expect(error).toContain('https');
        expect(error).toContain('token');
    });

    //A felhasználónevet tartalmazó alak titkosítás nélkül tárolódó hitelesítő adatot csempészne be
    it.each([
        'https://user:pass@git.ceg.hu',
        'https://user@git.ceg.hu'
    ])('elutasítja a címbe írt hitelesítő adatot (%s)', url => {
        expect(validateInstanceUrl(url)).toContain('felhasználónevet');
    });

    it.each([
        'https://git.ceg.hu?token=abc',
        'https://git.ceg.hu#fragment'
    ])('elutasítja a lekérdezést és a töredéket (%s)', url => {
        expect(validateInstanceUrl(url)).toContain('lekérdezést');
    });

    it.each([
        'git.ceg.hu',
        'nem egy cím',
        'ftp://git.ceg.hu'
    ])('elutasítja, ami nem https cím (%s)', url => {
        expect(validateInstanceUrl(url)).not.toBeNull();
    });

    it('elutasítja a túl hosszú címet', () => {
        expect(validateInstanceUrl('https://git.ceg.hu/' + 'a'.repeat(300))).not.toBeNull();
    });
});
