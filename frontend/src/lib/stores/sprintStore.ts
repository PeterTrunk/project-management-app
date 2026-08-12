import { writable } from 'svelte/store';
import type { SprintResponse } from '../api/sprintApi';

interface SprintState {
    sprints: SprintResponse[];
    activeSprint: SprintResponse | null;
}

const initialState: SprintState = {
    sprints: [],
    activeSprint: null
};

export const sprintStore = writable<SprintState>(initialState);

export function setSprints(sprints: SprintResponse[]) {
    sprintStore.update(state => ({ 
        ...state, 
        sprints,
        activeSprint: sprints.find(s => s.state === 'Active') ?? null
    }));
}

export function clearSprints() {
    sprintStore.set(initialState);
}

// SignalR handle metódusok

export function handleSprintCreated(payload: {
    id: string;
    name: string;
    goal: string | null;
    state: string;
    startDate: string | null;
    endDate: string | null;
    createdAt: string;
}) {
    sprintStore.update(state => ({
        ...state,
        sprints: [...state.sprints, payload as unknown as SprintResponse]
    }));
}

export function handleSprintUpdated(payload: {
    sprintId: string;
    name?: string;
    goal?: string | null;
    state?: string;
    startDate?: string | null;
    endDate?: string | null;
    rowVersion?: number;
}) {
    sprintStore.update(state => {
        const { sprintId, ...rest } = payload;
        const updatedSprints = state.sprints.map(s =>
            s.id === sprintId
                ? { ...s, ...rest } as unknown as SprintResponse
                : s
        );
        return {
            ...state,
            sprints: updatedSprints,
            activeSprint: updatedSprints.find(s => s.state === 'Active') ?? null
        };
    });
}

export function handleSprintDeleted(payload: { sprintId: string }) {
    sprintStore.update(state => {
        const updatedSprints = state.sprints.filter(s => s.id !== payload.sprintId);
        return {
            ...state,
            sprints: updatedSprints,
            activeSprint: updatedSprints.find(s => s.state === 'Active') ?? null
        };
    });
}