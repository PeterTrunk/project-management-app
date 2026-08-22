import apiClient from './client';
import axios from 'axios';

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

interface PresignedUrlRequest {
    fileName: string;
    contentType: string;
    sizeBytes: number;
}

interface PresignedUrlResponse {
    presignedUrl: string;
    storageKey: string;
    expiresAt: string;
}

interface ConfirmUploadRequest {
    storageKey: string;
}

// GET
export async function getTaskAttachmentsAsync(projectId: string, taskId: string): Promise<AttachmentResponse[]> {
    const response = await apiClient.get(`/projects/${projectId}/tasks/${taskId}/attachments`);
    return response.data;
}

export async function getProjectAttachmentsAsync(projectId: string): Promise<AttachmentResponse[]> {
    const response = await apiClient.get(`/projects/${projectId}/attachments`);
    return response.data;
}

// PRESIGNED URL generálás
export async function getTaskPresignedUrlAsync(projectId: string, taskId: string, data: PresignedUrlRequest): Promise<PresignedUrlResponse> {
    const response = await apiClient.post(`/projects/${projectId}/tasks/${taskId}/attachments/presigned`, data);
    return response.data;
}

export async function getProjectPresignedUrlAsync(projectId: string, data: PresignedUrlRequest): Promise<PresignedUrlResponse> {
    const response = await apiClient.post(`/projects/${projectId}/attachments/presigned`, data);
    return response.data;
}

// MinIO-ra közvetlen feltöltés
export async function uploadToMinIOAsync(presignedUrl: string, file: File, onProgress?: (progress: number) => void): Promise<void> {
    await axios.put(presignedUrl, file, {
        headers: { 'Content-Type': file.type },
        onUploadProgress: (e) => {
            if (onProgress && e.total) {
                onProgress(Math.round(e.loaded / e.total * 100));
            }
        }
    });
}

// CONFIRM
export async function confirmTaskUploadAsync(projectId: string, taskId: string, data: ConfirmUploadRequest): Promise<AttachmentResponse> {
    const response = await apiClient.post(`/projects/${projectId}/tasks/${taskId}/attachments/confirm`, data);
    return response.data;
}

export async function confirmProjectUploadAsync(projectId: string, data: ConfirmUploadRequest): Promise<AttachmentResponse> {
    const response = await apiClient.post(`/projects/${projectId}/attachments/confirm`, data);
    return response.data;
}

// DOWNLOAD (egységes)
export async function downloadAttachmentAsync(projectId: string, attachmentId: string, fileName: string): Promise<void> {
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

// DELETE (egységes)
export async function deleteAttachmentAsync(projectId: string, attachmentId: string): Promise<void> {
    await apiClient.delete(`/projects/${projectId}/attachments/${attachmentId}`);
}