<script lang="ts">
    import { onMount } from 'svelte';
    import { boardStore, setActiveBoard, setBoards } from '../stores/boardStore';
    import { updateBoardAsync, getBoardsAsync } from '../api/boardApi';
    import { validateBoardName, validateBoardDescription } from '../validators';
    import type { BoardResponse } from '../api/boardApi';

    export let isUpdateBoardOpen = false;
    export let projectId: string;
    export let onClose: () => void = () => {};

    let modalRef: HTMLElement;

    onMount(() => {
        modalRef?.focus();
    });

    function closeModal() {
        isUpdateBoardOpen = false;
        onClose();
    }

    let activeBoard: BoardResponse | null = null;
    let currentDefaultBoard: BoardResponse | null = null;
    let name = '';
    let description = '';
    let isDefault = false;

    boardStore.subscribe(state => {
        activeBoard = state.activeBoard;
        currentDefaultBoard = state.boards.find(b => b.isDefault) ?? null;
        // Csak akkor inicializáljuk ha még nem szerkesztett a user
        if (activeBoard && name === '') {
            name = activeBoard.name;
            description = activeBoard.description ?? '';
            isDefault = activeBoard.isDefault;
        }
    });


    let error = '';
    let success = '';

    async function handleUpdateBoard() {
        error = '';
        success = '';
        let errorOccured = false;
        const nameError = validateBoardName(name);
        const descError = validateBoardDescription(description);
        if (nameError) {
            error = error + nameError;
            errorOccured = true;
        }
        if (descError) {
            error = error + descError;
            errorOccured = true;
        }
        if (errorOccured) return;

        try {
            const response = await updateBoardAsync(projectId, activeBoard!.id, { name, description, isDefault });
            success = 'Board módosítva!';
            setActiveBoard(response);
            const boards = await getBoardsAsync(projectId);
            setBoards(boards);
        } catch (e) {
            error = 'Hiba történt a board módosítása során!';
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
        <form on:submit|preventDefault={handleUpdateBoard}>
            <h1>Board módosítása</h1>
            Board neve:
            <input type="text" bind:value={name} placeholder="Board neve">
            Board leírása:
            <textarea bind:value={description} placeholder="Board leírása (opcionális)"></textarea>
            Legyen alapvető board?
            <input type="checkbox" bind:checked={isDefault}>
            Jelenlegi alapvető board: <span>{currentDefaultBoard?.name ?? 'Nincs'}</span>
            {#if error}
                <p id="failed">{error}</p>
            {/if}
            {#if success}
                <p id="success">{success}</p>
            {/if}
            <button type="submit">Módosítás</button>
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

    form {
        display: flex;
        flex-direction: column;
        gap: 0.75rem;
    }

    input[type="text"], textarea {
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

    textarea {
        resize: vertical;
        min-height: 80px;
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