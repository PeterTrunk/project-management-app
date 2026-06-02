<script lang="ts">
    import { onMount, onDestroy } from 'svelte';
    import { getActivitiesAsync, type ActivityResponse } from '../api/activityApi';
    import { signalRService } from '../services/signalRService';

    import { 
        ClipboardList, Timer, MessageSquare, Pin, 
        LayoutDashboard, User, Folder, GitCommit, 
        GitPullRequest, Link, Circle,

        Icon

    } from 'lucide-svelte';
    import type { Component } from 'svelte';

    export let projectId: string;

    let activities: ActivityResponse[] = [];
    let loading = true;
    let loadingMore = false;
    let error = '';
    let page = 1;
    let hasMore = true;
    const PAGE_SIZE = 20;

    onMount(async () => {
        await loadActivities();
        registerSignalREvents();
    });

    async function loadActivities() {
        loading = true;
        error = '';
        try {
            const data = await getActivitiesAsync(projectId, 1, PAGE_SIZE);
            activities = data;
            hasMore = data.length === PAGE_SIZE;
            page = 1;
        } catch (e: any) {
            error = 'Hiba történt az aktivitások lekérésekor!';
        } finally {
            loading = false;
        }
    }

    async function loadMore() {
        loadingMore = true;
        try {
            const nextPage = page + 1;
            const data = await getActivitiesAsync(projectId, nextPage, PAGE_SIZE);
            activities = [...activities, ...data];
            hasMore = data.length === PAGE_SIZE;
            page = nextPage;
        } catch (e: any) {
            error = 'Hiba történt a betöltéskor!';
        } finally {
            loadingMore = false;
        }
    }

    function registerSignalREvents() {
        signalRService.off('ActivityCreated');
        signalRService.on('ActivityCreated', (data: ActivityResponse) => {
            activities = [data, ...activities];
        });
    }

    onDestroy(() => {
        signalRService.off('ActivityCreated');
    });

    function formatDate(dateString: string): string {
        const date = new Date(dateString);
        const now = new Date();
        const diffMs = now.getTime() - date.getTime();
        const diffMinutes = Math.floor(diffMs / 60000);
        const diffHours = Math.floor(diffMinutes / 60);
        const diffDays = Math.floor(diffHours / 24);

        if (diffMinutes < 1) return 'most';
        if (diffMinutes < 60) return `${diffMinutes} perce`;
        if (diffHours < 24) return `${diffHours} órája`;
        if (diffDays === 1) return 'tegnap';
        return date.toLocaleDateString('hu-HU');
    }

    function getEntityIcon(entityType: string): any {
        switch (entityType) {
            case 'Task':        return ClipboardList;
            case 'Sprint':      return Timer;
            case 'Comment':     return MessageSquare;
            case 'Board':       return Pin;
            case 'Column':      return LayoutDashboard;
            case 'Member':      return User;
            case 'Project':     return Folder;
            case 'Commit':      return GitCommit;
            case 'PullRequest': return GitPullRequest;
            case 'Integration': return Link;
            default:            return Circle;
        }
    }

    function highlightDescription(activity: ActivityResponse): string {
    return activity.description.replace(
        activity.actorName,
        `<span class="actor-name">${activity.actorName}</span>`
    );
}
</script>

<div class="activity-feed">
    {#if loading}
        <p class="loading">Betöltés...</p>
    {:else if error}
        <p class="error">{error}</p>
    {:else if activities.length === 0}
        <p class="empty">Még nincs aktivitás</p>
    {:else}
        <div class="activity-list">
            {#each activities as activity (activity.id)}
                <div class="activity-item">
                    <span class="activity-icon">
                        <svelte:component this={getEntityIcon(activity.entityType)} size={16} />
                    </span>
                    <div class="activity-content">
                        <div class="activity-row">
                            <p class="activity-description">
                                {@html highlightDescription(activity)}
                            </p>
                            <span class="activity-time">{formatDate(activity.createdAt)}</span>
                        </div>
                    </div>
                </div>
            {/each}
        </div>

        {#if hasMore}
            <button 
                class="load-more-btn" 
                on:click={loadMore}
                disabled={loadingMore}
            >
                {loadingMore ? 'Betöltés...' : 'Több betöltése'}
            </button>
        {/if}
    {/if}
</div>

<style>
    .activity-feed {
        display: flex;
        flex-direction: column;
        gap: 0.25rem;
    }

    .activity-list {
        display: flex;
        flex-direction: column;
        gap: 0.25rem;
    }

    .activity-item {
        display: flex;
        align-items: flex-start;
        gap: 0.75rem;
        padding: 0.5rem 0.75rem;
        border-radius: 6px;
        background: var(--bg-card);
        border: 1px solid var(--border);
    }

    .activity-item:hover {
        border-color: var(--border-hover);
    }

    .activity-icon {
        display: flex;
        align-items: center;
        justify-content: center;
        color: var(--text-primary);
        flex-shrink: 0;
        margin-top: 0.15rem;
    }

    .activity-content {
        display: flex;
        flex-direction: column;
        gap: 0.2rem;
        flex: 1;
    }

    .activity-row {
        display: flex;
        align-items: baseline;
        justify-content: space-between;
        gap: 1rem;
    }

    .activity-description {
        font-size: 0.9rem;
        margin: 0;
        color: var(--text-secondary);
        text-align: left;
    }

    .activity-time {
        font-size: 0.75rem;
        color: var(--text-muted);
        white-space: nowrap;
        flex-shrink: 0;
    }

    .load-more-btn {
        background: var(--bg-hover);
        border: 1px solid var(--border-hover);
        color: var(--text-secondary);
        padding: 0.5rem;
        border-radius: 6px;
        cursor: pointer;
        font-size: 0.85rem;
        width: 100%;
        margin-top: 0.5rem;
        transition: background 0.15s;
    }

    .load-more-btn:hover { background: var(--border-hover); }
    .load-more-btn:disabled { opacity: 0.5; cursor: not-allowed; }

    .loading, .empty {
        text-align: center;
        padding: 1rem;
        color: var(--text-muted);
        font-size: 0.9rem;
    }

    .error {
        text-align: center;
        padding: 1rem;
        color: var(--accent-red);
        font-size: 0.9rem;
    }

    :global(.actor-name) {
        color: var(--text-primary);
        font-weight: bold;
        padding: 0.1rem 0.4rem;
        border-radius: 4px;
        font-size: 0.8rem;
    }
</style>