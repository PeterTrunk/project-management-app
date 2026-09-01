<script lang="ts">
    import { onMount } from 'svelte';
    import { completeSprintAsync, getSprintsAsync, type SprintResponse } from '../api/sprintApi';
    import { getTasksAsync, type TaskResponse } from '../api/taskApi';
    import { setSprints } from '../stores/sprintStore';
    import { setTasks } from '../stores/taskStore';

    import { TriangleAlert, CircleCheck, X } from 'lucide-svelte';

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
            //console.error('Backend hiba részletek:', JSON.stringify(e.response?.data));
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
        <button class="close-btn" type="button" on:click={closeModal}>
            <X size={16} />
        </button>
        <h1>Sprint Lezárása — {sprint.name}</h1>
        
        {#if unfinishedTasks.length > 0}
            <div class="warning">
                <TriangleAlert size={16} /> {unfinishedTasks.length} befejezetlen task van a sprintben!
            </div>

            <div class="unfinished-list">
                {#each unfinishedTasks as task}
                    <div class="unfinished-task">
                        <span class="task-key">{task.taskKey}</span>
                        {#if task.priority}
                            <span class="priority priority-{task.priority}">{task.priority}</span>
                        {/if}
                        <span class="task-title truncate">{task.title}</span>
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
            <p class="all-done"><CircleCheck size={16} /> Minden task elkészült! A sprint lezárható.</p>
        {/if}

        {#if error}
            <p id="failed">{error}</p>
        {/if}

        <div class="buttons">
            <button type="button" on:click={closeModal}>Mégse</button>
            <button class="complete-btn" on:click={handleComplete}>
                <CircleCheck size={15} /> Sprint Lezárása
            </button>
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
        background: var(--shadow);
        display: flex;
        justify-content: center;
        align-items: center;
        z-index: 1000;
    }

    .modal-content {
        position: relative;
        background: var(--bg-card);
        border: 1px solid var(--border);
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

    @media (max-width: 480px) {
        .modal-content {
            padding: var(--card-padding);
        }
    }

    h1 {
        font-size: 1.3rem;
        border-bottom: 1px solid var(--border);
        padding-bottom: 0.75rem;
        word-break: break-word;
    }

    .warning {
        display: flex;
        align-items: center;
        gap: 0.5rem;
        background: var(--accent-yellow-bg);
        border: 1px solid var(--accent-yellow);
        border-radius: 6px;
        padding: 0.75rem;
        color: var(--accent-yellow);
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
        align-items: center;
        padding: 0.4rem 0.75rem;
        background: var(--bg-hover);
        border-radius: 6px;
        font-size: 0.9rem;
    }

    .task-key {
        color: var(--text-muted);
        min-width: 60px;
        flex-shrink: 0;
    }

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
        color: var(--text-secondary);
    }

    select {
        background: var(--bg-input);
        border: 1px solid var(--border-hover);
        border-radius: 6px;
        color: var(--text-primary);
        padding: 0.5rem;
        width: 100%;
    }

    select:focus {
        outline: none;
        border-color: var(--accent-blue);
    }

    .all-done {
        display: flex;
        align-items: center;
        gap: 0.5rem;
        color: var(--accent-green);
        font-size: 0.95rem;
    }

    .buttons {
        display: flex;
        justify-content: flex-end;
        gap: 0.75rem;
        margin-top: 0.5rem;
        flex-wrap: wrap;
    }

    button {
        display: flex;
        align-items: center;
        gap: 0.35rem;
        padding: 0.5rem 1rem;
        border-radius: 6px;
        cursor: pointer;
    }

    .close-btn {
        position: absolute;
        top: 0.75rem;
        right: 0.75rem;
        display: flex;
        align-items: center;
        background: transparent;
        border: none;
        color: var(--text-secondary);
        cursor: pointer;
        padding: 0.25rem;
        border-radius: 4px;
    }

    .close-btn:hover {
        color: var(--text-primary);
        background: var(--bg-hover);
    }

    .complete-btn {
        background: var(--accent-green-bg);
        border: 1px solid var(--accent-green);
        color: var(--accent-green);
        transition: background 0.15s;
    }

    .complete-btn:hover { background: var(--accent-green); color: #fff; }

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

    #failed { color: var(--accent-red); white-space: pre-line; word-break: break-word; }
</style>