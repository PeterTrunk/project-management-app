import { writable } from 'svelte/store';

import type { ProjectResponse } from '../api/projectApi';

interface ProjectState {
    projects: ProjectResponse[];
    activeProject: ProjectResponse | null;
}

const initialState: ProjectState = {
    projects: [],
    activeProject: null
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