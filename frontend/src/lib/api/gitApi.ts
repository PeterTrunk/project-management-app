import apiClient from './client';
import type { CommitLinkResponse, PrLinkResponse } from './taskApi';

export async function getUnmatchedCommitsAsync(projectId: string): Promise<CommitLinkResponse[]> {
    const response = await apiClient.get(`/projects/${projectId}/git/unmatched-commits`);
    return response.data;
}

export async function getUnmatchedPrsAsync(projectId: string): Promise<PrLinkResponse[]> {
    const response = await apiClient.get(`/projects/${projectId}/git/unmatched-prs`);
    return response.data;
}

export async function assignCommitToTaskAsync(projectId: string, commitId: string, taskId: string): Promise<void> {
    await apiClient.post(`/projects/${projectId}/git/commits/${commitId}/assign/${taskId}`);
}

export async function assignPrToTaskAsync(projectId: string, prId: string, taskId: string): Promise<void> {
    await apiClient.post(`/projects/${projectId}/git/prs/${prId}/assign/${taskId}`);
}