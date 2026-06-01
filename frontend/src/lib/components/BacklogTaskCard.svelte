<script lang="ts">
    import type { TaskResponse } from '../api/taskApi';
    import type { SprintResponse } from '../api/sprintApi';
    import type { BoardResponse } from '../api/boardApi';
    import LabelCard from './LabelCard.svelte';
    import { projectStore } from '../stores/projectStore';
    import type { LabelResponse } from '../api/labelApi';
    import { assignTaskToBoardAsync } from '../api/taskApi';
    import { teamStore } from '../stores/teamStore';
import type { MemberResponse } from '../api/teamApi';
    
    export let task: TaskResponse;
    export let boards: BoardResponse[] = [];
    export let sprints: SprintResponse[] = [];
    export let projectId: string = '';
    export let onAssignToSprint: (taskId: string, sprintId: string) => void = () => {};
    export let onBoardAssigned: () => Promise<void> = async () => {};
    export let onDelete: (taskId: string) => void = () => {};
    export let onOpenDetail: (task: TaskResponse) => void = () => {};

    let allLabels: LabelResponse[] = [];
    let members: MemberResponse[] = [];
    let isMenuOpen = false;
    


    $: selectedBoardId = task.boardId ?? '';
    $: selectedSprintId = task.sprintId ?? '';


    projectStore.subscribe(state => {
        allLabels = state.labels;
    });

    teamStore.subscribe(state => {
        members = state.members;
    });

    function handleSprintAssign() {
        if (selectedSprintId === '') {
            onAssignToSprint(task.id, '');
            return;
        }
        onAssignToSprint(task.id, selectedSprintId);
        isMenuOpen = false;
    }
    
    async function handleAssignToBoard() {
        console.log('handleAssignToBoard called, selectedBoardId:', selectedBoardId);
        try {
            const response = await assignTaskToBoardAsync(projectId, task.id, {
                boardId: selectedBoardId === '' ? null : selectedBoardId
            });
            console.log('response:', response);
            await onBoardAssigned();
        } catch (e: any) {
            console.error('Hiba:', e.response?.data);
        }
    }

    $: assignees = task.assigneeIds
        .map(id => members.find(m => m.userId === id))
        .filter(m => m !== undefined) as MemberResponse[];

    $: isOverdue = task.dueDate != null 
        && new Date(task.dueDate) < new Date() 
        && task.completedAt == null;

    $: isDueSoon = task.dueDate != null 
        && !isOverdue
        && task.completedAt == null
        && (new Date(task.dueDate).getTime() - new Date().getTime()) < 1 * 24 * 60 * 60 * 1000;
    
    $: isCompleted = task.completedAt != null;
</script>

<div 
    class="backlog-task-card"
    class:overdue={isOverdue} 
    class:due-soon={isDueSoon}
    class:completed={isCompleted}
    >
    <div class="card-main" 
        on:click={() => onOpenDetail(task)}
        on:keydown={(e) => e.key === 'Enter' && onOpenDetail(task)}
        role="button"
        tabindex="0"
    >
        <div class="card-header">
            <span class="task-key">{task.taskKey}</span>
            {#if task.priority}
                <span class="priority priority-{task.priority}">{task.priority}</span>
            {/if}
            <span class="task-title">{task.title}</span>
            <!-- Hamburger menü -->
            <div class="card-actions">
                <button class="menu-btn" on:click|stopPropagation={() => isMenuOpen = !isMenuOpen}>☰</button>
                {#if isMenuOpen}
                    <div class="dropdown-menu">
                        <div class="menu-section">
                            <p class="menu-label">Board hozzárendelés:</p>
                            <select bind:value={selectedBoardId} on:change={handleAssignToBoard}>
                                <option value="">Nincs Board</option>
                                {#each boards as board}
                                    <option value={board.id}>{board.name}</option>
                                {/each}
                            </select>
                            <p class="menu-label">Sprint hozzárendelés:</p>
                            <select bind:value={selectedSprintId} on:change={handleSprintAssign}>
                                <option value="">Projekt Backlog</option>
                                {#each sprints as sprint}
                                    <option value={sprint.id}>{sprint.name}</option>
                                {/each}
                            </select>
                        </div>
                        <div class="menu-divider"></div>
                        <button class="menu-delete-btn" on:click|stopPropagation={() => {
                            onDelete(task.id);
                            isMenuOpen = false;
                        }}>
                            🗑 Törlés
                        </button>
                    </div>
                {/if}
            </div>
        </div>
        <div class="card-footer">
            {#if task.labelIds.length > 0}
                <div class="labels-row">
                    {#each task.labelIds as labelId (labelId)}
                        {@const label = allLabels.find(l => l.id === labelId)}
                        {#if label}
                            <LabelCard {label} showDelete={false} small={true} />
                        {/if}
                    {/each}
                </div>
            {/if}

            {#if assignees.length > 0}
                <div class="assignees-row">
                    {#each assignees as member}
                        <span class="assignee-badge" title={member.displayName}>
                            {member.displayName.split(' ').map((n: string) => n[0]).join('').toUpperCase().slice(0, 2)}
                        </span>
                    {/each}
                </div>
            {/if}
            <div class="due-completed">
                {#if task.dueDate}
                    <span class="due-date" class:overdue={isOverdue} class:due-soon={isDueSoon}>
                        {new Date(task.dueDate).toLocaleString('hu-HU', { year: 'numeric', month: '2-digit', day: '2-digit', hour: '2-digit', minute: '2-digit' })}
                    </span>
                {/if}
                {#if isCompleted}
                    <span class="completed-badge">✓ Kész</span>
                {/if}
            </div>
        </div>
    </div>

    
</div>

<style>
    .backlog-task-card {
        display: flex;
        align-items: flex-start;
        gap: 0.5rem;
        background: #2a2a2a;
        border-radius: 6px;
        border: 1px solid #333;
        padding: 0.75rem;
        position: relative;
    }

    .backlog-task-card:hover {
        border-color: #555;
    }

    .card-main {
        flex: 1;
        cursor: pointer;
        display: flex;
        flex-direction: column;
        gap: 0.25rem;
    }

    .card-header {
        display: flex;
        align-items: center;
        gap: 0.5rem;
        flex-wrap: wrap;
    }

    .task-key {
        font-size: 0.75rem;
        color: #888;
    }

    .task-title {
        font-size: 0.9rem;
        margin: 0;
    }

    .due-date {
        font-size: 0.75rem;
        color: #888;
        margin-left: auto;
        font-weight: bold;
    }

    .labels-row {
        display: flex;
        flex-wrap: wrap;
        gap: 0.25rem;
        margin-top: 0.25rem;
    }

    .priority {
        font-size: 0.75rem;
        padding: 0.2rem 0.4rem;
        border-radius: 4px;
    }

    .due-date.overdue {
        color: #ff5555;
        font-weight: bold;
    }

    .due-date.due-soon {
        color: #f0a500;
        font-weight: bold;
    }

    .backlog-task-card.overdue {
        border-left: 3px solid #ff5555;
    }

    .backlog-task-card.due-soon {
        border-left: 3px solid #f0a500;
    }

    .backlog-task-card.completed {
        border-left: 3px solid #4caf50;
    }

    .completed-badge {
        font-size: 0.75rem;
        color: #4caf50;
        font-weight: bold;
    }

    .due-completed {
        display: flex;
        align-items: center;
        gap: 0.5rem;
        margin-left: auto;
    }

    .priority-low { background: #1a3a1a; color: #4caf50; }
    .priority-medium { background: #3a3a1a; color: #ffeb3b; }
    .priority-high { background: #3a1a1a; color: #ff5722; }
    .priority-critical { background: #4a0000; color: #ff0000; }
    .priority-normal { background: #2a2a2a; color: #aaa; }

    .card-actions {
        position: relative;
        margin-left: auto;
    }

    .menu-btn {
        background: transparent;
        border: none;
        color: #aaa;
        cursor: pointer;
        font-size: 1rem;
        padding: 0.2rem 0.4rem;
        border-radius: 4px;
    }

    .menu-btn:hover {
        background: #333;
        color: white;
    }

    .dropdown-menu {
        position: absolute;
        right: 0;
        bottom: 100%;
        top: auto;
        background: #2a2a2a;
        border: 1px solid #444;
        border-radius: 6px;
        padding: 0.75rem;
        min-width: 200px;
        z-index: 1000;
        display: flex;
        flex-direction: column;
        gap: 0.5rem;
    }

    .menu-label {
        font-size: 0.8rem;
        color: #888;
        margin: 0 0 0.25rem;
    }

    .menu-section {
        display: flex;
        flex-direction: column;
        gap: 0.4rem;
    }

    .menu-section select {
        background: #1e1e1e;
        border: 1px solid #444;
        border-radius: 6px;
        color: white;
        padding: 0.3rem 0.5rem;
        font-size: 0.85rem;
        width: 100%;
    }

    .menu-action-btn {
        background: #333;
        border: 1px solid #444;
        color: white;
        padding: 0.3rem 0.5rem;
        border-radius: 6px;
        cursor: pointer;
        font-size: 0.85rem;
        width: 100%;
    }

    .menu-action-btn:hover { background: #444; }

    .menu-divider {
        border-top: 1px solid #444;
    }

    .menu-delete-btn {
        background: transparent;
        border: none;
        color: #ff5555;
        cursor: pointer;
        font-size: 0.85rem;
        text-align: left;
        padding: 0.25rem 0;
    }

    .menu-delete-btn:hover { color: #ff3333; }

    .assignees-row {
        display: flex;
        flex-wrap: wrap;
        gap: 0.25rem;
        margin-top: 0.25rem;
    }

    .assignee-badge {
        width: 32px;
        height: 32px;
        border-radius: 50%;
        background: #1a2a3a;
        color: #4a9eff;
        display: flex;
        align-items: center;
        justify-content: center;
        font-size: 0.9rem;
        font-weight: bold;
    }

    .card-footer {
        display: flex;
        align-items: center;
        gap: 0.5rem;
        flex-wrap: wrap;
        margin-top: 0.25rem;
    }
</style>