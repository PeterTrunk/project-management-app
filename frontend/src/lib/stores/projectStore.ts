import { writable } from 'svelte/store';

interface Project {
    id: string;
    name: string;
    projKey: string;
    description: string | null;
    ownerName: string;
    isArchived: boolean;
    createdAt: Date;
    updatedAt: Date;
}

interface ProjectState {
    projects: Project[];
    activeProject: Project | null;
}

const initialState: ProjectState = {
    projects: [],
    activeProject: null
};

export const projectStore = writable<ProjectState>(initialState);

export function setProjects(projects: Project[]) {
    projectStore.update(state => ({ ...state, projects }));
}

export function setActiveProject(project: Project) {
    projectStore.update(state => ({ ...state, activeProject: project }));
}

export function clearProjects() {
    projectStore.set(initialState);
}