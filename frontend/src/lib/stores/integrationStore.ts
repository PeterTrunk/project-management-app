import { writable } from 'svelte/store';
import type { IntegrationResponse } from '../api/integrationApi';
import { insertIfAbsentBy } from './collections';

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
        //A gárda KIHAGY, és nem felülír: a `webhookToken` itt szándékosan üres, mert az
        //esemény nem hordozza. Ha az integráció már a tárolóban van, a felülírás egy valódi
        //tokent cserélne üres sztringre.
        integrations: insertIfAbsentBy(state.integrations, {
            ...payload,
            id: payload.integrationId,
            webhookToken: '',
            updatedAt: payload.createdAt
        } as unknown as IntegrationResponse, i => i.id)
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