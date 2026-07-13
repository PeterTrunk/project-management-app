import { writable } from 'svelte/store';
import type { IntegrationResponse } from '../api/integrationApi';

interface IntegrationState {
    integrations: IntegrationResponse[];
}

const initialState: IntegrationState = {
    integrations: []
};

export const integrationStore = writable<IntegrationState>(initialState);

export function setIntegrations(integrations: IntegrationResponse[]) {
    integrationStore.update(state => ({ ...state, integrations }));
}

export function updateIntegration(updated: IntegrationResponse) {
    integrationStore.update(state => ({
        ...state,
        integrations: state.integrations.map(i => 
            i.id === updated.id ? updated : i
        )
    }));
}

export function addIntegration(integration: IntegrationResponse) {
    integrationStore.update(state => ({
        ...state,
        integrations: [...state.integrations, integration]
    }));
}

export function removeIntegration(integrationId: string) {
    integrationStore.update(state => ({
        ...state,
        integrations: state.integrations.filter(i => i.id !== integrationId)
    }));
}

export function clearIntegrations() {
    integrationStore.set(initialState);
}

// ── SignalR handle metódusok ─────────────────────────────────────

export function handleIntegrationCreated(payload: {
    integrationId: string;
    provider: string;
    repoFullName: string;
    isEnabled: boolean;
    isVerified: boolean;
    webhookUrl: string;
    createdAt: string;
}) {
    integrationStore.update(state => ({
        ...state,
        integrations: [...state.integrations, {
            ...payload,
            id: payload.integrationId,
            webhookToken: '',
            hasAccessToken: false,
            updatedAt: payload.createdAt
        } as unknown as IntegrationResponse]
    }));
}

export function handleIntegrationUpdated(payload: {
    integrationId: string;
    isVerified?: boolean;
    isEnabled?: boolean;
}) {
    integrationStore.update(state => ({
        ...state,
        integrations: state.integrations.map(i =>
            i.id === payload.integrationId
                ? { ...i, ...payload, id: i.id }
                : i
        )
    }));
}

export function handleIntegrationVerified(payload: {
    integrationId: string;
    projectId: string;
}) {
    integrationStore.update(state => ({
        ...state,
        integrations: state.integrations.map(i =>
            i.id === payload.integrationId
                ? { ...i, isVerified: true }
                : i
        )
    }));
}

export function handleIntegrationDeleted(payload: { integrationId: string }) {
    integrationStore.update(state => ({
        ...state,
        integrations: state.integrations.filter(i => i.id !== payload.integrationId)
    }));
}