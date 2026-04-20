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