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

// SignalR handle metódusok

export function handleProjectUpdated(payload: {
    projectId: string;
    name: string;
    description: string | null;
}) {
    projectStore.update(state => ({
        ...state,
        projects: state.projects.map(p =>
            p.id === payload.projectId
                ? { ...p, name: payload.name, description: payload.description }
                : p
        ),
        activeProject: state.activeProject?.id === payload.projectId
            ? { ...state.activeProject, name: payload.name, description: payload.description }
            : state.activeProject
    }));
}

export function handleProjectArchived(payload: { projectId: string }) {
    projectStore.update(state => ({
        ...state,
        projects: state.projects.map(p =>
            p.id === payload.projectId ? { ...p, isArchived: true } : p
        ),
        activeProject: state.activeProject?.id === payload.projectId
            ? { ...state.activeProject, isArchived: true }
            : state.activeProject
    }));
}

export function handleProjectUnarchived(payload: { projectId: string }) {
    projectStore.update(state => ({
        ...state,
        projects: state.projects.map(p =>
            p.id === payload.projectId ? { ...p, isArchived: false } : p
        ),
        activeProject: state.activeProject?.id === payload.projectId
            ? { ...state.activeProject, isArchived: false }
            : state.activeProject
    }));
}

export function handleProjectDeleted(payload: { projectId: string }) {
    projectStore.update(state => ({
        ...state,
        projects: state.projects.filter(p => p.id !== payload.projectId),
        activeProject: state.activeProject?.id === payload.projectId
            ? null
            : state.activeProject
    }));
}

export function handleLabelCreated(payload: {
    id: string;
    name: string;
    color: string;
}) {
    projectStore.update(state => ({
        ...state,
        labels: [...state.labels, payload as unknown as LabelResponse]
    }));
}

export function handleLabelDeleted(payload: { labelId: string }) {
    projectStore.update(state => ({
        ...state,
        labels: state.labels.filter(l => l.id !== payload.labelId)
    }));
}