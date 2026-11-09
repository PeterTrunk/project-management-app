import { describe, it, expect, beforeEach } from 'vitest';
import { get } from 'svelte/store';
import type { TaskResponse } from '../api/taskApi';
import {
    taskStore,
    setTasks,
    setActiveTask,
    clearTask,
    handleTaskCreated,
    handleTaskUpdated,
    handleTaskMoved,
    handleTaskDeleted,
    handleTasksRebalanced,
    handleTaskAssigneeAdded,
    handleTaskAssigneeRemoved,
    handleTaskLabelAdded,
    handleTaskLabelRemoved
} from './taskStore';

function task(id: string, overrides: Partial<TaskResponse> = {}): TaskResponse {
    return {
        id,
        projectId: 'p1',
        boardId: 'b1',
        columnId: 'c1',
        sprintId: null,
        assigneeIds: [],
        labelIds: [],
        commitLinks: [],
        prLinks: [],
        attachments: [],
        createdByName: 'Teszt Elek',
        taskKey: `PMA-${id}`,
        title: `Task ${id}`,
        description: null,
        status: 'todo',
        priority: null,
        position: 'n',
        estimateInMinutes: null,
        rowVersion: 1,
        dueDate: null,
        closedAt: null,
        completedAt: null,
        createdAt: '2026-01-01T00:00:00Z',
        updatedAt: '2026-01-01T00:00:00Z',
        ...overrides
    } as unknown as TaskResponse;
}

describe('taskStore', () => {
    beforeEach(() => clearTask());

    describe('handleTaskCreated', () => {
        it('hozzáfűzi az új taskot', () => {
            setTasks([task('1')]);

            handleTaskCreated({
                id: '2', boardId: 'b1', columnId: 'c1', sprintId: null,
                taskKey: 'PMA-2', title: 'Új task', priority: null, dueDate: null,
                estimateInMinutes: null, position: 'p', createdAt: '2026-02-01T00:00:00Z',
                completedAt: null, rowVersion: 7
            });

            expect(get(taskStore).tasks.map(t => t.id)).toEqual(['1', '2']);
        });

        it('a konkurencia-tokent is átveszi a payloadból', () => {
            //A szerver küldi, a típus korábban nem deklarálta, és az `as unknown as` cast
            //elnémította. Token nélkül a szerkesztés 400-at kapott volna.
            setTasks([]);

            handleTaskCreated({
                id: '9', boardId: 'b1', columnId: 'c1', sprintId: null,
                taskKey: 'PMA-9', title: 'Token', priority: null, dueDate: null,
                estimateInMinutes: null, position: 'p', createdAt: '2026-02-01T00:00:00Z',
                completedAt: null, rowVersion: 42
            });

            expect(get(taskStore).tasks[0].rowVersion).toBe(42);
        });
    });

    describe('handleTaskUpdated', () => {
        it('csak a megnevezett taskot módosítja', () => {
            setTasks([task('1'), task('2')]);

            handleTaskUpdated({ taskId: '1', title: 'Átnevezve' });

            const state = get(taskStore);
            expect(state.tasks.find(t => t.id === '1')!.title).toBe('Átnevezve');
            expect(state.tasks.find(t => t.id === '2')!.title).toBe('Task 2');
        });

        it('csak a küldött mezőket írja felül', () => {
            setTasks([task('1', { description: 'Eredeti leírás' })]);

            handleTaskUpdated({ taskId: '1', title: 'Új cím' });

            const updated = get(taskStore).tasks[0];
            expect(updated.title).toBe('Új cím');
            expect(updated.description).toBe('Eredeti leírás');
        });

        it('nem csinál semmit ismeretlen taskra', () => {
            setTasks([task('1')]);

            handleTaskUpdated({ taskId: 'nincs-ilyen', title: 'Sehol' });

            expect(get(taskStore).tasks[0].title).toBe('Task 1');
        });
    });

    describe('handleTaskMoved', () => {
        it('átviszi a taskot másik oszlopba, új pozícióval', () => {
            setTasks([task('1')]);

            handleTaskMoved({
                taskId: '1', boardId: 'b2', columnId: 'c2', sprintId: null,
                position: 'z', completedAt: null, rowVersion: 5
            });

            const moved = get(taskStore).tasks[0];
            expect(moved.boardId).toBe('b2');
            expect(moved.columnId).toBe('c2');
            expect(moved.position).toBe('z');
            expect(moved.rowVersion).toBe(5);
        });

        //A backlogba mozgatásnál a columnId null, de a handler ilyenkor szándékosan
        //MEGTARTJA a korábbi oszlopot (?? t.columnId) - ezt rögzítjük szerződésként
        it('null columnId esetén megtartja a korábbi oszlopot', () => {
            setTasks([task('1', { columnId: 'c1' })]);

            handleTaskMoved({
                taskId: '1', boardId: null, columnId: null, sprintId: null,
                position: 'z', completedAt: null
            });

            expect(get(taskStore).tasks[0].columnId).toBe('c1');
        });

        it('dátummá alakítja a lezárás időpontját', () => {
            setTasks([task('1')]);

            handleTaskMoved({
                taskId: '1', boardId: 'b1', columnId: 'c2', sprintId: null,
                position: 'z', completedAt: '2026-03-01T10:00:00Z'
            });

            expect(get(taskStore).tasks[0].completedAt).toBeInstanceOf(Date);
        });
    });

    describe('handleTaskDeleted', () => {
        it('kiveszi a taskot a listából', () => {
            setTasks([task('1'), task('2')]);

            handleTaskDeleted({ taskId: '1' });

            expect(get(taskStore).tasks.map(t => t.id)).toEqual(['2']);
        });

        it('ismeretlen azonosítóra nem változtat semmit', () => {
            setTasks([task('1')]);

            handleTaskDeleted({ taskId: 'nincs-ilyen' });

            expect(get(taskStore).tasks).toHaveLength(1);
        });
    });

    describe('handleTasksRebalanced', () => {
        it('frissíti a felsorolt taskok pozícióját', () => {
            setTasks([task('1'), task('2'), task('3')]);

            handleTasksRebalanced({
                boardId: 'b1', columnId: 'c1',
                tasks: [
                    { id: '1', position: 'aaa', rowVersion: 2 },
                    { id: '3', position: 'ccc', rowVersion: 2 }
                ]
            });

            const state = get(taskStore);
            expect(state.tasks.find(t => t.id === '1')!.position).toBe('aaa');
            expect(state.tasks.find(t => t.id === '3')!.position).toBe('ccc');
        });

        it('érintetlenül hagyja a fel nem sorolt taskokat', () => {
            setTasks([task('1'), task('2')]);

            handleTasksRebalanced({
                boardId: 'b1', columnId: 'c1',
                tasks: [{ id: '1', position: 'aaa', rowVersion: 2 }]
            });

            const untouched = get(taskStore).tasks.find(t => t.id === '2')!;
            expect(untouched.position).toBe('n');
            expect(untouched.rowVersion).toBe(1);
        });
    });

    describe('hozzárendelések', () => {
        it('hozzáadja a felelőst', () => {
            setTasks([task('1')]);

            handleTaskAssigneeAdded({ taskId: '1', userId: 'u1' });

            expect(get(taskStore).tasks[0].assigneeIds).toEqual(['u1']);
        });

        //Ugyanaz az esemény kétszer is megérkezhet (újracsatlakozás, több replika),
        //ezért a handlernek idempotensnek kell lennie
        it('nem duplikálja a felelőst ismételt eseményre', () => {
            setTasks([task('1', { assigneeIds: ['u1'] })]);

            handleTaskAssigneeAdded({ taskId: '1', userId: 'u1' });

            expect(get(taskStore).tasks[0].assigneeIds).toEqual(['u1']);
        });

        it('eltávolítja a felelőst', () => {
            setTasks([task('1', { assigneeIds: ['u1', 'u2'] })]);

            handleTaskAssigneeRemoved({ taskId: '1', userId: 'u1' });

            expect(get(taskStore).tasks[0].assigneeIds).toEqual(['u2']);
        });

        //A megnyitott részletnézet külön hivatkozáson ül: ha nem frissül,
        //a modálban a régi felelősök maradnának, miközben a kártyán már az újak látszanak
        it('a megnyitott taskot is frissíti', () => {
            setTasks([task('1')]);
            setActiveTask(task('1'));

            handleTaskAssigneeAdded({ taskId: '1', userId: 'u1' });

            expect(get(taskStore).activeTask!.assigneeIds).toEqual(['u1']);
        });

        it('nem nyúl a megnyitott taskhoz, ha másikat módosítottak', () => {
            setTasks([task('1'), task('2')]);
            setActiveTask(task('1'));

            handleTaskAssigneeAdded({ taskId: '2', userId: 'u1' });

            expect(get(taskStore).activeTask!.assigneeIds).toEqual([]);
        });
    });

    describe('címkék', () => {
        it('hozzáadja a címkét', () => {
            setTasks([task('1')]);

            handleTaskLabelAdded({ taskId: '1', labelId: 'l1' });

            expect(get(taskStore).tasks[0].labelIds).toEqual(['l1']);
        });

        it('nem duplikálja a címkét ismételt eseményre', () => {
            setTasks([task('1', { labelIds: ['l1'] })]);

            handleTaskLabelAdded({ taskId: '1', labelId: 'l1' });

            expect(get(taskStore).tasks[0].labelIds).toEqual(['l1']);
        });

        it('eltávolítja a címkét', () => {
            setTasks([task('1', { labelIds: ['l1', 'l2'] })]);

            handleTaskLabelRemoved({ taskId: '1', labelId: 'l1' });

            expect(get(taskStore).tasks[0].labelIds).toEqual(['l2']);
        });

        it('a megnyitott taskot is frissíti', () => {
            setTasks([task('1')]);
            setActiveTask(task('1'));

            handleTaskLabelAdded({ taskId: '1', labelId: 'l1' });

            expect(get(taskStore).activeTask!.labelIds).toEqual(['l1']);
        });
    });

    //A git hivatkozások blokkja a `gitLinkStore.test.ts`-be került át, az adattal együtt.
    //Ott az állítások egyszerűbbek: a lapos, azonosító szerint kulcsolt tárolóban az
    //áthelyezés egyszerű csere, nem kézi fel- és levétel két task sorain.

    //A csatolmányok blokkja az `attachmentStore.test.ts`-be került át, az adattal együtt.
    //Ott nincs külön ág a projekt-szintű feltöltésre: a `taskId` egyszerű mező a soron,
    //nem pedig egy feltétel, ami mellett a fájl minden feladatra felkerülhetne.
});
