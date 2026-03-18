import apiClient from "./client";

interface CreateColumnRequest {
    boardId: string;
    name: string;
    mapsToStatus: string;
    wipLimit: number | null;
    position: number;
}

interface UpdateColumnRequest {
    name: string | null;
    mapsToStatus: string | null;
    wipLimit: number | null;
    position: number | null;
}

export interface ColumnResponse {
    id: string;
    boardId: string;
    name: string;
    mapsToStatus: string;
    wipLimit: number | null;
    position: number;
}

export async function getColumnsAsync(projectId: string, boardId: string): Promise<ColumnResponse[]> {
    const response = await apiClient.get('/projects/'+projectId+'/boards/'+boardId+'/columns');
    return response.data;
}

export async function createColumnAsync(projectId: string, boardId: string, data: CreateColumnRequest): Promise<ColumnResponse> {
    const response = await apiClient.post('/projects/'+projectId+'/boards/'+boardId+'/columns', data);
    return response.data;
}

export async function updateColumnAsync(projectId: string, boardId: string, columnId: string, data: UpdateColumnRequest): Promise<ColumnResponse> {
    const response = await apiClient.patch('/projects/'+projectId+'/boards/'+boardId+'/columns/'+columnId, data);
    return response.data;
}

export async function deleteColumnAsync(projectId: string, boardId: string, columnId: string): Promise<void> {
    await apiClient.delete('/projects/'+projectId+'/boards/'+boardId+'/columns/'+columnId);
}