<script lang="ts">
    import { onMount } from 'svelte';
    import { boardStore } from '../stores/boardStore';
    import { createBoardAsync } from '../api/boardApi';
    import { validateBoardDescription, validateBoardName, validateColumnName } from '../validators';
    import type { BoardResponse } from '../api/boardApi';
    import type { ProjectResponse } from '../api/projectApi';

    export let onClose: () => void = () => {};
    export let isBoardCreationOpen = false;
    export let projectId: string;
    export let activeProject: ProjectResponse;

    let modalRef: HTMLElement;

    onMount(() => {
        modalRef?.focus();
    });

    function closeModal() {
        isBoardCreationOpen = false;
        onClose();
    }
    
    let boards: BoardResponse[];
    let currentDefaultBoard: BoardResponse | null = null;
    boardStore.subscribe(state => {
        boards = state.boards;
        currentDefaultBoard = state.boards.find(b => b.isDefault) ?? null;
    });
    
    let name: string = '';
    let description: string = '';
    let isDefault:boolean = false;

    let error = '';
    let success = '';
    async function handleCreateBoard() {
        error = '';
        success = '';
        let errorOccured = false;
        let boardNameError = validateBoardName(name);
        let boardDescriptionError = validateBoardDescription(description);
        if(boardNameError){
            error = error + boardNameError;
            errorOccured = true;
        }
        if(boardDescriptionError){
            error = error + boardDescriptionError;
            errorOccured = true;
        }
        if(errorOccured){
            return;
        }
        try {
            const response = await createBoardAsync(projectId, { projectId, name, description, isDefault });
            const button = document.getElementById('create') as HTMLButtonElement;
            button.disabled = true;
            success = 'Board létrehozva!';
        } catch (e) {
            error = 'Hiba történt az task létrehozásakor!';
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
        <form on:submit|preventDefault={handleCreateBoard}>
            <h1>Új Board Létrehozás {activeProject.name}-hoz</h1>
            Új board neve:
            <input type="text" placeholder="Board Név" bind:value={name}>
            Új board leírása:
            <textarea placeholder="Board Leírása (Opcionális)" bind:value={description}></textarea>
            Legyen alapvető board? <input type="checkbox" bind:checked={isDefault}>
            Jelenlegi alapvető board 
            <span>{currentDefaultBoard ? currentDefaultBoard.name : 'Nincs alapvető board!'}</span>
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
        margin-bottom: 0.5rem;
        font-size: 1.5rem;
    }

    form {
        display: flex;
        flex-direction: column;
        gap: 1rem;
    }

    input[type="text"],
    textarea {
        background: var(--bg-input);
        border: 1px solid var(--border-hover);
        border-radius: 6px;
        color: var(--text-primary);
        padding: 0.5rem;
        font-size: 1rem;
        width: 100%;
    }

    input[type="text"]:focus,
    textarea:focus {
        outline: none;
        border-color: var(--accent-blue);
    }

    span {
        font-weight: bold;
        color: var(--accent-blue);
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