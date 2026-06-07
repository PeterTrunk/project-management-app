<script lang="ts">
    import { onMount } from 'svelte';
    import { createLabelAsync } from '../api/labelApi';

    import { X } from 'lucide-svelte';

    export let isOpen = false;
    export let projectId: string;
    export let onClose: () => void = () => {};

    let modalRef: HTMLElement;
    let name = '';
    let color = '#3a86ff';
    let error = '';
    let success = '';

    onMount(() => modalRef?.focus());

    function closeModal() {
        isOpen = false;
        onClose();
    }

    async function handleCreateLabel() {
        error = '';
        success = '';
        if (name.trim() === '') {
            error = 'Név szükséges!';
            return;
        }
        if(name.length> 40){
            error = 'Név nem lehet 40 karakternél hosszabb!'
            return;
        }
        try {
            await createLabelAsync(projectId, { name, color });
            success = 'Label létrehozva!';
            name = '';
            color = '#3a86ff';
        } catch (e) {
            error = 'Hiba történt a label létrehozásakor!';
        }
    }
</script>

<div class="modal-overlay"
    bind:this={modalRef}
    on:click|self={closeModal}
    on:keydown={(e) => e.key === 'Escape' && closeModal()}
    role="dialog"
    aria-modal="true"
    tabindex="-1"
>
    <div class="modal-content">
        <button class="close-btn" type="button" on:click={closeModal}>
            <X size={16} />
        </button>
        <h1>Új Label</h1>
        <form on:submit|preventDefault={handleCreateLabel}>
            Label neve:
            <input type="text" bind:value={name} placeholder="Label neve (max 40 karakter)">
            Label színe:
            <div class="color-picker">
                <input type="color" bind:value={color}>
                <span style="color: {color}">{color}</span>
            </div>
            {#if error}
                <p id="failed">{error}</p>
            {/if}
            {#if success}
                <p id="success">{success}</p>
            {/if}
            <button type="submit">Létrehozás</button>
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
        width: 400px;
        display: flex;
        flex-direction: column;
        gap: 1rem;
        position: relative;
    }

    form {
        display: flex;
        flex-direction: column;
        gap: 0.75rem;
    }

    input[type="text"] {
        background: var(--bg-input);
        border: 1px solid var(--border-hover);
        border-radius: 6px;
        color: var(--text-primary);
        padding: 0.5rem;
        width: 100%;
    }

    input[type="text"]:focus {
        outline: none;
        border-color: var(--accent-blue);
    }

    .color-picker {
        display: flex;
        align-items: center;
        gap: 1rem;
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

    h1 {
        margin-top: 1.5rem;
        font-size: 1.3rem;
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
    #failed { color: var(--accent-red); }
</style>