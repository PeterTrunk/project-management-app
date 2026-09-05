import apiClient from "./client";
import { type ColumnResponse } from "./columnApi";
import { validateCreateBoard, validateUpdateBoard } from "../utils/validators";

interface CreateBoardRequest {
    projectId: string;
    name: string;
    description: string;
    isDefault: boolean;
}

interface UpdateBoardRequest {
    name: string | null;
    description: string | null;
    isDefault: boolean | null;
    rowVersion: number;
}

export interface BoardResponse {
    id: string;
    projectId: string;
    name: string;
    description: string;
    isDefault: boolean;
    createdAt: Date;
    updatedAt: Date;
    columns?: ColumnResponse[];
    rowVersion: number;
}

export async function getBoardsAsync(projectId: string, scope?: string): Promise<BoardResponse[]> {
    const response = await apiClient.get('/projects/' + projectId + '/boards', {
        params: { scope }
    });
    return response.data;
}

export async function createBoardAsync(projectId: string, data: CreateBoardRequest): Promise<BoardResponse> {
    const error = validateCreateBoard(data.name, data.description);
    if (error) throw new Error(error);
    
    const response = await apiClient.post('/projects/'+projectId+'/boards', data);
    return response.data;
}

export async function updateBoardAsync(projectId: string, boardId: string, data: UpdateBoardRequest): Promise<BoardResponse> {
    const error = validateUpdateBoard(data.name, data.description);
    if (error) throw new Error(error);
    
    const response = await apiClient.patch('/projects/'+projectId+'/boards/'+boardId, data);
    return response.data;
}

export async function deleteBoardAsync(projectId: string, boardId: string): Promise<void> {
    await apiClient.delete('/projects/'+projectId+'/boards/'+boardId);
}