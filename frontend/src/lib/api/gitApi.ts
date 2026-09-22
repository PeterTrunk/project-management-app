import apiClient from './client';

/**
 * Egy commit hivatkozás a projekt szintű listában.
 *
 * A task mezők nullozhatók: a hozzárendeletlen hivatkozás pontosan az, ahol nincs task.
 * Ezért nincs külön "unmatched" lekérés, az egyetlen szűrés a `taskId === null`.
 */
export interface LinkedCommitResponse {
    id: string;
    commitSha: string;
    commitUrl: string | null;
    message: string;
    authorName: string;
    committedAt: string;
    isManuallyLinked: boolean;
    taskId: string | null;
    taskKey: string | null;
    taskTitle: string | null;
}

export interface LinkedPrResponse {
    id: string;
    prNumber: number;
    prUrl: string | null;
    title: string;
    state: string;
    authorName: string;
    createdAt: string;
    mergedAt: string | null;
    isManuallyLinked: boolean;
    taskId: string | null;
    taskKey: string | null;
    taskTitle: string | null;
}

export async function getCommitLinksAsync(projectId: string): Promise<LinkedCommitResponse[]> {
    const response = await apiClient.get(`/projects/${projectId}/git/commits`);
    return response.data;
}

export async function getPrLinksAsync(projectId: string): Promise<LinkedPrResponse[]> {
    const response = await apiClient.get(`/projects/${projectId}/git/prs`);
    return response.data;
}

export async function assignCommitToTaskAsync(projectId: string, commitId: string, taskId: string): Promise<void> {
    await apiClient.post(`/projects/${projectId}/git/commits/${commitId}/assign/${taskId}`);
}

export async function assignPrToTaskAsync(projectId: string, prId: string, taskId: string): Promise<void> {
    await apiClient.post(`/projects/${projectId}/git/prs/${prId}/assign/${taskId}`);
}
