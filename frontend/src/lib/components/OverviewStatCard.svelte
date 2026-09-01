<script lang="ts">
    import type { Icon } from 'lucide-svelte';

    export let icon: typeof Icon;
    export let title: string;
    export let value: string | number;
    export let subtitle: string = '';
    export let color: 'default' | 'red' | 'green' | 'yellow' | 'blue' = 'default';
</script>

<div class="stat-card {color}">
    <div class="stat-icon">
        <svelte:component this={icon} size={24} />
    </div>
    <div class="stat-content">
        <p class="stat-title">{title}</p>
        <p class="stat-value">{value}</p>
        {#if subtitle}
            <p class="stat-subtitle">{subtitle}</p>
        {/if}
    </div>
</div>

<style>
    .stat-card {
        background: var(--bg-card);
        border: 1px solid var(--border);
        border-radius: 8px;
        padding: 1rem;
        display: flex;
        align-items: center;
        gap: 0.75rem;
        flex: 1;
        min-width: 150px;
    }

    .stat-card.red    { border-left: 3px solid var(--accent-red); }
    .stat-card.green  { border-left: 3px solid var(--accent-green); }
    .stat-card.yellow { border-left: 3px solid var(--accent-yellow); }
    .stat-card.blue   { border-left: 3px solid var(--accent-blue); }

    .stat-icon {
        display: flex;
        align-items: center;
        justify-content: center;
        color: var(--text-muted);
        flex-shrink: 0;
    }

    .stat-card.red    .stat-icon { color: var(--accent-red); }
    .stat-card.green  .stat-icon { color: var(--accent-green); }
    .stat-card.yellow .stat-icon { color: var(--accent-yellow); }
    .stat-card.blue   .stat-icon { color: var(--accent-blue); }

    .stat-content {
        display: flex;
        flex-direction: column;
        gap: 0.1rem;
    }

    .stat-title {
        font-size: 0.75rem;
        color: var(--text-muted);
        margin: 0;
        text-transform: uppercase;
        letter-spacing: 0.05em;
        overflow-wrap: break-word;
    }

    .stat-value {
        font-size: 1.4rem;
        font-weight: bold;
        color: var(--text-primary);
        margin: 0;
    }

    .stat-subtitle {
        font-size: 0.75rem;
        color: var(--text-muted);
        margin: 0;
    }
</style>