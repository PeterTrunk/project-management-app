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
    
    import { Trash2, TextAlignJustify, Check } from 'lucide-svelte';

    export let showMenu: boolean = true;
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
             {#if showMenu}
                <div class="card-actions">
                    <button class="menu-btn" on:click|stopPropagation={() => isMenuOpen = !isMenuOpen}>
                        <TextAlignJustify size={15} />
                    </button>
                    {#if isMenuOpen}
                        <div 
                            class="dropdown-menu" 
                            on:click|stopPropagation 
                            on:keydown|stopPropagation
                            role="menu"
                            tabindex="-1"
                            >
                            <div class="menu-section">
                                <p class="menu-label">Board hozzárendelés:</p>
                                <select bind:value={selectedBoardId} on:change={handleAssignToBoard} on:click|stopPropagation>
                                    <option value="">Nincs Board</option>
                                    {#each boards as board}
                                        <option value={board.id}>{board.name}</option>
                                    {/each}
                                </select>
                                <p class="menu-label">Sprint hozzárendelés:</p>
                                <select bind:value={selectedSprintId} on:change={handleSprintAssign} on:click|stopPropagation>
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
                                <Trash2 size={14} /> Törlés
                            </button>
                        </div>
                    {/if}
                </div>
            {/if}
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
                    <span class="completed-badge"><Check size={12} /> Kész</span>
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
        background: var(--bg-hover);
        border-radius: 1px;
        border: 1px solid var(--border-subtle);
        padding: 0.75rem;
        position: relative;
    }

    .backlog-task-card:hover {
        border-color: var(--border-hover);
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
        color: var(--text-muted);
    }

    .task-title {
        font-size: 0.9rem;
        color: var(--text-primary);
        margin: 0;
    }

    .due-date {
        font-size: 0.75rem;
        color: var(--text-muted);
        margin-left: auto;
        font-weight: bold;
    }

    .due-date.overdue  { color: var(--accent-red); }
    .due-date.due-soon { color: var(--accent-yellow); }

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

    .priority-low      { background: var(--accent-green-bg);  color: var(--accent-green); }
    .priority-medium   { background: var(--accent-yellow-bg); color: var(--accent-yellow); }
    .priority-high     { background: var(--accent-red-bg);    color: var(--accent-yellow); }
    .priority-critical { background: var(--accent-red-bg);    color: var(--accent-red); }
    .priority-normal   { background: var(--bg-hover);         color: var(--text-muted); }

    .backlog-task-card.overdue   { border-left: 3px solid var(--accent-red); }
    .backlog-task-card.due-soon  { border-left: 3px solid var(--accent-yellow); }
    .backlog-task-card.completed { border-left: 3px solid var(--accent-green); }

    .completed-badge {
        display: flex;
        align-items: center;
        gap: 0.25rem;
        font-size: 0.75rem;
        color: var(--accent-green);
        font-weight: bold;
    }

    .due-completed {
        display: flex;
        align-items: center;
        gap: 0.5rem;
        margin-left: auto;
    }

    /* ── Card actions / menu ── */
    .card-actions {
        position: relative;
        margin-left: auto;
    }

    .menu-btn {
        display: flex;
        align-items: center;
        background: transparent;
        border: none;
        color: var(--text-secondary);
        cursor: pointer;
        padding: 0.2rem 0.4rem;
        border-radius: 4px;
    }

    .menu-btn:hover {
        background: var(--bg-hover);
        color: var(--text-primary);
    }

    .dropdown-menu {
        position: absolute;
        right: 0;
        bottom: 100%;
        background: var(--bg-card);
        border: 1px solid var(--border-hover);
        border-radius: 6px;
        padding: 0.75rem;
        min-width: 200px;
        z-index: 1000;
        display: flex;
        flex-direction: column;
        gap: 0.5rem;
        box-shadow: 0 4px 12px var(--shadow);
    }

    .menu-label {
        font-size: 0.8rem;
        color: var(--text-muted);
        margin: 0 0 0.25rem;
    }

    .menu-section {
        display: flex;
        flex-direction: column;
        gap: 0.4rem;
    }

    .menu-section select {
        background: var(--bg-secondary);
        border: 1px solid var(--border-hover);
        border-radius: 6px;
        color: var(--text-primary);
        padding: 0.3rem 0.5rem;
        font-size: 0.85rem;
        width: 100%;
    }

    .menu-divider {
        border-top: 1px solid var(--border);
    }

    .menu-delete-btn {
        display: flex;
        align-items: center;
        gap: 0.4rem;
        background: transparent;
        border: none;
        color: var(--accent-red);
        cursor: pointer;
        font-size: 0.85rem;
        text-align: left;
        padding: 0.25rem 0;
    }

    .menu-delete-btn:hover { color: var(--accent-red); opacity: 0.8; }

    /* ── Assignees ── */
    .assignees-row {
        display: flex;
        flex-wrap: wrap;
        gap: 0.25rem;
        margin-top: 0.25rem;
    }

    .assignee-badge {
        width: 28px;
        height: 28px;
        border-radius: 50%;
        background: var(--accent-blue-bg);
        color: var(--accent-blue);
        display: flex;
        align-items: center;
        justify-content: center;
        font-size: 0.75rem;
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