import apiClient from './client';

export interface TaskStatusDistribution {
    status: string;
    count: number;
}

export interface BurndownDataPoint {
    date: string;
    remainingTasks: number;
    totalTasks: number;
    completedTasks: number;
}

export interface WorkloadDataPoint {
    userName: string;
    taskCount: number;
}

export interface VelocityDataPoint {
    sprintName: string;
    completedTasks: number;
    sprintEndDate: string | null;
}

export interface StatusCount {
    status: string;
    count: number;
}

export interface CumulativeFlowDataPoint {
    date: string;
    statusCounts: StatusCount[];
}

export async function getTaskStatusDistributionAsync(projectId: string, sprintId?: string): Promise<TaskStatusDistribution[]> {
    const response = await apiClient.get(
        `/projects/${projectId}/statistics/task-status`,
        { params: { sprintId } }
    );
    return response.data;
}

export async function getBurndownAsync(projectId: string, sprintId: string): Promise<BurndownDataPoint[]> {
    const response = await apiClient.get(
        `/projects/${projectId}/statistics/burndown`,
        { params: { sprintId } }
    );
    return response.data;
}

export async function getWorkloadAsync(projectId: string, sprintId?: string): Promise<WorkloadDataPoint[]> {
    const response = await apiClient.get(
        `/projects/${projectId}/statistics/workload`,
        { params: { sprintId } }
    );
    return response.data;
}

export async function getVelocityAsync(projectId: string): Promise<VelocityDataPoint[]> {
    const response = await apiClient.get(
        `/projects/${projectId}/statistics/velocity`
    );
    return response.data;
}

export async function getCumulativeFlowAsync(projectId: string, dateFrom: string, dateTo: string): Promise<CumulativeFlowDataPoint[]> {
    const response = await apiClient.get(
        `/projects/${projectId}/statistics/cumulative-flow`,
        { params: { dateFrom, dateTo } }
    );
    return response.data;
}