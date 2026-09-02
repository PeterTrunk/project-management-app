<script lang="ts">
    import { onMount, onDestroy } from 'svelte';
    import { signalRService } from '../services/signalRService';
    import { integrationStore } from '../stores/integrationStore';
    import { taskStore } from '../stores/taskStore';
    import { getUnmatchedCommitsAsync, getUnmatchedPrsAsync, assignCommitToTaskAsync, assignPrToTaskAsync } from '../api/gitApi';
    import type { CommitLinkResponse, PrLinkResponse } from '../api/taskApi';
    import type { IntegrationResponse } from '../api/integrationApi';
    import type { TaskResponse } from '../api/taskApi';
    import CommitCard from './CommitCard.svelte';
    import PrCard from './PrCard.svelte';

    import { GitBranch, CircleCheck, X, Plus, ToggleLeft, ToggleRight } from 'lucide-svelte';

    import { notify } from '../stores/notificationStore';

    export let projectId: string;

    let unmatchedCommits: CommitLinkResponse[] = [];
    let unmatchedPrs: PrLinkResponse[] = [];
    let integrations: IntegrationResponse[] = [];
    let tasks: TaskResponse[] = [];
    let loading = true;
    let error = '';

    // Task selector state
    let selectedCommitId: string | null = null;
    let selectedPrId: string | null = null;
    let selectedTaskId: string = '';

    integrationStore.subscribe(state => {
        integrations = state.integrations;
    });

    taskStore.subscribe(state => {
        tasks = state.tasks;
    });

    onMount(async () => {
        await loadAll();
        signalRService.off('CommitLinked');
        signalRService.off('PrLinked');

        signalRService.on('CommitLinked', async () => {
            unmatchedCommits = await getUnmatchedCommitsAsync(projectId);
        });

        signalRService.on('PrLinked', async () => {
            unmatchedPrs = await getUnmatchedPrsAsync(projectId);
        });
    });

    async function loadAll() {
        loading = true;
        try {
            unmatchedCommits = await getUnmatchedCommitsAsync(projectId);
            unmatchedPrs = await getUnmatchedPrsAsync(projectId);
        } catch (e: any) {
            notify.error(e.response?.data ?? e.message ?? 'Hiba történt a git adatok lekérésekor!');
        } finally {
            loading = false;
        }
    }

    async function handleAssignCommit(commitId: string) {
        if (!selectedTaskId) return;
        try {
            await assignCommitToTaskAsync(projectId, commitId, selectedTaskId);
            unmatchedCommits = unmatchedCommits.filter(c => c.id !== commitId);
            selectedCommitId = null;
            selectedTaskId = '';
            notify.success('Commit hozzárendelve!');
        } catch (e: any) {
            notify.error(e.response?.data ?? e.message ?? 'Hiba történt a hozzárendeléskor!');
        }
    }

    async function handleAssignPr(prId: string) {
        if (!selectedTaskId) return;
        try {
            await assignPrToTaskAsync(projectId, prId, selectedTaskId);
            unmatchedPrs = unmatchedPrs.filter(p => p.id !== prId);
            selectedPrId = null;
            selectedTaskId = '';
            notify.success('PR hozzárendelve!');
        } catch (e: any) {
            notify.error(e.response?.data ?? e.message ?? 'Hiba történt a hozzárendeléskor!');
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
            <!-- Integrációk -->
            <div class="section">
                <h3>Integrációk</h3>
                {#if integrations.length === 0}
                    <p class="empty">Nincs integráció — add hozzá a Project Settings-ben!</p>
                {:else}
                    <div class="integrations-list">
                        {#each integrations as integration (integration.id)}
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

            <!-- Unmatched Commitok -->
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
                                    {#if selectedCommitId === commit.id}
                                        <select bind:value={selectedTaskId}>
                                            <option value="">Válassz taskot...</option>
                                            {#each tasks as task}
                                                <option value={task.id}>
                                                    {task.taskKey} — {task.title}
                                                </option>
                                            {/each}
                                        </select>
                                        <button 
                                            class="assign-btn" 
                                            disabled={!selectedTaskId}
                                            on:click={() => handleAssignCommit(commit.id)}>
                                            <CircleCheck size={14} /> Hozzárendelés
                                        </button>
                                        <button 
                                            class="cancel-btn"
                                            on:click={() => { selectedCommitId = null; selectedTaskId = ''; }}>
                                            <X size={14} /> Mégse
                                        </button>
                                    {:else}
                                        <button 
                                            class="select-btn"
                                            on:click={() => { selectedCommitId = commit.id; selectedTaskId = ''; }}>
                                            <Plus size={14} /> Task hozzárendelése
                                        </button>
                                    {/if}
                                </div>
                            </div>
                        {/each}
                    </div>
                {/if}
            </div>

            <!-- Unmatched PR-ok -->
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
                                    {#if selectedPrId === pr.id}
                                        <select bind:value={selectedTaskId}>
                                            <option value="">Válassz taskot...</option>
                                            {#each tasks as task}
                                                <option value={task.id}>
                                                    {task.taskKey} — {task.title}
                                                </option>
                                            {/each}
                                        </select>
                                        <button 
                                            class="assign-btn"
                                            disabled={!selectedTaskId}
                                            on:click={() => handleAssignPr(pr.id)}>
                                            <CircleCheck size={14} /> Hozzárendelés
                                        </button>
                                        <button 
                                            class="cancel-btn"
                                            on:click={() => { selectedPrId = null; selectedTaskId = ''; }}>
                                            <X size={14} /> Mégse
                                        </button>
                                    {:else}
                                        <button 
                                            class="select-btn"
                                            on:click={() => { selectedPrId = pr.id; selectedTaskId = ''; }}>
                                            <Plus size={14} /> Task hozzárendelése
                                        </button>
                                    {/if}
                                </div>
                            </div>
                        {/each}
                    </div>
                {/if}
            </div>
        {/if}
    </div>
</div>

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

    select {
        flex: 1;
        background: var(--bg-input);
        border: 1px solid var(--border-hover);
        border-radius: 6px;
        color: var(--text-primary);
        padding: 0.4rem 0.5rem;
        font-size: 0.85rem;
        min-width: 0;
    }

    select:focus {
        outline: none;
        border-color: var(--accent-blue);
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

    .select-btn { color: var(--accent-blue);  border-color: var(--accent-blue); }
    .select-btn:hover { background: var(--accent-blue-bg); }

    .assign-btn { color: var(--accent-green); border-color: var(--accent-green); }
    .assign-btn:hover { background: var(--accent-green-bg); }
    .assign-btn:disabled { opacity: 0.5; cursor: not-allowed; }

    .cancel-btn { color: var(--accent-red); border-color: var(--accent-red); }
    .cancel-btn:hover { background: var(--accent-red-bg); }

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