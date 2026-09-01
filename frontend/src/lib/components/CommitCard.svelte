<script lang="ts">
    import type { CommitLinkResponse } from '../api/taskApi';

    import { GitCommitHorizontal } from 'lucide-svelte';

    export let commit: CommitLinkResponse;

    function shortenSha(sha: string): string {
        return sha.substring(0, 7);
    }

    function formatDate(dateString: string): string {
        return new Date(dateString).toLocaleDateString('hu-HU');
    }
</script>

<div class="commit-card">
    <div class="commit-main-row">
        <span class="commit-icon">
            <GitCommitHorizontal size={15} />
        </span>
        <div class="commit-info">
            <div class="commit-main">
                {#if commit.commitUrl}
                    <a href={commit.commitUrl} target="_blank" class="commit-sha">
                        {shortenSha(commit.commitSha)}
                    </a>
                {:else}
                    <span class="commit-sha">{shortenSha(commit.commitSha)}</span>
                {/if}
                <span class="commit-message">{commit.message.split('\n')[0]}</span>
            </div>
            <div class="commit-meta">
                <span>{commit.authorName}</span>
                <span>·</span>
                <span>{commit.authorEmail}</span>
                <span>·</span>
                <span>{formatDate(commit.committedAt)}</span>
            </div>
        </div>
    </div>
    <slot name="actions" />
</div>

<style>
    .commit-card {
        display: flex;
        align-items: flex-start;
        gap: 0.5rem;
    }

    .commit-icon {
        display: flex;
        align-items: center;
        color: var(--accent-blue);
        flex-shrink: 0;
        margin-top: 0.1rem;
    }

    .commit-info {
        display: flex;
        flex-direction: column;
        gap: 0.2rem;
        flex: 1;
        min-width: 0;
    }

    .commit-main {
        display: flex;
        align-items: center;
        gap: 0.5rem;
        flex-wrap: wrap;
    }

    .commit-sha {
        font-family: monospace;
        font-size: 0.85rem;
        color: var(--accent-blue);
        text-decoration: none;
        flex-shrink: 0;
    }

    .commit-sha:hover { text-decoration: underline; }

    .commit-message {
        font-size: 0.9rem;
        color: var(--text-primary);
        white-space: nowrap;
        overflow: hidden;
        text-overflow: ellipsis;
        flex: 1;
        min-width: 0;
    }

    .commit-meta {
        display: flex;
        gap: 0.4rem;
        font-size: 0.75rem;
        color: var(--text-muted);
        flex-wrap: wrap;
    }
</style>