<script lang="ts">
    import { onMount } from 'svelte';
    import { updateColumnAsync, deleteColumnAsync } from '../api/columnApi';
    import { validateColumnName, validateColumnStatus } from '../validators';
    import type { ColumnResponse } from '../api/columnApi';
    import ConfirmModal from './ConfirmModal.svelte';

    export let isColumnDetailOpen = false;
    export let projectId: string;
    export let boardId: string;
    export let column: ColumnResponse;
    export let onClose: () => void = () => {};

    let modalRef: HTMLElement;

    onMount(() => {
        modalRef?.focus();
    });

    function closeModal() {
        isColumnDetailOpen = false;
        onClose();
    }

    let isEditing = false;
    let editName = column.name;
    let editMapsToStatus = column.mapsToStatus;
    let editWipLimit = column.wipLimit;
    let hasWip = column.wipLimit !== null;

    let error = '';
    let success = '';

    // ConfirmModal
    let isConfirmOpen = false;
    let confirmTitle = '';
    let confirmMessage = '';
    let confirmAction: () => Promise<void> = async () => {};

    function openConfirm(title: string, message: string, action: () => Promise<void>) {
        confirmTitle = title;
        confirmMessage = message;
        confirmAction = action;
        isConfirmOpen = true;
    }

    async function handleUpdate() {
        error = '';
        success = '';
        let errorOccured = false;
        const nameError = validateColumnName(editName);
        const statusError = validateColumnStatus(editMapsToStatus);
        if (nameError) { error += nameError; errorOccured = true; }
        if (statusError) { error += statusError; errorOccured = true; }
        if (errorOccured) return;

        try {
            await updateColumnAsync(projectId, boardId, column.id, {
                name: editName,
                mapsToStatus: editMapsToStatus,
                wipLimit: hasWip ? editWipLimit : null,
            });
            success = 'Oszlop módosítva!';
            isEditing = false;
        } catch (e) {
            error = 'Hiba történt a módosítás során!';
        }
    }

    async function handleDelete() {
        try {
            await deleteColumnAsync(projectId, boardId, column.id);
            closeModal();
        } catch (e) {
            error = 'Hiba történt a törlés során! (Lehet hogy taskok vannak az oszlopban)';
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
        <div class="header-actions">
            <button class="edit-btn" on:click={() => isEditing = !isEditing}>
                {isEditing ? '✕ Mégse' : '✏ Szerkesztés'}
            </button>
            <button class="delete-btn" on:click={() => openConfirm(
                'Oszlop törlése',
                'Biztosan törölni szeretnéd az oszlopot? Az oszlopban lévő taskok nem törölhetők, előbb helyezd át őket!',
                handleDelete
            )}>🗑 Törlés</button>
        </div>
        <button class="close-btn" type="button" on:click={closeModal}>✕</button>

        <h1>{column.name}</h1>
        <p>Státusz: <span>{column.mapsToStatus}</span></p>
        <p>WIP limit: <span>{column.wipLimit ?? 'Nincs limit'}</span></p>

        {#if isEditing}
            <form on:submit|preventDefault={() => openConfirm(
                'Módosítások mentése',
                'Biztosan menteni szeretnéd a változtatásokat?',
                handleUpdate
            )}>
                Oszlop neve:
                <input type="text" bind:value={editName} placeholder="Oszlop neve">
                Státusz:
                <input type="text" bind:value={editMapsToStatus} placeholder="Státusz">
                Legyen WIP limit?
                <input type="checkbox" bind:checked={hasWip}>
                {#if hasWip}
                    WIP limit:
                    <input type="number" bind:value={editWipLimit}>
                {/if}
                <button type="submit">Mentés</button>
            </form>
        {/if}

        {#if error}
            <p id="failed">{error}</p>
        {/if}
        {#if success}
            <p id="success">{success}</p>
        {/if}
    </div>
</div>

{#if isConfirmOpen}
    <ConfirmModal
        bind:isOpen={isConfirmOpen}
        title={confirmTitle}
        message={confirmMessage}
        confirmText="Megerősítés"
        onConfirm={confirmAction}
    />
{/if}

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
        margin-top: 2.5rem;
        padding-bottom: 1rem;
        border-bottom: 1px solid #333;
    }

    .header-actions {
        position: absolute;
        top: 0.75rem;
        left: 0.75rem;
        display: flex;
        gap: 0.5rem;
    }

    .edit-btn, .delete-btn {
        background: transparent;
        border: none;
        color: #aaa;
        font-size: 1.2rem;
        cursor: pointer;
    }

    .close-btn {
        position: absolute;
        top: 0.75rem;
        right: 0.75rem;
        background: transparent;
        border: none;
        color: #aaa;
        font-size: 1.2rem;
        cursor: pointer;
    }

    .edit-btn:hover { color: white; }
    .close-btn:hover { color: white; }
    .delete-btn:hover { color: #ff5555; }

    form {
        display: flex;
        flex-direction: column;
        gap: 0.75rem;
    }

    input[type="text"], input[type="number"] {
        background: #2a2a2a;
        border: 1px solid #444;
        border-radius: 6px;
        color: white;
        padding: 0.5rem;
        width: 100%;
    }

    input:focus { outline: none; border-color: #666; }

    button {
        padding: 0.5rem 1rem;
        border-radius: 6px;
        cursor: pointer;
        width: fit-content;
        align-self: center;
    }

    span { font-weight: bold; }
    #success { color: greenyellow; }
    #failed { color: red; white-space: pre-line; }
</style>