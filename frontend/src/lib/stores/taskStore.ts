import { writable } from 'svelte/store';
import type { TaskResponse } from '../api/taskApi';

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

export function setActiveTasks(task: TaskResponse | null) {
    taskStore.update(state => ({ ...state, activeTask: task}))
}

export function clearTask() {
    taskStore.set(initialState);
}