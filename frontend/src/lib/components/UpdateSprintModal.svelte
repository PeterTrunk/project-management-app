<script lang="ts">
    import { onMount } from 'svelte';
    import { updateSprintAsync, getSprintsAsync, type SprintResponse } from '../api/sprintApi';
    import { sprintStore, setSprints } from '../stores/sprintStore';
    import { requireRowVersion } from '../api/concurrency';
    
    import { notify } from '../stores/notificationStore';

    import { X } from 'lucide-svelte';

    export let isUpdateSprintOpen = false;
    export let projectId: string;
    export let sprint: SprintResponse;
    export let onClose: () => void = () => {};

    let modalRef: HTMLElement;
    onMount(() => modalRef?.focus());

    let name = sprint.name;
    let goal = sprint.goal ?? '';
    
    let startDate = sprint.startDate 
        ? new Date(sprint.startDate).toISOString().slice(0, 16) 
        : '';
    let endDate = sprint.endDate 
        ? new Date(sprint.endDate).toISOString().slice(0, 16) 
        : '';

    let error = '';
    let success = '';

    //A sprintet a szülő PILLANATKÉPként adja át (a kattintáskori objektum), ezért magától
    //nem követné a valós idejű frissítéseket. A megjelenítéshez és a token
    //összehasonlításához ezért a store élő listájából vezetjük le.
    $: liveSprint = ($sprintStore.sprints ?? []).find(s => s.id === sprint.id) ?? sprint;

    //A SZERKESZTÉS ALAPVONALA - ugyanaz a minta, mint a feladat, a board és az oszlop
    //szerkesztőjénél: a megjelenítés a store-t követi, a mentés viszont azt a tokent küldi,
    //amellyel a szerkesztés indult. Enélkül a valós idejű frissítés a tokent is átírná, és a
    //másik felhasználó módosítása némán felülíródna.
    let baselineRowVersion: number | null = null;
    //A 409 maga is bizonyíték a távoli módosításra, akkor is, ha az esemény nem jutott el.
    let conflictDetected = false;
    let isReloading = false;

    //A modal csak nyitott állapotban létezik, tehát az alapvonal itt a megnyitáskor fixálódik.
    $: if (baselineRowVersion === null) {
        baselineRowVersion = sprint.rowVersion;
    }

    $: hasRemoteChange = conflictDetected
        || (baselineRowVersion !== null && liveSprint.rowVersion !== baselineRowVersion);

    //A felhasználó kifejezett döntése: átveszi a friss állapotot alapvonalként, de a már
    //beírt értékeit megtartja. Szándékosan a szervertől kérdez, nem a store-ból: ha az
    //ütközést a mentés bukásából tudtuk meg, akkor a store is állott.
    async function acceptRemoteChanges() {
        if (isReloading) return;
        isReloading = true;
        try {
            const sprints = await getSprintsAsync(projectId);
            setSprints(sprints);
            const fresh = sprints.find(s => s.id === sprint.id);
            if (!fresh) {
                error = 'Ezt a sprintet időközben törölték.';
                notify.error(error);
                return;
            }
            sprint = fresh;
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

    function closeModal() {
        isUpdateSprintOpen = false;
        onClose();
    }

    async function handleUpdateSprint() {
        error = '';
        success = '';
        try {
            const updated = await updateSprintAsync(projectId, sprint.id, {
                name,
                goal: goal || null,
                startDate: startDate ? new Date(startDate) : null,
                endDate: endDate ? new Date(endDate) : null,
                rowVersion: requireRowVersion(baselineRowVersion, 'sprint')
            });
            sprint = updated;
            baselineRowVersion = updated.rowVersion;
            conflictDetected = false;
            success = 'Sprint módosítva!';
            notify.success('Sprint módosítva!');
        } catch (e: any) {
            if (e.response?.status === 409) {
                conflictDetected = true;
                error = 'Valaki módosította ezt a sprintet, amíg szerkesztetted. '
                      + 'A módosításaid megmaradtak - a „Változások betöltése" után mentheted újra.';
                notify.warning(error);
                return;
            }
            const message = e.response?.data ?? e.message ?? 'Hiba történt a sprint módosítása során!';
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
        <button class="close-btn" type="button" on:click={closeModal}>
            <X size={16} />
        </button>
        <h1>Sprint Szerkesztése</h1>
        <form on:submit|preventDefault={handleUpdateSprint}>
            <label>Sprint neve
                <input type="text" bind:value={name}>
            </label>
            <label>Sprint célja
                <input type="text" bind:value={goal}>
            </label>
            <label>Sprint kezdete
                <input type="datetime-local" bind:value={startDate}>
            </label>
            <label>Sprint vége
                <input type="datetime-local" bind:value={endDate}> 
            </label>
            {#if hasRemoteChange}
                <!-- Nem blokkoló jelzés: a felhasználó a MENTÉS ELŐTT tudja meg,
                     hogy ütközés lesz, nem a hibaüzenetből utólag. -->
                <div class="remote-change">
                    <span class="remote-change-text">
                        Ezt a sprintet időközben módosította valaki.
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
            <button type="submit">Mentés</button>
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
        max-height: 90vh;
        overflow-y: auto;
        position: relative;
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

    h1 {
        font-size: 1.5rem;
        margin-bottom: 0.5rem;
    }

    input {
        background: var(--bg-input);
        border: 1px solid var(--border-hover);
        border-radius: 6px;
        color: var(--text-primary);
        padding: 0.5rem;
        font-size: 1rem;
        width: 100%;
    }

    input:focus {
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

    button[type="submit"] {
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

    label { color: var(--text-muted); }

    #success { color: var(--accent-green); }
    #failed  { 
        color: var(--accent-red); 
        white-space: pre-line; 
        word-break: break-word; 
    }

    /* Az egyidejű szerkesztés jelzése: figyelemfelhívó, de nem blokkoló.
       Ugyanaz a minta, mint a feladat, a board és az oszlop szerkesztőjénél. */
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
</style>