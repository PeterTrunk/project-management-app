<script lang="ts">
    import { createProjectAsync } from '../../lib/api/projectApi';
    import { setProjects, projectStore } from '../../lib/stores/projectStore';

    export let isProjectCreationOpen = false;  // AppLayout-ból vezéreljük

    let name = '';
    let projKey = '';
    let description = '';

    let success = '';
    let error = '';

    function validateProjName(name: string): string | null{
        if (name.length > 120) return 'A projekt neve nem lehet hosszabb mint 120 karakter!\n';
        return null;
    }

    function validateProjKey(key: string): string | null{
        let aggregateError = '';
        if(!/^[A-Z0-9]+$/.test(key)) aggregateError += 'Hibás Projekt Kulcsa szignatúra! (Csak számok és nagybetűk, szóköz nélkül.)\n';
        if(key.length > 255) aggregateError += 'Projekt Kulcsa nem lehet hosszabb mint 10 karakter!\n';
        if(key.length < 2) aggregateError += 'Projekt Kulcsa nem lehett rövidebb 2 karakternél!\n';
        return aggregateError === '' ? null : aggregateError;
    }

    function validateDescription(desc: string): string | null {
        if (desc.length > 2000) return 'Leírás maximum 2000 karakter hosszú lehet!\n';
        return null;
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
        <h1>Projekt Létrehozás</h1>
        <form on:submit|preventDefault={handleCreateProject}>
            <input type="text" placeholder="Projekt Név" bind:value={name}/>
            <input type="text" placeholder="Projekt Kulcs" bind:value={projKey}/>
            <input type="text" placeholder="Leírás" bind:value={description}/>
            {#if error}
                <p id="failed">{error}</p>
            {/if}
            {#if success}
                <p id="success">{success}</p>
            {/if}
            <button type="submit" id="create">Létrehozás</button>
            <br>
        </form>
        <button on:click={closeModal}>Bezárás</button>
    </div>
</div>

<style>
#success{
    color: greenyellow;
}
#failed{
    color: red;
    white-space: pre-line;
}
.modal-overlay {
    position: fixed;
    top: 0;
    left: 0;
    width: 100%;
    height: 100%;
    background: rgba(0, 0, 0, 0.5);  /* sötét háttér */
    display: flex;
    justify-content: center;
    align-items: center;
    z-index: 1000;
}

.modal-content {
    background: rgba(0, 0, 0, 0.5);;
    padding: 2rem;
    border-radius: 8px;
    min-width: 400px;
}
</style>