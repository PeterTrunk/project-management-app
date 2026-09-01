<script lang="ts">
    import type { MemberResponse } from '../api/teamApi';
    import { updateMemberRoleAsync, removeMemberAsync } from '../api/teamApi';
    import ConfirmModal from './ConfirmModal.svelte';

    import { UserMinus } from 'lucide-svelte';

    export let member: MemberResponse;
    export let projectId: string;
    export let currentUserRole: string;
    export let currentUserId: string;
    export let onRefresh: () => Promise<void> = async () => {};

    const ROLES = ['Admin', 'Member', 'Viewer'];

    let isConfirmOpen = false;
    let confirmTitle = '';
    let confirmMessage = '';
    let confirmAction: () => Promise<void> = async () => {};
    let error = '';

    $: isOwner = currentUserRole === 'Owner';
    $: isMemberOwner = member.projectRole === 'Owner';
    $: canModify = isOwner && !isMemberOwner;

    function openConfirm(title: string, message: string, action: () => Promise<void>) {
        confirmTitle = title;
        confirmMessage = message;
        confirmAction = action;
        isConfirmOpen = true;
    }

    async function handleRoleChange(e: Event) {
        const newRole = (e.currentTarget as HTMLSelectElement).value;
        openConfirm(
            'Szerepkör módosítása',
            `Biztosan módosítod ${member.displayName} szerepkörét ${newRole}-re?`,
            async () => {
                try {
                    await updateMemberRoleAsync(projectId, member.userId, { projectRole: newRole });
                    await onRefresh();
                } catch (e: any) {
                    error = e.response?.data ?? 'Hiba történt!';
                }
            }
        );
    }

    async function handleRemove() {
        openConfirm(
            'Tag eltávolítása',
            `Biztosan eltávolítod ${member.displayName} tagot a projektből?`,
            async () => {
                try {
                    await removeMemberAsync(projectId, member.userId);
                    await onRefresh();
                } catch (e: any) {
                    error = e.response?.data ?? 'Hiba történt!';
                }
            }
        );
    }

    function getInitials(name: string): string {
        return name
            .split(' ')
            .map(n => n[0])
            .join('')
            .toUpperCase()
            .slice(0, 2);
    }

    function getRoleBadgeClass(role: string): string {
        switch (role) {
            case 'Owner': return 'badge-owner';
            case 'Admin': return 'badge-admin';
            case 'Member': return 'badge-member';
            case 'Viewer': return 'badge-viewer';
            default: return '';
        }
    }
</script>

<div class="member-card">
    <div class="member-avatar">
        {getInitials(member.displayName)}
    </div>

    <div class="member-info">
        <span class="member-name truncate" title={member.displayName}>{member.displayName}</span>
        <span class="member-email truncate" title={member.email}>{member.email}</span>
    </div>

    <div class="member-actions wrap-480">
        {#if canModify}
            <select 
                class="role-select"
                value={member.projectRole}
                on:change={handleRoleChange}
            >
                {#each ROLES as role}
                    <option value={role}>{role}</option>
                {/each}
            </select>
            <button class="remove-btn" on:click={handleRemove}>
                <UserMinus size={14} /> Eltávolítás
            </button>
        {:else}
            <span class="role-badge {getRoleBadgeClass(member.projectRole)}">
                {member.projectRole}
            </span>
        {/if}
    </div>

    {#if error}
        <p class="error">{error}</p>
    {/if}
</div>

{#if isConfirmOpen}
    <ConfirmModal
        bind:isOpen={isConfirmOpen}
        title={confirmTitle}
        message={confirmMessage}
        confirmText="Megerősítés"
        onConfirm={confirmAction}
    />
{/if}

<style>
    .member-card {
        display: flex;
        align-items: center;
        gap: 1rem;
        padding: 0.75rem 1rem;
        background: var(--bg-hover);
        border-radius: 8px;
        border: 1px solid var(--border-subtle);
        transition: border-color 0.15s;
        flex-wrap: wrap;
        justify-content: flex-end;
    }

    .member-card:hover {
        border-color: var(--border-hover);
    }

    .member-avatar {
        width: 40px;
        height: 40px;
        border-radius: 50%;
        background: var(--accent-purple-bg);
        color: var(--accent-purple);
        display: flex;
        align-items: center;
        justify-content: center;
        font-weight: bold;
        font-size: 0.9rem;
        flex-shrink: 0;
    }

    .member-info {
        display: flex;
        flex-direction: column;
        gap: 0.1rem;
        flex: 1;
        text-align: left;
        min-width: 120px;
    }

    .member-name {
        font-size: 0.95rem;
        font-weight: bold;
        color: var(--text-primary);
    }

    .member-email {
        font-size: 0.8rem;
        color: var(--text-muted);
    }

    .member-actions {
        display: flex;
        align-items: center;
        gap: 0.5rem;
        
    }

    .role-select {
        background: var(--bg-card);
        border: 1px solid var(--border-hover);
        border-radius: 6px;
        color: var(--text-primary);
        padding: 0.3rem 0.5rem;
        font-size: 0.85rem;
        cursor: pointer;
    }

    .remove-btn {
        display: flex;
        align-items: center;
        gap: 0.35rem;
        background: transparent;
        border: 1px solid var(--accent-red);
        color: var(--accent-red);
        padding: 0.3rem 0.6rem;
        border-radius: 6px;
        cursor: pointer;
        font-size: 0.85rem;
        transition: background 0.15s;
    }

    .remove-btn:hover { background: var(--accent-red-bg); }

    .role-badge {
        padding: 0.25rem 0.6rem;
        border-radius: 6px;
        font-size: 0.8rem;
        font-weight: bold;
    }

    .badge-owner  { background: var(--accent-yellow-bg); color: var(--accent-yellow); }
    .badge-admin  { background: var(--accent-blue-bg);   color: var(--accent-blue); }
    .badge-member { background: var(--accent-green-bg);  color: var(--accent-green); }
    .badge-viewer { background: var(--bg-hover);         color: var(--text-muted); }

    .error {
        color: var(--accent-red);
        font-size: 0.85rem;
        margin-top: 0.25rem;
        white-space: pre-line;
        word-break: break-word;
    }
</style>