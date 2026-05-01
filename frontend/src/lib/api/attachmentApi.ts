import apiClient from './client';

export interface AttachmentResponse {
    id: string;
    projectId: string;
    taskId: string | null;
    fileName: string;
    contentType: string;
    sizeBytes: number;
    attachmentType: string;
    uploadedByName: string;
    createdAt: string;
}

export async function getTaskAttachmentsAsync(projectId: string, taskId: string): Promise<AttachmentResponse[]> {
    const response = await apiClient.get(`/projects/${projectId}/tasks/${taskId}/attachments`);
    return response.data;
}

export async function uploadTaskAttachmentAsync(projectId: string, taskId: string, file: File): Promise<AttachmentResponse> {
    const formData = new FormData();
    formData.append('file', file);
    const response = await apiClient.post(
        `/projects/${projectId}/tasks/${taskId}/attachments`,
        formData,
        { headers: { 'Content-Type': 'multipart/form-data' } }
    );
    return response.data;
}

export async function downloadAttachmentAsync(projectId: string, taskId: string, attachmentId: string, fileName: string): Promise<void> {
    const response = await apiClient.get(
        `/projects/${projectId}/tasks/${taskId}/attachments/${attachmentId}/download`,
        { responseType: 'blob' }
    );
    // Letöltés trigger
    const url = window.URL.createObjectURL(new Blob([response.data]));
    const link = document.createElement('a');
    link.href = url;
    link.setAttribute('download', fileName);
    document.body.appendChild(link);
    link.click();
    link.remove();
    window.URL.revokeObjectURL(url);
}

export async function deleteTaskAttachmentAsync(projectId: string, taskId: string, attachmentId: string): Promise<void> {
    await apiClient.delete(`/projects/${projectId}/tasks/${taskId}/attachments/${attachmentId}`);
}

export async function getProjectAttachmentsAsync(projectId: string): Promise<AttachmentResponse[]> {
    const response = await apiClient.get(`/projects/${projectId}/attachments`);
    return response.data;
}

export async function uploadProjectAttachmentAsync(projectId: string, file: File): Promise<AttachmentResponse> {
    const formData = new FormData();
    formData.append('file', file);
    const response = await apiClient.post(
        `/projects/${projectId}/attachments`,
        formData,
        { headers: { 'Content-Type': 'multipart/form-data' } }
    );
    return response.data;
}

export async function deleteProjectAttachmentAsync(projectId: string, attachmentId: string): Promise<void> {
    await apiClient.delete(`/projects/${projectId}/attachments/${attachmentId}`);
}

export async function downloadProjectAttachmentAsync(projectId: string, attachmentId: string, fileName: string): Promise<void> {
    const response = await apiClient.get(
        `/projects/${projectId}/attachments/${attachmentId}/download`,
        { responseType: 'blob' }
    );
    const url = window.URL.createObjectURL(new Blob([response.data]));
    const link = document.createElement('a');
    link.href = url;
    link.setAttribute('download', fileName);
    document.body.appendChild(link);
    link.click();
    link.remove();
    window.URL.revokeObjectURL(url);
}