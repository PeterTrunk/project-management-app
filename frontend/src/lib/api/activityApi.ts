import apiClient from './client';

export interface ActivityResponse {
    id: string;
    actorName: string;
    entityType: string;
    entityId: string;
    action: string;
    description: string;
    payload: string | null;
    createdAt: string;
}

export async function getActivitiesAsync(projectId: string, page: number = 1, pageSize: number = 20): Promise<ActivityResponse[]> {
    const response = await apiClient.get(`/projects/${projectId}/activities`, 
        { params: { page, pageSize } });
    return response.data;
}