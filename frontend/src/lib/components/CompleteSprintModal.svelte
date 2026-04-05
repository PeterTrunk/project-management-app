<script lang="ts">
    import { onMount } from 'svelte';
    import { completeSprintAsync, getSprintsAsync, type SprintResponse } from '../api/sprintApi';
    import { getTasksAsync, type TaskResponse } from '../api/taskApi';
    import { setSprints } from '../stores/sprintStore';
    import { setTasks } from '../stores/taskStore';

    export let isCompleteSprintOpen = false;
    export let projectId: string;
    export let sprint: SprintResponse;
    export let unfinishedTasks: TaskResponse[] = [];
    export let sprints: SprintResponse[] = [];
    export let onClose: () => void = () => {};

    let modalRef: HTMLElement;
    onMount(() => modalRef?.focus());

    let selectedTargetSprintId: string = '';
    let moveToBacklog = true;
    let error = '';

    function closeModal() {
        isCompleteSprintOpen = false;
        onClose();
    }

    async function handleComplete() {
        error = '';
        try {
            const targetSprintId = moveToBacklog ? null : selectedTargetSprintId;
            
            if (!moveToBacklog && !selectedTargetSprintId) {
                error = 'Válassz célsprintet vagy helyezd Backlogba!';
                return;
            }

            await completeSprintAsync(projectId, sprint.id, targetSprintId === null ? undefined : targetSprintId);
            
            const data = await getSprintsAsync(projectId);
            setSprints(data);
            const _tasks = await getTasksAsync(projectId);
            setTasks(_tasks);
            
            closeModal();
        } catch (e: any) {
            console.error('Backend hiba:', e.response?.data);
            console.error('Backend hiba részletek:', JSON.stringify(e.response?.data));
            error = e.response?.data ?? 'Hiba történt a sprint lezárásakor!';
        }
    }
</script>

<div class="modal-overlay" on:click|self={closeModal}
    bind:this={modalRef}
    on:keydown={(e) => e.key === 'Escape' && closeModal()}
    role="dialog"
    aria-modal="true"
    tabindex="-1"
>
    <div class="modal-content">
        <h1>Sprint Lezárása — {sprint.name}</h1>

        {#if unfinishedTasks.length > 0}
            <div class="warning">
                ⚠ {unfinishedTasks.length} befejezetlen task van a sprintben!
            </div>

            <div class="unfinished-list">
                {#each unfinishedTasks as task}
                    <div class="unfinished-task">
                        <span class="task-key">{task.taskKey}</span>
                        {#if task.priority}
                            <span class="priority priority-{task.priority}">{task.priority}</span>
                        {/if}
                        <span class="task-title">{task.title}</span>
                    </div>
                {/each}
            </div>
            
            <div class="options">
                <label>
                    <input type="radio" bind:group={moveToBacklog} value={true}>
                    Visszarakás Backlogba
                </label>
                <label>
                    <input type="radio" bind:group={moveToBacklog} value={false}>
                    Áthelyezés következő sprintbe
                </label>
            </div>

            {#if !moveToBacklog}
                <select bind:value={selectedTargetSprintId}>
                    <option value="">Válassz sprintet</option>
                    {#each sprints.filter(s => s.state === 'Planning' && s.id !== sprint.id) as s}
                        <option value={s.id}>{s.name}</option>
                    {/each}
                </select>
            {/if}
        {:else}
            <p class="all-done">✓ Minden task elkészült! A sprint lezárható.</p>
        {/if}

        {#if error}
            <p id="failed">{error}</p>
        {/if}

        <div class="buttons">
            <button type="button" on:click={closeModal}>Mégse</button>
            <button class="complete-btn" on:click={handleComplete}>✓ Sprint Lezárása</button>
        </div>
    </div>
</div>

<style>
    .modal-overlay {
        position: fixed;
        top: 0;
        left: 0;
        width: 100%;
        height: 100%;
        background: rgba(0, 0, 0, 0.5);
        display: flex;
        justify-content: center;
        align-items: center;
        z-index: 1000;
    }

    .modal-content {
        background: #1e1e1e;
        padding: 2rem;
        border-radius: 8px;
        width: 550px;
        max-width: 95vw;
        max-height: 90vh;
        overflow-y: auto;
        display: flex;
        flex-direction: column;
        gap: 1rem;
    }

    h1 {
        font-size: 1.3rem;
        border-bottom: 1px solid #333;
        padding-bottom: 0.75rem;
    }

    .warning {
        background: #3a2a00;
        border: 1px solid #f0a500;
        border-radius: 6px;
        padding: 0.75rem;
        color: #f0a500;
        font-size: 0.9rem;
    }

    .unfinished-list {
        display: flex;
        flex-direction: column;
        gap: 0.25rem;
        max-height: 200px;
        overflow-y: auto;
    }

    .unfinished-task {
        display: flex;
        gap: 0.5rem;
        padding: 0.4rem 0.75rem;
        background: #2a2a2a;
        border-radius: 6px;
        font-size: 0.9rem;
    }

    .task-key { color: #888; min-width: 60px; }

    .options {
        display: flex;
        flex-direction: column;
        gap: 0.5rem;
    }

    .options label {
        display: flex;
        align-items: center;
        gap: 0.5rem;
        cursor: pointer;
        font-size: 0.9rem;
    }

    select {
        background: #2a2a2a;
        border: 1px solid #444;
        border-radius: 6px;
        color: white;
        padding: 0.5rem;
        width: 100%;
    }

    .all-done {
        color: #4caf50;
        font-size: 0.95rem;
    }

    .buttons {
        display: flex;
        justify-content: flex-end;
        gap: 0.75rem;
        margin-top: 0.5rem;
    }

    button {
        padding: 0.5rem 1rem;
        border-radius: 6px;
        cursor: pointer;
    }

    .complete-btn {
        background: #1a3a1a;
        border: 1px solid #4caf50;
        color: #4caf50;
    }

    .priority {
        font-size: 0.75rem;
        padding: 0.2rem 0.4rem;
        border-radius: 4px;
    }

    .priority-low { background: #1a3a1a; color: #4caf50; }
    .priority-medium { background: #3a3a1a; color: #ffeb3b; }
    .priority-high { background: #3a1a1a; color: #ff5722; }
    .priority-critical { background: #4a0000; color: #ff0000; }
    .priority-normal { background: #2a2a2a; color: #aaa; }

    .complete-btn:hover { background: #2a4a2a; }

    #failed { color: red; white-space: pre-line; }
</style>