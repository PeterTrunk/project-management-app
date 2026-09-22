import { describe, it, expect } from 'vitest';
import type { LinkedCommitResponse, LinkedPrResponse } from '../api/gitApi';
import { filterCommitLinks, filterPrLinks } from './gitLinks';

/**
 * A keresés fő használati esete: egy rossz task kulccsal beillesztett commitot keresünk,
 * és tudjuk, hova került tévedésből. Ezért a **task kulcs** ugyanolyan fontos keresési mező,
 * mint a sha vagy az üzenet.
 */

function commit(overrides: Partial<LinkedCommitResponse> = {}): LinkedCommitResponse {
    return {
        id: 'c1',
        commitSha: 'aabbccddeeff00112233445566778899aabbccdd',
        commitUrl: null,
        message: 'PMA-1 hibajavítás',
        authorName: 'Teszt Elek',
        committedAt: '2026-09-22T12:00:00Z',
        isManuallyLinked: false,
        taskId: 't1',
        taskKey: 'PMA-1',
        taskTitle: 'Első task',
        ...overrides
    };
}

function pr(overrides: Partial<LinkedPrResponse> = {}): LinkedPrResponse {
    return {
        id: 'p1',
        prNumber: 42,
        prUrl: null,
        title: 'PMA-1 hibajavítás',
        state: 'open',
        authorName: 'Teszt Elek',
        createdAt: '2026-09-22T12:00:00Z',
        mergedAt: null,
        isManuallyLinked: false,
        taskId: 't1',
        taskKey: 'PMA-1',
        taskTitle: 'Első task',
        ...overrides
    };
}

describe('filterCommitLinks', () => {
    const links = [
        commit({ id: 'c1', commitSha: 'aaaa111', message: 'PMA-1 javítás', taskKey: 'PMA-1' }),
        commit({ id: 'c2', commitSha: 'bbbb222', message: 'Refaktor', authorName: 'Másik Elek', taskKey: 'PMA-2' }),
        commit({ id: 'c3', commitSha: 'cccc333', message: 'Elfelejtett kulcs', taskId: null, taskKey: null, taskTitle: null })
    ];

    it('üres keresésre mindent visszaad', () => {
        expect(filterCommitLinks(links, '')).toHaveLength(3);
        expect(filterCommitLinks(links, '   ')).toHaveLength(3);
    });

    it('sha eleje szerint keres', () => {
        expect(filterCommitLinks(links, 'bbbb').map(l => l.id)).toEqual(['c2']);
    });

    it('üzenet szerint keres', () => {
        expect(filterCommitLinks(links, 'refaktor').map(l => l.id)).toEqual(['c2']);
    });

    it('szerző szerint keres', () => {
        expect(filterCommitLinks(links, 'Másik').map(l => l.id)).toEqual(['c2']);
    });

    //Ez a keresés lényege: tudom, melyik rossz taskra került, azt keresem
    it('task kulcs szerint keres', () => {
        expect(filterCommitLinks(links, 'PMA-2').map(l => l.id)).toEqual(['c2']);
    });

    it('nem érzékeny a kis- és nagybetűre', () => {
        expect(filterCommitLinks(links, 'pma-2').map(l => l.id)).toEqual(['c2']);
        expect(filterCommitLinks(links, 'REFAKTOR').map(l => l.id)).toEqual(['c2']);
    });

    //A hozzárendeletlen sorokon a task mezők null-ok: a szűrés nem hasalhat el rajtuk
    it('nem száll el a task nélküli hivatkozáson', () => {
        expect(filterCommitLinks(links, 'kulcs').map(l => l.id)).toEqual(['c3']);
        expect(filterCommitLinks(links, 'PMA').map(l => l.id)).toEqual(['c1', 'c2']);
    });

    it('találat nélkül üres listát ad', () => {
        expect(filterCommitLinks(links, 'nincs ilyen')).toEqual([]);
    });

    it('több találatot is visszaad', () => {
        expect(filterCommitLinks(links, 'Teszt Elek').map(l => l.id)).toEqual(['c1', 'c3']);
    });
});

describe('filterPrLinks', () => {
    const links = [
        pr({ id: 'p1', prNumber: 42, title: 'PMA-1 javítás', taskKey: 'PMA-1' }),
        pr({ id: 'p2', prNumber: 7, title: 'Refaktor', authorName: 'Másik Elek', taskKey: 'PMA-2' }),
        pr({ id: 'p3', prNumber: 13, title: 'Kulcs nélküli', taskId: null, taskKey: null, taskTitle: null })
    ];

    it('üres keresésre mindent visszaad', () => {
        expect(filterPrLinks(links, '')).toHaveLength(3);
    });

    it('cím szerint keres', () => {
        expect(filterPrLinks(links, 'refaktor').map(l => l.id)).toEqual(['p2']);
    });

    it('task kulcs szerint keres', () => {
        expect(filterPrLinks(links, 'PMA-2').map(l => l.id)).toEqual(['p2']);
    });

    it('sorszám szerint keres', () => {
        expect(filterPrLinks(links, '42').map(l => l.id)).toEqual(['p1']);
    });

    //A felületen "#42" alakban látszik: amit a felhasználó lát, azt is vissza tudja másolni
    it('a kettőskereszttel beírt sorszámot is megtalálja', () => {
        expect(filterPrLinks(links, '#42').map(l => l.id)).toEqual(['p1']);
        expect(filterPrLinks(links, '#7').map(l => l.id)).toEqual(['p2']);
    });

    //Önmagában a kettőskereszt nem szűkít: minden sorszám tartalmazza az üres részsztringet
    it('a magányos kettőskereszt nem ad hamis találatot', () => {
        expect(filterPrLinks(links, '#')).toEqual([]);
    });

    it('nem száll el a task nélküli hivatkozáson', () => {
        expect(filterPrLinks(links, 'kulcs').map(l => l.id)).toEqual(['p3']);
    });

    it('találat nélkül üres listát ad', () => {
        expect(filterPrLinks(links, 'nincs ilyen')).toEqual([]);
    });
});
