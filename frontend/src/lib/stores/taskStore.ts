import { writable, get } from 'svelte/store';
import type { TaskResponse } from '../api/taskApi';
import { projectStore } from './projectStore';

interface TaskState {
    tasks: TaskResponse[];
    activeTask: TaskResponse | null;
}

const initialState: TaskState = {
    tasks: [],
    activeTask: null
}

export const taskStore = writable<TaskState>(initialState);

export function setTasks(tasks: TaskResponse[]) {
    taskStore.update(state => ({ ...state, tasks}));
}

export function setActiveTask(task: TaskResponse | null) {
    taskStore.update(state => ({ ...state, activeTask: task}))
}

export function clearTask() {
    taskStore.set(initialState);
}

// SignalR handle metódusok

export function handleTaskCreated(payload: {
    id: string;
    boardId: string | null;
    columnId: string | null;
    sprintId: string | null;
    taskKey: string;
    title: string;
    priority: string | null;
    dueDate: string | null;
    estimateInMinutes: number | null;
    position: string;
    createdAt: string;
    completedAt: string | null;
}) {
    const projectId = get(projectStore).activeProject?.id ?? '';
    taskStore.update(state => ({
        ...state,
        tasks: [...state.tasks, {
            ...payload,
            projectId,
            description: null,
            status: '',
            assigneeIds: [],
            labelIds: [],
            commitLinks: [],
            prLinks: [],
            attachments: [],
            createdByName: '',
            closedAt: null,
            updatedAt: null
        } as unknown as TaskResponse]
    }));
}

export function handleTaskUpdated(payload: {
    taskId: string;
    title?: string;
    description?: string;
    priority?: string;
    dueDate?: string | null;
    estimateInMinutes?: number | null;
    boardId?: string | null;
    columnId?: string | null;
    position?: string;
    sprintId?: string | null;
}) {
    const { taskId, ...rest } = payload;
    taskStore.update(state => ({
        ...state,
        tasks: state.tasks.map(t =>
            t.id === taskId
                ? { ...t, ...rest } as TaskResponse
                : t
        )
    }));
}

export function handleTaskMoved(payload: {
    taskId: string;
    boardId: string | null;
    columnId: string | null;
    sprintId: string | null;
    position: string;
    completedAt: string | null;
}) {
    taskStore.update(state => ({
        ...state,
        tasks: state.tasks.map(t =>
            t.id === payload.taskId
                ? { ...t,
                    boardId: payload.boardId,
                    columnId: payload.columnId ?? t.columnId,
                    sprintId: payload.sprintId,
                    position: payload.position,
                    completedAt: payload.completedAt ? new Date(payload.completedAt) : null
                  } as unknown as TaskResponse
                : t
        )
    }));
}

export function handleTaskDeleted(payload: { taskId: string }) {
    taskStore.update(state => ({
        ...state,
        tasks: state.tasks.filter(t => t.id !== payload.taskId)
    }));
}

export function handleTasksRebalanced(payload: {
    boardId: string;
    columnId: string;
    tasks: { id: string; position: string }[];
}) {
    taskStore.update(state => ({
        ...state,
        tasks: state.tasks.map(t => {
            const rebalanced = payload.tasks.find(r => r.id === t.id);
            return rebalanced ? { ...t, position: rebalanced.position } : t;
        })
    }));
}

export function handleTaskAssigneeAdded(payload: { taskId: string; userId: string }) {
    taskStore.update(state => ({
        ...state,
        tasks: state.tasks.map(t =>
            t.id === payload.taskId
                ? { ...t, assigneeIds: [...t.assigneeIds, payload.userId] }
                : t
        )
    }));
}

export function handleTaskAssigneeRemoved(payload: { taskId: string; userId: string }) {
    taskStore.update(state => ({
        ...state,
        tasks: state.tasks.map(t =>
            t.id === payload.taskId
                ? { ...t, assigneeIds: t.assigneeIds.filter(id => id !== payload.userId) }
                : t
        )
    }));
}

export function handleTaskLabelAdded(payload: { taskId: string; labelId: string }) {
    taskStore.update(state => ({
        ...state,
        tasks: state.tasks.map(t =>
            t.id === payload.taskId
                ? { ...t, labelIds: [...t.labelIds, payload.labelId] }
                : t
        )
    }));
}

export function handleTaskLabelRemoved(payload: { taskId: string; labelId: string }) {
    taskStore.update(state => ({
        ...state,
        tasks: state.tasks.map(t =>
            t.id === payload.taskId
                ? { ...t, labelIds: t.labelIds.filter(id => id !== payload.labelId) }
                : t
        )
    }));
}