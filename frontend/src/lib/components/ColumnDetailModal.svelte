<script lang="ts">
    import { onMount } from 'svelte';
    import { updateColumnAsync, deleteColumnAsync, getColumnsAsync } from '../api/columnApi';
    import { boardStore, setColumns } from '../stores/boardStore';
    import { requireRowVersion } from '../api/concurrency';
    import type { ColumnResponse } from '../api/columnApi';
    import ConfirmModal from './ConfirmModal.svelte';

    import { notify } from '../stores/notificationStore';

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

    //Az oszlopot a szülő PILLANATKÉPként adja át (a kattintáskori objektum), ezért magától
    //nem követné a valós idejű frissítéseket. A megjelenítéshez ezért a store élő listájából
    //vezetjük le, és csak akkor esünk vissza a propra, ha az oszlop nincs a store-ban.
    $: liveColumn = ($boardStore.columns ?? []).find(c => c.id === column.id) ?? column;

    let isEditing = false;
    let editName = column.name;
    let editMapsToStatus = column.mapsToStatus;
    let editWipLimit: number | null = column.wipLimit;
    let error = '';

    //A SZERKESZTÉS ALAPVONALA - ugyanaz a minta, mint a feladat és a board szerkesztőjénél:
    //a megjelenítés a store-t követi, a mentés viszont azt a tokent küldi, amellyel a
    //szerkesztés indult. Enélkül a valós idejű frissítés a tokent is átírná, és a másik
    //felhasználó módosítása némán felülíródna.
    let baselineRowVersion: number | null = null;
    //A 409 maga is bizonyíték a távoli módosításra, akkor is, ha az esemény nem jutott el.
    let conflictDetected = false;

    $: hasRemoteChange = isEditing
        && (conflictDetected || (baselineRowVersion !== null && liveColumn.rowVersion !== baselineRowVersion));

    function startEditing() {
        baselineRowVersion = liveColumn.rowVersion;
        editName = liveColumn.name;
        editMapsToStatus = liveColumn.mapsToStatus;
        editWipLimit = liveColumn.wipLimit;
        error = '';
        conflictDetected = false;
        isEditing = true;
    }

    function acknowledgeRemoteChange() {
        baselineRowVersion = liveColumn.rowVersion;
        conflictDetected = false;
        error = '';
    }

    //Csak akkor fordulunk szerverhez, ha az ütközést a MENTÉS BUKÁSÁBÓL tudtuk meg: ilyenkor
    //az esemény nem jutott el hozzánk, tehát a tároló is állott, és a nyugtázás ugyanazt az
    //elavult tokent léptetné alapvonalra. A gyakori eset (az esemény megjött) ezt kihagyja.
    async function refreshAfterConflictAsync() {
        try {
            const cols = await getColumnsAsync(projectId, boardId);
            setColumns(cols.sort((a, b) => a.position - b.position));
            const fresh = cols.find(c => c.id === column.id);
            if (fresh) column = fresh;
        } catch {
            //A nyugtázás ettől függetlenül működik.
        }
    }

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
        try {
            const updated = await updateColumnAsync(projectId, boardId, column.id, {
                name: editName,
                mapsToStatus: editMapsToStatus,
                wipLimit: !editWipLimit || editWipLimit <= 0 ? null : Number(editWipLimit),
                rowVersion: requireRowVersion(baselineRowVersion, 'oszlop')
            });
            column = updated;
            
            setColumns(
                ($boardStore.columns ?? []).map(c => (c.id === updated.id ? updated : c)));

            baselineRowVersion = updated.rowVersion;
            conflictDetected = false;
            error = '';
            notify.success('Oszlop módosítva!');
            isEditing = false;
        } catch (e: any) {
            if (e.response?.status === 409) {
                conflictDetected = true;
                await refreshAfterConflictAsync();
                error = 'Valaki módosította ezt az oszlopot, amíg szerkesztetted. '
                      + 'A módosításaid megmaradtak - az „Értettem" után mentheted újra.';
                notify.warning(error);
                return;
            }
            const message = e.response?.data ?? e.message ?? 'Hiba történt a módosítás során!';
            error = message;
            notify.error(message);
        }
    }

    async function handleDelete() {
        try {
            await deleteColumnAsync(projectId, boardId, column.id);
            notify.success('Oszlop törölve!');
            closeModal();
        } catch (e: any) {
            const message = e.response?.data ?? e.message ?? 'Hiba történt a törlés során! (Lehet hogy taskok vannak az oszlopban)';
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
            <button class="edit-btn" on:click={() => isEditing ? isEditing = false : startEditing()}>
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

        <h1>{liveColumn.name}</h1>
        {#if !isEditing}
            <div class="section">
                <div class="meta-grid">
                    <span class="meta-label">Státusz</span>
                    <span class="meta-value">
                        {liveColumn.mapsToStatus}
                    </span>

                    <span class="meta-label">WIP limit</span>
                    <span class="meta-value">
                        {liveColumn.wipLimit ?? 'Nincs limit'}
                    </span>
                </div>
            </div>
        {/if}
        {#if isEditing}
            <h2 class="edit-title">Szerkesztés</h2>

            <div class="section">
                <div class="section-header">
                    <h3>Jelenlegi adatok</h3>
                </div>
                <div class="meta-grid">
                    <span class="meta-label">Státusz</span>
                    <span class="meta-value">
                        {liveColumn.mapsToStatus}
                    </span>

                    <span class="meta-label">WIP limit</span>
                    <span class="meta-value">
                        {liveColumn.wipLimit ?? 'Nincs limit'}
                    </span>
                </div>
            </div>

            <form on:submit|preventDefault={() => openConfirm(
                'Módosítások mentése',
                'Biztosan menteni szeretnéd a változtatásokat?',
                handleUpdate
            )}>
                <div class="section-header">
                    <h3>Szerkesztő űrlap</h3>
                </div>
                <label>Oszlop neve:
                    <input type="text" bind:value={editName} placeholder="Oszlop neve">
                </label>
                <label>Státusz:
                    <input type="text" bind:value={editMapsToStatus} placeholder="Státusz">
                </label>
                <label>
                    WIP limit (üresen hagyva = nincs limit):
                    <input type="number" min="1" bind:value={editWipLimit} placeholder="Nincs limit">
                </label>
                {#if hasRemoteChange}
                    <div class="remote-change">
                        <span class="remote-change-text">
                            Ezt az oszlopot időközben módosította valaki. A fenti <strong>Jelenlegi adatok</strong> szekció már a friss állapotot mutatja. Nézd át hogy mi változott, majd nyomj az <strong>Értettem</strong> gombra hogy menthess.
                        </span>
                        <button type="button" class="btn-reload" on:click={acknowledgeRemoteChange}>
                            Értettem
                        </button>
                    </div>
                {/if}
                {#if error}
                    <p class="msg-error">{error}</p>
                {/if}
                <button type="submit" disabled={hasRemoteChange}>Mentés</button>
            </form>
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

    .modal-content h1 {
        margin-top: 1rem;
        padding-bottom: 1rem;
        border-bottom: 1px solid var(--border);
        word-break: break-word;
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

    .edit-title {
        font-size: 1.1rem;
        color: var(--text-primary);
        margin: 0;
    }

    .header-actions {
        position: absolute;
        top: 0.75rem;
        left: 0.75rem;
        display: flex;
        gap: 0.5rem;
        flex-wrap: wrap;
        right: 2.75rem;
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

    span { 
        font-weight: bold; 
        color: var(--text-primary);
        word-break: break-word;
    }

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

    label { color: var(--text-muted); }

    .msg-error {
        color: var(--accent-red);
        font-size: var(--font-size-sm);
        margin: 0 0 var(--gap-sm) 0;
    }
</style>