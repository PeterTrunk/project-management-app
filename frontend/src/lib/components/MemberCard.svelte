<script lang="ts">
    import type { MemberResponse } from '../api/teamApi';
    import { updateMemberRoleAsync, removeMemberAsync } from '../api/teamApi';
    import ConfirmModal from './ConfirmModal.svelte';

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
        <span class="member-name">{member.displayName}</span>
        <span class="member-email">{member.email}</span>
    </div>

    <div class="member-actions">
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
                Eltávolítás
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
        background: #2a2a2a;
        border-radius: 8px;
        border: 1px solid #333;
    }

    .member-card:hover {
        border-color: #555;
    }

    .member-avatar {
        width: 40px;
        height: 40px;
        border-radius: 50%;
        background: #3a3a5a;
        color: #aaaaff;
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
    }

    .member-name {
        font-size: 0.95rem;
        font-weight: bold;
    }

    .member-email {
        font-size: 0.8rem;
        color: #888;
    }

    .member-actions {
        display: flex;
        align-items: center;
        gap: 0.5rem;
    }

    .role-select {
        background: #1e1e1e;
        border: 1px solid #444;
        border-radius: 6px;
        color: white;
        padding: 0.3rem 0.5rem;
        font-size: 0.85rem;
        cursor: pointer;
    }

    .remove-btn {
        background: transparent;
        border: 1px solid #ff5555;
        color: #ff5555;
        padding: 0.3rem 0.6rem;
        border-radius: 6px;
        cursor: pointer;
        font-size: 0.85rem;
    }

    .remove-btn:hover {
        background: #3a1a1a;
    }

    .role-badge {
        padding: 0.25rem 0.6rem;
        border-radius: 6px;
        font-size: 0.8rem;
        font-weight: bold;
    }

    .badge-owner { background: #3a2a00; color: #f0a500; }
    .badge-admin { background: #1a2a3a; color: #4a9eff; }
    .badge-member { background: #1a3a1a; color: #4caf50; }
    .badge-viewer { background: #2a2a2a; color: #aaa; }

    .error {
        color: red;
        font-size: 0.85rem;
        margin-top: 0.25rem;
    }
</style>