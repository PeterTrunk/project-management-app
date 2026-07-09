<script lang="ts">
    import type { SprintResponse } from '../api/sprintApi';
    import type { TaskResponse } from '../api/taskApi';
    import type { BoardResponse } from '../api/boardApi';
    import BacklogTaskCard from './BacklogTaskCard.svelte';
    import TaskDetailModal from './TaskDetailModal.svelte';
    import { setActiveTask, taskStore } from '../stores/taskStore';

    import { Pencil, Undo2, CircleCheck, Play, Trash2, Star, ChevronRight, ChevronDown  } from 'lucide-svelte';

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
    export let onLoadTasks: ((sprintId: string) => Promise<void>) | null = null;
    export let tasksLoaded: boolean = true;

    let isTaskDetailOpen = false;

    let collapsed = sprint.state === 'Completed';

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
            <button class="collapse-btn" on:click={() => collapsed = !collapsed}>
                {#if collapsed}
                    <ChevronRight size={14} />
                {:else}
                    <ChevronDown size={14} />
                {/if}
            </button>
            {#if sprint.state === 'Active'}
                <button on:click={() => onEdit(sprint)}>
                    <Pencil size={14} /> Szerkesztés
                </button>
                <button on:click={() => onPlan(sprint.id)}>
                    <Undo2 size={14} /> Visszatervezés
                </button>
                <button class="complete-btn" on:click={() => onComplete(sprint)}>
                    <CircleCheck size={14} /> Lezárás
                </button>
            {:else if sprint.state === 'Planning'}
                <button on:click|stopPropagation={() => onEdit(sprint)}>
                    <Pencil size={14} /> Szerkesztés
                </button>
                <button class="activate-btn" on:click={() => onActivate(sprint.id)}>
                    <Play size={14} /> Aktiválás
                </button>
                <button class="danger-btn" on:click|stopPropagation={() => onDelete(sprint.id)}>
                    <Trash2 size={14} /> Törlés
                </button>
            {/if}
        </div>
    </div>
    {#if sprint.goal}
        <p class="sprint-goal">Cél: {sprint.goal}</p>
    {/if}
    
    {#if !collapsed}
        {#each sortedGroupedTasks as [boardName, boardTasks]}
            <div class="board-group">
                <h4>
                    {boardName}
                    {#if boards.find(b => b.name === boardName)?.isDefault}
                        <span class="default-badge"><Star size={12} /></span>
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
        {#if tasks.length === 0 && (sprint.state !== 'Completed' || tasksLoaded)}
            <div>
                <p class="empty">Még nincs hozzárendelt Task.</p>
            </div>
        {/if}
        {#if sprint.state === 'Completed' && onLoadTasks && !tasksLoaded}
            <button on:click={() => onLoadTasks!(sprint.id)}>
                Taskok betöltése
            </button>
        {/if}
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
        background: var(--bg-card);
        border-radius: 8px;
        padding: 1rem;
        border: 1px solid var(--border-subtle);
        margin-bottom: 0.75rem;
    }

    .sprint-card.active {
        border-color: var(--accent-yellow);
        background: var(--accent-yellow-bg);
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
        color: var(--text-primary);
    }

    .active-badge {
        color: var(--accent-yellow);
        font-size: 0.85rem;
        font-weight: bold;
    }

    .sprint-dates {
        font-size: 0.85rem;
        color: var(--text-muted);
    }

    .sprint-actions {
        display: flex;
        gap: 0.5rem;
        flex-wrap: wrap;
    }

    .sprint-actions button {
        display: flex;
        align-items: center;
        gap: 0.35rem;
        padding: 0.3rem 0.6rem;
        border-radius: 6px;
        border: 1px solid var(--border-hover);
        background: var(--bg-hover);
        color: var(--text-secondary);
        cursor: pointer;
        font-size: 0.85rem;
        transition: background 0.15s, color 0.15s;
    }

    .sprint-actions button:hover {
        background: var(--border-hover);
        color: var(--text-primary);
    }

    .activate-btn { border-color: var(--accent-green) !important; color: var(--accent-green) !important; }
    .activate-btn:hover { background: var(--accent-green-bg) !important; }

    .complete-btn { border-color: var(--accent-blue) !important; color: var(--accent-blue) !important; }
    .complete-btn:hover { background: var(--accent-blue-bg) !important; }

    .danger-btn { border-color: var(--accent-red) !important; color: var(--accent-red) !important; }
    .danger-btn:hover { background: var(--accent-red-bg) !important; }

    .sprint-goal {
        font-size: 0.85rem;
        color: var(--text-secondary);
        margin-bottom: 0.75rem;
        font-style: italic;
    }

    .board-group h4 {
        display: flex;
        align-items: center;
        gap: 0.25rem;
        font-size: 0.8rem;
        color: var(--text-muted);
        margin: 0.5rem 0 0.25rem;
        text-transform: uppercase;
        letter-spacing: 0.05em;
    }

    .default-badge {
        display: flex;
        align-items: center;
        color: var(--accent-yellow);
    }

    .empty {
        color: var(--text-muted);
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