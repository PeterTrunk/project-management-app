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

export interface ActivityFilterParams {
    page?: number;
    pageSize?: number;
    entityType?: string;
    actorName?: string;
    dateFrom?: string;
    dateTo?: string;
}

export async function getActivitiesAsync(
    projectId: string, 
    params: ActivityFilterParams = {}
): Promise<ActivityResponse[]> {
    const response = await apiClient.get(`/projects/${projectId}/activities`, { 
        params: {
            page: params.page ?? 1,
            pageSize: params.pageSize ?? 20,
            entityType: params.entityType || undefined,
            actorName: params.actorName || undefined,
            dateFrom: params.dateFrom || undefined,
            dateTo: params.dateTo || undefined,
        }
    });
    return response.data;
}