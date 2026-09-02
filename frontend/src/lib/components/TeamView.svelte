<script lang="ts">
    import { type MemberResponse, type InviteLinkResponse, getInvitationsAsync, deleteInvitationAsync } from '../api/teamApi';
    import { authStore } from '../stores/authStore';
    import { teamStore  } from '../stores/teamStore';
    import MemberCard from './MemberCard.svelte';
    import InviteModal from './InviteModal.svelte';
    import ActivityFeed from './ActivityFeed.svelte';
    import InviteCard from './InviteCard.svelte';
    import ConfirmModal from './ConfirmModal.svelte';

    import { UserPlus, ChartNoAxesColumn, ChevronRight, ChevronDown, RefreshCw } from 'lucide-svelte';

    import { notify } from '../stores/notificationStore';

    export let projectId: string;

    let members: MemberResponse[] = [];
    let currentUserId = '';
    let currentUserRole = '';
    let isInviteModalOpen = false;
    let error = '';
    let loading = false;

    let invitesCollapsed = true;
    let invites: InviteLinkResponse[] = [];
    let invitesLoaded = false;
    let invitesLoading = false;
    let isConfirmOpen = false;
    let pendingDeleteToken = '';

    authStore.subscribe(state => {
        currentUserId = state.user?.userId ?? '';
    });

    teamStore.subscribe(state => {
        members = state.members;
    });

    $: currentUserRole = members.find(m => m.userId === currentUserId)?.projectRole ?? '';
    $: canInvite = currentUserRole === 'Owner' || currentUserRole === 'Admin';

    let lastRefreshTrigger = 0;
    teamStore.subscribe(state => {
        members = state.members;
        if (state.refreshTrigger > 0 && state.refreshTrigger !== lastRefreshTrigger) {
            lastRefreshTrigger = state.refreshTrigger;
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

    async function toggleInvites() {
        invitesCollapsed = !invitesCollapsed;
        if (!invitesCollapsed && !invitesLoaded) {
            invitesLoading = true;
            try {
                invites = await getInvitationsAsync(projectId);
                invitesLoaded = true;
            } catch (e: any) {
                notify.error(e.response?.data ?? e.message ?? 'Hiba a meghívók lekérésekor!');
            } finally {
                invitesLoading = false;
            }
        }
    }

    async function handleDeleteInvite(token: string) {
        try {
            await deleteInvitationAsync(projectId, token);
            invites = invites.filter(i => i.token !== token);
            notify.success('Meghívó törölve!');
        } catch (e: any) {
            const message = e.response?.data ?? e.message ?? 'Hiba a meghívó törlésekor!';
            error = message;
            notify.error(message);
        }
    }

    async function refreshInvites() {
        invitesLoading = true;
        try {
            invites = await getInvitationsAsync(projectId);
        } catch (e: any) {
            notify.error(e.response?.data ?? e.message ?? 'Hiba a meghívók frissítésekor!');
        } finally {
            invitesLoading = false;
        }
    }

    function requestDelete(token: string) {
        pendingDeleteToken = token;
        isConfirmOpen = true;
    }
</script>

<div class="team-container">
    <!-- Toolbar -->
    <div class="toolbar-with-title">
        <h2 class="toolbar-title">Csapattagok ({members.length})</h2>
        {#if canInvite}
            <div class="toolbar-actions">
                <button class="invite-btn" on:click={() => isInviteModalOpen = true}>
                    <UserPlus size={15} /> Meghívás
                </button>
                <button class="invite-btn" on:click={toggleInvites}>
                    {#if invitesCollapsed}
                        <ChevronRight size={15} /> Meghívók
                    {:else}
                        <ChevronDown size={15} /> Meghívók
                    {/if}
                </button>
            </div>
        {/if}
    </div>

    <!-- InviteList -->
    {#if canInvite && !invitesCollapsed}
        <div class="invites-section">
            <div class="invites-header">
                <span class="invites-title">Aktív meghívók {invitesLoaded ? `(${invites.length})` : ''}</span>
                <button class="refresh-btn" on:click={refreshInvites} title="Frissítés">
                    <RefreshCw size={14}  /> 
                </button>
            </div>
            {#if invitesLoading}
                <p class="loading">Betöltés...</p>
            {:else if invites.length === 0}
                <p class="empty">Nincsenek aktív meghívók</p>
            {:else}
                <div class="invites-list">
                    {#each invites as invite (invite.token)}
                        <InviteCard
                            {invite}
                            onDelete={() => requestDelete(invite.token)}
                        />
                    {/each}
                </div>
            {/if}
        </div>
    {/if}

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
                        onRefresh={async () => {}}
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

{#if isConfirmOpen}
    <ConfirmModal
        bind:isOpen={isConfirmOpen}
        title="Meghívó törlése"
        message="Biztosan törölni szeretnéd ezt a meghívót? A link ezután nem lesz használható!"
        confirmText="Törlés"
        onConfirm={async () => await handleDeleteInvite(pendingDeleteToken)}
    />
{/if}

<style>
    .team-container {
        display: flex;
        flex-direction: column;
        height: 100%;
        overflow-y: auto;
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

    .toolbar-actions {
        display: flex;
        gap: 0.5rem;
        align-items: center;
    }

    .toolbar-with-title h2 {
        font-size: 1rem;
        margin: 0 auto 0 0;
        color: var(--text-secondary);
    }

    .invites-header {
        display: flex;
        align-items: center;
        justify-content: space-between;
        margin-bottom: 0.5rem;
        flex-wrap: wrap;
        gap: 0.5rem;
    }

    .invites-title {
        font-size: 0.85rem;
        color: var(--text-secondary);
    }

    .refresh-btn {
        background: transparent;
        border: 1px solid var(--border-hover);
        color: var(--text-secondary);
        border-radius: 6px;
        padding: 0.3rem;
        cursor: pointer;
        display: flex;
        align-items: center;
    }

    .refresh-btn:hover {
        color: var(--text-primary);
        border-color: var(--text-muted);
    }

    .invites-section {
        padding: 1rem;
        border-bottom: 1px solid var(--border);
        flex-shrink: 0;
    }

    .invites-list {
        display: flex;
        flex-direction: column;
        gap: 0.5rem;
    }
</style>