import { writable } from 'svelte/store';

import type { ProjectResponse } from '../api/projectApi';
import type { LabelResponse } from '../api/labelApi';

interface ProjectState {
    projects: ProjectResponse[];
    activeProject: ProjectResponse | null;
    labels: LabelResponse[];
}

const initialState: ProjectState = {
    projects: [],
    activeProject: null,
    labels: []
};

export const projectStore = writable<ProjectState>(initialState);

export function setProjects(projects: ProjectResponse[]) {
    projectStore.update(state => ({ ...state, projects }));
}

export function setActiveProject(project: ProjectResponse | null) {
    projectStore.update(state => ({ ...state, activeProject: project }));
}

export function clearProjects() {
    projectStore.set(initialState);
}

export function setLabels(labels: LabelResponse[]) {
    projectStore.update(state => ({ ...state, labels}));
}