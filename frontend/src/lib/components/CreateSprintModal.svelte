<script lang="ts">
    import { onMount } from 'svelte';
    import type { ProjectResponse } from '../api/projectApi';
    import { createSprintAsync } from '../api/sprintApi';
    import { projectStore } from '../stores/projectStore';
    import { validateSprintDates, validateSprintGoal, validateSprintName } from '../validators';

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
            <button on:click={closeModal}>Bezárás</button>
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
    width: 500px;
    max-width: 95vw;
    max-height: 90vh;
    overflow-y: auto;
    display: flex;
    flex-direction: column;
    gap: 1rem;
    position: relative;
}

.modal-content h1 {
    margin-bottom: 0.5rem;
    font-size: 1.5rem;
}

form {
    display: flex;
    flex-direction: column;
    gap: 1rem;
}

input {
    background: #2a2a2a;
    border: 1px solid #444;
    border-radius: 6px;
    color: white;
    padding: 0.5rem;
    font-size: 1rem;
    width: 100%;
}

input:focus {
    outline: none;
    border-color: #666;
}

button {
    padding: 0.5rem 1rem;
    border-radius: 6px;
    cursor: pointer;
    width: fit-content;
    align-self: center;
}

#success { color: greenyellow; }
#failed { color: red; white-space: pre-line; }
</style>