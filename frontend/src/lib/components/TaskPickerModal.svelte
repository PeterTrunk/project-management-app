<script lang="ts">
    import { onMount } from 'svelte';
    import { getTasksAsync, type TaskResponse } from '../api/taskApi';
    import { getSprintsAsync, type SprintResponse } from '../api/sprintApi';
    import { Search, X } from 'lucide-svelte';
    import { notify } from '../stores/notificationStore';

    export let isOpen = false;
    export let projectId: string;
    export let onSelect: (taskId: string) => void = () => {};
    export let onClose: () => void = () => {};

    let sprints: SprintResponse[] = [];
    let tasks: TaskResponse[] = [];
    let loading = true;
    let searchQuery = '';
    let filterSprint = '';

    let selectedTask: TaskResponse | null = null;

    $: sprintMap = new Map(sprints.map(s => [s.id, s.name]));

    onMount(async () => {
        await Promise.all([
            loadTasks(),
            loadSprints()
        ]);
    });

    async function loadSprints() {
        try {
            sprints = await getSprintsAsync(projectId); //összes sprint, completed is kell!
        } catch (e: any) {
            notify.error(e.response?.data ?? e.message ?? 'Hiba a sprintek lekérésekor!');
        }
    }

    async function loadTasks() {
        loading = true;
        try {
            tasks = await getTasksAsync(projectId);
        } catch (e: any) {
            notify.error(e.response?.data ?? e.message ?? 'Hiba a taskok lekérésekor!');
        } finally {
            loading = false;
        }
    }

    $: filteredTasks = tasks.filter(t => {
        const matchesSearch = searchQuery === '' ||
            t.title.toLowerCase().includes(searchQuery.toLowerCase()) ||
            t.taskKey.toLowerCase().includes(searchQuery.toLowerCase());
        const matchesSprint = filterSprint === '' || t.sprintId === filterSprint;
        return matchesSearch && matchesSprint;
    });

    function handleSelect(task: TaskResponse) {
        selectedTask = task;
    }

    function handleConfirm() {
        if (selectedTask) {
            onSelect(selectedTask.id);
            onClose();
        }
    }
</script>

{#if isOpen}
    <div class="modal-overlay" on:click|self={onClose}
        on:keydown={(e) => e.key === 'Escape' && onClose()}
        role="dialog"
        aria-modal="true"
        tabindex="-1">
        <div class="modal-content">
            <div class="modal-header">
                <h2>Task kiválasztása</h2>
                <button class="close-btn" on:click={onClose}>
                    <X size={16} />
                </button>
            </div>

            <div class="filters">
                <div class="search-wrapper">
                    <Search size={14} />
                    <input
                        type="text"
                        placeholder="Keresés (cím, task key)..."
                        bind:value={searchQuery}
                        class="search-input"
                    />
                </div>
                <select bind:value={filterSprint} class="filter-select">
                    <option value="">Összes sprint</option>
                    {#each sprints as sprint}
                        <option value={sprint.id}>{sprint.name} ({sprint.state})</option>
                    {/each}
                </select>
            </div>

            {#if loading}
                <div class="loading-state">Taskok betöltése...</div>
            {:else if filteredTasks.length === 0}
                <div class="empty-state">Nincs találat</div>
            {:else}
                <div class="task-list">
                    {#each filteredTasks as task (task.id)}
                        <button 
                            class="task-row" 
                            class:selected={selectedTask?.id === task.id}
                            on:click={() => handleSelect(task)}>
                            <span class="task-key">{task.taskKey}</span>
                            <span class="task-title truncate">{task.title}</span>
                            {#if task.sprintId}
                                <span class="task-sprint">{sprintMap.get(task.sprintId) ?? ''}</span>
                            {/if}
                        </button>
                    {/each}
                </div>
            {/if}
            {#if selectedTask}
                <div class="confirm-bar">
                    <span class="confirm-text truncate">
                        <strong>{selectedTask.taskKey} {selectedTask.title}</strong> hozzárendelése
                    </span>
                    <button class="confirm-btn" on:click={handleConfirm}>
                        Megerősítés
                    </button>
                </div>
            {/if}
        </div>
    </div>
{/if}

<style>
    .modal-overlay {
        position: fixed;
        top: 0;
        left: 0;
        width: 100%;
        height: 100%;
        background: var(--shadow);
        display: flex;
        justify-content: center;
        align-items: center;
        z-index: 1000;
    }

    .modal-content {
        background: var(--bg-card);
        border: 1px solid var(--border);
        border-radius: var(--border-radius-lg);
        padding: 1.5rem;
        width: var(--modal-width);
        max-width: 95vw;
        max-height: 80vh;
        display: flex;
        flex-direction: column;
        gap: 1rem;
        overflow-y: auto;
    }

    @media (max-width: 480px) {
        .modal-content {
            padding: var(--card-padding);
        }
    }

    .modal-header {
        display: flex;
        align-items: center;
        justify-content: space-between;
    }

    .modal-header h2 {
        font-size: 1.1rem;
        font-weight: 600;
    }

    .close-btn {
        background: transparent;
        border: none;
        color: var(--text-secondary);
        cursor: pointer;
        padding: 0.25rem;
        border-radius: 4px;
        display: flex;
        align-items: center;
    }

    .close-btn:hover {
        color: var(--text-primary);
        background: var(--bg-hover);
    }

    .filters {
        display: flex;
        gap: 0.5rem;
        flex-wrap: wrap;
    }

    .search-wrapper {
        display: flex;
        align-items: center;
        gap: 0.5rem;
        flex: 1;
        background: var(--bg-input);
        border: 1px solid var(--border);
        border-radius: var(--border-radius);
        padding: 0.35rem 0.75rem;
        color: var(--text-muted);
        min-width: 0;
    }

    .search-input {
        background: transparent;
        border: none;
        color: var(--text-primary);
        font-size: var(--font-size-sm);
        width: 100%;
        outline: none;
    }

    .filter-select {
        background: var(--bg-input);
        border: 1px solid var(--border);
        border-radius: var(--border-radius);
        color: var(--text-primary);
        font-size: var(--font-size-sm);
        padding: 0.35rem 0.5rem;
    }

    .task-list {
        overflow-y: auto;
        display: flex;
        flex-direction: column;
        gap: 0.25rem;
        max-height: 50vh;
    }

    .task-row {
        display: flex;
        align-items: center;
        gap: 0.75rem;
        padding: 0.6rem 0.75rem;
        border-radius: var(--border-radius);
        background: var(--bg-secondary);
        border: 1px solid var(--border);
        cursor: pointer;
        text-align: left;
        width: 100%;
        transition: background 0.15s;
    }

    .task-row:hover {
        background: var(--bg-hover);
        border-color: var(--border-hover);
    }

    .task-key {
        font-size: var(--font-size-xs);
        color: var(--text-muted);
        white-space: nowrap;
        font-weight: 600;
        flex-shrink: 0;
    }

    .task-title {
        flex: 1;
        font-size: var(--font-size-sm);
        color: var(--text-primary);
        min-width: 0;
    }

    .task-sprint {
        font-size: var(--font-size-xs);
        color: var(--text-muted);
        white-space: nowrap;
        flex-shrink: 0;
    }

    .task-row.selected {
        background: var(--accent-blue-bg);
        border-color: var(--accent-blue);
    }

    .confirm-bar {
        display: flex;
        align-items: center;
        justify-content: space-between;
        gap: 0.75rem;
        padding: 0.75rem;
        background: var(--bg-secondary);
        border: 1px solid var(--border);
        border-radius: var(--border-radius);
    }

    .confirm-text {
        font-size: var(--font-size-sm);
        color: var(--text-secondary);
        flex: 1;
        min-width: 0;
    }

    .confirm-btn {
        background: var(--accent-blue-bg);
        border: 1px solid var(--accent-blue);
        color: var(--accent-blue);
        padding: 0.4rem 0.8rem;
        border-radius: var(--border-radius);
        cursor: pointer;
        font-size: var(--font-size-sm);
        white-space: nowrap;
        flex-shrink: 0;
    }

    .confirm-btn:hover {
        background: var(--accent-blue);
        color: white;
    }
</style>