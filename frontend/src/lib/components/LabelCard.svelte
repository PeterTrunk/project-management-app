<script lang="ts">
    import type { LabelResponse } from '../api/labelApi';
    import ConfirmModal from './ConfirmModal.svelte';

    export let label: LabelResponse;
    export let onDelete: (labelId: string) => void = () => {};
    export let showDelete: boolean = true;
    export let small: boolean = false;

</script>

<div class="label-card" class:small>
    <div class="label-color" style="background-color: {label.color}"></div>
    <span class="label-name">{label.name}</span>
    {#if showDelete}
        <button class="delete-btn" on:click={() => onDelete(label.id)}>🗑</button>
    {/if}
</div>

<style>
    .label-card {
        display: flex;
        align-items: center;
        gap: 0.5rem;
        background: #2a2a2a;
        border-radius: 6px;
        padding: 0.5rem 0.75rem;
        border: 1px solid #333;
    }

    .label-color {
        width: 16px;
        height: 16px;
        border-radius: 50%;
        flex-shrink: 0;
    }

    .label-card.small {
        padding: 0.2rem 0.4rem;
        font-size: 0.75rem;
    }

    .label-card.small .label-color {
        width: 10px;
        height: 10px;
    }

    .label-name {
        flex: 1;
        font-size: 0.9rem;
    }

    .delete-btn {
        background: transparent;
        border: none;
        color: #aaa;
        cursor: pointer;
        padding: 0;
        font-size: 0.9rem;
    }

    .delete-btn:hover {
        color: #ff5555;
    }
</style>