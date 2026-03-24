<script lang="ts">
    import type { TaskResponse } from '../api/taskApi';

    export let task: TaskResponse;
    export let onClick: (task: TaskResponse) => void = () => {};
</script>

<div 
    class="task-card" 
    on:click={() => onClick(task)}
    on:keydown={(e) => e.key === 'Enter' && onClick(task)}
    role="button"
    tabindex="0"
>
    <div class="task-header">
        <p class="task-key">{task.taskKey}</p>
        {#if task.priority}
            <span class="priority priority-{task.priority}">{task.priority}</span>
        {/if}
    </div>
    <p class="task-title">{task.title}</p>
    {#if task.dueDate}
        <span class="due-date">Határidő: {new Date(task.dueDate).toLocaleDateString('hu-HU')}</span>
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
    }

    .priority {
        font-size: 0.75rem;
        padding: 0.2rem 0.5rem;
        border-radius: 4px;
        width: fit-content;
    }

    .priority-low { background: #1a3a1a; color: #4caf50; }
    .priority-high { background: #3a1a1a; color: #ff5722; }
    .priority-critical { background: #4a0000; color: #ff0000; }
    .priority-normal { background: #2a2a2a; color: #aaa; }
</style>