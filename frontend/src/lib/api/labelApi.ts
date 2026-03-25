import apiClient from "./client";

interface CreateLabelRequest {
    name: string;
    color: string;
}

export interface LabelResponse {
    id: string;
    projectId: string;
    name: string;
    color: string;
}

export async function createLabelAsync(projectId:string, data:CreateLabelRequest): Promise<LabelResponse> {
    const response = await apiClient.post('/projects/' + projectId + '/labels', data);
    return response.data;
}

export async function getLabelsAsync(projectId:string): Promise<LabelResponse[]> {
    const response = await apiClient.get('/projects/' + projectId + '/labels');
    return response.data;
}

export async function deleteLabelAsync(projectId:string, labelId:string): Promise<void> {
    await apiClient.delete('/projects/' + projectId + '/labels/' + labelId);
}

export async function addLabelToTaskAsync(projectId:string, taskId:string ,labelId:string): Promise<void> {
    await apiClient.post
        ('/projects/' + projectId + '/labels/tasks/' + taskId + '/labels/' + labelId);
}

export async function removeLabelFromTaskAsync(projectId:string, taskId:string ,labelId:string): Promise<void> {
    await apiClient.delete
        ('/projects/' + projectId + '/labels/tasks/' + taskId + '/labels/' + labelId);
}