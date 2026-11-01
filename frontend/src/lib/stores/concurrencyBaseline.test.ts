import { describe, it, expect, beforeEach } from 'vitest';
import { get } from 'svelte/store';
import {
    taskStore, setTasks, setActiveTask, clearTask,
    handleTaskUpdated, handleTaskMoved, handleTasksRebalanced
} from './taskStore';
import type { TaskResponse } from '../api/taskApi';

function task(id: string, rowVersion: number, overrides: Partial<TaskResponse> = {}) {
    return {
        id,
        projectId: 'p1',
        boardId: 'b1',
        columnId: 'c1',
        sprintId: null,
        taskKey: `PMA-${id}`,
        title: `Task ${id}`,
        description: null,
        status: 'todo',
        priority: null,
        position: 'i',
        estimateInMinutes: null,
        assigneeIds: [],
        labelIds: [],
        commitLinks: [],
        prLinks: [],
        attachments: [],
        createdByName: '',
        dueDate: null,
        closedAt: null,
        completedAt: null,
        createdAt: '2026-01-01T00:00:00Z',
        updatedAt: null,
        rowVersion,
        ...overrides
    } as unknown as TaskResponse;
}

// A szerkesztő a MEGJELENÍTÉST a store-ból veszi, a mentéskor küldött tokent viszont a saját
// alapvonalából. Ez a két dolog szándékosan külön van: ha a SignalR-frissítés a tokent is
// átírná, a mentés mindig sikerülne, és a másik felhasználó módosítása némán felülíródna.
//
// Ezek a tesztek a store oldalát rögzítik: a friss token ELJUT az aktív elemig (hogy a
// felületnek legyen mihez hasonlítania), de a token léptetése önmagában nem old fel semmit.
describe('a konkurencia-alapvonal és a store viszonya', () => {
    beforeEach(() => clearTask());

    describe('handleTaskUpdated', () => {
        it('a friss tokent az AKTÍV elemre is átvezeti', () => {
            // Korábban csak a listára vezette át, ezért egy nyitott részletnézet nem mutatta
            // a másik felhasználó módosítását, és a felhasználó nem tudta, miért bukik a mentés.
            setTasks([task('1', 10)]);
            setActiveTask(task('1', 10));

            handleTaskUpdated({ taskId: '1', title: 'Átírt cím', rowVersion: 11 });

            const state = get(taskStore);
            expect(state.tasks[0].rowVersion).toBe(11);
            expect(state.activeTask?.rowVersion).toBe(11);
            expect(state.activeTask?.title).toBe('Átírt cím');
        });

        it('token nélküli payload NEM írja felül a meglévő tokent', () => {
            // A spread (`{...t, ...rest}`) korábban undefined-ra tudta volna állítani.
            setTasks([task('1', 10)]);
            setActiveTask(task('1', 10));

            handleTaskUpdated({ taskId: '1', title: 'Csak cím' });

            expect(get(taskStore).tasks[0].rowVersion).toBe(10);
            expect(get(taskStore).activeTask?.rowVersion).toBe(10);
        });

        it('más feladat módosítása az aktív elemet nem érinti', () => {
            setTasks([task('1', 10), task('2', 20)]);
            setActiveTask(task('1', 10));

            handleTaskUpdated({ taskId: '2', title: 'Másik', rowVersion: 21 });

            expect(get(taskStore).activeTask?.id).toBe('1');
            expect(get(taskStore).activeTask?.rowVersion).toBe(10);
        });
    });

    describe('handleTaskMoved', () => {
        it('az AKTÍV elemet is követi, nem csak a listát', () => {
            // Ez volt a hiba: a board a nyitott részletnézet mögött frissült, a fejléc
            // (board - státusz sor) viszont befagyott, mert az aktív elem nem követte.
            setTasks([task('1', 10, { columnId: 'c1', status: 'In Progress' })]);
            setActiveTask(task('1', 10, { columnId: 'c1', status: 'In Progress' }));

            handleTaskMoved({
                taskId: '1',
                boardId: 'b1',
                columnId: 'c2',
                sprintId: null,
                position: 'q',
                completedAt: null,
                status: 'To Do',
                rowVersion: 11
            });

            const state = get(taskStore);
            expect(state.activeTask?.columnId).toBe('c2');
            expect(state.activeTask?.status).toBe('To Do');
            expect(state.activeTask?.rowVersion).toBe(11);
        });

        it('status nélküli payload NEM írja felül a meglévő státuszt', () => {
            setTasks([task('1', 10, { columnId: 'c1', status: 'In Progress' })]);
            setActiveTask(task('1', 10, { columnId: 'c1', status: 'In Progress' }));

            handleTaskMoved({
                taskId: '1', boardId: 'b1', columnId: 'c2', sprintId: null,
                position: 'q', completedAt: null, rowVersion: 11
            });

            expect(get(taskStore).activeTask?.status).toBe('In Progress');
        });
    });

    describe('handleTasksRebalanced', () => {
        it('az újrasorszámozás friss tokenjét az aktív elemre is átvezeti', () => {
            // Ez a rendszer SAJÁT írása: emberi ütközés nélkül lépteti a tokent minden
            // feladatra az oszlopban.
            setTasks([task('1', 10), task('2', 20)]);
            setActiveTask(task('1', 10));

            handleTasksRebalanced({
                boardId: 'b1',
                columnId: 'c1',
                tasks: [
                    { id: '1', position: 'a', rowVersion: 11 },
                    { id: '2', position: 'b', rowVersion: 21 }
                ]
            });

            const state = get(taskStore);
            expect(state.tasks.map(t => t.rowVersion)).toEqual([11, 21]);
            expect(state.activeTask?.rowVersion).toBe(11);
            expect(state.activeTask?.position).toBe('a');
        });
    });

    describe('a védelem lényege', () => {
        it('a store tokenje változik, de ez önmagában nem oldja fel az ütközést', () => {
            // A szerkesztő alapvonala a komponensben él, és NEM követi a store-t. Itt azt
            // rögzítjük, hogy a store oldalán tényleg keletkezik eltérés, amire a felület
            // a sávot megjelenítheti: baseline (10) != aktuális (11).
            const baseline = 10;
            setTasks([task('1', baseline)]);
            setActiveTask(task('1', baseline));

            handleTaskUpdated({ taskId: '1', title: 'Közben módosult', rowVersion: 11 });

            const current = get(taskStore).activeTask!.rowVersion;
            expect(current).not.toBe(baseline);
        });
    });
});
