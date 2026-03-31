<script lang="ts">
    import type { SprintResponse } from '../api/sprintApi';
    import type { TaskResponse } from '../api/taskApi';
    import type { BoardResponse } from '../api/boardApi';

    export let sprint: SprintResponse;
    export let tasks: TaskResponse[] = [];
    export let boards: BoardResponse[] = [];
    export let onActivate: (sprintId: string) => void = () => {};
    export let onPlan: (sprintId: string) => void = () => {};
    export let onEdit: (sprint: SprintResponse) => void = () => {};
    export let onComplete: (sprint: SprintResponse) => void = () => {};
    export let onDelete: (sprintId: string) => void = () => {};
    export let onRemoveTask: (taskId: string, sprintId: string) => void = () => {};

    // Taskok board szerint csoportosítva
    function getTasksByBoard(): [string, TaskResponse[]][] {
        let boardMap: Record<string, TaskResponse[]> = {};
        
        tasks.forEach(task => {
            const boardName = task.boardId
                ? (boards.find(b => b.id === task.boardId)?.name ?? 'Ismeretlen board')
                : 'Backlog';
            
            if (!boardMap[boardName]) boardMap[boardName] = [];
            boardMap[boardName].push(task);
        });
        
        return Object.entries(boardMap);
    }
</script>

<div class="sprint-card" class:active={sprint.state === 'Active'} 
                         class:planning={sprint.state === 'Planning'}
                         class:completed={sprint.state === 'Completed'}>
    
    <div class="sprint-header">
        <div class="sprint-title">
            {#if sprint.state === 'Active'}
                <span class="active-badge">★ AKTÍV</span>
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
                <button on:click={() => onEdit(sprint)}>✏ Szerkesztés</button>
                <button class="activate-btn" on:click={() => onActivate(sprint.id)}>▶ Aktiválás</button>
                <button class="danger-btn" on:click={() => onDelete(sprint.id)}>🗑 Törlés</button>
            {/if}
        </div>
    </div>

    {#if sprint.goal}
        <p class="sprint-goal">Cél: {sprint.goal}</p>
    {/if}

    <div class="sprint-tasks">
        {#each getTasksByBoard() as [boardName, boardTasks]}
            <div class="board-group">
                <h4>{boardName}</h4>
                {#each boardTasks as task}
                    <div class="sprint-task-row">
                        <span class="task-key">{task.taskKey}</span>
                        <span class="task-title">{task.title}</span>
                        {#if sprint.state !== 'Completed'}
                            <button class="remove-btn" 
                                on:click={() => onRemoveTask(task.id, sprint.id)}>✕</button>
                        {/if}
                    </div>
                {/each}
            </div>
        {/each}
        {#if tasks.length === 0}
            <p class="empty">Nincs task ebben a sprintben</p>
        {/if}
    </div>
</div>

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

    .sprint-task-row {
        display: flex;
        align-items: center;
        gap: 0.5rem;
        padding: 0.4rem 0.5rem;
        background: #2a2a2a;
        border-radius: 6px;
        margin-bottom: 0.25rem;
    }

    .task-key {
        font-size: 0.75rem;
        color: #888;
        min-width: 60px;
    }

    .task-title {
        flex: 1;
        font-size: 0.9rem;
    }

    .remove-btn {
        background: transparent;
        border: none;
        color: #aaa;
        cursor: pointer;
        font-size: 0.8rem;
        padding: 0;
    }

    .remove-btn:hover { color: #ff5555; }

    .empty {
        font-size: 0.85rem;
        color: #555;
        padding: 0.5rem;
    }
</style>