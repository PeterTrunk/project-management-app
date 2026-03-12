import apiClient from './client';

interface CreateProjectRequest {
    name: string;
    projKey: string;
    description: string | null;
}

export interface ProjectResponse {
    id: string;
    name: string;
    projKey: string;
    description: string | null;
    ownerName: string;
    isArchived: boolean;
    createdAt: Date;
    updatedAt: Date;
}

export async function getProjectsAsync(): Promise<ProjectResponse[]> {
    const response = await apiClient.get('/project');
    return response.data;  
}

export async function createProjectAsync(data: CreateProjectRequest): Promise<ProjectResponse> {
    const response = await apiClient.post('/project', data);
    return response.data;  
}

export async function deleteProjectAsync(id: string): Promise<void> {
    await apiClient.delete('/project/' + id);
}

