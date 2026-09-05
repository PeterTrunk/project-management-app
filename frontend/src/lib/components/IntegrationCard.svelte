<script lang="ts">
    import type { IntegrationResponse } from '../api/integrationApi';
    import { deleteIntegrationAsync, regenerateWebhookTokenAsync, toggleIntegrationAsync, resetWebhookSecretAsync } from '../api/integrationApi';
    import { removeIntegration, updateIntegration } from '../stores/integrationStore';
    import ConfirmModal from './ConfirmModal.svelte';

    import { 
        GitBranch, Copy, Check, RefreshCw, KeyRound, Trash2, 
        BookOpen, ToggleLeft, ToggleRight, CircleCheck, Clock 
    } from 'lucide-svelte';

    import { notify } from '../stores/notificationStore';

    export let integration: IntegrationResponse;
    export let projectId: string;

    let isConfirmOpen = false;
    let confirmTitle = '';
    let confirmMessage = '';
    let confirmAction: () => Promise<void> = async () => {};
    let confirmText = 'Megerősítés';
    let error = '';
    let copiedUrl = false;
    let showGuide = false;
    let isResetSecretOpen = false;
    let newSecret = '';
    let resetError = '';
    let resetLoading = false;

    function openConfirm(title: string, message: string, action: () => Promise<void>, text: string = 'Megerősítés') {
        confirmTitle = title;
        confirmMessage = message;
        confirmAction = action;
        confirmText = text;
        isConfirmOpen = true;
    }

    async function handleDelete() {
        openConfirm(
            'Integráció törlése',
            `Biztosan törölni szeretnéd a ${integration.provider} — ${integration.repoFullName} integrációt?`,
            async () => {
                try {
                    await deleteIntegrationAsync(projectId, integration.id);
                    removeIntegration(integration.id);
                    notify.success('Integráció törölve!');
                } catch (e: any) {
                    const message = e.response?.data ?? e.message ?? 'Hiba történt a törléskor!';
                    error = message;
                    notify.error(message);
                }
            },
            'Törlés'
        );
    }

    async function handleRegenerate() {
        openConfirm(
            'Token regenerálása',
            'Biztosan regenerálod a webhook tokent? A régi URL érvénytelenné válik — frissítsd a GitHub/GitLab beállításokban!',
            async () => {
                try {
                    const updated = await regenerateWebhookTokenAsync(projectId, integration.id);
                    updateIntegration(updated);
                    notify.success('Webhook token regenerálva!');
                } catch (e: any) {
                    const message = e.response?.data ?? e.message ?? 'Hiba történt a regeneráláskor!';
                    error = message;
                    notify.error(message);
                }
            },
            'Regenerálás'
        );
    }

    async function handleToggle() {
        try {
            await toggleIntegrationAsync(projectId, integration.id, !integration.isEnabled);
            updateIntegration({ ...integration, isEnabled: !integration.isEnabled });
        } catch (e: any) {
            notify.error(e.response?.data ?? e.message ?? 'Hiba történt!');
        }
    }

    async function handleResetSecret() {
        resetError = '';
        if (newSecret.length < 16) {
            resetError = 'A secret legalább 16 karakter kell legyen!';
            return;
        }
        resetLoading = true;
        try {
            await resetWebhookSecretAsync(projectId, integration.id, newSecret);
            updateIntegration({ ...integration, isVerified: false });
            notify.success('Webhook secret módosítva!');
            isResetSecretOpen = false;
            newSecret = '';
        } catch (e: any) {
            const message = e.response?.data ?? e.message ?? 'Hiba történt a secret reseteléskor!';
            resetError = message;
            notify.error(message);
        } finally {
            resetLoading = false;
        }
    }

    async function copyUrl() {
        await navigator.clipboard.writeText(integration.webhookUrl);
        copiedUrl = true;
        setTimeout(() => copiedUrl = false, 2000);
    }

    function getProviderIcon(provider: string): any {
        return GitBranch;
    }
</script>

<div class="integration-card card-overflow-hidden">
    <div class="card-header stack-480">
        <div class="card-title wrap-480">
            <span class="provider-icon">
                <svelte:component this={getProviderIcon(integration.provider)} size={18} />
            </span>
            <span class="provider">{integration.provider}</span>
            <span class="repo truncate">{integration.repoFullName}</span>
        </div>
        <div class="card-badges flags">
            {#if integration.isVerified}
                <span class="badge badge-green"><CircleCheck size={12} /> Verified</span>
            {:else}
                <span class="badge badge-yellow"><Clock size={12} /> Nem ellenőrzött</span>
            {/if}
            <span class="badge" class:badge-green={integration.isEnabled} class:disabled={!integration.isEnabled}>
                {#if integration.isEnabled}
                    <ToggleRight size={12} /> Aktív
                {:else}
                    <ToggleLeft size={12} /> Inaktív
                {/if}
            </span>
        </div>
    </div>

    <div class="webhook-info">
        <div class="info-row">
            <label>Webhook URL:
                <div class="copy-row">
                    <input type="text" class="truncate" readonly value={integration.webhookUrl} />
                    <button class="copy-btn" on:click={copyUrl}>
                        {#if copiedUrl}
                            <Check size={14} /> Másolva!
                        {:else}
                            <Copy size={14} />
                        {/if}
                    </button>
                </div>
            </label>
        </div>
    </div>

    <button class="guide-toggle" on:click={() => showGuide = !showGuide}>
        <BookOpen size={14} /> {showGuide ? 'Útmutató elrejtése' : 'Beállítási útmutató'}
    </button>

    {#if showGuide}
        <div class="guide word-break">
            {#if integration.provider === 'GitHub'}
                <ol>
                    <li>Menj a repo <strong>Settings/Webhooks/Add webhook</strong> menüjébe</li>
                    <li>Payload URL: <code>{integration.webhookUrl}</code></li>
                    <li>Content type: <code>application/json</code></li>
                    <li>Secret: <strong>a létrehozáskor megadott webhook secret</strong></li>
                    <li>Events: <strong>Pushes</strong> és <strong>Pull requests</strong></li>
                    <li>Kattints az <strong>Add webhook</strong> gombra</li>
                </ol>
            {:else if integration.provider === 'GitLab'}
                <ol>
                    <li>Menj a repo <strong>Settings → Webhooks</strong></li>
                    <li>URL: <code>{integration.webhookUrl}</code></li>
                    <li>Secret token: <strong>a létrehozáskor megadott webhook secret</strong></li>
                    <li>Triggers: <strong>Push events</strong> és <strong>Merge request events</strong></li>
                    <li>Kattints az <strong>Add webhook</strong> gombra</li>
                </ol>
            {/if}
        </div>
    {/if}

    {#if isResetSecretOpen}
        <div class="reset-secret-form">
            <label for="newSecret">Új Webhook Secret</label>
            <input
                id="newSecret"
                type="password"
                placeholder="Minimum 16 karakter"
                bind:value={newSecret}
            />
            {#if resetError}
                <p class="error">{resetError}</p>
            {/if}
            <div class="reset-buttons">
                <button on:click={() => { isResetSecretOpen = false; newSecret = ''; resetError = ''; }}>
                    Mégse
                </button>
                <button class="confirm-reset-btn" on:click={handleResetSecret} disabled={resetLoading}>
                    {#if resetLoading}
                        Mentés...
                    {:else}
                        <Check size={14} /> Secret frissítése
                    {/if}
                </button>
            </div>
        </div>
    {/if}

    <div class="card-actions">
        <button class="toggle-btn" on:click={handleToggle}>
            {#if integration.isEnabled}
                <ToggleLeft size={15} /> Letiltás
            {:else}
                <ToggleRight size={15} /> Engedélyezés
            {/if}
        </button>
        <button class="regenerate-btn" on:click={handleRegenerate}>
            <RefreshCw size={15} /> Token regenerálás
        </button>
        <button class="reset-btn" on:click={() => isResetSecretOpen = !isResetSecretOpen}>
            <KeyRound size={15} /> Secret reset
        </button>
        <button class="delete-btn" on:click={handleDelete}>
            <Trash2 size={15} /> Törlés
        </button>
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
        confirmText={confirmText}
        onConfirm={confirmAction}
    />
{/if}

<style>
    .integration-card {
        background: var(--bg-card);
        border: 1px solid var(--border-subtle);
        border-radius: 8px;
        padding: 1rem;
        display: flex;
        flex-direction: column;
        gap: 0.75rem;
    }

    .card-header {
        display: flex;
        align-items: center;
        justify-content: space-between;
        flex-wrap: wrap;
        gap: 0.5rem;
    }

    .card-title {
        display: flex;
        align-items: center;
        gap: 0.5rem;
        min-width: 0;
        flex: 1;
    }

    .provider-icon {
        display: flex;
        align-items: center;
        color: var(--text-secondary);
        flex-shrink: 0;
    }

    .provider {
        font-weight: bold;
        font-size: 0.95rem;
        color: var(--text-primary);
        flex-shrink: 0;
        white-space: nowrap;
    }
    .repo {
        color: var(--text-muted);
        font-size: 0.9rem;
        flex: 1 1 100%;
    }

    .card-badges {
        display: flex;
        gap: 0.5rem;
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

    .disabled   { background: var(--bg-hover);         color: var(--text-muted); }

    .webhook-info {
        display: flex;
        flex-direction: column;
        gap: 0.5rem;
    }

    .info-row label {
        font-size: 0.8rem;
        color: var(--text-muted);
        display: flex;
        flex-direction: column;
        gap: 0.35rem;
    }

    .copy-row {
        display: flex;
        gap: 0.5rem;
    }

    .copy-row input {
        flex: 1;
        background: var(--bg-hover);
        border: 1px solid var(--border);
        border-radius: 6px;
        color: var(--text-secondary);
        padding: 0.4rem 0.6rem;
        font-size: 0.8rem;
    }

    .copy-btn {
        display: flex;
        align-items: center;
        gap: 0.3rem;
        background: var(--bg-hover);
        border: 1px solid var(--border-hover);
        color: var(--text-secondary);
        padding: 0.4rem 0.6rem;
        border-radius: 6px;
        cursor: pointer;
        white-space: nowrap;
        font-size: 0.8rem;
    }

    .copy-btn:hover { background: var(--border-hover); color: var(--text-primary); }

    .guide-toggle {
        display: flex;
        align-items: center;
        gap: 0.4rem;
        background: transparent;
        border: none;
        color: var(--accent-blue);
        cursor: pointer;
        font-size: 0.85rem;
        text-align: left;
        padding: 0;
    }

    .guide-toggle:hover { text-decoration: underline; }

    .guide {
        background: var(--bg-hover);
        border-radius: 6px;
        padding: 0.75rem 1rem;
        font-size: 0.85rem;
        color: var(--text-secondary);
        overflow: hidden;
    }

    .guide ol {
        margin: 0;
        padding-left: 1.25rem;
        display: flex;
        flex-direction: column;
        gap: 0.4rem;
    }

    .guide code {
        background: var(--bg-primary);
        padding: 0.1rem 0.3rem;
        border-radius: 3px;
        font-size: 0.8rem;
        color: var(--accent-blue);
    }

    .reset-secret-form {
        background: var(--bg-hover);
        border-radius: 6px;
        padding: 0.75rem;
        display: flex;
        flex-direction: column;
        gap: 0.5rem;
    }

    .reset-secret-form label {
        font-size: 0.85rem;
        color: var(--text-secondary);
    }

    .reset-secret-form input {
        background: var(--bg-card);
        border: 1px solid var(--border-hover);
        border-radius: 6px;
        color: var(--text-primary);
        padding: 0.5rem;
        font-size: 0.9rem;
        width: 100%;
    }

    .reset-buttons {
        display: flex;
        justify-content: flex-end;
        gap: 0.5rem;
        flex-wrap: wrap;
    }

    .confirm-reset-btn {
        display: flex;
        align-items: center;
        gap: 0.35rem;
        background: var(--accent-green-bg);
        border-color: var(--accent-green);
        color: var(--accent-green);
    }

    .confirm-reset-btn:hover { background: var(--accent-green); color: #fff; }
    .confirm-reset-btn:disabled { opacity: 0.5; cursor: not-allowed; }

    .card-actions {
        display: flex;
        gap: 0.5rem;
        flex-wrap: wrap;
    }

    button {
        display: flex;
        align-items: center;
        gap: 0.35rem;
        padding: 0.4rem 0.8rem;
        border-radius: 6px;
        cursor: pointer;
        border: 1px solid var(--border-hover);
        background: var(--bg-hover);
        color: var(--text-secondary);
        font-size: 0.85rem;
        transition: background 0.15s, color 0.15s;
    }

    button:hover { background: var(--border-hover); color: var(--text-primary); }

    .toggle-btn     { color: var(--accent-yellow); border-color: var(--accent-yellow); }
    .toggle-btn:hover { background: var(--accent-yellow-bg); }

    .regenerate-btn { color: var(--accent-blue); border-color: var(--accent-blue); }
    .regenerate-btn:hover { background: var(--accent-blue-bg); }

    .reset-btn      { color: var(--accent-purple); border-color: var(--accent-purple); }
    .reset-btn:hover { background: var(--accent-purple-bg); }

    .delete-btn     { color: var(--accent-red); border-color: var(--accent-red); }
    .delete-btn:hover { background: var(--accent-red-bg); }

    .error { color: var(--accent-red); font-size: 0.85rem; }
</style>