<script lang="ts">
    import { onMount, onDestroy } from 'svelte';
    import { signalRService } from '../services/signalRService';
    import { integrationStore } from '../stores/integrationStore';
    import { getCommitLinksAsync, getPrLinksAsync, assignCommitToTaskAsync, assignPrToTaskAsync } from '../api/gitApi';
    import type { LinkedCommitResponse, LinkedPrResponse } from '../api/gitApi';
    import { filterCommitLinks, filterPrLinks } from '../utils/gitLinks';
    import CommitCard from './CommitCard.svelte';
    import PrCard from './PrCard.svelte';
    import TaskPickerModal from '../components/TaskPickerModal.svelte';

    import { GitBranch, CircleCheck, ToggleLeft, ToggleRight, Search, CornerUpRight } from 'lucide-svelte';

    import { notify } from '../stores/notificationStore';

    export let projectId: string;

    //A projekt ÖSSZES hivatkozása egy listában. A "hozzárendeletlen" nézet ebből szűrés,
    //nem külön lekérés - ugyanaz a feltétel, ami az adatbázisban is (TaskId == null).
    //
    //Szándékosan NEM a taskStore-ból: az csak a backlog és a nyitott sprintek taskjait
    //tartalmazza, tehát egy lezárt sprintben lévő task hivatkozása némán kimaradna.
    let commitLinks: LinkedCommitResponse[] = [];
    let prLinks: LinkedPrResponse[] = [];

    let activeTab: 'unmatched' | 'linked' = 'unmatched';
    let searchQuery = '';

    let loading = true;
    let error = '';

    let isTaskPickerOpen = false;
    let pendingCommitId = '';
    let pendingPrId = '';

    $: unmatchedCommits = commitLinks.filter(c => c.taskId === null);
    $: unmatchedPrs = prLinks.filter(p => p.taskId === null);

    $: linkedCommits = filterCommitLinks(commitLinks.filter(c => c.taskId !== null), searchQuery);
    $: linkedPrs = filterPrLinks(prLinks.filter(p => p.taskId !== null), searchQuery);

    $: linkedTotal = commitLinks.filter(c => c.taskId !== null).length
        + prLinks.filter(p => p.taskId !== null).length;

    onMount(async () => {
        await loadAll();
        signalRService.off('CommitLinked');
        signalRService.off('PrLinked');

        signalRService.on('CommitLinked', refresh);
        signalRService.on('PrLinked', refresh);
    });

    async function loadAll() {
        loading = true;
        try {
            await refresh();
        } finally {
            loading = false;
        }
    }

    //Betöltésjelző nélkül: a SignalR eseményre és a hozzárendelés után is ez fut,
    //és ott a lista villanása zavaróbb lenne, mint hasznos
    async function refresh() {
        try {
            [commitLinks, prLinks] = await Promise.all([
                getCommitLinksAsync(projectId),
                getPrLinksAsync(projectId)
            ]);
        } catch (e: any) {
            notify.error(e.response?.data ?? e.message ?? 'Hiba történt a git adatok lekérésekor!');
        }
    }

    function openTaskPicker(kind: 'commit' | 'pr', linkId: string) {
        pendingCommitId = kind === 'commit' ? linkId : '';
        pendingPrId = kind === 'pr' ? linkId : '';
        isTaskPickerOpen = true;
    }

    function closeTaskPicker() {
        isTaskPickerOpen = false;
        pendingCommitId = '';
        pendingPrId = '';
    }

    async function handleTaskSelected(taskId: string) {
        try {
            if (pendingCommitId) {
                await assignCommitToTaskAsync(projectId, pendingCommitId, taskId);
                notify.success('Commit hozzárendelve!');
            } else if (pendingPrId) {
                await assignPrToTaskAsync(projectId, pendingPrId, taskId);
                notify.success('Pull request hozzárendelve!');
            }

            //A SignalR esemény is újratölt, de arra nem támaszkodunk: egy szétesett
            //kapcsolat mellett a felhasználó a saját műveletének eredményét ne veszítse el
            await refresh();
        } catch (e: any) {
            notify.error(e.response?.data ?? e.message ?? 'Hiba a hozzárendeléskor!');
        } finally {
            closeTaskPicker();
        }
    }

    onDestroy(() => {
        signalRService.off('CommitLinked');
        signalRService.off('PrLinked');
    });
</script>

<div class="git-container">
    <!-- Toolbar -->
    <div class="git-toolbar">
        <h2>Git Activity</h2>
    </div>

    <div class="git-content">
        {#if loading}
            <p class="loading">Betöltés...</p>
        {:else if error}
            <p class="error">{error}</p>
        {:else}
            <!-- Integrációk: a füleken kívül, mert mindkét nézetre vonatkozik -->
            <div class="section">
                <h3>Integrációk</h3>
                {#if $integrationStore.integrations.length === 0}
                    <p class="empty">Nincs integráció — add hozzá a Project Settings-ben!</p>
                {:else}
                    <div class="integrations-list">
                        {#each $integrationStore.integrations as integration (integration.id)}
                            <div class="integration-item">
                                <span class="provider-icon"><GitBranch size={16} /></span>
                                <span class="repo truncate">{integration.repoFullName}</span>
                                <div class="flags">
                                    <span class="badge" class:badge-green={integration.isEnabled} class:disabled={!integration.isEnabled}>
                                        {#if integration.isEnabled}
                                            <ToggleRight size={12} /> Aktív
                                        {:else}
                                            <ToggleLeft size={12} /> Inaktív
                                        {/if}
                                    </span>
                                    {#if integration.isVerified}
                                        <span class="badge badge-green"><CircleCheck size={12} /> Verified</span>
                                    {/if}
                                </div>
                            </div>
                        {/each}
                    </div>
                {/if}
            </div>

            <div class="git-tabs">
                <button class="tab-btn" class:active={activeTab === 'unmatched'}
                    on:click={() => activeTab = 'unmatched'}>
                    Hozzárendeletlen
                    {#if unmatchedCommits.length + unmatchedPrs.length > 0}
                        <span class="tab-badge">{unmatchedCommits.length + unmatchedPrs.length}</span>
                    {/if}
                </button>
                <button class="tab-btn" class:active={activeTab === 'linked'}
                    on:click={() => activeTab = 'linked'}>
                    Kapcsolt
                    {#if linkedTotal > 0}
                        <span class="tab-badge">{linkedTotal}</span>
                    {/if}
                </button>
            </div>

            {#if activeTab === 'unmatched'}
                <div class="section">
                    <h3>Hozzárendeletlen Commitok ({unmatchedCommits.length})</h3>
                    {#if unmatchedCommits.length === 0}
                        <p class="empty">Minden commit hozzá van rendelve taskhoz!</p>
                    {:else}
                        <div class="git-list">
                            {#each unmatchedCommits as commit (commit.id)}
                                <div class="unmatched-item">
                                    <CommitCard {commit} />
                                    <div class="assign-row">
                                        <button class="assign-btn" on:click={() => openTaskPicker('commit', commit.id)}>
                                            Hozzárendelés
                                        </button>
                                    </div>
                                </div>
                            {/each}
                        </div>
                    {/if}
                </div>

                <div class="section">
                    <h3>Hozzárendeletlen Pull Requestek ({unmatchedPrs.length})</h3>
                    {#if unmatchedPrs.length === 0}
                        <p class="empty">Minden PR hozzá van rendelve taskhoz!</p>
                    {:else}
                        <div class="git-list">
                            {#each unmatchedPrs as pr (pr.id)}
                                <div class="unmatched-item">
                                    <PrCard {pr} />
                                    <div class="assign-row">
                                        <button class="assign-btn" on:click={() => openTaskPicker('pr', pr.id)}>
                                            Hozzárendelés
                                        </button>
                                    </div>
                                </div>
                            {/each}
                        </div>
                    {/if}
                </div>
            {:else}
                <!-- A kapcsolt elemek: ide eddig nem volt út a felületen, ezért egy rossz
                     task kulccsal beillesztett commit elérhetetlen volt -->
                <div class="search-row">
                    <span class="search-icon"><Search size={14} /></span>
                    <input
                        type="text"
                        placeholder="Keresés sha, üzenet, szerző, PR-szám vagy task kulcs szerint"
                        bind:value={searchQuery}
                    />
                </div>

                <div class="section">
                    <h3>Kapcsolt Commitok ({linkedCommits.length})</h3>
                    {#if linkedCommits.length === 0}
                        <p class="empty">
                            {searchQuery.trim() === ''
                                ? 'Még nincs taskhoz kapcsolt commit.'
                                : 'Nincs találat erre a keresésre.'}
                        </p>
                    {:else}
                        <div class="git-list">
                            {#each linkedCommits as commit (commit.id)}
                                <div class="unmatched-item">
                                    <CommitCard {commit} />
                                    <div class="assign-row">
                                        <span class="task-chip" title={commit.taskTitle ?? ''}>{commit.taskKey}</span>
                                        {#if commit.isManuallyLinked}
                                            <span class="manual-chip" title="Ezt a hozzárendelést ember állította be">kézi</span>
                                        {/if}
                                        <button class="assign-btn" on:click={() => openTaskPicker('commit', commit.id)}>
                                            <CornerUpRight size={13} /> Áthelyezés
                                        </button>
                                    </div>
                                </div>
                            {/each}
                        </div>
                    {/if}
                </div>

                <div class="section">
                    <h3>Kapcsolt Pull Requestek ({linkedPrs.length})</h3>
                    {#if linkedPrs.length === 0}
                        <p class="empty">
                            {searchQuery.trim() === ''
                                ? 'Még nincs taskhoz kapcsolt pull request.'
                                : 'Nincs találat erre a keresésre.'}
                        </p>
                    {:else}
                        <div class="git-list">
                            {#each linkedPrs as pr (pr.id)}
                                <div class="unmatched-item">
                                    <PrCard {pr} />
                                    <div class="assign-row">
                                        <span class="task-chip" title={pr.taskTitle ?? ''}>{pr.taskKey}</span>
                                        {#if pr.isManuallyLinked}
                                            <span class="manual-chip" title="Ezt a hozzárendelést ember állította be">kézi</span>
                                        {/if}
                                        <button class="assign-btn" on:click={() => openTaskPicker('pr', pr.id)}>
                                            <CornerUpRight size={13} /> Áthelyezés
                                        </button>
                                    </div>
                                </div>
                            {/each}
                        </div>
                    {/if}
                </div>
            {/if}
        {/if}
    </div>
</div>

<TaskPickerModal
    isOpen={isTaskPickerOpen}
    {projectId}
    onSelect={handleTaskSelected}
    onClose={closeTaskPicker}
/>

<style>
    .git-container {
        display: flex;
        flex-direction: column;
        height: 100%;
        overflow: hidden;
    }

    .git-toolbar {
        display: flex;
        align-items: center;
        padding: 0.5rem 1rem;
        background: var(--bg-secondary);
        border-bottom: 1px solid var(--border);
        flex-shrink: 0;
    }

    .git-toolbar h2 {
        font-size: 1rem;
        margin: 0;
        color: var(--text-secondary);
    }

    .git-content {
        padding: 1rem;
        overflow-y: auto;
        flex: 1;
        display: flex;
        flex-direction: column;
        gap: 1rem;
    }

    .section h3 {
        font-size: 0.85rem;
        color: var(--text-secondary);
        text-transform: uppercase;
        letter-spacing: 0.05em;
        margin: 0 0 0.75rem;
        border-bottom: 1px solid var(--border);
        padding-bottom: 0.5rem;
    }

    .integrations-list {
        display: flex;
        flex-direction: column;
        gap: 0.5rem;
    }

    .integration-item {
        display: flex;
        align-items: center;
        gap: 0.5rem;
        padding: 0.5rem 0.75rem;
        background: var(--bg-card);
        border-radius: 6px;
        border: 1px solid var(--border-subtle);
        flex-wrap: wrap;
    }

    .provider-icon {
        display: flex;
        align-items: center;
        color: var(--text-muted);
    }

    .repo {
        flex: 1;
        font-size: 0.9rem;
        color: var(--text-secondary);
        min-width: 0;
    }

    .badge {
        display: flex;
        align-items: center;
        gap: 0.3rem;
        padding: 0.2rem 0.5rem;
        border-radius: 4px;
        font-size: 0.75rem;
        font-weight: bold;
    }

    .disabled { background: var(--bg-hover);        color: var(--text-muted); }

    /* Fulek - ugyanaz az idioma, mint a TaskDetailModal reszletnezetenel */
    .git-tabs {
        display: flex;
        gap: 0.25rem;
        padding: 0 1rem;
        border-bottom: 1px solid var(--border-subtle);
        flex-shrink: 0;
    }

    .tab-btn {
        display: flex;
        align-items: center;
        gap: 0.35rem;
        padding: 0.65rem 0.85rem;
        border: none;
        border-bottom: 2px solid transparent;
        background: transparent;
        color: var(--text-secondary);
        font-size: 0.85rem;
        cursor: pointer;
        border-radius: 6px 6px 0 0;
        margin-bottom: -1px;
        transition: color 0.15s, border-color 0.15s, background 0.15s;
        white-space: nowrap;
    }

    .tab-btn:hover { color: var(--text-primary); background: var(--bg-hover); }

    .tab-btn.active {
        color: var(--accent-blue);
        border-bottom-color: var(--accent-blue);
        background: transparent;
    }

    .tab-badge {
        background: var(--bg-hover);
        color: var(--text-muted);
        font-size: 0.7rem;
        padding: 0.1rem 0.4rem;
        border-radius: 10px;
    }

    .search-row {
        display: flex;
        align-items: center;
        gap: 0.5rem;
        margin: 1rem 1rem 0;
        padding: 0.4rem 0.6rem;
        background: var(--bg-input);
        border: 1px solid var(--border-subtle);
        border-radius: 6px;
    }

    .search-icon {
        display: flex;
        align-items: center;
        color: var(--text-muted);
        flex-shrink: 0;
    }

    .search-row input {
        flex: 1;
        min-width: 0;
        background: transparent;
        border: none;
        outline: none;
        color: var(--text-primary);
        font-size: 0.85rem;
    }

    /* A task kulcsa a kapcsolt kartyan: enelkul nem derulne ki, hova kerult az elem */
    .task-chip {
        padding: 0.1rem 0.45rem;
        border-radius: 4px;
        background: var(--bg-hover);
        color: var(--text-secondary);
        font-size: 0.75rem;
        font-weight: 600;
        white-space: nowrap;
    }

    .manual-chip {
        padding: 0.1rem 0.45rem;
        border-radius: 4px;
        background: var(--bg-hover);
        color: var(--text-muted);
        font-size: 0.7rem;
        white-space: nowrap;
    }

    .git-list {
        display: flex;
        flex-direction: column;
        gap: 0.75rem;
    }

    .unmatched-item {
        display: flex;
        flex-direction: column;
        gap: 0.4rem;
        padding: 0.5rem 0.75rem;
        background: var(--bg-hover);
        border-radius: 6px;
        border: 1px solid var(--border-subtle);
        transition: border-color 0.15s;
    }

    .unmatched-item:hover {
        border-color: var(--border-hover);
    }

    .assign-row {
        display: flex;
        align-items: center;
        gap: 0.5rem;
        flex-wrap: wrap;
        padding-top: 0.5rem;
        border-top: 1px solid var(--border-subtle);
    }

    button {
        display: flex;
        align-items: center;
        gap: 0.35rem;
        padding: 0.35rem 0.75rem;
        border-radius: 6px;
        cursor: pointer;
        font-size: 0.85rem;
        border: 1px solid var(--border-hover);
        background: var(--bg-hover);
        color: var(--text-secondary);
        white-space: nowrap;
        transition: background 0.15s, color 0.15s;
    }

    .assign-btn { color: var(--accent-green); border-color: var(--accent-green); }
    .assign-btn:hover { background: var(--accent-green-bg); }
    .assign-btn:disabled { opacity: 0.5; cursor: not-allowed; }

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