<script lang="ts">
    import type { TaskResponse } from '../api/taskApi';
    import type { SprintResponse } from '../api/sprintApi';
    import type { BoardResponse } from '../api/boardApi';

    import BacklogTaskCard from './BacklogTaskCard.svelte';
    import CreateTaskModal from './CreateTaskModal.svelte';
    import TaskDetailModal from './TaskDetailModal.svelte';
    import { setActiveTask, taskStore } from '../stores/taskStore';

    import { ChevronRight, ChevronDown, Plus } from 'lucide-svelte';

    export let projectId: string;
    export let tasks: TaskResponse[] = [];
    export let sprints: SprintResponse[] = [];
    export let boards: BoardResponse[] = [];
    export let onRefresh: () => Promise<void> = async () => {};
    export let onDelete: (taskId: string) => void = () => {};
    export let onAssignToSprint: (taskId: string, sprintId: string) => void = () => {};
    
    let isCreateTaskOpen = false;
    let isTaskDetailOpen = false;
    let isCollapsed = false;

    $: backlogTasks = tasks.filter(t => !t.sprintId && !t.closedAt);
    $: availableSprints = sprints.filter(s => s.state !== 'Completed');
</script>

<div class="backlog-container">
    <div class="backlog-header">
        <button class="section-toggle" on:click={() => isCollapsed = !isCollapsed}>
            {#if isCollapsed}
                <ChevronRight size={14} />
            {:else}
                <ChevronDown size={14} />
            {/if}
            Projekt Backlog ({backlogTasks.length})
        </button>
        <button class="create-btn" on:click={() => isCreateTaskOpen = true}>
            <Plus size={14} /> Új task
        </button>
    </div>

    {#if !isCollapsed}
        <div class="backlog-tasks">
            {#if backlogTasks.length > 0}
                {#each backlogTasks as task}
                    <BacklogTaskCard
                        {task}
                        {boards}
                        sprints={availableSprints}
                        projectId={projectId}
                        onAssignToSprint={onAssignToSprint}
                        onDelete={(taskId) => onDelete(taskId)}
                        onBoardAssigned={async () => await onRefresh()}
                        onOpenDetail={async (task) => {
                            setActiveTask(task);
                            isTaskDetailOpen = true;
                        }}
                    />
                {/each}
            {:else}
                <p class="empty">Nincs backlog task</p>
            {/if}
        </div>
    {/if}
</div>

{#if isCreateTaskOpen}
    <CreateTaskModal
        bind:isTaskCreationOpen={isCreateTaskOpen}
        projectId={projectId}
        boardId={null}
        isBacklogMode={true}
        onClose={async () => {
            await onRefresh();
        }}
    />
{/if}

{#if isTaskDetailOpen && $taskStore.activeTask}
    <TaskDetailModal
        bind:isTaskDetailOpen={isTaskDetailOpen}
        projectId={projectId}
        task={$taskStore.activeTask!}
        onClose={async () => {
            isTaskDetailOpen = false;
            setActiveTask(null);
            await onRefresh();
        }}
    />
{/if}

<style>
    .backlog-container {
        background: var(--bg-card);
        border-radius: 8px;
        border: 1px solid var(--border-subtle);
        overflow: visible;
    }

    .backlog-header {
        display: flex;
        align-items: center;
        justify-content: space-between;
        padding: 0.75rem 1rem;
        background: var(--bg-secondary);
        border-bottom: 1px solid var(--border);
        border-radius: 8px 8px 0 0;
    }

    .section-toggle {
        display: flex;
        align-items: center;
        gap: 0.4rem;
        background: transparent;
        border: none;
        color: var(--text-secondary);
        cursor: pointer;
        font-size: 0.95rem;
        font-weight: bold;
        padding: 0;
        transition: color 0.15s;
    }

    .section-toggle:hover { color: var(--text-primary); }

    .create-btn {
        display: flex;
        align-items: center;
        gap: 0.35rem;
        background: var(--bg-hover);
        border: 1px solid var(--border-hover);
        color: var(--text-secondary);
        padding: 0.3rem 0.75rem;
        border-radius: 6px;
        cursor: pointer;
        font-size: 0.85rem;
        transition: background 0.15s, color 0.15s;
    }

    .create-btn:hover { background: var(--border-hover); color: var(--text-primary); }

    .backlog-tasks {
        padding: 0.75rem;
        display: flex;
        flex-direction: column;
        gap: 0.25rem;
        overflow: visible;
    }

    .empty {
        font-size: 0.85rem;
        color: var(--text-muted);
        padding: 0.5rem;
        text-align: center;
    }
</style>