/**
 * A jogi dokumentumok verziója. Dátum alapú, hogy emberi szemmel is olvasható legyen.
 *
 * FONTOS: ezt az értéket a backend `TermsVersion` táblája is tárolja, 
 * és a regisztrációkor rögzített elfogadás erre a verzióra mutat. 
 * Ha a dokumentumok érdemben változnak, itt is és a backend seedben is új verziót kell felvenni:
 * különben a felhasználók elfogadása egy olyan szövegre hivatkozna, amit már senki nem lát.
 */
export const LEGAL_VERSION = '2026-09-09';
/** A dokumentumok hatálybalépésének napja, megjelenítésre. */
export const LEGAL_EFFECTIVE_DATE = '2026. szeptember 9.';

/**
 * Az adatkezelő azonosító adatai környezeti változóból jönnek, hogy a repóba ne kerüljön bele
 * senkinek a neve és címe. Így egy szakdolgozatot értékelő oktató a saját adataival futtathatja,
 * a git történet pedig nem őriz meg személyes adatot.
 *
 * FONTOS: a VITE_* változók build időben beépülnek a bundle-be, tehát NEM titkok - és nem is
 * annak szánjuk őket. Az adatkezelő azonosíthatósága jogszabályi követelmény (GDPR 13. cikk),
 * a közzétett oldalon ezeknek látszaniuk KELL. A cél kizárólag a repó tisztán tartása.
 */
const PLACEHOLDER_PREFIX = '[KITÖLTENDŐ';

function legalValue(value: string | undefined, label: string): string {
    //Hiányzó érték esetén a feltűnő helyőrző marad: egy láthatóan hiányos dokumentum jobb,
    //mint egy csendben üres mező, amit senki nem vesz észre.
    return value?.trim() ? value.trim() : `${PLACEHOLDER_PREFIX}: ${label}]`;
}

/** Igaz, ha az érték kitöltetlen - a felület ilyenkor kiemeli. */
export function isPlaceholder(value: string): boolean {
    return value.startsWith(PLACEHOLDER_PREFIX);
}

export const LEGAL_CONTROLLER_NAME = legalValue(
    import.meta.env.VITE_LEGAL_CONTROLLER_NAME, 'adatkezelő neve');

export const LEGAL_CONTROLLER_ADDRESS = legalValue(
    import.meta.env.VITE_LEGAL_CONTROLLER_ADDRESS, 'levelezési cím');

export const LEGAL_CONTACT_EMAIL = legalValue(
    import.meta.env.VITE_LEGAL_CONTACT_EMAIL, 'kapcsolattartási e-mail cím');

export const LEGAL_HOSTING_PROVIDER = legalValue(
    import.meta.env.VITE_LEGAL_HOSTING_PROVIDER, 'tárhelyszolgáltató neve és címe');
