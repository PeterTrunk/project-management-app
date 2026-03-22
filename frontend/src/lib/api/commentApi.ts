import apiClient from './client';

export interface CommentResponse {
    id: string;
    taskId: string;
    userId: string;
    userName: string;
    body: string;
    createdAt: Date;
    updatedAt: Date;
}

interface CreateCommentRequest {
    body: string;
}

export async function getCommentsAsync(projectId: string, taskId: string): Promise<CommentResponse[]> {
    const response = await apiClient.get('/projects/' + projectId + '/tasks/' + taskId + '/comments');
    return response.data;
}

export async function createCommentAsync(projectId: string, taskId: string, data: CreateCommentRequest): Promise<CommentResponse> {
    const response = await apiClient.post('/projects/' + projectId + '/tasks/' + taskId + '/comments', data);
    return response.data;
}

export async function deleteCommentAsync(projectId: string, taskId: string, commentId: string): Promise<void> {
    await apiClient.delete('/projects/' + projectId + '/tasks/' + taskId + '/comments/' + commentId);
}