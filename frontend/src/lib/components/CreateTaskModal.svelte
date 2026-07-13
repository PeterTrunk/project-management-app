<script lang="ts">
    import { onMount } from 'svelte';
    import { boardStore } from '../stores/boardStore';
    import { createTaskAsync } from '../api/taskApi';
    import type { ColumnResponse } from '../api/columnApi';
    import { validateTaskTitle, validateTaskDescription, validateTaskDueDate } from '../validators';
    import LabelCard from './LabelCard.svelte';
    import { projectStore } from '../stores/projectStore';
    import type { LabelResponse } from '../api/labelApi';
    import { addLabelToTaskAsync } from '../api/labelApi';
    import { sprintStore } from '../stores/sprintStore';

    import { X, Plus } from 'lucide-svelte';

    export let isTaskCreationOpen = false;
    export let isBacklogMode: boolean = false;
    export let projectId: string;
    export let boardId: string | null = null;

    let modalRef: HTMLElement;

    $: if (isTaskCreationOpen && columns.length > 0) {
        columnId = columns.filter(c => c.position > 0)[0]?.id ?? '';
    }

    onMount(() => {
        modalRef?.focus();
    });

    let columns: ColumnResponse[] = [];
    let activeBoardName = '';
    boardStore.subscribe(state => {
        activeBoardName = state.activeBoard?.name ?? '';
        columns = state.columns;
    });

    let allLabels: LabelResponse[] = [];
    let selectedLabelIds: string[] = [];
    projectStore.subscribe(state => {
        allLabels = state.labels;
    });

    let activeSprintId: string = '';
    sprintStore.subscribe(state => {
        activeSprintId = state.activeSprint?.id ?? '';
    });

    
    let columnId = columns[0]?.id ?? '';

    let title: string;
    let description: string = '';
    let priority: string = '';
    let estimateInMinutes: number;
    let dueDate: string = '';

    let error = '';
    let success = '';

    async function handleCreateTask() {
        error = '';
        success = '';
        let errorOccured = false;
        const titleError = validateTaskTitle(title);
        const descError = validateTaskDescription(description);
        const dueDateError = validateTaskDueDate(new Date(dueDate));
        if(titleError){
            error = error + titleError;
            errorOccured = true;
        }
        if(descError){
            error = error + descError;
            errorOccured = true;
        }
        if(dueDateError){
            error = error + dueDateError;
            errorOccured = true;
        }
        if(errorOccured){
            return;
        }
        try {
            const response = await createTaskAsync(projectId, {
                columnId: isBacklogMode ? null : columnId,
                boardId: isBacklogMode ? null : boardId,
                sprintId: isBacklogMode ? null : (activeSprintId || null),
                title,
                description,
                priority: priority !== '' ? priority : null,
                estimateInMinutes,
                dueDate: dueDate ? new Date(dueDate) : null
            });
            success = 'Task létrehozva!';
            for (const labelId of selectedLabelIds) {
                await addLabelToTaskAsync(projectId, response.id, labelId);
            }
            const button = document.getElementById('create') as HTMLButtonElement;
            button.disabled = true;
            
        } catch (e: any) {
            console.error('Backend hiba:', e.response?.data);
            error = 'Hiba történt az task létrehozásakor!';
        }
    }

    export let onClose: () => void = () => {};
    function closeModal() {
        isTaskCreationOpen = false;
        onClose();
    }
</script>

<div class="modal-overlay" on:click|self={closeModal}
    //Accessability configolása
    bind:this={modalRef} //Autofocus helyett.
    on:keydown={(e) => e.key === 'Escape' && closeModal()}
    role="dialog"
    aria-modal="true"
    tabindex="-1"
    >
    <div class="modal-content">
        <button class="close-btn" type="button" on:click={closeModal}>
            <X size={16} />
        </button>
        <form on:submit|preventDefault={handleCreateTask}>
            <h1>Task Létrehozás {activeBoardName}-hoz</h1>
            Új Task címe:
            <input type="text" placeholder="Task cím" bind:value={title}/>
            {#if !isBacklogMode}
                Válasszon Oszlopot
                <select bind:value={columnId}>
                    {#each columns.filter(c => c.position > 0) as column}
                        <option value={column.id}>{column.name}</option>
                    {/each}
                </select>
            {/if}
            Labelek
            <div class="labels-grid">
                {#each allLabels as label}
                    <div class="label-select-row">
                        <LabelCard {label} showDelete={false} small={true} />
                        {#if selectedLabelIds.includes(label.id)}
                            <button type="button" class="label-remove-btn"
                                on:click={() => selectedLabelIds = selectedLabelIds.filter(id => id !== label.id)}>
                                <X size={12} />
                            </button>
                        {:else}
                            <button type="button" class="label-add-btn"
                                on:click={() => selectedLabelIds = [...selectedLabelIds, label.id]}>
                                <Plus size={12} />
                            </button>
                        {/if}
                    </div>
                {/each}
            </div>
            <div id="optional-fields">
                <h2>Opcionális mezők</h2>
                Task leírása
                <textarea placeholder="Leírás" bind:value={description}></textarea>
                Válasszon prioritást 
                <select bind:value={priority}>
                    <option value="">Nincs prioritás</option>
                    <option value="low">Alacsony</option>
                    <option value="medium">Közepes</option>
                    <option value="high">Magas</option>
                    <option value="critical">Kritikus</option>
                </select>
                <br>Becsült idő
                <input type="number" bind:value={estimateInMinutes}>
                Task határidő
                <input type="datetime-local" bind:value={dueDate}>
            </div>
            {#if error}
                <p id="failed">{error}</p>
            {/if}
            {#if success}
                <p id="success">{success}</p>
            {/if}
            <button type="submit" id="create">Létrehozás</button>
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

    input, textarea {
        background: var(--bg-input);
        border: 1px solid var(--border-hover);
        border-radius: 6px;
        color: var(--text-primary);
        padding: 0.5rem;
        font-size: 1rem;
        width: 100%;
    }

    input:focus, textarea:focus {
        outline: none;
        border-color: var(--accent-blue);
    }

    select {
        background: var(--bg-input);
        border: 1px solid var(--border-hover);
        border-radius: 6px;
        color: var(--text-primary);
        padding: 0.5rem;
        font-size: 1rem;
        width: 100%;
    }

    select:focus {
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

    .modal-content h1 {
        margin-top: 1.5rem;
        margin-bottom: 0.5rem;
        font-size: 1.5rem;
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

    #optional-fields {
        background: var(--bg-primary);
        border-radius: 8px;
        padding: 1rem;
        display: flex;
        flex-direction: column;
        gap: 0.75rem;
        margin-top: 0.5rem;
        border: 1px solid var(--border);
    }

    #optional-fields h2 {
        font-size: 1rem;
        color: var(--text-secondary);
        margin-bottom: 0.25rem;
    }

    .labels-grid {
        display: flex;
        flex-wrap: wrap;
        gap: 0.5rem;
    }

    .label-select-row {
        display: flex;
        align-items: center;
        gap: 0.25rem;
    }

    .label-remove-btn {
        display: flex;
        align-items: center;
        background: transparent;
        border: none;
        color: var(--accent-red);
        cursor: pointer;
        padding: 0.15rem;
        border-radius: 3px;
    }

    .label-remove-btn:hover { background: var(--accent-red-bg); }

    .label-add-btn {
        display: flex;
        align-items: center;
        background: transparent;
        border: none;
        color: var(--accent-green);
        cursor: pointer;
        padding: 0.15rem;
        border-radius: 3px;
    }

    .label-add-btn:hover { background: var(--accent-green-bg); }

    #success { color: var(--accent-green); }
    #failed  { color: var(--accent-red); white-space: pre-line; }
</style>