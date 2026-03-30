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