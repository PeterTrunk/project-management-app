<script lang="ts">
    import { onMount } from 'svelte';
    import { updateColumnAsync, deleteColumnAsync } from '../api/columnApi';
    import { validateColumnName, validateColumnStatus } from '../validators';
    import type { ColumnResponse } from '../api/columnApi';
    import ConfirmModal from './ConfirmModal.svelte';

    import { X, Pencil, Trash2 } from 'lucide-svelte';

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
                rowVersion: column.rowVersion ?? ''
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
                {#if isEditing}
                    <X size={15} /> Mégse
                {:else}
                    <Pencil size={15} /> Szerkesztés
                {/if}
            </button>
            <button class="delete-btn" on:click={() => openConfirm(
                'Oszlop törlése',
                'Biztosan törölni szeretnéd az oszlopot? Az oszlopban lévő taskok nem törölhetők, előbb helyezd át őket!',
                handleDelete
            )}><Trash2 size={15} /> Törlés</button>
        </div>
        <button class="close-btn" type="button" on:click={closeModal}>
            <X size={16} />
        </button>

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
        margin-top: 2.5rem;
        padding-bottom: 1rem;
        border-bottom: 1px solid var(--border);
    }

    .header-actions {
        position: absolute;
        top: 0.75rem;
        left: 0.75rem;
        display: flex;
        gap: 0.5rem;
    }

    .edit-btn, .delete-btn {
        display: flex;
        align-items: center;
        gap: 0.35rem;
        background: transparent;
        border: none;
        color: var(--text-secondary);
        font-size: 0.85rem;
        cursor: pointer;
        padding: 0.3rem 0.6rem;
        border-radius: 5px;
        transition: background 0.15s, color 0.15s;
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

    .edit-btn:hover   { background: var(--bg-hover); color: var(--text-primary); }
    .close-btn:hover  { background: var(--bg-hover); color: var(--text-primary); }
    .delete-btn:hover { background: var(--accent-red-bg); color: var(--accent-red); }

    form {
        display: flex;
        flex-direction: column;
        gap: 0.75rem;
    }

    input[type="text"], input[type="number"] {
        background: var(--bg-input);
        border: 1px solid var(--border-hover);
        border-radius: 6px;
        color: var(--text-primary);
        padding: 0.5rem;
        width: 100%;
    }

    input:focus { outline: none; border-color: var(--accent-blue); }

    button {
        padding: 0.5rem 1rem;
        border-radius: 6px;
        cursor: pointer;
        width: fit-content;
        align-self: center;
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

    span { font-weight: bold; color: var(--text-primary); }
    #success { color: var(--accent-green); }
    #failed  { color: var(--accent-red); white-space: pre-line; }
</style>