import apiClient from './client';
import { validateCreateIntegration, validateWebhookSecret, validateInstanceUrl } from "../utils/validators";

export interface IntegrationResponse {
    id: string;
    provider: string;
    repoFullName: string;
    webhookToken: string;
    webhookUrl: string;
    isEnabled: boolean;
    isVerified: boolean;

    /** Saját telepítésű szolgáltató címe, vagy null a felhős változatnál. */
    instanceUrl: string | null;

    /**
     * Van-e beállított hozzáférési token. Maga a token SOSEM jön meg, ahogy a webhook titok
     * sem: a felületnek csak azt kell tudnia, hogy beállított állapotot írjon ki.
     */
    hasAccessToken: boolean;

    /** Megkísérli-e a rendszer magától a kimaradt commitok pótlását. */
    autoBackfillEnabled: boolean;

    createdAt: string;
    updatedAt: string;
}

export interface CreateIntegrationRequest {
    provider: string;
    repoFullName: string;
    webhookSecret: string;
    authorityConfirmed: boolean;

    //Mindhárom opcionális: a pótláshoz kellenek, nem a webhook fogadásához
    instanceUrl?: string | null;
    accessToken?: string | null;
    autoBackfillEnabled?: boolean;
}

export async function getIntegrationsAsync(projectId: string): Promise<IntegrationResponse[]> {
    const response = await apiClient.get(`/projects/${projectId}/integrations`);
    return response.data;
}

export async function createIntegrationAsync(projectId: string, data: CreateIntegrationRequest): Promise<IntegrationResponse> {
    const error = validateCreateIntegration(data.provider, data.repoFullName, data.webhookSecret)
        ?? validateInstanceUrl(data.instanceUrl);
    if (error) throw new Error(error);
    
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
    const error = validateWebhookSecret(newSecret);
    if (error) throw new Error(error);
    
    await apiClient.post(`/projects/${projectId}/integrations/${integrationId}/reset-secret`, { newSecret });
}
/**
 * A szolgáltatói API hozzáférési tokenje. Üres értékkel a tárolt token TÖRLŐDIK - ez a
 * visszavonás útja, és azért ugyanez a hívás, mert a kettő ugyanaz a döntés: a felhasználó
 * megmondja, mi legyen a tárolt hitelesítő adat.
 *
 * A válasz szándékosan üres: a token értéke semmilyen válaszban nem jelenik meg.
 */
export async function setAccessTokenAsync(
    projectId: string, integrationId: string, accessToken: string | null
): Promise<void> {
    await apiClient.put(`/projects/${projectId}/integrations/${integrationId}/access-token`, { accessToken });
}

export async function setAutoBackfillAsync(
    projectId: string, integrationId: string, isEnabled: boolean
): Promise<void> {
    await apiClient.patch(
        `/projects/${projectId}/integrations/${integrationId}/auto-backfill?isEnabled=${isEnabled}`);
}

/**
 * A saját telepítésű szolgáltató kiszolgálójának címe. Üres értékkel a szolgáltató felhős
 * változatára áll vissza.
 */
export async function setInstanceUrlAsync(
    projectId: string, integrationId: string, instanceUrl: string | null
): Promise<void> {
    const error = validateInstanceUrl(instanceUrl);
    if (error) throw new Error(error);

    await apiClient.put(`/projects/${projectId}/integrations/${integrationId}/instance-url`, { instanceUrl });
}
