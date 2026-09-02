import apiClient from './client';
import { validateMemberRole, validateInviteLink } from "../utils/validators";

export interface MemberResponse {
    userId: string;
    displayName: string;
    email: string;
    projectRole: string;
    joinedAt: Date;
}

export interface InviteLinkResponse {
    token: string;
    expiresAt: Date | null;
    maxUses: number | null;
    useCount: number;
    inviteUrl: string;
}

export interface GenerateInviteLinkRequest {
    maxUses: number | null;
    expiresInDays: number | null;
}

export interface UpdateMemberRoleRequest {
    projectRole: string;
}

export async function getMembersAsync(projectId: string): Promise<MemberResponse[]> {
    const response = await apiClient.get(`/projects/${projectId}/members`);
    return response.data;
}

export async function removeMemberAsync(projectId: string, userId: string): Promise<void> {
    await apiClient.delete(`/projects/${projectId}/members/${userId}`);
}

export async function updateMemberRoleAsync(projectId: string, userId: string, data: UpdateMemberRoleRequest): Promise<MemberResponse> {
    const error = validateMemberRole(data.projectRole);
    if (error) throw new Error(error);
    
    const response = await apiClient.patch(`/projects/${projectId}/members/${userId}/role`, data);
    return response.data;
}

export async function generateInviteLinkAsync(projectId: string, data: GenerateInviteLinkRequest): Promise<InviteLinkResponse> {
    const error = validateInviteLink(data.maxUses, data.expiresInDays);
    if (error) throw new Error(error);

    const response = await apiClient.post(`/projects/${projectId}/members/invite`, data);
    return response.data;
}

export async function joinProjectAsync(token: string): Promise<MemberResponse> {
    const response = await apiClient.post(`/projects/join/${token}`);
    return response.data;
}

export async function getInvitationsAsync(projectId: string): Promise<InviteLinkResponse[]> {
    const response = await apiClient.get(`/projects/${projectId}/members/invites`);
    return response.data;
}

export async function deleteInvitationAsync(projectId: string, token: string): Promise<void> {
    await apiClient.delete(`/projects/${projectId}/members/invites/${token}`);
}