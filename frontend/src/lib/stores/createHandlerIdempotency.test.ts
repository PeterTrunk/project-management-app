import { describe, it, expect, beforeEach } from 'vitest';
import { get } from 'svelte/store';
import { boardStore, setBoards, setColumns, handleBoardCreated, handleColumnCreated } from './boardStore';
import { sprintStore, setSprints, handleSprintCreated } from './sprintStore';
import { teamStore, setMembers, handleMemberAdded } from './teamStore';
import { projectStore, setLabels, handleLabelCreated } from './projectStore';
import { integrationStore, setIntegrations, handleIntegrationCreated } from './integrationStore';
import { taskStore, setTasks, handleTaskCreated } from './taskStore';

/**
 * A létrehozási kezelők idempotenciája.
 *
 * **Miért EZ a tesztosztály, és miért most.** A teljes újratöltés alatt érkező események
 * pufferelése (`signalREventBuffer`) a pillanatkép alkalmazása UTÁN játssza vissza az
 * eseményeket. Egy létrehozási esemény tehát olyan elemre is lefut, amely a pillanatképben MÁR
 * benne volt - gárda nélkül ez nem esetleges, hanem BIZTOS duplikáció, ráadásul azonos
 * kulccsal, amitől a Svelte kulcsolt `each` blokkja hibát dob.
 *
 * Az `insertIfAbsentBy` saját tesztje a segédfüggvényt méri; ez a fájl azt méri, hogy
 * mindegyik kezelő **be is van kötve** rá. A kettő nem ugyanaz: egy kimaradt kezelő a
 * segédfüggvény tesztjeit nem buktatja.
 *
 * Az `addIntegration` szándékosan nincs a listán: nem eseménykezelő, és nincs hívója.
 * A `handleActivityCreated` sem: az aktivitások nem részei egyetlen pufferelt betöltésnek sem,
 * tehát nincs pillanatkép, amivel ütközhetnének.
 */
describe('a létrehozási kezelők nem duplikálnak', () => {
    beforeEach(() => {
        setBoards([]);
        setColumns([]);
        setSprints([]);
        setMembers([]);
        setLabels([]);
        setIntegrations([]);
        setTasks([]);
    });

    it('handleBoardCreated', () => {
        const payload = { id: 'b1', name: 'Board', description: null, isDefault: false };

        handleBoardCreated(payload);
        handleBoardCreated(payload);

        expect(get(boardStore).boards).toHaveLength(1);
    });

    it('handleColumnCreated', () => {
        const payload = {
            id: 'c1', boardId: 'b1', name: 'Todo',
            position: 1, mapsToStatus: 'Todo', wipLimit: null
        };

        handleColumnCreated(payload);
        handleColumnCreated(payload);

        expect(get(boardStore).columns).toHaveLength(1);
    });

    it('handleSprintCreated', () => {
        const payload = {
            id: 's1', name: 'Sprint 1', goal: null, state: 'Planned',
            startDate: null, endDate: null, createdAt: '2026-11-08T00:00:00Z'
        };

        handleSprintCreated(payload);
        handleSprintCreated(payload);

        expect(get(sprintStore).sprints).toHaveLength(1);
    });

    it('handleLabelCreated', () => {
        const payload = { id: 'l1', name: 'bug', color: '#ff0000' };

        handleLabelCreated(payload);
        handleLabelCreated(payload);

        expect(get(projectStore).labels).toHaveLength(1);
    });

    it('handleMemberAdded a userId-t használja kulcsként, nem az id-t', () => {
        //A tagság a (projekt, felhasználó) páron azonosítódik. Egy beégetett `id` kulcs itt
        //némán nem találna egyezést, és a tag kétszer jelenne meg.
        const payload = { userId: 'u1', displayName: 'Teszt Elek', projectRole: 'Member' };

        handleMemberAdded(payload);
        handleMemberAdded(payload);

        expect(get(teamStore).members).toHaveLength(1);
    });

    it('handleIntegrationCreated', () => {
        const payload = {
            integrationId: 'i1', provider: 'GitHub', repoFullName: 'user/repo',
            isEnabled: true, isVerified: false,
            webhookUrl: 'https://example.invalid/hook', createdAt: '2026-11-08T00:00:00Z'
        };

        handleIntegrationCreated(payload);
        handleIntegrationCreated(payload);

        expect(get(integrationStore).integrations).toHaveLength(1);
    });

    it('handleTaskCreated', () => {
        const payload = {
            id: 't1', boardId: 'b1', columnId: 'c1', sprintId: null,
            taskKey: 'PRJ-1', title: 'Feladat', priority: null, dueDate: null,
            estimateInMinutes: null, position: '0|i00000',
            createdAt: '2026-11-08T00:00:00Z', completedAt: null, rowVersion: 42
        };

        handleTaskCreated(payload);
        handleTaskCreated(payload);

        expect(get(taskStore).tasks).toHaveLength(1);
    });

    describe('ütközéskor a pillanatkép adata marad, nem a helykitöltő', () => {
        it('handleTaskCreated nem írja felül a valódi értékeket', () => {
            //EZ AZ INDOKA ANNAK, hogy a gárda KIHAGY és nem felülír. A `TaskCreated`
            //payloadja nem hordozza a leírást, a státuszt, a felelősöket és a címkéket, ezért
            //a kezelő helykitöltőt tesz a helyükre. Egy felülíró gárda tehát a pillanatkép
            //valódi értékeit cserélné üresre - vagyis a duplikáció helyett adatot veszítenénk.
            setTasks([{
                id: 't1',
                title: 'Feladat',
                description: 'valódi leírás',
                status: 'InProgress',
                assigneeIds: ['u1'],
                labelIds: ['l1']
            } as never]);

            handleTaskCreated({
                id: 't1', boardId: 'b1', columnId: 'c1', sprintId: null,
                taskKey: 'PRJ-1', title: 'Feladat', priority: null, dueDate: null,
                estimateInMinutes: null, position: '0|i00000',
                createdAt: '2026-11-08T00:00:00Z', completedAt: null, rowVersion: 42
            });

            const task = get(taskStore).tasks[0] as never as {
                description: string; status: string; assigneeIds: string[]; labelIds: string[];
            };

            expect(get(taskStore).tasks).toHaveLength(1);
            expect(task.description).toBe('valódi leírás');
            expect(task.status).toBe('InProgress');
            expect(task.assigneeIds).toEqual(['u1']);
            expect(task.labelIds).toEqual(['l1']);
        });

        it('handleIntegrationCreated nem nullázza a webhook tokent', () => {
            //Ugyanaz a hibaosztály: az esemény nem hordozza a tokent, ezért a kezelő üres
            //sztringet tesz be. Felülírással egy valódi token tűnne el.
            setIntegrations([{ id: 'i1', webhookToken: 'valodi-token' } as never]);

            handleIntegrationCreated({
                integrationId: 'i1', provider: 'GitHub', repoFullName: 'user/repo',
                isEnabled: true, isVerified: false,
                webhookUrl: 'https://example.invalid/hook', createdAt: '2026-11-08T00:00:00Z'
            });

            const integration = get(integrationStore).integrations[0] as never as {
                webhookToken: string;
            };

            expect(get(integrationStore).integrations).toHaveLength(1);
            expect(integration.webhookToken).toBe('valodi-token');
        });
    });
});
