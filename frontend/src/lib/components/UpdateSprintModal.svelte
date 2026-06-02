<script lang="ts">
    import { onMount } from 'svelte';
    import { updateSprintAsync, type SprintResponse } from '../api/sprintApi';
    import { projectStore } from '../stores/projectStore';
    import { validateSprintName, validateSprintGoal, validateSprintDates } from '../validators';

    export let isUpdateSprintOpen = false;
    export let projectId: string;
    export let sprint: SprintResponse;
    export let onClose: () => void = () => {};

    let modalRef: HTMLElement;
    onMount(() => modalRef?.focus());

    let name = sprint.name;
    let goal = sprint.goal ?? '';
    
    let startDate = sprint.startDate 
        ? new Date(sprint.startDate).toISOString().slice(0, 16) 
        : '';
    let endDate = sprint.endDate 
        ? new Date(sprint.endDate).toISOString().slice(0, 16) 
        : '';

    let error = '';
    let success = '';

    function closeModal() {
        isUpdateSprintOpen = false;
        onClose();
    }

    async function handleUpdateSprint() {
        error = '';
        success = '';
        let errorOccured = false;

        const nameError = validateSprintName(name);
        const goalError = validateSprintGoal(goal);
        const dateError = validateSprintDates(startDate, endDate);

        if (nameError) { error += nameError; errorOccured = true; }
        if (goalError) { error += goalError; errorOccured = true; }
        if (dateError) { error += dateError; errorOccured = true; }
        if (errorOccured) return;

        try {
            await updateSprintAsync(projectId, sprint.id, {
                name,
                goal: goal || null,
                startDate: startDate ? new Date(startDate) : null,
                endDate: endDate ? new Date(endDate) : null
            });
            success = 'Sprint módosítva!';
        } catch (e) {
            error = 'Hiba történt a sprint módosítása során!';
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
        <form on:submit|preventDefault={handleUpdateSprint}>
            <h1>Sprint Szerkesztése</h1>
            Sprint neve
            <input type="text" bind:value={name}>
            Sprint célja
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
            <button type="submit">Mentés</button>
            <button type="button" on:click={closeModal}>Bezárás</button>
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
        max-width: 95vw;
        max-height: 90vh;
        overflow-y: auto;
        position: relative;
    }

    form {
        display: flex;
        flex-direction: column;
        gap: 1rem;
    }

    h1 {
        font-size: 1.5rem;
        margin-bottom: 0.5rem;
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

    #success { color: var(--accent-green); }
    #failed  { color: var(--accent-red); white-space: pre-line; }
</style>