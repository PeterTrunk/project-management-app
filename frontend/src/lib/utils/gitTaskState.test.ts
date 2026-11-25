import { describe, it, expect } from 'vitest';
import type { LinkedCommitResponse, LinkedPrResponse } from '../api/gitApi';
import {
    summarizeTaskGit, formatGitSummary, gitSummaryBadgeClass, hasGitActivity,
    detectGitContradictions
} from './gitTaskState';

/**
 * A kártyán megjelenő git állapot és az ellentmondás-jelzés.
 *
 * Ez a réteg nem új adatot állít elő, hanem KÖVETKEZTETÉST von le a meglévő hivatkozásokból -
 * és a következtetés az, ami tévedhet. Ezért van rá teszt: a szabályok így állítások, nem
 * sablonba írt feltételek, amiket csak kézzel, a felületen lehetne végigpróbálni.
 */
const commit = (over: Partial<LinkedCommitResponse> = {}): LinkedCommitResponse => ({
    id: 'cm1',
    commitSha: 'abc1234def',
    commitUrl: null,
    message: 'PMA-1 hibajavítás',
    authorName: 'Teszt Elek',
    committedAt: '2026-11-24T12:00:00Z',
    isManuallyLinked: false,
    taskId: 't1',
    taskKey: 'PMA-1',
    taskTitle: 'Hibajavítás',
    ...over
});

const pr = (over: Partial<LinkedPrResponse> = {}): LinkedPrResponse => ({
    id: 'pr1',
    prNumber: 42,
    prUrl: null,
    title: 'PMA-1 hibajavítás',
    state: 'open',
    authorName: 'Teszt Elek',
    createdAt: '2026-11-24T12:00:00Z',
    mergedAt: null,
    isManuallyLinked: false,
    taskId: 't1',
    taskKey: 'PMA-1',
    taskTitle: 'Hibajavítás',
    ...over
});

describe('summarizeTaskGit', () => {
    it('git tevékenység nélkül nincs mit kiírni', () => {
        const summary = summarizeTaskGit([], []);

        expect(hasGitActivity(summary)).toBe(false);
        expect(formatGitSummary(summary)).toBeNull();
    });

    it('a commitokat és a pull requestet egy sorban mondja el', () => {
        const summary = summarizeTaskGit(
            [commit({ id: 'cm1' }), commit({ id: 'cm2' }), commit({ id: 'cm3' })],
            [pr({ state: 'merged' })]);

        expect(formatGitSummary(summary)).toBe('3 commit · PR #42 merged');
    });

    it('csak commit esetén nem ír ki üres PR-részt', () => {
        expect(formatGitSummary(summarizeTaskGit([commit()], []))).toBe('1 commit');
    });

    it('csak pull request esetén sem', () => {
        expect(formatGitSummary(summarizeTaskGit([], [pr()]))).toBe('PR #42 open');
    });

    /**
     * Több pull requestnél azt emeljük ki, amelyik a legtöbbet mondja a feladat állapotáról:
     * az összeolvadt a legerősebb (az a munka bekerült), utána a nyitott, végül a lezárt.
     * A többit a kártya egy számmal jelzi, hogy ne tűnjön el a létezésük.
     */
    it('az összeolvadt pull request erősebb a nyitottnál és a lezártnál', () => {
        const summary = summarizeTaskGit([], [
            pr({ id: 'a', prNumber: 10, state: 'closed' }),
            pr({ id: 'b', prNumber: 11, state: 'merged' }),
            pr({ id: 'c', prNumber: 12, state: 'open' })
        ]);

        expect(summary.pr).toEqual({ number: 11, state: 'merged' });
        expect(formatGitSummary(summary)).toBe('PR #11 merged +2');
    });

    it('azonos állapotnál a frissebb sorszám nyer', () => {
        const summary = summarizeTaskGit([], [
            pr({ id: 'a', prNumber: 7, state: 'open' }),
            pr({ id: 'b', prNumber: 9, state: 'open' })
        ]);

        expect(summary.pr?.number).toBe(9);
    });

    it('a jelölő színe a pull request állapotából jön', () => {
        expect(gitSummaryBadgeClass(summarizeTaskGit([], [pr({ state: 'merged' })]))).toBe('badge-green');
        expect(gitSummaryBadgeClass(summarizeTaskGit([], [pr({ state: 'open' })]))).toBe('badge-yellow');
        expect(gitSummaryBadgeClass(summarizeTaskGit([], [pr({ state: 'closed' })]))).toBe('badge-blue');
        expect(gitSummaryBadgeClass(summarizeTaskGit([commit()], []))).toBe('badge-blue');
    });
});

describe('detectGitContradictions', () => {
    it('az összhangban lévő feladatra nem jelez', () => {
        const result = detectGitContradictions({
            isCompleted: true,
            isClosed: false,
            commits: [commit()],
            prs: [pr({ state: 'merged' })]
        });

        expect(result).toHaveLength(0);
    });

    it('a nyitott feladat folyamatban lévő munkája sem ellentmondás', () => {
        const result = detectGitContradictions({
            isCompleted: false,
            isClosed: false,
            commits: [commit()],
            prs: [pr({ state: 'open' })]
        });

        expect(result).toHaveLength(0);
    });

    it('kész feladat nyitott PR-rel: jelzés', () => {
        const result = detectGitContradictions({
            isCompleted: true,
            isClosed: false,
            commits: [commit()],
            prs: [pr({ state: 'open' })]
        });

        expect(result.map(c => c.code)).toEqual(['openPrOnDoneTask']);
        expect(result[0].uncertain).toBe(false);
    });

    it('összeolvadt PR nyitott feladaton: jelzés', () => {
        const result = detectGitContradictions({
            isCompleted: false,
            isClosed: false,
            commits: [commit()],
            prs: [pr({ state: 'merged' })]
        });

        expect(result.map(c => c.code)).toEqual(['mergedPrOnOpenTask']);
    });

    it('kész feladat commit nélkül: jelzés', () => {
        const result = detectGitContradictions({
            isCompleted: true,
            isClosed: false,
            commits: [],
            prs: []
        });

        expect(result.map(c => c.code)).toEqual(['doneWithoutCommits']);
        expect(result[0].uncertain).toBe(false);
    });

    /**
     * A legfontosabb eset: amíg a projekten van befejezetlen commit pótlás, a „nincs commit"
     * jelzés TÉVES is lehet - a szolgáltató levágta a commitlistát, nem a fejlesztő hagyta ki a
     * kulcsot. Ilyenkor a jelzés megmarad, de bizonytalanként, és a magyarázat a pótlásra
     * mutat. Egy biztos és egy bizonytalan jelzés ugyanúgy kiírva azt tanítaná meg, hogy
     * egyiket se vegye komolyan a felhasználó.
     */
    it('nyitott pótlás mellett a commit-hiány bizonytalan jelzés', () => {
        const result = detectGitContradictions({
            isCompleted: true,
            isClosed: false,
            commits: [],
            prs: [],
            hasOpenBackfill: true
        });

        const only = result[0];
        expect(only.code).toBe('doneWithoutCommits');
        expect(only.uncertain).toBe(true);
        expect(only.hint).toContain('hiányosan érkezett');
    });

    it('a nyitott pótlás a többi jelzést nem érinti', () => {
        const result = detectGitContradictions({
            isCompleted: true,
            isClosed: false,
            commits: [commit()],
            prs: [pr({ state: 'open' })],
            hasOpenBackfill: true
        });

        expect(result.map(c => c.code)).toEqual(['openPrOnDoneTask']);
        expect(result[0].uncertain).toBe(false);
    });

    it('két ellentmondás egyszerre is állhat', () => {
        const result = detectGitContradictions({
            isCompleted: true,
            isClosed: false,
            commits: [],
            prs: [pr({ state: 'open' })]
        });

        expect(result.map(c => c.code)).toEqual(['openPrOnDoneTask', 'doneWithoutCommits']);
    });

    /**
     * A „kész" és a „lezárt" nem ugyanaz: a `completedAt` a munka állapota (az aktív sprint
     * utolsó oszlopa), a `closedAt` a SPRINT lezárása. Egy lezárt sprint feladatán már nincs
     * mit eldönteni, tehát a figyelmeztetés ott csak a múltat kritizálná - a jelölés (commitok,
     * PR) viszont megmarad, mert az tény, nem figyelmeztetés.
     */
    it('lezárt sprint feladatán nincs figyelmeztetés', () => {
        const result = detectGitContradictions({
            isCompleted: true,
            isClosed: true,
            commits: [],
            prs: [pr({ state: 'open' })]
        });

        expect(result).toHaveLength(0);
    });

    it('a lezárás a nyitott feladat jelzését is elhallgatja', () => {
        const result = detectGitContradictions({
            isCompleted: false,
            isClosed: true,
            commits: [commit()],
            prs: [pr({ state: 'merged' })]
        });

        expect(result).toHaveLength(0);
    });

    it('a feliratok a „kész" szót használják, nem a „lezárt"-at', () => {
        const [first] = detectGitContradictions({
            isCompleted: true,
            isClosed: false,
            commits: [],
            prs: []
        });

        expect(first.label).toBe('Kész feladat commit nélkül');
    });

    //A lezárt pull request nem ellentmondás: elutasított munka is létezik
    it('lezárt PR nyitott feladaton nem jelzés', () => {
        const result = detectGitContradictions({
            isCompleted: false,
            isClosed: false,
            commits: [commit()],
            prs: [pr({ state: 'closed' })]
        });

        expect(result).toHaveLength(0);
    });
});
