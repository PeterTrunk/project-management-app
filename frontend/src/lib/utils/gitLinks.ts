import type { LinkedCommitResponse, LinkedPrResponse } from '../api/gitApi';

/**
 * A git hivatkozások keresése a git nézetben.
 *
 * Azért külön fájlban és tiszta függvényként, 
 * mert a keresés szabályai hogy mely mezőkre illeszkedjen és mikor NE pont olyan részletek,
 * amiket olcsón és pontosan csak így lehet mérni. Egy komponensbe zárva minden állításhoz DOM kellene.
 *
 * A legfontosabb mező a **task kulcsa**: a keresés fő használati esete az, hogy egy rossz
 * kulccsal beillesztett commitot keresünk, és tudjuk, hova került tévedésből.
 */

/** Üres vagy csak szóközt tartalmazó keresésnél minden elem megmarad. */
function normalise(query: string): string | null {
    const trimmed = query.trim().toLowerCase();
    return trimmed === '' ? null : trimmed;
}

function matches(value: string | null | undefined, query: string): boolean {
    return value != null && value.toLowerCase().includes(query);
}

export function filterCommitLinks(
    links: LinkedCommitResponse[],
    query: string
): LinkedCommitResponse[] {
    const q = normalise(query);
    if (q === null) return links;

    return links.filter(link =>
        matches(link.commitSha, q) ||
        matches(link.message, q) ||
        matches(link.authorName, q) ||
        matches(link.taskKey, q)
    );
}

export function filterPrLinks(
    links: LinkedPrResponse[],
    query: string
): LinkedPrResponse[] {
    const q = normalise(query);
    if (q === null) return links;

    //A felületen "#42" alakban látszik a sorszám, tehát a kettőskereszttel beírt keresésnek
    //is működnie kell — különben a felhasználó azt másolja vissza, amit lát, és nem talál semmit
    const numeric = q.startsWith('#') ? q.slice(1) : q;

    return links.filter(link =>
        matches(link.title, q) ||
        matches(link.authorName, q) ||
        matches(link.taskKey, q) ||
        (numeric !== '' && String(link.prNumber).includes(numeric))
    );
}
