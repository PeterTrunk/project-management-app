<script lang="ts">
    import type { TaskResponse } from '../api/taskApi';
    import LabelCard from './LabelCard.svelte';
    import { projectStore } from '../stores/projectStore';
    import type { LabelResponse } from '../api/labelApi';
    import { teamStore } from '../stores/teamStore';
    import type { MemberResponse } from '../api/teamApi';

    let allLabels: LabelResponse[] = [];
    projectStore.subscribe(state => {
        allLabels = state.labels;
    });

    let members: MemberResponse[] = [];
    teamStore.subscribe(state => {
        members = state.members;
    });

    export let task: TaskResponse;
    export let onClick: (task: TaskResponse) => void = () => {};

    $: assignees = task.assigneeIds
        .map(id => members.find(m => m.userId === id))
        .filter(m => m !== undefined) as MemberResponse[];

    $: isOverdue = task.dueDate != null 
        && new Date(task.dueDate) < new Date() 
        && task.completedAt == null;

    // 1 napon belül
    $: isDueSoon = task.dueDate != null 
        && !isOverdue
        && task.completedAt == null
        && (new Date(task.dueDate).getTime() - new Date().getTime()) < 1 * 24 * 60 * 60 * 1000; 

    $: isCompleted = task.completedAt != null;
</script>

<div 
    class="task-card" 
    class:overdue={isOverdue} 
    class:due-soon={isDueSoon}
    class:completed={isCompleted}
    on:click={() => onClick(task)}
    on:keydown={(e) => e.key === 'Enter' && onClick(task)}
    role="button"
    tabindex="0"
    >
    <div class="task-header">
        <p class="task-key">{task.taskKey}</p>
        <!--
        {#if task.priority}
            <span class="priority priority-{task.priority}">{task.priority}</span>
        {/if} 
        -->
    </div>
    <p class="task-title">{task.title}</p>
    {#if task.labelIds.length > 0}
        <div class="labels-row">
            <!-- {console.log(JSON.stringify(task.labelIds))} -->    
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
                    {member.displayName.split(' ').map(n => n[0]).join('').toUpperCase().slice(0, 2)}
                </span>
            {/each}
        </div>
    {/if}
    {#if task.dueDate}
        <span class="due-date" class:overdue={isOverdue} class:due-soon={isDueSoon}>
            {new Date(task.dueDate).toLocaleString('hu-HU', { year: 'numeric', month: '2-digit', day: '2-digit', hour: '2-digit', minute: '2-digit' })}
        </span>
    {/if}
    {#if isCompleted}
        <span class="completed-badge">✓ Kész</span>
    {/if}
</div>

<style>
    .task-card {
        background: #2a2a2a;
        border-radius: 6px;
        padding: 0.75rem;
        margin-bottom: 0.5rem;
        border: 1px solid #333;
        cursor: pointer;
        display: flex;
        flex-direction: column;
        gap: 0.25rem;
    }

    .task-card:hover {
        border-color: #555;
    }

    .task-header {
        display: flex;
        justify-content: space-between;
        align-items: center;
    }

    .task-key {
        font-size: 0.75rem;
        color: #888;
    }

    .task-title {
        font-size: 0.9rem;
    }

    .due-date {
        font-size: 0.75rem;
        color: #888;
        font-weight: bold;
    }

    .priority {
        font-size: 0.75rem;
        padding: 0.2rem 0.5rem;
        border-radius: 4px;
        width: fit-content;
    }

    .labels-row {
        display: flex;
        flex-wrap: wrap;
        gap: 0.25rem;
        margin-top: 0.25rem;
    }

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
        title: attr(title);
    }

    .due-date.overdue {
        color: #ff5555;
        font-weight: bold;
    }

    .due-date.due-soon {
        color: #f0a500;
        font-weight: bold;
    }

    .task-card.overdue {
        border-left: 3px solid #ff5555;
    }

    .task-card.due-soon {
        border-left: 3px solid #f0a500;
    }

    .task-card.completed {
        border-left: 3px solid #4caf50;
    }

    .completed-badge {
        font-size: 0.75rem;
        color: #4caf50;
        font-weight: bold;
    }

    .priority-low { background: #1a3a1a; color: #4caf50; }
    .priority-medium { background: #3a3a1a; color: #ffeb3b; }
    .priority-high { background: #3a1a1a; color: #ff5722; }
    .priority-critical { background: #4a0000; color: #ff0000; }
    .priority-normal { background: #2a2a2a; color: #aaa; }
</style>