import type { AttachmentResponse } from './attachmentApi';
import apiClient from './client';

interface CreateTaskRequest {
    title: string;
    description: string | null;
    boardId: string | null;
    columnId: string | null;
    sprintId: string | null;
    priority: string | null;
    estimateInMinutes: number | null;
    dueDate: Date | null;
}

interface MoveTaskRequest {
    columnId: string;
    afterTaskId: string | null; 
}

interface UpdateTaskRequest {
    title: string | null;
    description: string | null;
    priority: string | null;
    estimateInMinutes: number | null;
    dueDate: Date | null;
}

interface AssignTaskToBoardRequest {
    boardId: string | null;
}

export interface CommitLinkResponse {
    id: string;
    commitSha: string;
    commitUrl: string | null;
    message: string;
    authorName: string;
    authorEmail: string;
    committedAt: string;
}

export interface PrLinkResponse {
    id: string;
    prNumber: number;
    prUrl: string | null;
    title: string;
    state: string;
    authorName: string;
    createdAt: string;
    mergedAt: string | null;
}

export interface TaskResponse {
    id: string;
    projectId: string;
    boardId: string | null;
    columnId: string;
    sprintId: string | null;
    taskKey: string;
    title: string;
    description: string | null;
    status: string;
    priority: string;
    position: string;
    estimateInMinutes: number | null;
    dueDate: Date | null;
    assigneeIds: string[];
    labelIds: string[];
    commitLinks: CommitLinkResponse[];
    prLinks: PrLinkResponse[];
    attachments: AttachmentResponse[];
    createdByName: string;
    closedAt: Date;
    completedAt: Date;
    createdAt: Date;
    updatedAt: Date;
}

export async function getTaskByIdAsync(projectId: string, taskId: string): Promise<TaskResponse> {
    const response = await apiClient.get('/projects/' + projectId + '/tasks/' + taskId);
    return response.data;
}

export async function getTasksAsync(projectId: string, boardId?: string, sprintId?: string): Promise<TaskResponse[]> {
    const response = await apiClient.get('/projects/' + projectId + '/tasks', {
        params: {
            boardId: boardId ?? undefined,
            sprintId: sprintId ?? undefined
        }
    });
    return response.data;
}

export async function createTaskAsync(projectId: string, data: CreateTaskRequest): Promise<TaskResponse> {
    const response = await apiClient.post('/projects/' + projectId + '/tasks', data);
    return response.data;
}

export async function updateTaskAsync(projectId: string, taskId: string, data: UpdateTaskRequest): Promise<TaskResponse> {
    const response = await apiClient.patch('/projects/' + projectId + '/tasks/' + taskId, data);
    return response.data;
}

export async function deleteTaskAsync(projectId: string, taskId: string) {
    const response = await apiClient.delete('/projects/' + projectId + '/tasks/' + taskId);
}

export async function moveTaskAsync(projectId: string, taskId: string, data: MoveTaskRequest): Promise<TaskResponse> {
    const response = await apiClient.patch('/projects/' + projectId + '/tasks/' + taskId + '/move', data);
    return response.data;
}

export async function assignTaskToBoardAsync(projectId: string, taskId: string, data: AssignTaskToBoardRequest): Promise<TaskResponse> {
    const response = await apiClient.post('/projects/' + projectId + '/tasks/' + taskId + '/board', data);
    return response.data;
}

export async function addAssigneeAsync(projectId: string, taskId: string, userId: string): Promise<void> {
    await apiClient.post(`/projects/${projectId}/tasks/${taskId}/assignees/${userId}`);
}

export async function removeAssigneeAsync(projectId: string, taskId: string, userId: string): Promise<void> {
    await apiClient.delete(`/projects/${projectId}/tasks/${taskId}/assignees/${userId}`);
}