//Az access token lejáratának kiolvasása és a megújítás időzítésének kiszámítása.
//
//Miért külön modul: ez a rész tiszta számítás - időzítő, globális állapot és
//környezeti változó nélkül. Így tesztelhető az, ami korábban egy setTimeout belsejében
//lakott, és csak futás közben derült volna ki róla, ha rosszul számol.
//
//Miért a tokenből: a lejárat a token SAJÁT adata. Korábban egy fordításkor beégetett
//konstansból (VITE_JWT_ACCESS_TOKEN_LIFETIME) számoltuk újra ugyanazt a tényt, ami így
//két helyen létezett - és a frontend oldali példány csak image-újraépítéssel tudott
//követni egy szerveroldali változtatást.

//A megújítás ráhagyása a HÁTRALÉVŐ idő negyede, ezek közé szorítva. Arányos, mert a fix
//érték rövid élettartamnál elfogy: a böngészők a háttérfülek időzítőit visszafogják
//(a Chrome „intensive throttling"-ja percenként egyszer ébreszti őket), tehát egy
//egyperces ráhagyás nagyjából pont akkora, mint a csúszás, amit el kellene nyelnie.
export const MIN_REFRESH_MARGIN_MS = 30_000;
export const MAX_REFRESH_MARGIN_MS = 5 * 60_000;

/**
 * A JWT `exp` mezője ezredmásodpercben, vagy `null`, ha nem olvasható ki.
 *
 * FONTOS: ez NEM a token ellenőrzése. Egy hitelesítetlen állítást olvasunk ki, és
 * kizárólag ÜTEMEZÉSRE használjuk - a legrosszabb eset egy rosszul időzített megújítás,
 * nem jogosultsági döntés. Az érvényességet a szerver ellenőrzi, aláírással.
 */
export function expiryFromToken(token: string | null | undefined): number | null {
    if (!token) return null;

    const parts = token.split('.');
    if (parts.length !== 3) return null;

    try {
        //base64url -> base64: a - és _ visszacserélése, majd a kitöltés pótlása,
        //mert az atob a hiányzó = jeleket nem tűri, a base64url viszont elhagyja őket.
        const base64 = parts[1].replace(/-/g, '+').replace(/_/g, '/');
        const padded = base64.padEnd(base64.length + ((4 - (base64.length % 4)) % 4), '=');

        //Az atob bájtonként dekódol, a payload viszont UTF-8 JSON: az ékezetes
        //igénynevek enélkül elromlanának.
        const bytes = Uint8Array.from(atob(padded), c => c.charCodeAt(0));
        const claims = JSON.parse(new TextDecoder().decode(bytes));

        return typeof claims.exp === 'number' ? claims.exp * 1000 : null;
    } catch {
        return null;
    }
}

/** A hátralévő időhöz tartozó ráhagyás. */
export function refreshMarginMs(remainingMs: number): number {
    return Math.min(Math.max(remainingMs / 4, MIN_REFRESH_MARGIN_MS), MAX_REFRESH_MARGIN_MS);
}

/**
 * Mennyit kell várni a megújításig. Soha nem negatív: a már lejárt token azonnal
 * megújítandó, nem „múltbeli" időzítővel.
 */
export function refreshDelayMs(expiresAt: number, now: number): number {
    const remaining = expiresAt - now;
    if (remaining <= 0) return 0;

    return Math.max(remaining - refreshMarginMs(remaining), 0);
}

/**
 * A tényleges ütemezéshez használt késleltetés.
 *
 * A tartalék élettartam csak akkor lép életbe, ha a tokenből nem olvasható ki a lejárat -
 * a gyakorlatban ez azt jelenti, hogy nincs is használható tokenünk.
 */
export function nextRefreshDelayMs(
    token: string | null | undefined,
    fallbackLifetimeMinutes: number,
    now: number
): number {
    const expiresAt = expiryFromToken(token) ?? now + fallbackLifetimeMinutes * 60_000;

    return refreshDelayMs(expiresAt, now);
}
