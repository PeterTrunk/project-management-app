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
    export let onColumnClick: (column: ColumnResponse) => void = () => {};
    export let isReordering: boolean = false;
</script>

<div class="column">
    <button 
        class="column-title-btn"
        on:click|stopPropagation={() => onColumnClick(column)}
        disabled={isReordering}
    >
        {column.name}
    </button>
    {#if isReordering}
        <div class="drag-handle">
            Fogja meg itt az átrendezéshez
        </div>
    {/if}
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
        width: 250px;
        border: 1px solid #333;
        height: calc(100% - 8px);
        display: flex;
        flex-direction: column;
    }

    .column-title-btn {
        background: transparent;
        border: none;
        color: #ccc;
        font-size: 1.1rem;
        font-weight: bold;
        text-align: center;
        width: 100%;
        cursor: pointer;
        padding: 0;
        margin-bottom: 0.5rem;
        flex-shrink: 0;
    }

    .drag-handle {
        font-size: 0.85rem;
        color: #666;
        text-align: center;
        padding: 0.25rem;
        border: 1px dashed #444;
        border-radius: 4px;
        margin-bottom: 0.5rem;
        cursor: grab;
    }

    .column-title-btn:hover:not(:disabled) {
        color: white;
    }

    .column-title-btn:disabled {
        cursor: default;
        color: #ccc;
    }

    .task-list {
        flex: 1;
        overflow-y: auto;
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