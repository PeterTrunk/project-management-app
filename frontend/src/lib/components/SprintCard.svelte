<script lang="ts">
    import type { SprintResponse } from '../api/sprintApi';
    import type { TaskResponse } from '../api/taskApi';
    import type { BoardResponse } from '../api/boardApi';
    import BacklogTaskCard from './BacklogTaskCard.svelte';
    import TaskDetailModal from './TaskDetailModal.svelte';
    import { setActiveTask, taskStore } from '../stores/taskStore';

    export let sprint: SprintResponse;
    export let tasks: TaskResponse[] = [];
    export let boards: BoardResponse[] = [];
    export let sprints: SprintResponse[] = [];
    export let projectId: string = '';
    export let onActivate: (sprintId: string) => void = () => {};
    export let onPlan: (sprintId: string) => void = () => {};
    export let onEdit: (sprint: SprintResponse) => void = () => {};
    export let onComplete: (sprint: SprintResponse) => void = () => {};
    export let onDelete: (sprintId: string) => void = () => {};
    export let onRemoveTask: (taskId: string, sprintId: string) => void = () => {};
    export let onBoardAssigned: () => Promise<void> = async () => {};
    export let onAssignToSprint: (taskId: string, sprintId: string) => void = () => {};
    export let onDeleteTask: (taskId: string) => void = () => {};

    let isTaskDetailOpen = false;

    $: groupedTasks = buildGroupedTasks(tasks, boards);

    function buildGroupedTasks(taskList: TaskResponse[], boardList: BoardResponse[]): Record<string, TaskResponse[]> {
        let map: Record<string, TaskResponse[]> = {};
        taskList.forEach(task => {
            const boardName = task.boardId
                ? (boardList.find(b => b.id === task.boardId)?.name ?? 'Ismeretlen board')
                : 'Nincs board';
            if (!map[boardName]) map[boardName] = [];
            map[boardName].push(task);
        });
        return map;
    }

    // Rendezett entries: default board először, utána ABC, végén "Nincs board"
    $: sortedGroupedTasks = getSortedEntries(groupedTasks, boards);

    function getSortedEntries(grouped: Record<string, TaskResponse[]>, boardList: BoardResponse[]): [string, TaskResponse[]][] {
        const defaultBoard = boardList.find(b => b.isDefault);
        
        return Object.entries(grouped).sort(([nameA], [nameB]) => {
            if (nameA === 'Nincs board') return 1;   // Nincs board mindig utoljára
            if (nameB === 'Nincs board') return -1;
            if (defaultBoard) {
                if (nameA === defaultBoard.name) return -1;  // Default board először
                if (nameB === defaultBoard.name) return 1;
            }
            return nameA.localeCompare(nameB);  // Többi ABC sorrendben
        });
    }

</script>

<div class="sprint-card" class:active={sprint.state === 'Active'} 
                         class:planning={sprint.state === 'Planning'}
                         class:completed={sprint.state === 'Completed'}
>
    <div class="sprint-header">
        <div class="sprint-title">
            {#if sprint.state === 'Active'}
                <span class="active-badge">AKTÍV</span>
            {/if}
            <h2>{sprint.name}</h2>
        </div>

        <div class="sprint-dates">
            {sprint.startDate ? new Date(sprint.startDate).toLocaleDateString('hu-HU') : '?'}
            —
            {sprint.endDate ? new Date(sprint.endDate).toLocaleDateString('hu-HU') : '?'}
        </div>
        <div class="sprint-actions">
            {#if sprint.state === 'Active'}
                <button on:click={() => onEdit(sprint)}>✏ Szerkesztés</button>
                <button on:click={() => onPlan(sprint.id)}>↩ Visszatervezés</button>
                <button class="complete-btn" on:click={() => onComplete(sprint)}>✓ Lezárás</button>
            {:else if sprint.state === 'Planning'}
                <button on:click|stopPropagation={() => onEdit(sprint)}>✏ Szerkesztés</button>
                <button class="activate-btn" on:click={() => onActivate(sprint.id)}>▶ Aktiválás</button>
                <button class="danger-btn" on:click|stopPropagation={() => onDelete(sprint.id)}>🗑 Törlés</button>
            {/if}
        </div>
    </div>
    {#if sprint.goal}
        <p class="sprint-goal">Cél: {sprint.goal}</p>
    {/if}
    {#each sortedGroupedTasks as [boardName, boardTasks]}
        <div class="board-group">
            <h4>
                {boardName}
                {#if boards.find(b => b.name === boardName)?.isDefault}
                    <span class="default-badge">★</span>
                {/if}
            </h4>
            <div class="task-list">
                {#each boardTasks as task (task.id)}
                    <BacklogTaskCard
                        {task}
                        {boards}
                        sprints={sprints}
                        projectId={projectId}
                        onAssignToSprint={onAssignToSprint}
                        onDelete={() => onDeleteTask(task.id)}
                        onBoardAssigned={async () => {
                            await onBoardAssigned();
                        }}
                        onOpenDetail={(task) => {
                            setActiveTask(task);
                            isTaskDetailOpen = true;
                        }}
                    />
                {/each}
                {#if boardTasks.length === 0}
                    <p class="empty">↓ Húzz ide taskot</p>
                {/if}
            </div>
        </div>
    {/each}
    {#if tasks.length === 0}
        <div>
            <p class="empty">Még nincs hozzárendelt Task.</p>
        </div>
    {/if}
</div>

{#if isTaskDetailOpen && $taskStore.activeTask}
    <TaskDetailModal
        bind:isTaskDetailOpen={isTaskDetailOpen}
        projectId={projectId}
        task={$taskStore.activeTask!}
        onClose={async () => {
            isTaskDetailOpen = false;
            setActiveTask(null);
            onBoardAssigned();
        }}
    />
{/if}

<style>
    .sprint-card {
        background: #1e1e1e;
        border-radius: 8px;
        padding: 1rem;
        border: 1px solid #333;
        margin-bottom: 0.75rem;
    }

    .sprint-card.active {
        border-color: #f0a500;
        background: #1e1a0e;
    }

    .sprint-card.completed {
        opacity: 0.7;
    }

    .sprint-header {
        display: flex;
        align-items: center;
        gap: 1rem;
        margin-bottom: 0.5rem;
        flex-wrap: wrap;
    }

    .sprint-title {
        display: flex;
        align-items: center;
        gap: 0.5rem;
        flex: 1;
    }

    .sprint-title h2 {
        font-size: 1rem;
        margin: 0;
    }

    .active-badge {
        color: #f0a500;
        font-size: 0.85rem;
        font-weight: bold;
    }

    .sprint-dates {
        font-size: 0.85rem;
        color: #888;
    }

    .sprint-actions {
        display: flex;
        gap: 0.5rem;
    }

    .sprint-actions button {
        padding: 0.3rem 0.6rem;
        border-radius: 6px;
        border: 1px solid #444;
        background: #2a2a2a;
        color: white;
        cursor: pointer;
        font-size: 0.85rem;
    }

    .activate-btn { border-color: #4caf50; color: #4caf50; }
    .complete-btn { border-color: #2196f3; color: #2196f3; }
    .danger-btn { border-color: #ff5555; color: #ff5555; }

    .sprint-goal {
        font-size: 0.85rem;
        color: #aaa;
        margin-bottom: 0.75rem;
        font-style: italic;
    }

    .board-group h4 {
        font-size: 0.8rem;
        color: #888;
        margin: 0.5rem 0 0.25rem;
        text-transform: uppercase;
        letter-spacing: 0.05em;
    }

    .default-badge {
        color: #f0a500;
        font-size: 0.75rem;
        margin-left: 0.25rem;
    }
    
    .empty {
        color: #555;
        font-size: 0.8rem;
        margin: 0;
    }

    .task-list {
        display: flex;
        flex-direction: column;
        gap: 0.25rem;
        margin-top: 0.25rem;
    }
</style>