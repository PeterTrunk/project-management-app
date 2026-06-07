<script lang="ts">
    import { onMount } from 'svelte';
    import type { ProjectResponse } from '../api/projectApi';
    import { createSprintAsync } from '../api/sprintApi';
    import { projectStore } from '../stores/projectStore';
    import { validateSprintDates, validateSprintGoal, validateSprintName } from '../validators';

    import { X } from 'lucide-svelte';

    export let isSprintCreationOpen = false;
    export let projectId: string;
    let activeProject: ProjectResponse | null;

    let name: string = '';
    let goal: string = '';
    let startDate: string = '';
    let endDate: string = '';

    let error = '';
    let success = '';

    let modalRef: HTMLElement;
    onMount(() => {
        modalRef?.focus();
    });

    projectStore.subscribe(state => {
        activeProject = state.activeProject ?? null;
    });

    async function handleSprintCreation() {
        error = '';
        success = '';
        let errorOccured = false;
        let nameError = validateSprintName(name);
        let goalError = validateSprintGoal(goal);
        let dateError = validateSprintDates(startDate, endDate);
        
        if(nameError){
            error = error + nameError;
            errorOccured = true;
        }
        if(goalError){
            error = error + goalError;
            errorOccured = true;
        }
        if(dateError){
            error = error + dateError;
            errorOccured = true;
        }
        if(errorOccured){
            return;
        }
        try {
            const response = await createSprintAsync(projectId, {
                projectId: projectId,
                name: name,
                goal: goal,
                startDate: startDate ? new Date(startDate) : null,
                endDate: endDate ? new Date(endDate) : null,
                state: 'Planning'
            });
            const button = document.getElementById('create') as HTMLButtonElement;
            button.disabled = true;
            success = 'Sprint létrehozva!';
        } catch (e) {
             error = 'Hiba történt a sprint létrehozásakor!';
        }
    }

    export let onClose: () => void = () => {};
    function closeModal() {
        isSprintCreationOpen = false;
        onClose();
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
        <form>
            <h1>Sprint Létrehozás {activeProject?.name}-hoz</h1>
            Sprint neve
            <input type="text" bind:value={name}>
            Sprint goal
            <input type="text" bind:value={goal}>
            Sprint kezdete
            <input type="datetime-local" bind:value={startDate}>
            Sprint vége
            <input type="datetime-local" bind:value={endDate}>
            {#if error}
                <p id="failed">{error}</p>
            {/if}
            {#if success}
                <p id="success">{success}</p>
            {/if}
            <button type="submit" on:click={handleSprintCreation} id="create">Létrehozás</button>
        </form>
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
        background: var(--bg-card);
        border: 1px solid var(--border);
        padding: 2rem;
        border-radius: 8px;
        width: 500px;
        display: flex;
        flex-direction: column;
        gap: 1rem;
        position: relative;
    }

    .modal-content h1 {
        margin-top: 1.5rem;
        margin-bottom: 0.5rem;
        font-size: 1.5rem;
    }

    form {
        display: flex;
        flex-direction: column;
        gap: 1rem;
    }

    input {
        background: var(--bg-input);
        border: 1px solid var(--border-hover);
        border-radius: 6px;
        color: var(--text-primary);
        padding: 0.5rem;
        font-size: 1rem;
        width: 100%;
    }

    input:focus {
        outline: none;
        border-color: var(--accent-blue);
    }

    button {
        padding: 0.5rem 1rem;
        border-radius: 6px;
        cursor: pointer;
        width: fit-content;
        align-self: center;
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

    button[type="submit"] {
        display: flex;
        align-items: center;
        gap: 0.4rem;
        background: var(--accent-blue-bg);
        border: 1px solid var(--accent-blue);
        color: var(--accent-blue);
        font-size: 0.9rem;
        transition: background 0.15s;
    }

    button[type="submit"]:hover {
        background: var(--accent-blue);
        color: #fff;
    }

    #success { color: var(--accent-green); }
    #failed  { color: var(--accent-red); white-space: pre-line; }
</style>