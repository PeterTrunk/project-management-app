import type { CycleTimeDataPoint } from '../api/statisticsApi';

/**
 * Az átfutási idő összegzése és megjelenítése.
 *
 * Az összegzést szándékosan a KLIENS számolja, nem a lekérdezés: a kimutatások egyike sem
 * tárolódik, hanem lekérdezéskor áll elő, és a középértéknek nincs olyan változata, amelyet az
 * adatbázis jobban tudna. Így viszont tesztelhető, és a kihagyott pontok száma is kimondható.
 *
 * A szerver ezért nyers sorokat ad, szűrés nélkül - a lekérdezés nem dönt arról, mit nem
 * mutatunk meg.
 */

/** Mérhető-e a pont. A negatív érték nem hiba, de középértéket nem képezhet. */
export function isMeasurable(point: CycleTimeDataPoint): boolean {
    return point.hours >= 0;
}

export interface CycleTimeSummary {
    /** Ahány feladat átfutási ideje mérhető volt. */
    count: number;

    /**
     * Ahány sort kihagytunk, mert az összeolvadás a legkorábbi commit ELŐTT történt. Ez nem
     * hiba: a commit időbélyege a fejlesztő gépéről jön (a git engedi átírni), és egy commit
     * utólag is hozzárendelhető egy már összeolvadt pull requesthez. Azért látszik a számuk,
     * mert egy csendben eldobott sor ugyanaz a hibafajta, mint a néma adatvesztés.
     */
    discarded: number;

    /**
     * A középérték (medián). Azért ez a vezető szám és nem az átlag: egy hónapokig nyitva
     * maradt pull request az átlagot elviszi, a középértéket viszont nem - és a kérdés az,
     * hogy egy TIPIKUS feladat mennyi idő alatt ér be.
     */
    medianHours: number | null;

    averageHours: number | null;

    /** A leghosszabb mérés - a kiugró értékre a felhasználót külön érdemes ránézetni. */
    longest: { taskKey: string; hours: number } | null;
}

export function summarizeCycleTime(points: CycleTimeDataPoint[]): CycleTimeSummary {
    const measurable = points.filter(isMeasurable);

    if (measurable.length === 0) {
        return {
            count: 0,
            discarded: points.length,
            medianHours: null,
            averageHours: null,
            longest: null
        };
    }

    const hours = measurable.map(p => p.hours).sort((a, b) => a - b);
    const middle = Math.floor(hours.length / 2);

    const median = hours.length % 2 === 0
        ? (hours[middle - 1] + hours[middle]) / 2
        : hours[middle];

    const longest = measurable.reduce((best, point) => point.hours > best.hours ? point : best);

    return {
        count: measurable.length,
        discarded: points.length - measurable.length,
        medianHours: median,
        averageHours: hours.reduce((sum, h) => sum + h, 0) / hours.length,
        longest: { taskKey: longest.taskKey, hours: longest.hours }
    };
}

/**
 * Időtartam emberi alakban.
 *
 * Órában kapjuk, de órában olvasni használhatatlan: a „312 óra" nem mond semmit, a „13 nap"
 * igen. A napon belüli értéknél viszont az óra a hasznos felbontás, egy órán belülinél pedig
 * már a pontos szám is félrevezető lenne.
 */
export function formatCycleTime(hours: number | null): string {
    if (hours === null) return '—';

    if (hours < 0) return 'nem értelmezhető';
    if (hours < 1) return 'kevesebb mint 1 óra';

    if (hours < 24) {
        const rounded = Math.round(hours);
        return `${rounded} óra`;
    }

    const days = Math.floor(hours / 24);
    const remainingHours = Math.round(hours - days * 24);

    //A 24-re kerekedő maradék egy nappal többet jelent, nem "3 nap 24 óra"-t
    if (remainingHours === 24) return `${days + 1} nap`;

    return remainingHours === 0 ? `${days} nap` : `${days} nap ${remainingHours} óra`;
}
