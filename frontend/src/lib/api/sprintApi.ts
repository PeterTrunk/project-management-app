import apiClient from './client';
import type { TaskResponse } from './taskApi';

export interface SprintResponse {
    id: string;
    projectId: string;
    name: string;
    goal: string | null;
    startDate: Date | null;
    endDate: Date | null;
    state: string;
    createdAt: Date;
    updatedAt: Date;
}

interface CreateSprintRequest {
    projectId: string;
    name: string;
    goal: string | null;
    startDate: Date | null;
    endDate: Date | null;
    state: string;
}

interface UpdateSprintRequest {
    name: string | null;
    goal: string | null;
    startDate: Date | null;
    endDate: Date | null;
}

export async function getSprintsAsync(projectId: string): Promise<SprintResponse[]>  {
    const response = await apiClient.get("/projects/"+ projectId +"/sprints");
    return response.data;
} 

export async function createSprintAsync(projectId: string, data: CreateSprintRequest): Promise<SprintResponse> {
    const response = await apiClient.post("/projects/"+ projectId +"/sprints", data);
    return response.data;
}

export async function updateSprintAsync(projectId: string, sprintId: string, data: UpdateSprintRequest): Promise<SprintResponse> {
    const response = await apiClient.put("/projects/"+ projectId +"/sprints/" + sprintId, data);
    return response.data
}

export async function deleteSprintAsync(projectId: string, sprintId: string): Promise<void> {
    await apiClient.delete("/projects/"+ projectId +"/sprints/" + sprintId);
}

export async function getUnfinishedTasksAsync(projectId: string, sprintId: string): Promise<TaskResponse[]> {
    const response = await apiClient.get("/projects/"+ projectId +"/sprints/" + sprintId + "/unfinished"); 
    return response.data;
} 

export async function planSprintAsync(projectId: string, sprintId: string): Promise<SprintResponse> {
    const response = await apiClient.patch("/projects/"+ projectId +"/sprints/" + sprintId + "/plan"); 
    return response.data;
}

export async function activateSprintAsync(projectId: string, sprintId: string): Promise<SprintResponse> {
    const response = await apiClient.patch("/projects/"+ projectId +"/sprints/" + sprintId + "/activate"); 
    return response.data;
}

export async function completeSprintAsync(projectId: string, sprintId: string, targetSprintId?: string): Promise<SprintResponse> {
    const response = await apiClient.post("/projects/"+ projectId +"/sprints/" + sprintId + "/complete", targetSprintId ?? null); 
    return response.data;
}

export async function assignTaskToSprintAsync(projectId: string, sprintId: string, taskId: string) {
    const response = await apiClient.post("/projects/"+ projectId +"/sprints/" + sprintId + "/tasks/" + taskId);
}

export async function removeTaskFromSprintAsync(projectId: string, sprintId: string, taskId: string) {
    const response = await apiClient.delete("/projects/"+ projectId +"/sprints/" + sprintId + "/tasks/" + taskId);
}
