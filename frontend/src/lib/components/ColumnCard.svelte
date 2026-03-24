<script lang="ts">
    import { dndzone } from 'svelte-dnd-action';
    import type { ColumnResponse } from '../api/columnApi';
    import type { TaskResponse } from '../api/taskApi';
    import TaskCard from './TaskCard.svelte';

    export let column: ColumnResponse;
    export let tasks: TaskResponse[];
    export let onConsider: (e: CustomEvent, columnId: string) => void;
    export let onFinalize: (e: CustomEvent, columnId: string) => void;
    export let onTaskClick: (task: TaskResponse) => void;
</script>

<div class="column">
    <h3>{column.name}</h3>
    <div class="task-list"
        use:dndzone={{
            items: tasks,
            flipDurationMs: 200,
            type: 'task',
            dropTargetStyle: { outline: '2px dashed #555' }
        }}
        on:consider={(e) => onConsider(e, column.id)}
        on:finalize={(e) => onFinalize(e, column.id)}
    >
        {#each tasks as task (task.id)}
            <TaskCard {task} onClick={onTaskClick} />
        {:else}
            <div class="empty-column-placeholder">
                Húzz ide egy taskot
            </div>
        {/each}
    </div>
</div>

<style>
    .column {
        background: #1e1e1e;
        border-radius: 8px;
        padding: 1rem;
        min-width: 250px;
        border: 1px solid #333;
        min-height: calc(100vh - 200px);
        display: flex;
        flex-direction: column;
    }

    .column h3 {
        margin-bottom: 0.5rem;
        font-size: 1rem;
        color: #ccc;
    }

    .task-list {
        flex: 1;
        min-height: 80px;
    }

    .empty-column-placeholder {
        color: #555;
        text-align: center;
        padding: 1rem;
        font-size: 0.85rem;
        pointer-events: none;
    }
</style>