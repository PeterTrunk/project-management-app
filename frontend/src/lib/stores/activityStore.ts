import { writable } from 'svelte/store';
import type { ActivityResponse } from '../api/activityApi';

const LIVE_LIMIT = 50;

interface ActivityState {
    liveActivities: ActivityResponse[];
    pagedActivities: ActivityResponse[];
    hasMore: boolean;
    currentPage: number;
}

const initialState: ActivityState = {
    liveActivities: [],
    pagedActivities: [],
    hasMore: true,
    currentPage: 1
};

export const activityStore = writable<ActivityState>(initialState);

export function setPagedActivities(activities: ActivityResponse[], page: number, hasMore: boolean) {
    activityStore.update(state => ({
        ...state,
        pagedActivities: page === 1
            ? activities
            : [...state.pagedActivities, ...activities],
        currentPage: page,
        hasMore
    }));
}

export function clearActivities() {
    activityStore.set(initialState);
}

// SignalR handle metódus

export function handleActivityCreated(payload: ActivityResponse) {
    activityStore.update(state => ({
        ...state,
        liveActivities: [payload, ...state.liveActivities].slice(0, LIVE_LIMIT)
    }));
}