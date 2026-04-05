<script lang="ts">
    import type { TaskResponse } from '../api/taskApi';
    import type { SprintResponse } from '../api/sprintApi';
    import type { BoardResponse } from '../api/boardApi';

    import BacklogTaskCard from './BacklogTaskCard.svelte';
    import CreateTaskModal from './CreateTaskModal.svelte';
    import TaskDetailModal from './TaskDetailModal.svelte';
    import { getTasksAsync } from '../api/taskApi';
    import { setActiveTask, setTasks, taskStore } from '../stores/taskStore';

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
            {isCollapsed ? '▶' : '▼'} Projekt Backlog ({backlogTasks.length})
        </button>
        <button class="create-btn" on:click={() => isCreateTaskOpen = true}>+ Új task</button>
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
        background: #1e1e1e;
        border-radius: 8px;
        border: 1px solid #333;
        overflow: hidden;
    }

    .backlog-header {
        display: flex;
        align-items: center;
        justify-content: space-between;
        padding: 0.75rem 1rem;
        background: #1a1a1a;
        border-bottom: 1px solid #333;
    }

    .section-toggle {
        background: transparent;
        border: none;
        color: #ccc;
        cursor: pointer;
        font-size: 0.95rem;
        font-weight: bold;
        padding: 0;
    }

    .section-toggle:hover { color: white; }

    .create-btn {
        background: #2a2a2a;
        border: 1px solid #444;
        color: white;
        padding: 0.3rem 0.75rem;
        border-radius: 6px;
        cursor: pointer;
        font-size: 0.85rem;
    }

    .create-btn:hover { background: #333; }

    .backlog-tasks {
        padding: 0.75rem;
        display: flex;
        flex-direction: column;
        gap: 0.25rem;
    }

    .empty {
        font-size: 0.85rem;
        color: #555;
        padding: 0.5rem;
        text-align: center;
    }
</style>