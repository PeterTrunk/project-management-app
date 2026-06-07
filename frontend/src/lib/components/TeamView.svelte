<script lang="ts">
    import { onMount } from 'svelte';
    import { push } from 'svelte-spa-router'
    import { signalRService } from '../services/signalRService';
    import { onDestroy } from 'svelte';
    import { getMembersAsync, type MemberResponse } from '../api/teamApi';
    import { authStore } from '../stores/authStore';
    import { projectStore } from '../stores/projectStore';
    import { teamStore, setMembers, triggerTeamRefresh } from '../stores/teamStore';
    import MemberCard from './MemberCard.svelte';
    import InviteModal from './InviteModal.svelte';
    import ActivityFeed from './ActivityFeed.svelte';

    import { UserPlus, ChartNoAxesColumn } from 'lucide-svelte';

    export let projectId: string;

    let members: MemberResponse[] = [];
    let currentUserId = '';
    let currentUserRole = '';
    let isInviteModalOpen = false;
    let error = '';
    let loading = true;

    authStore.subscribe(state => {
        currentUserId = state.user?.userId ?? '';
    });

    $: currentUserRole = members.find(m => m.userId === currentUserId)?.projectRole ?? '';
    $: canInvite = currentUserRole === 'Owner' || currentUserRole === 'Admin';

    onMount(async () => {
        await loadMembers();
        registerSignalREvents();
    });

    function registerSignalREvents() {
        signalRService.off('MemberAdded');
        signalRService.off('MemberRoleUpdated');

        signalRService.on('MemberAdded', async () => {
            await loadMembers();
        });

        signalRService.on('MemberRoleUpdated', async () => {
            await loadMembers();
        });
    }

    async function loadMembers() {
        loading = true;
        error = '';
        try {
            const data = await getMembersAsync(projectId);
            setMembers(data);
            members = data;
        } catch (e: any) {
            error = e.response?.data ?? 'Hiba történt!';
        } finally {
            loading = false;
        }
    }

    let lastRefreshTrigger = 0;
    teamStore.subscribe(state => {
        members = state.members;
        if (state.refreshTrigger > 0 && state.refreshTrigger !== lastRefreshTrigger) {
            lastRefreshTrigger = state.refreshTrigger;
            loadMembers();
        }
    });

    // Rendezés: Owner első, utána Admin, Member, Viewer ABC sorrendben
    $: sortedMembers = [...members].sort((a, b) => {
        const roleOrder = { Owner: 0, Admin: 1, Member: 2, Viewer: 3 };
        const aOrder = roleOrder[a.projectRole as keyof typeof roleOrder] ?? 4;
        const bOrder = roleOrder[b.projectRole as keyof typeof roleOrder] ?? 4;
        if (aOrder !== bOrder) return aOrder - bOrder;
        return a.displayName.localeCompare(b.displayName);
    });

    onDestroy(() => {
        signalRService.off('MemberAdded');
        signalRService.off('MemberRoleUpdated');
    });
</script>

<div class="team-container">
    <!-- Toolbar -->
    <div class="team-toolbar">
        <h2>Csapattagok ({members.length})</h2>
        {#if canInvite}
            <button class="invite-btn" on:click={() => isInviteModalOpen = true}>
                <UserPlus size={15} /> Meghívás
            </button>
        {/if}
    </div>

    <!-- Tagok listája -->
    <div class="members-section">
        {#if loading}
            <p class="loading">Betöltés...</p>
        {:else if error}
            <p class="error">{error}</p>
        {:else if sortedMembers.length === 0}
            <p class="empty">Nincsenek tagok</p>
        {:else}
            <div class="members-list">
                {#each sortedMembers as member (member.userId)}
                    <MemberCard
                        {member}
                        {projectId}
                        {currentUserRole}
                        {currentUserId}
                        onRefresh={loadMembers}
                    />
                {/each}
            </div>
        {/if}
    </div>

    <div class="activity-section">
        <h3><ChartNoAxesColumn size={14} /> Recent Activity</h3>
        <ActivityFeed {projectId} />
    </div>
</div>

{#if isInviteModalOpen}
    <InviteModal
        bind:isInviteModalOpen={isInviteModalOpen}
        {projectId}
        onClose={() => isInviteModalOpen = false}
    />
{/if}

<style>
    .team-container {
        display: flex;
        flex-direction: column;
        height: 100%;
        overflow-y: auto;
    }

    .team-toolbar {
        display: flex;
        align-items: center;
        justify-content: space-between;
        padding: 0.5rem 1rem;
        background: var(--bg-secondary);
        border-bottom: 1px solid var(--border);
        flex-shrink: 0;
    }

    .team-toolbar h2 {
        font-size: 1rem;
        margin: 0;
        color: var(--text-secondary);
    }

    .invite-btn {
        display: flex;
        align-items: center;
        gap: 0.35rem;
        background: var(--accent-green-bg);
        border: 1px solid var(--accent-green);
        color: var(--accent-green);
        padding: 0.4rem 0.8rem;
        border-radius: 6px;
        cursor: pointer;
        font-size: 0.9rem;
        transition: background 0.15s;
    }

    .invite-btn:hover { background: var(--accent-green); color: #fff; }

    .members-section {
        padding: 1rem;
        flex-shrink: 0;
    }

    .members-list {
        display: flex;
        flex-direction: column;
        gap: 0.5rem;
    }

    .activity-section {
        padding: 1rem;
        border-top: 1px solid var(--border);
        flex: 1;
    }

    .activity-section h3 {
        display: flex;
        align-items: center;
        gap: 0.4rem;
        font-size: 0.95rem;
        color: var(--text-secondary);
        margin: 0 0 0.75rem;
        text-transform: uppercase;
        letter-spacing: 0.05em;
    }

    .loading, .empty {
        text-align: center;
        padding: 1rem;
        color: var(--text-muted);
        font-size: 0.9rem;
    }

    .error {
        text-align: center;
        padding: 1rem;
        color: var(--accent-red);
        font-size: 0.9rem;
    }
</style>