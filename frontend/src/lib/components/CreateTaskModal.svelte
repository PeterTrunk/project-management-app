<script lang="ts">
    import { onMount } from 'svelte';
    import { boardStore, setActiveBoard } from '../stores/boardStore';
    import { createTaskAsync } from '../api/taskApi';
    import type { ColumnResponse } from '../api/columnApi';
    import { validateTaskTitle, validateTaskDescription, validateTaskDueDate } from '../validators';

    export let isTaskCreationOpen = false;
    export let projectId: string;
    export let boardId: string;

    let modalRef: HTMLElement;

    onMount(() => {
        modalRef?.focus();
    });

    let columns: ColumnResponse[] = [];
    let activeBoardName = '';
    boardStore.subscribe(state => {
        activeBoardName = state.activeBoard?.name ?? '';
        columns = state.columns;
    });
    
    let columnId = columns[0]?.id ?? '';

    let title: string;
    let description: string;
    let sprintId: string = '';
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
            await createTaskAsync(projectId, {
                columnId,
                boardId,
                sprintId: sprintId !== '' ? sprintId : null,
                title,
                description,
                priority: priority !== '' ? priority : null,
                estimateInMinutes,
                dueDate: dueDate ? new Date(dueDate) : null
            });
            const button = document.getElementById('create') as HTMLButtonElement;
            button.disabled = true;
            success = 'Task létrehozva!';
        } catch (e) {
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
        <form on:submit|preventDefault={handleCreateTask}>
            <h1>Task Létrehozás {activeBoardName}-hoz</h1>
            Új Task címe:
            <input type="text" placeholder="Task cím" bind:value={title}/>
            Válasszon Oszlopot
            <select bind:value={columnId}>
                {#each columns as column}
                    <option value={column.id}>{column.name}</option>
                {/each}
            </select>
            <div id="optional-fields">
                <h2>Opcionális mezők</h2>
                <!-- TODO: Sprint választó - SprintStore elkészítése után -->
                <!-- <input type="text" bind:value={sprintId}> -->
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
    background: #2a2a2a;
    border: 1px solid #444;
    border-radius: 6px;
    color: white;
    padding: 0.5rem;
    font-size: 1rem;
    width: 100%;
}

input:focus, textarea:focus {
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

#optional-fields {
    background: #161616;
    border-radius: 8px;
    padding: 1rem;
    display: flex;
    flex-direction: column;
    gap: 0.75rem;
    margin-top: 0.5rem;
    border: 1px solid #2a2a2a;
}

#optional-fields h2 {
    font-size: 1rem;
    color: #aaa;
    margin-bottom: 0.25rem;
}


#success { color: greenyellow; }
#failed { color: red; white-space: pre-line; }
</style>