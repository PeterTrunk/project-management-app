import { describe, it, expect } from 'vitest';
import type { CycleTimeDataPoint } from '../api/statisticsApi';
import { summarizeCycleTime, formatCycleTime, isMeasurable } from './cycleTime';

/**
 * Az átfutási idő összegzése.
 *
 * Azért van rá teszt, mert itt két döntés él, és mindkettő befolyásolja, mit hisz a felhasználó
 * a csapatról: hogy a VEZETŐ szám a középérték (az átlagot egy hónapokig nyitva maradt pull
 * request elviszi), és hogy a nem értelmezhető sorokat nem csendben dobjuk el, hanem megmondjuk,
 * mennyi volt belőlük.
 */
const point = (over: Partial<CycleTimeDataPoint> = {}): CycleTimeDataPoint => ({
    taskId: 't1',
    taskKey: 'PMA-1',
    title: 'Hibajavítás',
    firstCommitAt: '2026-11-10T12:00:00Z',
    mergedAt: '2026-11-12T12:00:00Z',
    hours: 48,
    commitCount: 3,
    prNumber: 42,
    ...over
});

describe('summarizeCycleTime', () => {
    it('üres halmazra nincs középérték', () => {
        const summary = summarizeCycleTime([]);

        expect(summary.count).toBe(0);
        expect(summary.medianHours).toBeNull();
        expect(summary.averageHours).toBeNull();
        expect(summary.longest).toBeNull();
        expect(summary.discarded).toBe(0);
    });

    it('páratlan számú mérésnél a középső érték', () => {
        const summary = summarizeCycleTime([
            point({ taskId: '1', hours: 10 }),
            point({ taskId: '2', hours: 100 }),
            point({ taskId: '3', hours: 20 })
        ]);

        expect(summary.count).toBe(3);
        expect(summary.medianHours).toBe(20);
    });

    it('páros számú mérésnél a két középső átlaga', () => {
        const summary = summarizeCycleTime([
            point({ taskId: '1', hours: 10 }),
            point({ taskId: '2', hours: 30 }),
            point({ taskId: '3', hours: 50 }),
            point({ taskId: '4', hours: 70 })
        ]);

        expect(summary.medianHours).toBe(40);
    });

    /**
     * Ez az eset mutatja meg, miért a középérték a vezető szám: egy négy hónapig nyitva maradt
     * pull request az átlagot a valóságtól elszakítja, a középérték viszont továbbra is azt
     * mondja meg, hogy egy tipikus feladat mennyi idő alatt ér be.
     */
    it('a kiugró érték az átlagot elviszi, a középértéket nem', () => {
        const summary = summarizeCycleTime([
            point({ taskId: '1', hours: 10 }),
            point({ taskId: '2', hours: 12 }),
            point({ taskId: '3', hours: 14 }),
            point({ taskId: '4', taskKey: 'PMA-99', hours: 3000 })
        ]);

        expect(summary.medianHours).toBe(13);
        expect(summary.averageHours).toBeGreaterThan(700);
        expect(summary.longest).toEqual({ taskKey: 'PMA-99', hours: 3000 });
    });

    /**
     * A negatív érték nem hiba: a commit időbélyege a fejlesztő gépéről jön, és egy commit
     * utólag is hozzárendelhető egy már összeolvadt pull requesthez. A középértékből kimarad,
     * de a száma látszik - egy csendben eldobott sor ugyanaz a hibafajta, mint a néma adatvesztés.
     */
    it('a nem értelmezhető sorok kimaradnak, de megszámolódnak', () => {
        const summary = summarizeCycleTime([
            point({ taskId: '1', hours: 20 }),
            point({ taskId: '2', hours: -5 }),
            point({ taskId: '3', hours: 40 })
        ]);

        expect(summary.count).toBe(2);
        expect(summary.discarded).toBe(1);
        expect(summary.medianHours).toBe(30);
    });

    it('ha minden sor nem értelmezhető, nincs középérték, de a szám megvan', () => {
        const summary = summarizeCycleTime([
            point({ taskId: '1', hours: -1 }),
            point({ taskId: '2', hours: -2 })
        ]);

        expect(summary.count).toBe(0);
        expect(summary.discarded).toBe(2);
        expect(summary.medianHours).toBeNull();
    });

    it('a nulla óra mérhető: ugyanaznapi commit és összeolvadás', () => {
        expect(isMeasurable(point({ hours: 0 }))).toBe(true);
        expect(summarizeCycleTime([point({ hours: 0 })]).count).toBe(1);
    });
});

describe('formatCycleTime', () => {
    it('üres értéknél gondolatjel', () => {
        expect(formatCycleTime(null)).toBe('—');
    });

    it('egy órán belül nem ír pontos számot', () => {
        expect(formatCycleTime(0.4)).toBe('kevesebb mint 1 óra');
    });

    it('napon belül órában mér', () => {
        expect(formatCycleTime(5.4)).toBe('5 óra');
        expect(formatCycleTime(23.6)).toBe('24 óra');
    });

    //Órában olvasni használhatatlan: a "312 óra" nem mond semmit, a "13 nap" igen
    it('napon túl napra vált', () => {
        expect(formatCycleTime(48)).toBe('2 nap');
        expect(formatCycleTime(50)).toBe('2 nap 2 óra');
        expect(formatCycleTime(312)).toBe('13 nap');
    });

    it('a 24-re kerekedő maradék egy nappal többet jelent', () => {
        expect(formatCycleTime(47.8)).toBe('2 nap');
    });

    it('a negatív értéket kimondja, nem kerekíti el', () => {
        expect(formatCycleTime(-3)).toBe('nem értelmezhető');
    });
});
