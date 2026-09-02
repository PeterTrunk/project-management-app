import apiClient from './client';
import { validateCreateProject, validateUpdateProject } from "../utils/validators";

interface CreateProjectRequest {
    name: string;
    projKey: string;
    description: string | null;
}

interface UpdateProjectRequest {
    name: string | null;
    description: string | null;
    isArchived: boolean | null;
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

export async function getProjectByIdAsync(id: string): Promise<ProjectResponse> {
    const response = await apiClient.get('/project/' + id);
    return response.data;
}

export async function createProjectAsync(data: CreateProjectRequest): Promise<ProjectResponse> {
    const error = validateCreateProject(data.name, data.projKey, data.description);
    if (error) throw new Error(error);

    const response = await apiClient.post('/project', data);
    return response.data;  
}

export async function deleteProjectAsync(id: string): Promise<void> {
    await apiClient.delete('/project/' + id);
}

export async function updateProjectAsync(data:UpdateProjectRequest, id: string): Promise<ProjectResponse> {
    const error = validateUpdateProject(data.name, data.description);
    if (error) throw new Error(error);
    
    const response = await apiClient.put('/project/' + id, data);
    return response.data;
}

export async function archiveProjectAsync(id:string): Promise<void> {
    await apiClient.patch('/project/' + id + '/archive');
}

export async function unarchiveProjectAsync(id:string): Promise<void> {
    await apiClient.patch('/project/' + id + '/unarchive');

}

export async function deleteProject(id: string): Promise<void> {
    await apiClient.delete('/project/' + id);
}


