import { writable } from 'svelte/store';
import type { MemberResponse } from '../api/teamApi';

interface TeamState {
    members: MemberResponse[];
    refreshTrigger: number;
}

const initialState: TeamState = {
    members: [],
    refreshTrigger: 0
};

export const teamStore = writable<TeamState>(initialState);

export function setMembers(members: MemberResponse[]) {
    teamStore.update(state => ({ ...state, members }));
}

export function triggerTeamRefresh() {
    teamStore.update(state => ({ 
        ...state, 
        refreshTrigger: state.refreshTrigger + 1 
    }));
}

export function clearTeam() {
    teamStore.set(initialState);
}

// SignalR handle metódusok

export function handleMemberAdded(payload: {
    userId: string;
    displayName: string;
    projectRole: string;
}) {
    teamStore.update(state => ({
        ...state,
        members: [...state.members, payload as unknown as MemberResponse]
    }));
}

export function handleMemberRemoved(payload: { userId: string }) {
    teamStore.update(state => ({
        ...state,
        members: state.members.filter(m => m.userId !== payload.userId)
    }));
}

export function handleMemberRoleUpdated(payload: {
    userId: string;
    projectRole: string;
}) {
    teamStore.update(state => ({
        ...state,
        members: state.members.map(m =>
            m.userId === payload.userId
                ? { ...m, projectRole: payload.projectRole }
                : m
        )
    }));
}