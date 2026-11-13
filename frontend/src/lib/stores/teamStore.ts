import { writable } from 'svelte/store';
import type { MemberResponse } from '../api/teamApi';
import { insertIfAbsentBy } from './collections';

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
        //A kulcs a userId, nem az id: a tagság a (projekt, felhasználó) páron azonosítódik.
        members: insertIfAbsentBy(
            state.members, payload as unknown as MemberResponse, m => m.userId)
    }));
}

//Mi történjen, ha valakit kivettek egy projektből.
export type MemberRemovedAction =
    | { kind: 'other' }
    | { kind: 'self'; leaveProjectId: string; exitView: boolean };

export function decideMemberRemoved(
    payload: { userId: string; projectId: string },
    currentUserId: string | undefined,
    activeProjectId: string | undefined
): MemberRemovedAction {
    if (payload.userId !== currentUserId) return { kind: 'other' };

    return {
        kind: 'self',
        leaveProjectId: payload.projectId,
        exitView: activeProjectId === payload.projectId
    };
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