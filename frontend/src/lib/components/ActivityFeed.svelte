<script lang="ts">
    import { onMount } from 'svelte';
    import { getActivitiesAsync, type ActivityResponse } from '../api/activityApi';
    import { activityStore, setPagedActivities } from '../stores/activityStore';

    import { 
        ClipboardList, Timer, MessageSquare, Pin, 
        LayoutDashboard, User, Folder, GitCommitHorizontal, 
        GitPullRequest, Link, Circle, X
    } from 'lucide-svelte';

    export let projectId: string;

    let activities: ActivityResponse[] = [];
    let loading = true;
    let loadingMore = false;
    let error = '';
    let page = 1;
    let hasMore = true;
    const PAGE_SIZE = 20;
    
    let liveActivities: ActivityResponse[] = [];
    activityStore.subscribe(state => {
        liveActivities = state.liveActivities;
    });

    // Szűrő state-ek
    let filterEntityType = '';
    let filterActorName = '';
    let filterDateFrom = '';
    let filterDateTo = '';
    let hasActiveFilter = false;
    let isTodayFilter = false;

    $: hasActiveFilter = filterEntityType !== '' || filterActorName !== '' || 
        filterDateFrom !== '' || filterDateTo !== '';

    $: displayedActivities = hasActiveFilter
    ? activities
    : [...liveActivities, ...activities]
        .filter((a, i, arr) => arr.findIndex(b => b.id === a.id) === i);

    onMount(async () => {
        await loadActivities();
    });

    async function loadActivities() {
        console.log('loadActivities fut:', { filterEntityType, filterActorName, filterDateFrom, filterDateTo });
        loading = true;
        activities = [];
        error = '';
        try {
            const data = await getActivitiesAsync(projectId, {
                page: 1,
                pageSize: PAGE_SIZE,
                entityType: filterEntityType || undefined,
                actorName: filterActorName || undefined,
                dateFrom: filterDateFrom ? toUtcString(filterDateFrom) : undefined,
                dateTo: filterDateTo ? toUtcString(filterDateTo) : undefined,
            });
            console.log('API válasz:', data);
            console.log('API válasz hossza:', data.length);
            console.log('filterDateFrom:', filterDateFrom);
            console.log('filterDateTo:', filterDateTo);
            activities = [...data];
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
            const data = await getActivitiesAsync(projectId, {
                page: nextPage,
                pageSize: PAGE_SIZE,
                entityType: filterEntityType || undefined,
                actorName: filterActorName || undefined,
                dateFrom: filterDateFrom || undefined,
                dateTo: filterDateTo || undefined,
            });
            activities = [...activities, ...data];
            hasMore = data.length === PAGE_SIZE;
            page = nextPage;
        } catch (e: any) {
            error = 'Hiba történt a betöltéskor!';
        } finally {
            loadingMore = false;
        }
    }

    function toggleTodayFilter() {
        isTodayFilter = !isTodayFilter;
        if (isTodayFilter) {
            const today = new Date().toISOString().split('T')[0];
            filterDateFrom = `${today}T00:00`;
            filterDateTo = `${today}T23:59`;
        } else {
            filterDateFrom = '';
            filterDateTo = '';
        }
        loadActivities();
    }

    function toUtcString(localDateTimeString: string): string {
        if (!localDateTimeString) return '';
        return new Date(localDateTimeString).toISOString();
    }

    function clearFilters() {
        filterEntityType = '';
        filterActorName = '';
        filterDateFrom = '';
        filterDateTo = '';
        isTodayFilter = false;
        loadActivities();
    }

    function formatDate(dateString: string): string {
        const date = new Date(dateString);
        const now = new Date();
        const diffMs = now.getTime() - date.getTime();
        const diffMinutes = Math.floor(diffMs / 60000);
        const diffHours = Math.floor(diffMinutes / 60);
        const diffDays = Math.floor(diffHours / 24);

        const timeStr = date.toLocaleTimeString('hu-HU', { hour: '2-digit', minute: '2-digit' });

        if (diffMinutes < 1) return 'most';
        if (diffMinutes < 60) return `${diffMinutes} perce (${timeStr})`;
        if (diffHours < 24) return `${diffHours} órája (${timeStr})`;
        if (diffDays === 1) return `tegnap ${timeStr}`;
        return `${date.toLocaleDateString('hu-HU')} ${timeStr}`;
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
            case 'Commit':      return GitCommitHorizontal;
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

<div class="filter-toolbar">
    <input
        type="text"
        placeholder="Felhasználó..."
        bind:value={filterActorName}
        on:change={loadActivities}
    />

    <select bind:value={filterEntityType} on:change={loadActivities}>
        <option value="">Minden típus</option>
        <option value="Task">Task</option>
        <option value="Sprint">Sprint</option>
        <option value="Comment">Komment</option>
        <option value="Board">Board</option>
        <option value="Column">Oszlop</option>
        <option value="Member">Tag</option>
        <option value="Project">Projekt</option>
        <option value="Commit">Commit</option>
        <option value="PullRequest">Pull Request</option>
        <option value="Integration">Integráció</option>
    </select>
    <label>
        <input 
            type="datetime-local"
            bind:value={filterDateFrom}
            on:change={loadActivities}
        />
        -tól 
    </label>
    <label>
        <input
            type="datetime-local"
            bind:value={filterDateTo}
            on:change={loadActivities}
        />
        -ig 
    </label>

    <button 
        class="today-btn"
        class:active={isTodayFilter}
        on:click={toggleTodayFilter}>
        Csak ma történt
    </button>

    {#if hasActiveFilter}
        <button class="clear-btn" on:click={clearFilters}><X size={13} /></button>
    {/if}
</div>

<div class="activity-feed">
    {#if loading}
        <p class="loading">Betöltés...</p>
    {:else if error}
        <p class="error">{error}</p>
    {:else if displayedActivities.length === 0}
        <p class="empty">Még nincs aktivitás</p>
    {:else}
        <div class="activity-list">
            {#each displayedActivities as activity (activity.id)}
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

    .today-btn {
        padding: 0.3rem 0.6rem;
        border-radius: 6px;
        border: 1px solid var(--border-hover);
        background: transparent;
        color: var(--text-secondary);
        cursor: pointer;
        font-size: 0.85rem;
        white-space: nowrap;
    }

    .today-btn.active {
        background: var(--accent-blue-bg);
        border-color: var(--accent-blue);
        color: var(--accent-blue);
    }

    .filter-toolbar {
        display: flex;
        align-items: center;
        gap: 0.5rem;
        flex-wrap: wrap;
        margin-bottom: 0.75rem;
    }

    .filter-toolbar input[type="text"],
    .filter-toolbar select,
    .filter-toolbar input[type="datetime-local"] {
        background: var(--bg-input);
        border: 1px solid var(--border-hover);
        border-radius: 6px;
        color: var(--text-primary);
        padding: 0.3rem 0.5rem;
        font-size: 0.85rem;
    }

    .filter-toolbar input[type="text"]:focus,
    .filter-toolbar select:focus,
    .filter-toolbar input[type="datetime-local"]:focus {
        outline: none;
        border-color: var(--accent-blue);
    }

    .filter-toolbar label {
        display: flex;
        align-items: center;
        gap: 0.35rem;
        font-size: 0.85rem;
        color: var(--text-muted);
    }

    .clear-btn {
        display: flex;
        align-items: center;
        background: var(--accent-red-bg);
        border: 1px solid var(--accent-red);
        color: var(--accent-red);
        padding: 0.3rem 0.6rem;
        border-radius: 6px;
        cursor: pointer;
        font-size: 0.85rem;
        transition: background 0.15s;
    }

    .clear-btn:hover { background: var(--accent-red); color: #fff; }
</style>