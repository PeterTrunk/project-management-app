<script lang="ts">
    import { onMount } from 'svelte';
    import { boardStore, setActiveBoard, setBoards } from '../stores/boardStore';
    import { updateBoardAsync, getBoardsAsync } from '../api/boardApi';
    import type { BoardResponse } from '../api/boardApi';

    import { deleteBoardAsync } from '../api/boardApi';
    import ConfirmModal from './ConfirmModal.svelte';

    import { X, Trash2 } from 'lucide-svelte';

    import { notify } from '../stores/notificationStore';
    import { requireRowVersion } from '../api/concurrency';

    export let isUpdateBoardOpen = false;
    export let projectId: string;
    export let onClose: () => void = () => {};

    let isConfirmOpen = false;

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

    //A SZERKESZTÉS ALAPVONALA - ugyanaz a minta, mint a feladat részletnézeténél.
    //
    //A megjelenítés a store-ból jön (activeBoard), a mentés viszont azt a tokent küldi,
    //amellyel a szerkesztés indult. Ha a kettő egy változó lenne, a SignalR-frissítés a
    //tokent is átírná, a mentés mindig sikerülne, és a másik felhasználó módosítása NÉMÁN
    //felülíródna.
    let baselineRowVersion: number | null = null;
    //A 409 maga is bizonyíték a távoli módosításra, akkor is, ha az esemény nem jutott el.
    let conflictDetected = false;

    //Az alapvonal a megnyitáskor fixálódik - a modal csak nyitott állapotban létezik.
    $: if (activeBoard && baselineRowVersion === null) {
        baselineRowVersion = activeBoard.rowVersion;
    }

    $: hasRemoteChange = !!activeBoard
        && (conflictDetected || (baselineRowVersion !== null && activeBoard.rowVersion !== baselineRowVersion));
    
    function acknowledgeRemoteChange() {
        if (!activeBoard) return;
        baselineRowVersion = activeBoard.rowVersion;
        conflictDetected = false;
        error = '';
    }

    //Csak akkor fordulunk szerverhez, ha az ütközést a MENTÉS BUKÁSÁBÓL tudtuk meg: ilyenkor
    //az esemény nem jutott el hozzánk, tehát a tároló is állott, és a nyugtázás ugyanazt az
    //elavult tokent léptetné alapvonalra. A gyakori eset (az esemény megjött) ezt kihagyja.
    async function refreshAfterConflictAsync() {
        if (!activeBoard) return;
        try {
            const boards = await getBoardsAsync(projectId);
            setBoards(boards);
            const fresh = boards.find(b => b.id === activeBoard!.id);
            if (fresh) setActiveBoard(fresh);
        } catch {
            //A nyugtázás ettől függetlenül működik.
        }
    }

    async function handleUpdateBoard() {
        error = '';
        success = '';
        try {
            const response = await updateBoardAsync(projectId, activeBoard!.id, { 
                name, 
                description, 
                isDefault,
                rowVersion: requireRowVersion(baselineRowVersion, 'board')
            });
            success = 'Board módosítva!';
            notify.success('Board módosítva!');
            setActiveBoard(response);
            baselineRowVersion = response.rowVersion;
            conflictDetected = false;
            const boards = await getBoardsAsync(projectId);
            setBoards(boards);
        } catch (e: any) {
            if (e.response?.status === 409) {
                conflictDetected = true;
                await refreshAfterConflictAsync();
                error = 'Valaki módosította ezt a boardot, amíg szerkesztetted. '
                      + 'A módosításaid megmaradtak - az „Értettem" után mentheted újra.';
                notify.warning(error);
                return;
            }
            const message = e.response?.data ?? e.message ?? 'Hiba történt a board módosítása során!';
            error = message;
            notify.error(message);
        }
    }

    async function handleDeleteBoard() {
        try {
            await deleteBoardAsync(projectId, activeBoard!.id);
            const boards = await getBoardsAsync(projectId);
            setBoards(boards);
            if (boards.length > 0) {
                setActiveBoard(boards[0]);
            } else {
                setActiveBoard(null);
            }
            notify.success('Board törölve!');
            closeModal();
        } catch (e: any) {
            const message = e.response?.data ?? e.message ?? 'Hiba történt a board törlése során!';
            error = message;
            notify.error(message);
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
            <button class="delete-btn" type="button" on:click={() => isConfirmOpen = true}>
                <Trash2 size={15} /> Törlés
            </button>
        </div>
        <button class="close-btn" type="button" on:click={closeModal}>
            <X size={16} />
        </button>
        <h1>{activeBoard?.name}</h1>
        <h2 class="edit-title">Szerkesztés</h2>
        <div class="section">
            <div class="section-header">
                <h3>Jelenlegi adatok</h3>
            </div>
            <div class="meta-grid">
                <span class="meta-label">Board neve</span>
                <span class="meta-value">
                    {activeBoard?.name}
                </span>

                <span class="meta-label">Leírás</span>
                <span class="meta-value">
                    {activeBoard?.description ? activeBoard?.description : 'Nincs' }
                </span>

                <span class="meta-label">Alapvető board?</span>
                <span class="meta-value">
                    {activeBoard?.isDefault ? 'Igen' : 'Nem'}
                </span>
            </div>
        </div>

        <form on:submit|preventDefault={handleUpdateBoard}>
            <div class="section-header">
                <h3>Szerkesztő űrlap</h3>
            </div>
            <label>Board neve:
                <input type="text" bind:value={name} placeholder="Board neve">
            </label>
            <label>Board leírása:
                <textarea bind:value={description} placeholder="Board leírása (opcionális)"></textarea>
            </label>
            <label>Legyen alapvető board?
                <input type="checkbox" bind:checked={isDefault}>
            </label>
            <label>
                Jelenlegi alapvető board: <span>{currentDefaultBoard?.name ?? 'Nincs'}</span>
                <!-- Ne adjon warningot a label -->
                <button style="visibility: hidden;" >Dummy</button>
            </label>
            {#if hasRemoteChange}
                <div class="remote-change">
                    <span class="remote-change-text">
                        Ezt a boardot időközben módosította valaki. A fenti <strong>Jelenlegi adatok</strong> szekció már a friss állapotot mutatja. Nézd át hogy mi változott, majd nyomj az <strong>Értettem</strong> gombra hogy menthess.
                    </span>
                    <button type="button" class="btn-reload" on:click={acknowledgeRemoteChange}>
                        Értettem
                    </button>
                </div>
            {/if}
            {#if error}
                <p id="failed">{error}</p>
            {/if}
            {#if success}
                <p id="success">{success}</p>
            {/if}
            <button type="submit" disabled={hasRemoteChange}>Módosítások mentése</button>
        </form>
    </div>
</div>

{#if isConfirmOpen}
    <ConfirmModal
        bind:isOpen={isConfirmOpen}
        title="Board törlése"
        message="Biztosan törölni szeretnéd a {activeBoard?.name} boardot? Az összes oszlop és task elvész!"
        confirmText="Törlés"
        onConfirm={handleDeleteBoard}
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
        max-width: 95vw;
        display: flex;
        flex-direction: column;
        gap: 1rem;
        position: relative;
    }

    .modal-content h1 {
        margin-top: 1rem;
        padding-bottom: 1rem;
        border-bottom: 1px solid var(--border);
        word-break: break-word;
    }

    @media (max-width: 480px) {
        .modal-content {
            padding: var(--card-padding);
        }
    }

    .section {
        display: flex;
        flex-direction: column;
        gap: 0.5rem;
    }

    .section-header {
        display: flex;
        align-items: center;
        justify-content: space-between;
        margin-bottom: 0.5rem;
    }

    .meta-grid {
        display: grid;
        grid-template-columns: auto 1fr;
        gap: 0.35rem 1rem;
        font-size: 0.9rem;
        border-radius: 6px;
    }

    .meta-label { color: var(--text-muted); white-space: nowrap; }
    .meta-value { color: var(--text-primary); font-weight: 500; }

    h3 {
        font-size: 0.78rem;
        text-transform: uppercase;
        letter-spacing: 0.07em;
        color: var(--text-muted);
    }

    .header-actions {
        position: absolute;
        top: 0.75rem;
        left: 0.75rem;
    }

    .edit-title {
        font-size: 1.1rem;
        color: var(--text-primary);
        margin: 0;
    }

    .delete-btn {
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

    .delete-btn:hover {
        background: var(--accent-red-bg);
        color: var(--accent-red);
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
    
    form {
        display: flex;
        flex-direction: column;
        gap: 0.75rem;
    }

    input[type="text"], textarea {
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

    textarea {
        resize: vertical;
        min-height: 80px;
    }

    span {
        font-weight: bold;
        color: var(--accent-blue);
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

    button[type="submit"]:hover:not(:disabled) {
        background: var(--accent-blue);
        color: #fff;
    }

    button[type="submit"]:disabled {
        background: var(--bg-hover);
        border-color: var(--border-hover);
        color: var(--text-muted);
        cursor: not-allowed;
    }

    button {
        padding: 0.5rem 1rem;
        border-radius: 6px;
        cursor: pointer;
        width: fit-content;
        align-self: center;
    }

    #success { color: var(--accent-green); }
    #failed  { color: var(--accent-red); white-space: pre-line; word-break: break-word; }

    .remote-change {
        display: flex;
        align-items: center;
        gap: var(--gap-sm);
        flex-wrap: wrap;
        padding: var(--card-padding);
        margin-bottom: var(--gap-sm);
        border: 1px solid var(--accent-yellow);
        border-radius: var(--border-radius);
        background: var(--bg-secondary);
        font-size: var(--font-size-sm);
    }

    .remote-change-text {
        flex: 1 1 auto;
        min-width: 0;
    }

    .btn-reload {
        flex: 0 0 auto;
        background: var(--accent-yellow-bg);
        border: 1px solid var(--accent-yellow);
        color: var(--accent-yellow);
        padding: 0.4rem 0.85rem;
        border-radius: 6px;
        cursor: pointer;
        font-size: 0.85rem;
        white-space: nowrap;
        transition: background 0.15s;
    }

    .btn-reload:hover:not(:disabled) {
        background: var(--accent-yellow);
        color: #000;
    }

    .btn-reload:disabled {
        opacity: 0.6;
        cursor: default;
    }

    label { color: var(--text-muted) }
</style>