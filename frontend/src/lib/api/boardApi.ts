import apiClient from "./client";

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
}

export interface BoardResponse {
    id: string;
    projectId: string;
    name: string;
    description: string;
    isDefault: boolean;
    createdAt: Date;
    updatedAt: Date;
}

export async function getBoardsAsync(projectId: string): Promise<BoardResponse[]> {
    const response = await apiClient.get('/projects/'+projectId+'/boards');
    return response.data;
}

export async function createBoardAsync(projectId: string, data: CreateBoardRequest): Promise<BoardResponse> {
    const response = await apiClient.post('/projects/'+projectId+'/boards', data);
    return response.data;
}

export async function updateBoardAsync(projectId: string, boardId: string, data: UpdateBoardRequest): Promise<BoardResponse> {
    const response = await apiClient.patch('/projects/'+projectId+'/boards/'+boardId, data);
    return response.data;
}

export async function deleteBoardAsync(projectId: string, boardId: string): Promise<void> {
    await apiClient.delete('/projects/'+projectId+'/boards/'+boardId);
}

