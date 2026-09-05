<script lang="ts">
    import { createProjectAsync } from '../../lib/api/projectApi';

    import { X } from 'lucide-svelte';

    import { notify } from '../stores/notificationStore';

    export let isProjectCreationOpen = false;  // AppLayout-ból vezéreljük

    let name = '';
    let projKey = '';
    let description = '';

    let success = '';
    let error = '';
    
    async function handleCreateProject() {
        error = '';
        try {
            const response = await createProjectAsync({ name, projKey, description });
            const button = document.getElementById('create') as HTMLButtonElement;
            button.disabled = true;
            success = 'Sikeres Projekt létrehozás! Bezárhatja az ablakot';
            notify.success('Projekt létrehozva!');
            
        } catch (e: any) {
            const message = e.response?.data ?? e.message ?? 'Hiba történt a létrehozás során!';
            error = message;
            notify.error(message);
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

    @media (max-width: 480px) {
        .modal-content {
            padding: var(--card-padding);
        }
    }

    #success { color: var(--accent-green); }
    #failed { color: var(--accent-red); white-space: pre-line; }
</style>