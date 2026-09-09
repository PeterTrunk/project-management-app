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
