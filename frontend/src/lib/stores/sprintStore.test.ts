import { describe, it, expect, beforeEach } from 'vitest';
import { get } from 'svelte/store';
import type { SprintResponse } from '../api/sprintApi';
import {
    sprintStore,
    setSprints,
    clearSprints,
    handleSprintCreated,
    handleSprintUpdated,
    handleSprintDeleted
} from './sprintStore';

/**
 * A store handlerek a SignalR eseményekből frissítik a kliens állapotát.
 *
 * Azért van értelme tesztelni őket, mert itt NINCS backend háló: 
 * ha egy handler rossz sorra ír vagy nem törli a listából az elemet, a szerver adata helyes marad,
 * a felhasználó mégis hibás felületet lát valós időben. 
 * Egy ilyen hibát csak úgy lehet észrevenni, ha valaki éppen nézi a képernyőt.
 */

function sprint(id: string, overrides: Partial<SprintResponse> = {}): SprintResponse {
    return {
        id,
        projectId: 'p1',
        name: `Sprint ${id}`,
        goal: null,
        startDate: null,
        endDate: null,
        state: 'Planning',
        createdAt: '2026-01-01T00:00:00Z',
        updatedAt: '2026-01-01T00:00:00Z',
        rowVersion: 1,
        ...overrides
    } as SprintResponse;
}

describe('sprintStore', () => {
    beforeEach(() => clearSprints());

    describe('setSprints', () => {
        it('kiválasztja az aktív sprintet a listából', () => {
            setSprints([sprint('a'), sprint('b', { state: 'Active' })]);

            expect(get(sprintStore).activeSprint?.id).toBe('b');
        });

        it('null az aktív sprint, ha egyik sem aktív', () => {
            setSprints([sprint('a'), sprint('b')]);

            expect(get(sprintStore).activeSprint).toBeNull();
        });
    });

    describe('handleSprintCreated', () => {
        it('hozzáfűzi az új sprintet a meglévők után', () => {
            setSprints([sprint('a')]);

            handleSprintCreated({
                id: 'b',
                name: 'Új sprint',
                goal: null,
                state: 'Planning',
                startDate: null,
                endDate: null,
                createdAt: '2026-02-01T00:00:00Z'
            });

            expect(get(sprintStore).sprints.map(s => s.id)).toEqual(['a', 'b']);
        });
    });

    describe('handleSprintUpdated', () => {
        it('csak a megnevezett sprintet módosítja', () => {
            setSprints([sprint('a'), sprint('b')]);

            handleSprintUpdated({ sprintId: 'a', name: 'Átnevezve' });

            const state = get(sprintStore);
            expect(state.sprints.find(s => s.id === 'a')!.name).toBe('Átnevezve');
            expect(state.sprints.find(s => s.id === 'b')!.name).toBe('Sprint b');
        });

        it('csak a küldött mezőket írja felül, a többit megtartja', () => {
            setSprints([sprint('a', { goal: 'Eredeti cél' })]);

            handleSprintUpdated({ sprintId: 'a', name: 'Új név' });

            const updated = get(sprintStore).sprints[0];
            expect(updated.name).toBe('Új név');
            expect(updated.goal).toBe('Eredeti cél');
        });

        //Az aktív sprint SZÁRMAZTATOTT állapot: a state mezőből következik, 
        //nem külön eseményből. Ha ez elromlik, a sprint nézet néma marad egy aktiválás után.
        it('átveszi az aktív sprintet, ha egy másik lett aktív', () => {
            setSprints([sprint('a', { state: 'Active' }), sprint('b')]);

            handleSprintUpdated({ sprintId: 'a', state: 'Completed' });
            handleSprintUpdated({ sprintId: 'b', state: 'Active' });

            expect(get(sprintStore).activeSprint?.id).toBe('b');
        });

        it('null-ra állítja az aktív sprintet, ha lezárták', () => {
            setSprints([sprint('a', { state: 'Active' })]);

            handleSprintUpdated({ sprintId: 'a', state: 'Completed' });

            expect(get(sprintStore).activeSprint).toBeNull();
        });

        it('nem csinál semmit ismeretlen sprintre', () => {
            setSprints([sprint('a')]);

            handleSprintUpdated({ sprintId: 'nincs-ilyen', name: 'Sehol' });

            expect(get(sprintStore).sprints).toHaveLength(1);
            expect(get(sprintStore).sprints[0].name).toBe('Sprint a');
        });
    });

    describe('handleSprintDeleted', () => {
        it('kiveszi a sprintet a listából', () => {
            setSprints([sprint('a'), sprint('b')]);

            handleSprintDeleted({ sprintId: 'a' });

            expect(get(sprintStore).sprints.map(s => s.id)).toEqual(['b']);
        });

        //Egy törölt aktív sprint után nem maradhat mutogató hivatkozás
        it('törli az aktív hivatkozást is, ha az aktív sprintet törölték', () => {
            setSprints([sprint('a', { state: 'Active' })]);

            handleSprintDeleted({ sprintId: 'a' });

            expect(get(sprintStore).activeSprint).toBeNull();
        });

        it('ismeretlen azonosítóra nem változtat semmit', () => {
            setSprints([sprint('a'), sprint('b')]);

            handleSprintDeleted({ sprintId: 'nincs-ilyen' });

            expect(get(sprintStore).sprints).toHaveLength(2);
        });
    });
});
