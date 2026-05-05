<script lang="ts">
    import type { PrLinkResponse } from '../api/taskApi';

    export let pr: PrLinkResponse;

    function getPrStateClass(state: string): string {
        switch (state) {
            case 'open': return 'pr-open';
            case 'merged': return 'pr-merged';
            case 'closed': return 'pr-closed';
            default: return '';
        }
    }

    function getPrStateIcon(state: string): string {
        switch (state) {
            case 'open': return '🟡';
            case 'merged': return '🟣';
            case 'closed': return '🔴';
            default: return '⚪';
        }
    }

    function formatDate(dateString: string): string {
        return new Date(dateString).toLocaleDateString('hu-HU');
    }
</script>

<div class="pr-card">
    <span class="pr-icon">{getPrStateIcon(pr.state)}</span>
    <div class="pr-info">
        <div class="pr-main">
            {#if pr.prUrl}
                <a href={pr.prUrl} target="_blank" class="pr-number">
                    #{pr.prNumber}
                </a>
            {:else}
                <span class="pr-number">#{pr.prNumber}</span>
            {/if}
            <span class="pr-title">{pr.title}</span>
            <span class="pr-state {getPrStateClass(pr.state)}">
                {pr.state}
            </span>
        </div>
        <div class="pr-meta">
            <span>{pr.authorName}</span>
            <span>·</span>
            <span>{formatDate(pr.createdAt)}</span>
            {#if pr.mergedAt}
                <span>· merged: {formatDate(pr.mergedAt)}</span>
            {/if}
        </div>
    </div>
</div>

<style>
    .pr-card {
        display: flex;
        align-items: flex-start;
        gap: 0.5rem;
        padding: 0.5rem 0.75rem;
        background: #2a2a2a;
        border-radius: 6px;
        border: 1px solid #333;
    }

    .pr-card:hover {
        border-color: #555;
    }

    .pr-icon {
        font-size: 0.9rem;
        flex-shrink: 0;
        margin-top: 0.1rem;
    }

    .pr-info {
        display: flex;
        flex-direction: column;
        gap: 0.2rem;
        flex: 1;
        min-width: 0;
    }

    .pr-main {
        display: flex;
        align-items: center;
        gap: 0.5rem;
        flex-wrap: wrap;
    }

    .pr-number {
        font-family: monospace;
        font-size: 0.85rem;
        color: #4a9eff;
        text-decoration: none;
        flex-shrink: 0;
    }

    .pr-number:hover { text-decoration: underline; }

    .pr-title {
        font-size: 0.9rem;
        color: #ddd;
        white-space: nowrap;
        overflow: hidden;
        text-overflow: ellipsis;
        flex: 1;
        min-width: 0;
    }

    .pr-state {
        font-size: 0.75rem;
        padding: 0.1rem 0.4rem;
        border-radius: 4px;
        font-weight: bold;
        flex-shrink: 0;
    }

    .pr-open { background: #3a3a1a; color: #f0a500; }
    .pr-merged { background: #2a1a3a; color: #b39ddb; }
    .pr-closed { background: #3a1a1a; color: #ff5555; }

    .pr-meta {
        display: flex;
        gap: 0.4rem;
        font-size: 0.75rem;
        color: #666;
    }
</style>