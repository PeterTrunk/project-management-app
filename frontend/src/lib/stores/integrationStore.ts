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