<script lang="ts">
    import { onMount } from 'svelte';

    export let isOpen = false;
    export let title = 'Megerősítés';
    export let message = '';
    export let confirmText = 'Megerősítés';
    export let cancelText = 'Mégsem';
    export let onConfirm: () => void = () => {};
    export let onCancel: () => void = () => {};

    let modalRef: HTMLElement;

    onMount(() => {
        modalRef?.focus();
    });

    function handleConfirm() {
        isOpen = false;
        onConfirm();
    }

    function handleCancel() {
        isOpen = false;
        onCancel();
    }
</script>

<div
    class="modal-overlay"
    bind:this={modalRef}
    on:click|self={handleCancel}
    on:keydown={(e) => e.key === 'Escape' && handleCancel()}
    role="dialog"
    aria-modal="true"
    tabindex="-1"
>
    <div class="modal-content">
        <h2>{title}</h2>
        <p>{message}</p>
        <div class="buttons">
            <button class="cancel" on:click={handleCancel}>{cancelText}</button>
            <button class="confirm" on:click={handleConfirm}>{confirmText}</button>
        </div>
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
        min-width: 350px;
        display: flex;
        flex-direction: column;
        gap: 1rem;
    }

    .buttons {
        display: flex;
        justify-content: flex-end;
        gap: 1rem;
    }

    .confirm {
        color: red;
    }
</style>