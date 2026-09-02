<script lang="ts">
    import type { LabelResponse } from '../api/labelApi';

    import { Trash2 } from 'lucide-svelte';

    import { notify } from '../stores/notificationStore';

    export let label: LabelResponse;
    export let onDelete: (labelId: string) => void = () => {};
    export let showDelete: boolean = true;
    export let small: boolean = false;

</script>

<div class="label-card card-overflow-hidden" class:small>
    <div class="label-color" style="background-color: {label.color}"></div>
    <span class="label-name truncate">{label.name}</span>
    {#if showDelete}
        <button class="delete-btn" on:click={() => onDelete(label.id)}>
            <Trash2 size={14} />
        </button>
    {/if}
</div>

<style>
    .label-card {
        display: flex;
        align-items: center;
        gap: 0.5rem;
        background: var(--bg-hover);
        border-radius: 6px;
        padding: 0.5rem 0.75rem;
        border: 1px solid var(--border-subtle);
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
        color: var(--text-primary);
        min-width: 0;
    }

    .delete-btn {
        display: flex;
        align-items: center;
        background: transparent;
        border: none;
        color: var(--text-secondary);
        cursor: pointer;
        padding: 0.15rem;
        border-radius: 3px;
        flex-shrink: 0;
    }

    .delete-btn:hover {
        color: var(--accent-red);
        background: var(--accent-red-bg);
    }
</style>