import apiClient from './client';
import axios from 'axios';
import { validateFileUpload } from '../utils/validators.ts';

export interface AttachmentResponse {
    id: string;
    projectId: string;
    taskId: string | null;
    fileName: string;
    contentType: string;
    sizeBytes: number;
    attachmentType: string;
    uploadedByName: string;
    version: number;
    createdAt: string;
}

interface PresignedUrlRequest {
    fileName: string;
    contentType: string;
    sizeBytes: number;
}

interface PresignedUrlResponse {
    uploadUrl: string;
    fields: Record<string, string>;
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
    const error = validateFileUpload(data.fileName, data.sizeBytes, data.contentType);
    if (error) throw new Error(error);
    
    const response = await apiClient.post(`/projects/${projectId}/tasks/${taskId}/attachments/presigned`, data);
    return response.data;
}

export async function getProjectPresignedUrlAsync(projectId: string, data: PresignedUrlRequest): Promise<PresignedUrlResponse> {
    const error = validateFileUpload(data.fileName, data.sizeBytes, data.contentType);
    if (error) throw new Error(error);
    
    const response = await apiClient.post(`/projects/${projectId}/attachments/presigned`, data);
    return response.data;
}

// Közvetlen feltöltés az objektumtárolóba, a backend által kiadott presigned URL-re.
// A függvény szolgáltató-független: csak egy PUT-ot küld az aláírt címre.
// Alairt POST policy-val toltunk fel, nem sima PUT-tal: igy a tarolo maga kenyszeriti ki a
// fajltipust es a meretet. A fields minden mezoje valtozatlanul kell, kulonben az alairas nem egyezik,
// a Content-Type pedig FORM MEZOKENT utazik - a policy arra van alairva.
export async function uploadToStorageAsync(
    upload: Pick<PresignedUrlResponse, 'uploadUrl' | 'fields'>,
    file: File,
    onProgress?: (progress: number) => void
): Promise<void> {
    const form = new FormData();

    for (const [key, value] of Object.entries(upload.fields)) {
        form.append(key, value);
    }
    form.append('Content-Type', file.type);

    // A file mezo kotelezoen az utolso - az S3 POST a sorrendre erzekeny.
    form.append('file', file);

    // A Content-Type fejlecet NEM allitjuk be: a boundary-t a bongeszonek kell generalnia.
    await axios.post(upload.uploadUrl, form, {
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