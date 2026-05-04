import apiClient from './client';

export interface IntegrationResponse {
    id: string;
    provider: string;
    repoFullName: string;
    webhookToken: string;
    webhookUrl: string;
    isEnabled: boolean;
    isVerified: boolean;
    hasAccessToken: boolean;
    createdAt: string;
    updatedAt: string;
}

export interface CreateIntegrationRequest {
    provider: string;
    repoFullName: string;
    webhookSecret: string;
    accessToken?: string | null;
}

export async function getIntegrationsAsync(projectId: string): Promise<IntegrationResponse[]> {
    const response = await apiClient.get(`/projects/${projectId}/integrations`);
    return response.data;
}

export async function createIntegrationAsync(projectId: string, data: CreateIntegrationRequest): Promise<IntegrationResponse> {
    const response = await apiClient.post(`/projects/${projectId}/integrations`, data);
    return response.data;
}

export async function deleteIntegrationAsync(projectId: string, integrationId: string): Promise<void> {
    await apiClient.delete(`/projects/${projectId}/integrations/${integrationId}`);
}

export async function regenerateWebhookTokenAsync(projectId: string, integrationId: string): Promise<IntegrationResponse> {
    const response = await apiClient.post(`/projects/${projectId}/integrations/${integrationId}/regenerate`);
    return response.data;
}

export async function toggleIntegrationAsync(projectId: string, integrationId: string, isEnabled: boolean): Promise<void> {
    await apiClient.patch(`/projects/${projectId}/integrations/${integrationId}/toggle?isEnabled=${isEnabled}`);
}

export async function resetWebhookSecretAsync(projectId: string, integrationId: string, newSecret: string): Promise<void> {
    await apiClient.post(`/projects/${projectId}/integrations/${integrationId}/reset-secret`, { newSecret });
}