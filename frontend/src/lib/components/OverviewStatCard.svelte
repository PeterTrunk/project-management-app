<script lang="ts">
    export let icon: string;
    export let title: string;
    export let value: string | number;
    export let subtitle: string = '';
    export let color: 'default' | 'red' | 'green' | 'yellow' | 'blue' = 'default';
</script>

<div class="stat-card {color}">
    <div class="stat-icon">{icon}</div>
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
        background: #1e1e1e;
        border: 1px solid #2a2a2a;
        border-radius: 8px;
        padding: 1rem;
        display: flex;
        align-items: center;
        gap: 0.75rem;
        flex: 1;
        min-width: 150px;
    }

    .stat-card.red { border-left: 3px solid #ff5555; }
    .stat-card.green { border-left: 3px solid #4caf50; }
    .stat-card.yellow { border-left: 3px solid #f0a500; }
    .stat-card.blue { border-left: 3px solid #4a9eff; }

    .stat-icon { font-size: 1.5rem; }

    .stat-content {
        display: flex;
        flex-direction: column;
        gap: 0.1rem;
    }

    .stat-title {
        font-size: 0.75rem;
        color: #888;
        margin: 0;
        text-transform: uppercase;
        letter-spacing: 0.05em;
    }

    .stat-value {
        font-size: 1.4rem;
        font-weight: bold;
        color: #fff;
        margin: 0;
    }

    .stat-subtitle {
        font-size: 0.75rem;
        color: #666;
        margin: 0;
    }
</style>