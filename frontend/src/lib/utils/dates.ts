/**
 * Dátumkezelés a felület és a szerver között.
 *
 * A szerver kizárólag UTC időbélyegeket küld és fogad (`timestamptz` oszlopok,
 * `DateTime.UtcNow` mindenhol), a felhasználó viszont a saját időzónájában gondolkodik.
 * A két oldal közötti átváltás **egyetlen helye** ez a fájl.
 *
 * Azért kell központilag, mert a JavaScript **kétféleképpen** értelmezi az ISO alakokat,
 * és ez a legtöbb dátumhibának az oka (mérve, Europe/Budapest, +02:00):
 *
 *     new Date("2026-10-15T14:30")  ->  2026-10-15T12:30:00.000Z   LOKÁLIS-ként
 *     new Date("2026-10-15")        ->  2026-10-15T00:00:00.000Z   UTC-ként
 *
 * Vagyis a dátum-idő alak lokális, a csupasz dátum viszont UTC. 
 * Aki a kettőt ugyanannak hiszi, két órát (nyáron) téved - vagy egy napot, ha a határ átlépődik.
 *
 * A másik visszatérő hiba a `toISOString().split('T')[0]`: az UTC szerinti napot adja,
 * nem a felhasználó szerintit. Éjfél és a helyi eltolás között ez az előző napra mutat.
 */

function pad(value: number): string {
    return String(value).padStart(2, '0');
}

/** Értelmezhető dátum-e. A `new Date(...)` érvénytelen bemenetre NaN-t ad, nem hibát. */
function isValid(date: Date): boolean {
    return !Number.isNaN(date.getTime());
}

/**
 * A szervertől kapott UTC időbélyegből `<input type="datetime-local">` érték.
 *
 * A kimenet szándékosan **lokális idő**, mert a datetime-local input időzóna nélkül értelmez: 
 * amit beírunk, azt a felhasználó a saját óráján látja.
 *
 * NEM használható helyette a `toISOString().slice(0, 16)`: 
 * az UTC-t adna, tehát a mező a helyi eltolással elcsúszott időt mutatná.
 */
export function toDateTimeLocalValue(value: string | Date | null | undefined): string {
    if (value === null || value === undefined || value === '') return '';

    const date = value instanceof Date ? value : new Date(value);
    if (!isValid(date)) return '';

    return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}`
        + `T${pad(date.getHours())}:${pad(date.getMinutes())}`;
}

/**
 * `<input type="datetime-local">` értékből a szervernek küldhető időpont.
 *
 * A `new Date(...)` a dátum-idő alakot lokálisként értelmezi, 
 * a szerializálás pedig UTC-ben történik - így a felhasználó által beírt idő helyesen kerül át. 
 * Üres mező esetén `null`, mert a határidő és a sprint dátumok elhagyhatók.
 */
export function fromDateTimeLocalValue(value: string | null | undefined): Date | null {
    if (!value) return null;

    const date = new Date(value);
    return isValid(date) ? date : null;
}

/**
 * `<input type="datetime-local">` értékből a szervernek küldhető UTC ISO sztring.
 *
 * Szűrőkhöz és lekérdezési paraméterekhez való, ahol nem Date objektum, hanem sztring kell.
 * Üres bemenetre `undefined`, hogy a hívási helyen ne kelljen külön vizsgálni - a hiányzó
 * paraméter így egyszerűen kimarad a kérésből.
 *
 * Ez a függvény azért van, mert enélkül könnyű az egyik hívási helyen konvertálni, 
 * a másikon nem - és akkor a lapozás más időszakot kér, mint az első lap.
 */
export function toUtcIsoString(value: string | null | undefined): string | undefined {
    const date = fromDateTimeLocalValue(value);
    return date === null ? undefined : date.toISOString();
}

/**
 * A nap azonosítója `YYYY-MM-DD` alakban, **a felhasználó időzónájában**.
 *
 * Erre azért van szükség, mert a `new Date().toISOString().split('T')[0]` az UTC szerinti
 * napot adja: Budapesten nyáron éjfél és 02:00 között még az előző napot, tehát a "mai"
 * szűrő és a statisztikák alapértelmezett időszaka egy nappal elcsúszna.
 */
export function toLocalDateKey(value: string | Date | null | undefined = new Date()): string {
    if (value === null || value === undefined || value === '') return '';

    const date = value instanceof Date ? value : new Date(value);
    if (!isValid(date)) return '';

    return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}`;
}

/**
 * Egy `YYYY-MM-DD` alakú nap-azonosítóból dátum, a felhasználó időzónájában.
 *
 * A `new Date("2026-10-15")` ezt UTC éjfélként értelmezné, ami a helyi eltolás miatt
 * átcsúszhat a szomszédos napra. Ezért az összetevőket külön adjuk át.
 */
export function fromLocalDateKey(value: string | null | undefined): Date | null {
    if (!value) return null;

    const match = /^(\d{4})-(\d{2})-(\d{2})$/.exec(value);
    if (!match) return null;

    const [, year, month, day] = match;
    const date = new Date(Number(year), Number(month) - 1, Number(day));

    return isValid(date) ? date : null;
}
