<script lang="ts">
    import { createProjectAsync } from '../../lib/api/projectApi';
    
    import { validateDescription, validateProjName } from '../validators';

    import { X } from 'lucide-svelte';

    export let isProjectCreationOpen = false;  // AppLayout-ból vezéreljük

    let name = '';
    let projKey = '';
    let description = '';

    let success = '';
    let error = '';

    function validateProjKey(key: string): string | null{
        let aggregateError = '';
        if(!/^[A-Z0-9]+$/.test(key)) aggregateError += 'Hibás Projekt Kulcsa szignatúra! (Csak számok és nagybetűk, szóköz nélkül.)\n';
        if(key.length > 255) aggregateError += 'Projekt Kulcsa nem lehet hosszabb mint 10 karakter!\n';
        if(key.length < 2) aggregateError += 'Projekt Kulcsa nem lehett rövidebb 2 karakternél!\n';
        return aggregateError === '' ? null : aggregateError;
    }

    async function handleCreateProject() {
        error = '';
        let errorOccured: boolean = false;
        const projNameError = validateProjName(name);
        const projKeyError = validateProjKey(projKey);
        const descriptionError = validateDescription(description);
        if(projNameError!=null){
            error = error + projNameError;
            errorOccured = true;
        }
        if(projKeyError!=null){
            error = error + projKeyError;
            errorOccured = true;
        }
        if (descriptionError!=null) {
            error = error + descriptionError;
            errorOccured = true;
        }
        if(errorOccured) {
            return;
        }
        try {
            const response = await createProjectAsync({ name, projKey, description });
            const button = document.getElementById('create') as HTMLButtonElement;
            button.disabled = true;
            success = 'Sikeres Projekt létrehozás! Bezárhatja az ablakot';
            
        } catch (e) {
            error = 'Hiba történt a létrehozás során!';
        }
    }

    export let onClose: () => void = () => {};

    function closeModal() {
        isProjectCreationOpen = false;
        onClose();
    }

    import { onMount } from 'svelte';

    let modalRef: HTMLElement;

    onMount(() => {
        modalRef?.focus();
    });

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
        <form on:submit|preventDefault={handleCreateProject}>
            <h1>Projekt Létrehozás</h1>
            Új projekt neve
            <input type="text" placeholder="Projekt Név" bind:value={name}/>
            Új projekt kulcsa (Végleges érték létrehozás után)
            <input type="text" placeholder="Projekt Kulcs" bind:value={projKey}/>
            Projekt leírása
            <textarea placeholder="Leírás" bind:value={description}></textarea>
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

    input, textarea {
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
        border-color: var(--text-muted);
    }

    button {
        padding: 0.5rem 1rem;
        border-radius: 6px;
        cursor: pointer;
        width: fit-content;
        align-self: center;
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
        margin-top: 1.5rem;
        margin-bottom: 0.5rem;
        font-size: 1.5rem;
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
    #failed { color: var(--accent-red); white-space: pre-line; }
</style>