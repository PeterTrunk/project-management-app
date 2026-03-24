import apiClient from './client';

interface CreateTaskRequest {
    title: string;
    description: string | null;
    boardId: string
    columnId: string;
    sprintId: string | null;
    priority: string | null;
    estimateInMinutes: number | null;
    dueDate: Date | null;
}

interface MoveTaskRequest {
    position: number;
    columnId: string; 
}

interface UpdateTaskRequest {
    title: string | null;
    description: string | null;
    priority: string | null;
    estimateInMinutes: number | null;
    dueDate: Date | null;
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
    position: number;
    estimateInMinutes: number | null;
    dueDate: Date | null;
    assigneeNames: string[];
    labelNames: string[];
    commitLinks: string[];
    prLinks: string[];
    createdByName: string;
    createdAt: Date;
    updatedAt: Date;
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

