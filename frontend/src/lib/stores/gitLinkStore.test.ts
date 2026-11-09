import { describe, it, expect, beforeEach } from 'vitest';
import { get } from 'svelte/store';
import {
    gitLinkStore, setGitLinks, clearGitLinks,
    handleCommitLinked, handlePrLinked, linksForTask
} from './gitLinkStore';
import type { LinkedCommitResponse, LinkedPrResponse } from '../api/gitApi';

/**
 * A git hivatkozások tárolója.
 *
 * Ezek a tesztek nagyrészt a `taskStore` git-blokkjából kerültek át, de az ÁLLÍTÁSUK
 * egyszerűbb lett, és ez önmagában a szerkezeti változás mérése: amíg a hivatkozások a
 * feladatok sorain éltek, az áthelyezést a kezelőnek KÉZZEL kellett végrehajtania (felvétel az
 * egyik taskra, levétel a másikról), és ennek minden ága külön hibalehetőség volt. A lapos,
 * azonosító szerint kulcsolt tárolóban az áthelyezés egyszerű csere - a taskonkénti nézet
 * származtatott, tehát magától követi.
 */
describe('gitLinkStore', () => {
    beforeEach(() => clearGitLinks());

    const commit = (over: Partial<LinkedCommitResponse> = {}): LinkedCommitResponse => ({
        id: 'cm1',
        commitSha: 'abc1234def',
        commitUrl: 'https://example.invalid/commit/abc1234',
        message: 'PMA-1 hibajavítás',
        authorName: 'Teszt Elek',
        committedAt: '2026-11-08T12:00:00Z',
        isManuallyLinked: false,
        taskId: 't1',
        taskKey: 'PMA-1',
        taskTitle: 'Hibajavítás',
        ...over
    });

    const pr = (over: Partial<LinkedPrResponse> = {}): LinkedPrResponse => ({
        id: 'pr1',
        prNumber: 42,
        prUrl: 'https://example.invalid/pr/42',
        title: 'PMA-1 hibajavítás',
        state: 'open',
        authorName: 'Teszt Elek',
        createdAt: '2026-11-08T12:00:00Z',
        mergedAt: null,
        isManuallyLinked: false,
        taskId: 't1',
        taskKey: 'PMA-1',
        taskTitle: 'Hibajavítás',
        ...over
    });

    describe('felvétel és csere', () => {
        it('felveszi az új hivatkozást', () => {
            handleCommitLinked(commit());

            expect(get(gitLinkStore).commits).toHaveLength(1);
        });

        it('ugyanazt az azonosítót FELÜLÍRJA, nem duplikálja', () => {
            //Itt a csere a helyes művelet, nem a kihagyás: egy pull request sora az
            //állapotváltozásaival együtt újra és újra megérkezik. Kihagyással a merge sosem
            //látszódna - ezért tér el ez a `collections.ts` létrehozás-gárdájától.
            handlePrLinked(pr());
            handlePrLinked(pr({ state: 'merged', mergedAt: '2026-11-08T13:00:00Z' }));

            const prs = get(gitLinkStore).prs;
            expect(prs).toHaveLength(1);
            expect(prs[0].state).toBe('merged');
        });

        it('a különböző azonosítójú hivatkozások egymás mellé kerülnek', () => {
            handlePrLinked(pr());
            handlePrLinked(pr({ id: 'pr2', prNumber: 43 }));

            expect(get(gitLinkStore).prs.map(p => p.id)).toEqual(['pr1', 'pr2']);
        });
    });

    describe('áthelyezés másik taskra', () => {
        it('a sor átkerül, és a korábbi task alól eltűnik', () => {
            //A régi tárolóban ehhez a kezelőnek kézzel kellett levennie a hivatkozást a
            //korábbi taskról. Itt csak csere történik: a taskonkénti nézet származtatott.
            handleCommitLinked(commit());

            handleCommitLinked(commit({ taskId: 't2', taskKey: 'PMA-2' }));

            const commits = get(gitLinkStore).commits;
            expect(commits).toHaveLength(1);
            expect(linksForTask(commits, 't1')).toHaveLength(0);
            expect(linksForTask(commits, 't2')).toHaveLength(1);
        });

        it('a többi hivatkozást nem viszi magával', () => {
            handlePrLinked(pr());
            handlePrLinked(pr({ id: 'pr2', prNumber: 43 }));

            handlePrLinked(pr({ taskId: 't2' }));

            const prs = get(gitLinkStore).prs;
            expect(linksForTask(prs, 't1').map(p => p.id)).toEqual(['pr2']);
            expect(linksForTask(prs, 't2').map(p => p.id)).toEqual(['pr1']);
        });
    });

    describe('a helyőrző sor előléptetése', () => {
        it('a kapcsolt sor megérkezésekor a hozzárendeletlen párja eltűnik', () => {
            // EZ A LÉNYEGI ÁLLÍTÁS. A szerver ilyenkor TÖRLI a helyőrzőt, és erről nincs külön
            // esemény - a kapcsolt sor payloadja hordozza a sha-t, tehát a párja abból
            // azonosítható. Enélkül ugyanaz a commit egyszerre látszana a hozzárendeletlen
            // listában és a task alatt.
            setGitLinks([commit({ id: 'cm-placeholder', taskId: null, taskKey: null })], []);

            handleCommitLinked(commit({ id: 'cm1', taskId: 't1' }));

            const commits = get(gitLinkStore).commits;
            expect(commits).toHaveLength(1);
            expect(commits[0].id).toBe('cm1');
        });

        it('pull requestnél a PR száma azonosítja a párt', () => {
            setGitLinks([], [pr({ id: 'pr-placeholder', taskId: null, taskKey: null })]);

            handlePrLinked(pr({ id: 'pr1', taskId: 't1' }));

            const prs = get(gitLinkStore).prs;
            expect(prs).toHaveLength(1);
            expect(prs[0].id).toBe('pr1');
        });

        it('MÁS sha hozzárendeletlen sorát nem veszi ki', () => {
            //A szűrés tárgyra megy, nem "minden hozzárendeletlenre": egy másik commit
            //helyőrzője nem tűnhet el csak azért, mert egy harmadik illeszkedett.
            setGitLinks([commit({ id: 'other', commitSha: 'ffff999', taskId: null })], []);

            handleCommitLinked(commit({ id: 'cm1', taskId: 't1' }));

            expect(get(gitLinkStore).commits.map(c => c.id).sort()).toEqual(['cm1', 'other']);
        });

        it('hozzárendeletlen esemény nem vesz ki semmit', () => {
            //Csak az előléptetés takarít. Enélkül egy hozzárendeletlen frissítés (például egy
            //force push üzenet-átírása) kivenné a saját kapcsolt párját.
            setGitLinks([commit({ id: 'cm1', taskId: 't1' })], []);

            handleCommitLinked(commit({ id: 'cm-placeholder', taskId: null, taskKey: null }));

            expect(get(gitLinkStore).commits).toHaveLength(2);
        });

        it('a hozzárendelt sorok közül egyiket sem veszi ki', () => {
            //Egy commit üzenete több task kulcsát is tartalmazhatja, és a webhook ilyenkor
            //TÖBB sort hoz létre. A második sor megérkezése nem viheti el az elsőt - pedig
            //ugyanaz a sha-juk.
            handleCommitLinked(commit({ id: 'cm1', taskId: 't1' }));

            handleCommitLinked(commit({ id: 'cm2', taskId: 't2' }));

            expect(get(gitLinkStore).commits).toHaveLength(2);
        });
    });

    describe('linksForTask', () => {
        it('csak az adott feladat hivatkozásait adja', () => {
            setGitLinks([
                commit({ id: 'cm1', taskId: 't1' }),
                commit({ id: 'cm2', taskId: 't2' }),
                commit({ id: 'cm3', taskId: null })
            ], []);

            expect(linksForTask(get(gitLinkStore).commits, 't1').map(c => c.id)).toEqual(['cm1']);
        });

        it('a hozzárendeletlen sorokat egyetlen feladathoz sem adja', () => {
            setGitLinks([commit({ id: 'cm3', taskId: null })], []);

            expect(linksForTask(get(gitLinkStore).commits, 't1')).toHaveLength(0);
        });
    });
});
