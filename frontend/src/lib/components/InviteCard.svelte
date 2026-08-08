<script lang="ts">
    import type { InviteLinkResponse } from '../api/teamApi';
    import { Copy, Check, Trash2 } from 'lucide-svelte';

    export let invite: InviteLinkResponse;
    export let onDelete: () => void = () => {};

    let copied = false;

    function copyInviteUrl() {
        navigator.clipboard.writeText(invite.inviteUrl);
        copied = true;
        setTimeout(() => copied = false, 2000);
    }

    function formatExpiry(date: Date | null): string {
        if (!date) return 'Nem jár le';
        const d = new Date(date);
        if (d.getFullYear() > 9000) return 'Nem jár le';
        return d.toLocaleDateString('hu-HU') + ' ' + d.toLocaleTimeString('hu-HU', { hour: '2-digit', minute: '2-digit' });
    }

    $: isExpired = invite.expiresAt 
        ? new Date(invite.expiresAt) < new Date() 
        : false;
    
    $: isMaxed = invite.maxUses !== null && invite.useCount >= invite.maxUses;
</script>

<div class="invite-card" class:expired={isExpired || isMaxed}>
    <div class="invite-info">
        <div class="invite-row">
            <span class="invite-url">
                <a href={invite.inviteUrl} target="_blank" rel="noopener noreferrer" class="invite-url">
                    {invite.inviteUrl}
                </a>
            </span>
        </div>
        <div class="invite-meta">
            <span>Lejár: {formatExpiry(invite.expiresAt)}</span>
            <span>Használva: {invite.useCount}{invite.maxUses ? `/${invite.maxUses}` : ''}</span>
            {#if isExpired}
                <span class="badge expired">Lejárt</span>
            {:else if isMaxed}
                <span class="badge maxed">Limit elérve</span>
            {:else}
                <span class="badge active">Aktív</span>
            {/if}
        </div>
    </div>
    <div class="invite-actions">
        <button class="copy-btn" on:click={copyInviteUrl} title="Másolás">
            {#if copied}
                <Check size={14} />
            {:else}
                <Copy size={14} />
            {/if}
        </button>
        <button class="delete-btn" on:click={onDelete} title="Törlés">
            <Trash2 size={14} />
        </button>
    </div>
</div>

<style>
    .invite-card {
        display: flex;
        align-items: center;
        justify-content: space-between;
        padding: 0.75rem;
        border-radius: 8px;
        background: var(--bg-hover);
        border: 1px solid var(--border-subtle);
        gap: 1rem;
    }

    .invite-card.expired {
        opacity: 0.6;
    }

    .invite-info {
        display: flex;
        flex-direction: column;
        gap: 0.25rem;
        min-width: 0;
    }

    .invite-url {
        font-size: 0.8rem;
        color: var(--text-secundary);
        white-space: nowrap;
        overflow: hidden;
        text-overflow: ellipsis;
        max-width: 300px;
    }

    .invite-meta {
        display: flex;
        align-items: center;
        gap: 0.75rem;
        font-size: 0.75rem;
        color: var(--text-muted);
    }

    .badge {
        padding: 0.1rem 0.4rem;
        border-radius: 4px;
        font-size: 0.7rem;
        font-weight: 600;
    }

    .badge.active {
        background: var(--accent-green-bg);
        color: var(--accent-green);
    }

    .badge.expired {
        background: var(--accent-red-bg);
        color: var(--accent-red);
    }

    .badge.maxed {
        background: var(--accent-yellow-bg);
        color: var(--accent-yellow);
    }

    .invite-actions {
        display: flex;
        gap: 0.5rem;
        flex-shrink: 0;
    }

    .copy-btn, .delete-btn {
        background: transparent;
        border: 1px solid var(--border-hover);
        color: var(--text-secundary);
        border-radius: 6px;
        padding: 0.4rem;
        cursor: pointer;
        display: flex;
        align-items: center;
    }

    .copy-btn:hover {
        color: var(--text-primary);
        border-color: var(--text-muted);
    }

    .delete-btn:hover {
        color: var(--accent-red);
        border-color: var(--accent-red);
    }
    
    .invite-url:hover {
        color: var(--accent-blue);
        text-decoration: underline;
    }
</style>