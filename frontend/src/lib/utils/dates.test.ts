import { describe, it, expect } from 'vitest';
import {
    toDateTimeLocalValue,
    fromDateTimeLocalValue,
    toUtcIsoString,
    toLocalDateKey,
    fromLocalDateKey
} from './dates';

/**
 * Ezek a tesztek a JavaScript dátum-értelmezés aszimmetriáját kötik le: a dátum-idő alakot
 * a motor lokálisként, a csupasz dátumot UTC-ként értelmezi. A legtöbb állítás ezért az
 * **oda-vissza út** helyességét méri, nem egy konkrét időzónához kötött szöveget - így a
 * teszt bármelyik gépen és CI-ban is érvényes marad.
 *
 * Ahol mégis eltolásra van szükség, ott a mért eltolást a teszt maga számolja ki.
 */

/** A futtató gép UTC-eltolása percben, az adott időpontra (nyári időszámítással együtt). */
function offsetMinutes(date: Date): number {
    return -date.getTimezoneOffset();
}

describe('toDateTimeLocalValue', () => {
    it('a szerver UTC időbélyegéből a felhasználó óráját adja', () => {
        //Egy konkrét UTC pillanat, és az, amit a felhasználó ugyanakkor a saját óráján lát
        const utc = '2026-10-15T12:30:00Z';
        const local = new Date(utc);

        const expected =
            `${local.getFullYear()}-${String(local.getMonth() + 1).padStart(2, '0')}`
            + `-${String(local.getDate()).padStart(2, '0')}`
            + `T${String(local.getHours()).padStart(2, '0')}`
            + `:${String(local.getMinutes()).padStart(2, '0')}`;

        expect(toDateTimeLocalValue(utc)).toBe(expected);
    });

    it('NEM az UTC időt adja vissza, ha a gép eltolása nem nulla', () => {
        const utc = '2026-10-15T12:30:00Z';
        const naiveUtc = utc.slice(0, 16); // amit a hibás toISOString().slice(0,16) adna

        if (offsetMinutes(new Date(utc)) !== 0) {
            expect(toDateTimeLocalValue(utc)).not.toBe(naiveUtc);
        } else {
            //UTC gépen a kettő egybeesik - ilyenkor nincs mit megkülönböztetni
            expect(toDateTimeLocalValue(utc)).toBe(naiveUtc);
        }
    });

    it('a datetime-local input által megkívánt alakot adja', () => {
        expect(toDateTimeLocalValue('2026-01-05T08:07:00Z')).toMatch(/^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}$/);
    });

    it('üres értéket ad hiányzó vagy értelmezhetetlen bemenetre', () => {
        expect(toDateTimeLocalValue(null)).toBe('');
        expect(toDateTimeLocalValue(undefined)).toBe('');
        expect(toDateTimeLocalValue('')).toBe('');
        expect(toDateTimeLocalValue('ez nem datum')).toBe('');
    });

    it('Date objektumot is elfogad, nem csak sztringet', () => {
        const d = new Date('2026-10-15T12:30:00Z');
        expect(toDateTimeLocalValue(d)).toBe(toDateTimeLocalValue('2026-10-15T12:30:00Z'));
    });
});

describe('fromDateTimeLocalValue', () => {
    it('a beírt időt lokálisként értelmezi', () => {
        const date = fromDateTimeLocalValue('2026-10-15T14:30');

        expect(date).not.toBeNull();
        expect(date!.getHours()).toBe(14);
        expect(date!.getMinutes()).toBe(30);
    });

    it('null-t ad üres mezőre - a határidő elhagyható', () => {
        expect(fromDateTimeLocalValue('')).toBeNull();
        expect(fromDateTimeLocalValue(null)).toBeNull();
        expect(fromDateTimeLocalValue(undefined)).toBeNull();
    });

    it('null-t ad értelmezhetetlen bemenetre, nem Invalid Date-et', () => {
        expect(fromDateTimeLocalValue('2026-13-45T99:99')).toBeNull();
        expect(fromDateTimeLocalValue('akarmi')).toBeNull();
    });
});

describe('a datetime-local oda-vissza út', () => {
    //Ez a lényegi állítás: amit a felhasználó beír, azt lássa vissza is. A szerveren
    //közben UTC-ben tárolódik, de ez a felületen nem látszódhat.
    it('a beírt idő változatlanul jön vissza a szerveren át', () => {
        const typed = '2026-10-15T14:30';

        const sentToServer = fromDateTimeLocalValue(typed)!.toISOString();
        const shownAgain = toDateTimeLocalValue(sentToServer);

        expect(shownAgain).toBe(typed);
    });

    it('akkor is, ha a nap határát átlépi a konverzió', () => {
        //Pozitív eltolásnál a késő esti helyi idő már a KÖVETKEZŐ UTC napra esik,
        //negatívnál a kora reggeli az ELŐZŐRE. Mindkettő vissza kell jöjjön.
        for (const typed of ['2026-10-15T23:45', '2026-10-15T00:15', '2026-01-01T00:00']) {
            const sent = fromDateTimeLocalValue(typed)!.toISOString();
            expect(toDateTimeLocalValue(sent)).toBe(typed);
        }
    });
});

describe('toUtcIsoString', () => {
    it('a lokálisan beírt időt UTC ISO alakra váltja', () => {
        const typed = '2026-10-15T14:30';
        const expected = new Date(typed).toISOString();

        expect(toUtcIsoString(typed)).toBe(expected);
    });

    it('undefined-ot ad üres bemenetre, hogy a paraméter kimaradhasson a kérésből', () => {
        expect(toUtcIsoString('')).toBeUndefined();
        expect(toUtcIsoString(null)).toBeUndefined();
        expect(toUtcIsoString(undefined)).toBeUndefined();
    });

    it('undefined-ot ad értelmezhetetlen bemenetre, nem "Invalid Date" sztringet', () => {
        expect(toUtcIsoString('akarmi')).toBeUndefined();
    });

    it('NEM a nyers bemenetet adja vissza - ez volt a lapozás hibája', () => {
        //A loadMore korábban a lokális sztringet küldte, a loadActivities a UTC-t:
        //a két hívás más időszakot kért.
        const typed = '2026-10-15T14:30';
        expect(toUtcIsoString(typed)).not.toBe(typed);
    });
});

describe('toLocalDateKey', () => {
    it('a felhasználó szerinti napot adja, nem az UTC szerintit', () => {
        const now = new Date();
        const expected =
            `${now.getFullYear()}-${String(now.getMonth() + 1).padStart(2, '0')}`
            + `-${String(now.getDate()).padStart(2, '0')}`;

        expect(toLocalDateKey(now)).toBe(expected);
    });

    it('eltér a toISOString() napjától, amikor az eltolás átlépi az éjfelet', () => {
        //Egy UTC időpont közvetlenül éjfél ELŐTT: pozitív eltolású gépen ez helyben
        //már a következő nap, tehát a két számítás NEM egyezhet.
        const utc = '2026-10-15T23:30:00Z';
        const local = new Date(utc);
        const utcKey = utc.slice(0, 10);

        if (local.getDate() !== new Date(utc).getUTCDate()) {
            expect(toLocalDateKey(utc)).not.toBe(utcKey);
        } else {
            expect(toLocalDateKey(utc)).toBe(utcKey);
        }
    });

    it('argumentum nélkül a mai napot adja', () => {
        expect(toLocalDateKey()).toBe(toLocalDateKey(new Date()));
    });

    it('üres értéket ad értelmezhetetlen bemenetre', () => {
        expect(toLocalDateKey('akarmi')).toBe('');
        expect(toLocalDateKey(null)).toBe('');
    });
});

describe('fromLocalDateKey', () => {
    it('a napot helyi éjfélként értelmezi, nem UTC éjfélként', () => {
        const date = fromLocalDateKey('2026-10-15');

        expect(date).not.toBeNull();
        expect(date!.getFullYear()).toBe(2026);
        expect(date!.getMonth()).toBe(9); // október, nulla-alapú
        expect(date!.getDate()).toBe(15);
        expect(date!.getHours()).toBe(0);
    });

    it('oda-vissza úton megtartja a napot', () => {
        for (const key of ['2026-01-01', '2026-10-15', '2026-12-31']) {
            expect(toLocalDateKey(fromLocalDateKey(key))).toBe(key);
        }
    });

    it('null-t ad, ha a bemenet nem YYYY-MM-DD alakú', () => {
        expect(fromLocalDateKey('2026-10-15T14:30')).toBeNull();
        expect(fromLocalDateKey('15/10/2026')).toBeNull();
        expect(fromLocalDateKey('')).toBeNull();
        expect(fromLocalDateKey(null)).toBeNull();
    });
});
