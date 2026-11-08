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

    //A modal csak nyitott állapotban létezik, tehát az alapvonal itt a megnyitáskor fixálódik.
    $: if (baselineRowVersion === null) {
        baselineRowVersion = sprint.rowVersion;
    }

    $: hasRemoteChange = conflictDetected
        || (baselineRowVersion !== null && liveSprint.rowVersion !== baselineRowVersion);

    //A felhasználó NYUGTÁZÁSA, nem újratöltés.
    //
    //A tárolóból olvasott élő állapot mindig friss, tehát a változás már megtörtént,
    //mire ez a gomb megjelenik.
    //A gomb egyetlen dolga, hogy a felhasználó kimondja: látta. Ettől lép tovább az alapvonal
    //a friss tokenre, és ettől válik újra menthetővé a szerkesztés.
    //
    //Miért nem automatikus: az alapvonal csendes léptetése épp a védelem feladása lenne - a
    //mentés mindig sikerülne, és a másik felhasználó módosítása NÉMÁN felülíródna.
    function acknowledgeRemoteChange() {
        baselineRowVersion = liveSprint.rowVersion;
        conflictDetected = false;
        error = '';
    }

    //Csak akkor fordulunk szerverhez, ha az ütközést a MENTÉS BUKÁSÁBÓL tudtuk meg: ilyenkor
    //az esemény nem jutott el hozzánk, tehát a tároló is állott, és a nyugtázás ugyanazt az
    //elavult tokent léptetné alapvonalra. A gyakori eset (az esemény megjött) ezt kihagyja.
    async function refreshAfterConflictAsync() {
        try {
            const sprints = await getSprintsAsync(projectId);
            setSprints(sprints);
            const fresh = sprints.find(s => s.id === sprint.id);
            if (fresh) sprint = fresh;
        } catch {
            //A nyugtázás ettől függetlenül működik.
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

            //A TÁROLÓT is frissítjük, nem csak a lokális példányt. Enélkül az élő
            //összehasonlítás (`liveSprint`) a mentés után is az ELŐZŐ tokent látná, tehát a
            //saját mentésünk után jelent volna meg a "valaki módosította" sáv - és mivel a
            //sáv mostantól tiltja a mentést, a felhasználó a saját sikeres mentése után nem
            //tudott volna újra menteni. A SignalR-esemény ugyan megjön, de arra nem
            //támaszkodhatunk: egy kimaradt üzenet beragasztaná a tiltást.
            setSprints(
                ($sprintStore.sprints ?? []).map(s => (s.id === updated.id ? updated : s)));

            baselineRowVersion = updated.rowVersion;
            conflictDetected = false;
            success = 'Sprint módosítva!';
            notify.success('Sprint módosítva!');
        } catch (e: any) {
            if (e.response?.status === 409) {
                conflictDetected = true;
                await refreshAfterConflictAsync();
                error = 'Valaki módosította ezt a sprintet, amíg szerkesztetted. '
                      + 'A módosításaid megmaradtak - az „Értettem" után mentheted újra.';
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
        <h1>{liveSprint.name}</h1>
        <h2 class="edit-title">Szerkesztés</h2>
        <div class="section">
            <div class="section-header">
                <h3>Jelenlegi adatok</h3>
            </div>
            <div class="meta-grid">
                <span class="meta-label">Sprint neve</span>
                <span class="meta-value">
                    {liveSprint.name}
                </span>

                <span class="meta-label">Sprint célja</span>
                <span class="meta-value">
                    {liveSprint.goal ? liveSprint.goal : 'Nincs'}
                </span>

                <span class="meta-label">Állapot</span>
                <span class="meta-value">{liveSprint.state}</span>

                <span class="meta-label">Sprint kezdete</span>
                <span class="meta-value">{liveSprint.startDate ? new Date(liveSprint.startDate).toLocaleDateString('hu-HU') : 'Nincs megadva'}</span>

                <span class="meta-label">Sprint vége</span>
                <span class="meta-value">{liveSprint.endDate ? new Date(liveSprint.endDate).toLocaleDateString('hu-HU') : 'Nincs megadva'}</span>
            </div>
        </div>

        <form on:submit|preventDefault={handleUpdateSprint}>
            <div class="section-header">
                <h3>Szerkesztő űrlap</h3>
            </div>
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
                <div class="remote-change">
                    <span class="remote-change-text">
                        Ezt a sprintet időközben módosította valaki. A fenti <strong>Jelenlegi adatok</strong> szekció már a friss állapotot mutatja. Nézd át hogy mi változott, majd nyomj az <strong>Értettem</strong> gombra hogy menthess.
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
            <button type="submit" disabled={hasRemoteChange}>Mentés</button>
        </form>
    </div>
</div>

<style>
    .edit-title {
        font-size: 1.1rem;
        color: var(--text-primary);
        margin: 0;
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
        display: flex;
        flex-direction: column;
        gap: 1rem;
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
        gap: 0.75rem;
    }

    .modal-content h1 {
        margin-top: 1rem;
        padding-bottom: 1rem;
        border-bottom: 1px solid var(--border);
        word-break: break-word;
    }

    h3 {
        font-size: 0.78rem;
        text-transform: uppercase;
        letter-spacing: 0.07em;
        color: var(--text-muted);
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

    label { color: var(--text-muted); }

    #success { color: var(--accent-green); }
    #failed  { 
        color: var(--accent-red); 
        white-space: pre-line; 
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
</style>