<script lang="ts">
    import { onMount } from 'svelte';
    import { boardStore, setActiveBoard } from '../stores/boardStore';
    import { createColumnAsync } from '../../lib/api/columnApi';
    import { validateColumnStatus, validateColumnName } from '../validators';

    export let isColumnCreationOpen = false;
    export let projectId: string;
    export let boardId: string;
    
    let modalRef: HTMLElement;

    onMount(() => {
        modalRef?.focus();
    });

    let activeBoardName = '';
    boardStore.subscribe(state => {
        activeBoardName = state.activeBoard?.name ?? '';
    });

    let name = '';
    let mapsToStatus = '';
    let wipLimit: number | null = null;
    let hasWip: boolean = false;
    let position = 1;
    let error = '';
    let success = '';

    async function handleCreateColumn() {
        error = '';
        success = '';
        let errorOccured = false;
        const columnNameError = validateColumnName(name);
        const columnStatusError = validateColumnStatus(mapsToStatus);
        if(columnNameError != null){
            error = error + columnNameError;
            errorOccured = true;
        }
        if(columnStatusError != null){
            error = error + columnStatusError;
            errorOccured = true;
        }
        if(errorOccured){
            return;
        }
        try {
            await createColumnAsync(projectId, boardId, {
                boardId,
                name,
                mapsToStatus,
                wipLimit: hasWip ? wipLimit : null,
                position
            });
            const button = document.getElementById('create') as HTMLButtonElement;
            button.disabled = true;
            success = 'Oszlop létrehozva!';
        } catch (e) {
            error = 'Hiba történt az oszlop létrehozásakor!'+ e;
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
            Oszlop pozíciója
            <input type="number" bind:value={position}>
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