import apiClient from "./client";
import { validateCreateColumn, validateUpdateColumn } from "../utils/validators";

interface CreateColumnRequest {
    boardId: string;
    name: string;
    mapsToStatus: string;
    wipLimit: number | null;
    position: number;
}

interface ColumnOrderRequest {
    id: string;
    position: number;
    rowVersion: number;
}

interface UpdateColumnRequest {
    name: string | null;
    mapsToStatus: string | null;
    wipLimit: number | null;
    rowVersion: number;
}

export interface ColumnResponse {
    id: string;
    boardId: string;
    name: string;
    mapsToStatus: string;
    wipLimit: number | null;
    position: number;
    rowVersion: number;
}

export async function getColumnsAsync(projectId: string, boardId: string): Promise<ColumnResponse[]> {
    const response = await apiClient.get('/projects/'+projectId+'/boards/'+boardId+'/columns');
    return response.data;
}

export async function createColumnAsync(projectId: string, boardId: string, data: CreateColumnRequest): Promise<ColumnResponse> {
    const error = validateCreateColumn(data.name, data.mapsToStatus);
    if (error) throw new Error(error);
    
    const response = await apiClient.post('/projects/'+projectId+'/boards/'+boardId+'/columns', data);
    return response.data;
}

export async function updateColumnAsync(projectId: string, boardId: string, columnId: string, data: UpdateColumnRequest): Promise<ColumnResponse> {
    const error = validateUpdateColumn(data.name, data.mapsToStatus);
    if (error) throw new Error(error);
    
    const response = await apiClient.patch('/projects/'+projectId+'/boards/'+boardId+'/columns/'+columnId, data);
    return response.data;
}

export async function deleteColumnAsync(projectId: string, boardId: string, columnId: string): Promise<void> {
    await apiClient.delete('/projects/'+projectId+'/boards/'+boardId+'/columns/'+columnId);
}

export async function reorderColumnsAsync(projectId: string, boardId: string, order: ColumnOrderRequest[]): Promise<ColumnResponse[]> {
    const response = await apiClient.post('/projects/'+projectId+'/boards/'+boardId+'/columns/reorder', order);
    return response.data;
}