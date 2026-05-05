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
        registerSignalREvents();
    });

    async function loadAll() {
        loading = true;
        error = '';
        try {
            unmatchedCommits = await getUnmatchedCommitsAsync(projectId);
            unmatchedPrs = await getUnmatchedPrsAsync(projectId);
        } catch (e: any) {
            error = 'Hiba történt a git adatok lekérésekor!';
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
        } catch (e: any) {
            error = e.response?.data ?? 'Hiba történt a hozzárendeléskor!';
        }
    }

    async function handleAssignPr(prId: string) {
        if (!selectedTaskId) return;
        try {
            await assignPrToTaskAsync(projectId, prId, selectedTaskId);
            unmatchedPrs = unmatchedPrs.filter(p => p.id !== prId);
            selectedPrId = null;
            selectedTaskId = '';
        } catch (e: any) {
            error = e.response?.data ?? 'Hiba történt a hozzárendeléskor!';
        }
    }

    function registerSignalREvents() {
        signalRService.off('CommitLinked');
        signalRService.off('PrLinked');

        signalRService.on('CommitLinked', async () => {
            unmatchedCommits = await getUnmatchedCommitsAsync(projectId);
        });

        signalRService.on('PrLinked', async () => {
            unmatchedPrs = await getUnmatchedPrsAsync(projectId);
        });
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
                                <span>{integration.provider === 'GitHub' ? '🐙' : '🦊'}</span>
                                <span class="repo">{integration.repoFullName}</span>
                                <span class="badge" class:enabled={integration.isEnabled} class:disabled={!integration.isEnabled}>
                                    {integration.isEnabled ? '● Aktív' : '○ Inaktív'}
                                </span>
                                {#if integration.isVerified}
                                    <span class="badge verified">✓ Verified</span>
                                {/if}
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
                                            ✓ Hozzárendelés
                                        </button>
                                        <button 
                                            class="cancel-btn"
                                            on:click={() => { selectedCommitId = null; selectedTaskId = ''; }}>
                                            Mégse
                                        </button>
                                    {:else}
                                        <button 
                                            class="select-btn"
                                            on:click={() => { selectedCommitId = commit.id; selectedTaskId = ''; }}>
                                            + Task hozzárendelése
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
                                            ✓ Hozzárendelés
                                        </button>
                                        <button
                                            class="cancel-btn"
                                            on:click={() => { selectedPrId = null; selectedTaskId = ''; }}>
                                            Mégse
                                        </button>
                                    {:else}
                                        <button
                                            class="select-btn"
                                            on:click={() => { selectedPrId = pr.id; selectedTaskId = ''; }}>
                                            + Task hozzárendelése
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
        background: #1a1a1a;
        border-bottom: 1px solid #2a2a2a;
        flex-shrink: 0;
    }

    .git-toolbar h2 {
        font-size: 1rem;
        margin: 0;
        color: #ccc;
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
        color: #aaa;
        text-transform: uppercase;
        letter-spacing: 0.05em;
        margin: 0 0 0.75rem;
        border-bottom: 1px solid #2a2a2a;
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
        background: #1e1e1e;
        border-radius: 6px;
        border: 1px solid #333;
    }

    .repo {
        flex: 1;
        font-size: 0.9rem;
        color: #ccc;
    }

    .badge {
        padding: 0.2rem 0.5rem;
        border-radius: 4px;
        font-size: 0.75rem;
        font-weight: bold;
    }

    .enabled { background: #1a3a1a; color: #4caf50; }
    .disabled { background: #2a2a2a; color: #666; }
    .verified { background: #1a3a1a; color: #4caf50; }

    .git-list {
        display: flex;
        flex-direction: column;
        gap: 0.75rem;
    }

    .unmatched-item {
        display: flex;
        flex-direction: column;
        gap: 0.4rem;
    }

    .assign-row {
        display: flex;
        align-items: center;
        gap: 0.5rem;
        padding-left: 0.5rem;
    }

    select {
        flex: 1;
        background: #2a2a2a;
        border: 1px solid #444;
        border-radius: 6px;
        color: white;
        padding: 0.4rem 0.5rem;
        font-size: 0.85rem;
    }

    button {
        padding: 0.35rem 0.75rem;
        border-radius: 6px;
        cursor: pointer;
        font-size: 0.85rem;
        border: 1px solid #444;
        background: #2a2a2a;
        color: #aaa;
        white-space: nowrap;
    }

    .select-btn { color: #4a9eff; border-color: #4a9eff; }
    .select-btn:hover { background: #1a2a3a; }

    .assign-btn { color: #4caf50; border-color: #4caf50; }
    .assign-btn:hover { background: #1a3a1a; }
    .assign-btn:disabled { opacity: 0.5; cursor: not-allowed; }

    .cancel-btn { color: #ff5555; border-color: #ff5555; }
    .cancel-btn:hover { background: #3a1a1a; }

    .loading, .empty, .error {
        text-align: center;
        padding: 1rem;
        color: #555;
        font-size: 0.9rem;
    }

    .error { color: red; }
</style>