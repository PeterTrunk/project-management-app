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
    import LabelCard from './LabelCard.svelte';

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
    let isReloading = false;

    //Az alapvonal a megnyitáskor fixálódik - a modal csak nyitott állapotban létezik.
    $: if (activeBoard && baselineRowVersion === null) {
        baselineRowVersion = activeBoard.rowVersion;
    }

    $: hasRemoteChange = !!activeBoard
        && (conflictDetected || (baselineRowVersion !== null && activeBoard.rowVersion !== baselineRowVersion));

    //A felhasználó kifejezett döntése: átveszi a friss állapotot alapvonalként, de a már
    //beírt értékeit megtartja. Szándékosan a szervertől kérdez, nem a store-ból: ha az
    //ütközést a mentés bukásából tudtuk meg, akkor a store is állott.
    async function acceptRemoteChanges() {
        if (isReloading || !activeBoard) return;
        isReloading = true;
        try {
            const boards = await getBoardsAsync(projectId);
            setBoards(boards);
            const fresh = boards.find(b => b.id === activeBoard!.id);
            if (!fresh) {
                error = 'Ezt a boardot időközben törölték.';
                notify.error(error);
                return;
            }
            setActiveBoard(fresh);
            baselineRowVersion = fresh.rowVersion;
            conflictDetected = false;
            error = '';
            notify.info('A friss állapot betöltve. A módosításaid megmaradtak - mentheted.');
        } catch (e: any) {
            const message = e.response?.data ?? e.message ?? 'Nem sikerült betölteni a változásokat!';
            error = message;
            notify.error(message);
        } finally {
            isReloading = false;
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
                error = 'Valaki módosította ezt a boardot, amíg szerkesztetted. '
                      + 'A módosításaid megmaradtak - a „Változások betöltése" után mentheted újra.';
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

        <form on:submit|preventDefault={handleUpdateBoard}>
            <h1>Board módosítása</h1>
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
                <!-- Nem blokkoló jelzés: a felhasználó a MENTÉS ELŐTT tudja meg,
                     hogy ütközés lesz, nem a hibaüzenetből utólag. -->
                <div class="remote-change">
                    <span class="remote-change-text">
                        Ezt a boardot időközben módosította valaki.
                    </span>
                    <button type="button" class="btn-reload" on:click={acceptRemoteChanges}
                            disabled={isReloading}>
                        {isReloading ? 'Betöltés…' : 'Változások betöltése'}
                    </button>
                </div>
            {/if}
            {#if error}
                <p id="failed">{error}</p>
            {/if}
            {#if success}
                <p id="success">{success}</p>
            {/if}
            <button type="submit">Módosítások mentése</button>
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

    @media (max-width: 480px) {
        .modal-content {
            padding: var(--card-padding);
        }
    }

    .header-actions {
        position: absolute;
        top: 0.75rem;
        left: 0.75rem;
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

    form h1 {
        margin-top: 2rem;
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

    button[type="submit"]:hover {
        background: var(--accent-blue);
        color: #fff;
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

    /* Az egyidejű szerkesztés jelzése: figyelemfelhívó, de nem blokkoló.
       Ugyanaz a minta, mint a feladat részletnézeténél. */
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