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
    handleTaskLabelRemoved,
    handleCommitLinked,
    handlePrLinked,
    handleAttachmentUploaded,
    handleAttachmentDeleted
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
                completedAt: null
            });

            expect(get(taskStore).tasks.map(t => t.id)).toEqual(['1', '2']);
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

    describe('git hivatkozások', () => {
        //A payload alakja megegyezik a REST válasz DTO-jával, plusz a taskId - ez a
        //backenddel kötött szerződés, nem a store kényelmi alakja
        const commitPayload = {
            taskId: '1',
            id: 'cm1',
            commitSha: 'abc1234def',
            commitUrl: 'https://example.com/commit/abc1234',
            message: 'PMA-1 hibajavítás',
            authorName: 'Teszt Elek',
            committedAt: '2026-09-16T12:00:00Z'
        };

        const prPayload = {
            taskId: '1',
            id: 'pr1',
            prNumber: 42,
            prUrl: 'https://example.com/pr/42',
            title: 'PMA-1 hibajavítás',
            state: 'open',
            authorName: 'Teszt Elek',
            createdAt: '2026-09-16T12:00:00Z',
            mergedAt: null
        };

        it('hozzáfűzi a commitot a taskhoz', () => {
            setTasks([task('1')]);

            handleCommitLinked(commitPayload);

            const [commit] = get(taskStore).tasks[0].commitLinks;
            expect(commit.id).toBe('cm1');
            expect(commit.commitSha).toBe('abc1234def');
            //A taskId a címzés, nem a kártya adata: nem maradhat benne
            expect(commit).not.toHaveProperty('taskId');
        });

        it('hozzáfűzi a pull requestet a taskhoz', () => {
            setTasks([task('1')]);

            handlePrLinked(prPayload);

            const [pr] = get(taskStore).tasks[0].prLinks;
            expect(pr.id).toBe('pr1');
            expect(pr.prNumber).toBe(42);
            expect(pr).not.toHaveProperty('taskId');
        });

        //Egy pull request állapota többször is változik (megnyitás, szerkesztés, merge), 
        //és a backend mindannyiszor elküldi a friss sort. 
        //Vak hozzáfűzéssel ugyanaz a kártya többször jelenne meg azonos kulccsal,
        //amitől a Svelte each blokkja hibát dob.
        it('nem duplikálja a pull requestet ismételt eseményre, hanem frissíti', () => {
            setTasks([task('1')]);

            handlePrLinked(prPayload);
            handlePrLinked({ ...prPayload, state: 'merged', mergedAt: '2026-09-16T13:00:00Z' });

            const prLinks = get(taskStore).tasks[0].prLinks;
            expect(prLinks).toHaveLength(1);
            expect(prLinks[0].state).toBe('merged');
            expect(prLinks[0].mergedAt).toBe('2026-09-16T13:00:00Z');
        });

        it('nem duplikálja a commitot ismételt eseményre', () => {
            setTasks([task('1')]);

            handleCommitLinked(commitPayload);
            handleCommitLinked({ ...commitPayload, message: 'PMA-1 javított üzenet' });

            const commitLinks = get(taskStore).tasks[0].commitLinks;
            expect(commitLinks).toHaveLength(1);
            expect(commitLinks[0].message).toBe('PMA-1 javított üzenet');
        });

        it('a különböző azonosítójú hivatkozások egymás mellé kerülnek', () => {
            setTasks([task('1')]);

            handlePrLinked(prPayload);
            handlePrLinked({ ...prPayload, id: 'pr2', prNumber: 43 });

            expect(get(taskStore).tasks[0].prLinks.map(p => p.id)).toEqual(['pr1', 'pr2']);
        });

        //A megnyitott részletnézet külön hivatkozáson ül: enélkül a modálban nem jelenne meg
        //az élőben érkező commit vagy PR
        it('a megnyitott taskot is frissíti', () => {
            setTasks([task('1')]);
            setActiveTask(task('1'));

            handlePrLinked(prPayload);
            handleCommitLinked(commitPayload);

            const active = get(taskStore).activeTask!;
            expect(active.prLinks).toHaveLength(1);
            expect(active.commitLinks).toHaveLength(1);
        });

        it('nem nyúl a megnyitott taskhoz, ha másik taskhoz érkezett a hivatkozás', () => {
            setTasks([task('1'), task('2')]);
            setActiveTask(task('1'));

            handlePrLinked({ ...prPayload, taskId: '2' });

            expect(get(taskStore).activeTask!.prLinks).toHaveLength(0);
        });

        //Egy hivatkozás az adatbázisban EGY sor, egy TaskId-vel. Ha megjelenik a másik task
        //alatt, akkor az előzőről el is kell tűnnie - különben egy áthelyezés duplikálásnak
        //látszana az oldal újratöltéséig.
        describe('áthelyezés másik taskra', () => {
            it('a pull request elkerül a korábbi taskról', () => {
                setTasks([task('1'), task('2')]);
                handlePrLinked(prPayload);

                handlePrLinked({ ...prPayload, taskId: '2' });

                const state = get(taskStore);
                expect(state.tasks.find(t => t.id === '1')!.prLinks).toHaveLength(0);
                expect(state.tasks.find(t => t.id === '2')!.prLinks).toHaveLength(1);
            });

            it('a commit elkerül a korábbi taskról', () => {
                setTasks([task('1'), task('2')]);
                handleCommitLinked(commitPayload);

                handleCommitLinked({ ...commitPayload, taskId: '2' });

                const state = get(taskStore);
                expect(state.tasks.find(t => t.id === '1')!.commitLinks).toHaveLength(0);
                expect(state.tasks.find(t => t.id === '2')!.commitLinks).toHaveLength(1);
            });

            //A nyitott részletnézet is követi az elvitelt, nem csak az érkezést
            it('a megnyitott taskról is eltűnik', () => {
                setTasks([task('1'), task('2')]);
                handlePrLinked(prPayload);
                setActiveTask(get(taskStore).tasks.find(t => t.id === '1')!);

                handlePrLinked({ ...prPayload, taskId: '2' });

                expect(get(taskStore).activeTask!.prLinks).toHaveLength(0);
            });

            it('a többi hivatkozást nem viszi magával', () => {
                setTasks([task('1'), task('2')]);
                handlePrLinked(prPayload);
                handlePrLinked({ ...prPayload, id: 'pr2', prNumber: 43 });

                handlePrLinked({ ...prPayload, taskId: '2' });

                const state = get(taskStore);
                expect(state.tasks.find(t => t.id === '1')!.prLinks.map(p => p.id)).toEqual(['pr2']);
                expect(state.tasks.find(t => t.id === '2')!.prLinks.map(p => p.id)).toEqual(['pr1']);
            });

            //Az érintetlen taskok hivatkozása maradjon ugyanaz az objektum: a felesleges
            //újrarenderelést ez akadályozza meg
            it('nem írja újra az érintetlen taskokat', () => {
                setTasks([task('1'), task('2'), task('3')]);
                const before = get(taskStore).tasks.find(t => t.id === '3')!;

                handlePrLinked(prPayload);

                expect(get(taskStore).tasks.find(t => t.id === '3')).toBe(before);
            });
        });
    });

    describe('csatolmányok', () => {
        const uploadPayload = {
            attachmentId: 'a1', projectId: 'p1', taskId: '1',
            fileName: 'terv.pdf', contentType: 'application/pdf', sizeBytes: 1024,
            attachmentType: 'task', uploadedByName: 'Teszt Elek', version: 1,
            createdAt: '2026-01-01T00:00:00Z'
        };

        it('hozzáadja a csatolmányt', () => {
            setTasks([task('1')]);

            handleAttachmentUploaded(uploadPayload);

            expect(get(taskStore).tasks[0].attachments).toHaveLength(1);
        });

        it('nem duplikálja ugyanazt a csatolmányt', () => {
            setTasks([task('1')]);

            handleAttachmentUploaded(uploadPayload);
            handleAttachmentUploaded(uploadPayload);

            expect(get(taskStore).tasks[0].attachments).toHaveLength(1);
        });

        //A projekt szintű feltöltésnek nincs taskId-ja: a handlernek némán ki kell lépnie,
        //nem pedig minden taskra ráaggatnia a fájlt
        it('taskId nélküli feltöltésnél nem nyúl semmihez', () => {
            setTasks([task('1')]);

            handleAttachmentUploaded({ ...uploadPayload, taskId: null });

            expect(get(taskStore).tasks[0].attachments).toHaveLength(0);
        });

        it('törli a csatolmányt', () => {
            setTasks([task('1')]);
            handleAttachmentUploaded(uploadPayload);

            handleAttachmentDeleted({ attachmentId: 'a1', projectId: 'p1', taskId: '1' });

            expect(get(taskStore).tasks[0].attachments).toHaveLength(0);
        });

        it('a megnyitott taskot is frissíti feltöltéskor', () => {
            setTasks([task('1')]);
            setActiveTask(task('1'));

            handleAttachmentUploaded(uploadPayload);

            expect(get(taskStore).activeTask!.attachments).toHaveLength(1);
        });
    });
});
