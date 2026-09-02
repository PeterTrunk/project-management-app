<script lang="ts">
    import { onMount } from 'svelte';
    import { boardStore } from '../stores/boardStore';
    import { createColumnAsync, reorderColumnsAsync } from '../../lib/api/columnApi';
    import type { ColumnResponse } from '../../lib/api/columnApi';

    import { X } from 'lucide-svelte';

    import { notify } from '../stores/notificationStore';

    export let isColumnCreationOpen = false;
    export let projectId: string;
    export let boardId: string;
    
    let modalRef: HTMLElement;

    onMount(() => {
        modalRef?.focus();
    });

    let columns: ColumnResponse[] = [];
    let activeBoardName = '';
    boardStore.subscribe(state => {
        columns = state.columns;
        activeBoardName = state.activeBoard?.name ?? '';
    });

    let name = '';
    let mapsToStatus = '';
    let wipLimit: number | null = null;
    let hasWip: boolean = false;
    let afterColumnId: string = '';
    let error = '';
    let success = '';

    async function handleCreateColumn() {
        error = '';
        success = '';
        try {
            const newColumn = await createColumnAsync(projectId, boardId, {
                boardId,
                name,
                mapsToStatus,
                wipLimit: hasWip ? wipLimit : null,
                position: columns.length + 1
            });

            const button = document.getElementById('create') as HTMLButtonElement;
            button.disabled = true;
            success = 'Oszlop létrehozva!\n';
            notify.success('Oszlop létrehozva!');
            
            // Új sorrend összerakása
            let ordered = [...columns];
            const insertAfterIndex = afterColumnId 
                ? ordered.findIndex(c => c.id === afterColumnId)
                : -1;
            
            ordered.splice(insertAfterIndex + 1, 0, newColumn);
            
            const order = ordered
                .filter(c => c.position > 0)
                .map((col, index) => ({
                    id: col.id,
                    position: index + 1,
                    rowVersion: col.rowVersion ?? ''
                }));
            try {
                await reorderColumnsAsync(projectId, boardId, order);
                success = success + 'Rendezés sikeres!';
                notify.success('Rendezés sikeres!');
            } catch (e: any) {
                const message = e.response?.data ?? e.message ?? 'Rendezés sikeretelen!';
                error = error + message;
                notify.error(message);
            }
            
        } catch (e: any) {
            const message = e.response?.data ?? e.message ?? 'Hiba történt az oszlop létrehozásakor!';
            error = message;
            notify.error(message);
        }
    }

    export let onClose: () => void = () => {};

    function closeModal() {
        isColumnCreationOpen = false;
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
        <form on:submit|preventDefault={handleCreateColumn}>
            <h1>Oszlop létrehozása {activeBoardName}-hoz</h1>
            Új oszlop neve
            <input type="text" placeholder="Oszlop Név" bind:value={name}/>
            Az új oszlopban lévő Feladatok státusza
            <input type="text" placeholder="Státusz" bind:value={mapsToStatus}/>
            <div> 
                Legyen WIP-Limit?  
                <input type="checkbox" bind:checked={hasWip}>
                {#if hasWip}
                    Limit számossága:
                    <input type="number" bind:value={wipLimit}>
                {/if}
            </div>
            Legyen ez az oszlop után:
            <select bind:value={afterColumnId}>
                <option value="">Legelső oszlop legyen</option>
                {#each columns.filter(c => c.position > 0) as column}
                    <option value={column.id}>{column.name}</option>
                {/each}
            </select>
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
        display: flex;
        flex-direction: column;
        gap: 1rem;
        position: relative;
    }

    @media (max-width: 480px) {
        .modal-content {
            padding: var(--card-padding);
        }
    }

    form {
        display: flex;
        flex-direction: column;
        gap: 1rem;
    }

    input[type="text"],
    input[type="number"] {
        background: var(--bg-input);
        border: 1px solid var(--border-hover);
        border-radius: 6px;
        color: var(--text-primary);
        padding: 0.5rem;
        font-size: 1rem;
        width: 100%;
    }

    input[type="text"]:focus,
    input[type="number"]:focus {
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
        word-break: break-word;
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
    #failed  { color: var(--accent-red); white-space: pre-line; word-break: break-word; }
</style>