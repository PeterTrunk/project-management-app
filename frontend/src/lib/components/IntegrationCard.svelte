<script lang="ts">
    import type { IntegrationResponse } from '../api/integrationApi';
    import { deleteIntegrationAsync, regenerateWebhookTokenAsync, toggleIntegrationAsync, resetWebhookSecretAsync } from '../api/integrationApi';
    import { removeIntegration, updateIntegration } from '../stores/integrationStore';
    import ConfirmModal from './ConfirmModal.svelte';

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
                } catch (e: any) {
                    error = e.response?.data ?? 'Hiba történt a törléskor!';
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
                } catch (e: any) {
                    error = e.response?.data ?? 'Hiba történt a regeneráláskor!';
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
            error = e.response?.data ?? 'Hiba történt!';
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
            isResetSecretOpen = false;
            newSecret = '';
        } catch (e: any) {
            resetError = e.response?.data ?? 'Hiba történt a secret reseteléskor!';
        } finally {
            resetLoading = false;
        }
    }

    async function copyUrl() {
        await navigator.clipboard.writeText(integration.webhookUrl);
        copiedUrl = true;
        setTimeout(() => copiedUrl = false, 2000);
    }

    function getProviderIcon(provider: string): string {
        switch (provider) {
            case 'GitHub': return '🐙';
            case 'GitLab': return '🦊';
            default: return '🔗';
        }
    }
</script>

<div class="integration-card">
    <div class="card-header">
        <div class="card-title">
            <span class="provider-icon">{getProviderIcon(integration.provider)}</span>
            <span class="provider">{integration.provider}</span>
            <span class="repo">{integration.repoFullName}</span>
        </div>
        <div class="card-badges">
            {#if integration.isVerified}
                <span class="badge verified">✓ Verified</span>
            {:else}
                <span class="badge unverified">⏳ Nem ellenőrzött</span>
            {/if}
            <span class="badge" class:enabled={integration.isEnabled} class:disabled={!integration.isEnabled}>
                {integration.isEnabled ? '● Aktív' : '○ Inaktív'}
            </span>
        </div>
    </div>

    <div class="webhook-info">
        <div class="info-row">
            <label>Webhook URL:</label>
            <div class="copy-row">
                <input type="text" readonly value={integration.webhookUrl} />
                <button class="copy-btn" on:click={copyUrl}>
                    {copiedUrl ? '✓ Másolva!' : '📋'}
                </button>
            </div>
        </div>
    </div>

    <!-- Beállítási útmutató -->
    <button class="guide-toggle" on:click={() => showGuide = !showGuide}>
        📖 {showGuide ? 'Útmutató elrejtése' : 'Beállítási útmutató'}
    </button>

    {#if showGuide}
        <div class="guide">
            {#if integration.provider === 'GitHub'}
                <ol>
                    <li>Menj a repo <strong>Settings → Webhooks → Add webhook</strong></li>
                    <li>Payload URL: <code>{integration.webhookUrl}</code></li>
                    <li>Content type: <code>application/json</code></li>
                    <li>Secret: <strong>a létrehozáskor megadott webhook secret</strong></li>
                    <li>Events: ✓ <strong>Pushes</strong> ✓ <strong>Pull requests</strong></li>
                    <li>Kattints az <strong>Add webhook</strong> gombra</li>
                </ol>
            {:else if integration.provider === 'GitLab'}
                <ol>
                    <li>Menj a repo <strong>Settings → Webhooks</strong></li>
                    <li>URL: <code>{integration.webhookUrl}</code></li>
                    <li>Secret token: <strong>a létrehozáskor megadott webhook secret</strong></li>
                    <li>Triggers: ✓ <strong>Push events</strong> ✓ <strong>Merge request events</strong></li>
                    <li>Kattints az <strong>Add webhook</strong> gombra</li>
                </ol>
            {/if}
        </div>
    {/if}

    <!-- Secret reset form -->
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
                    {resetLoading ? 'Mentés...' : '✓ Secret frissítése'}
                </button>
            </div>
        </div>
    {/if}

    <div class="card-actions">
        <button class="toggle-btn" on:click={handleToggle}>
            {integration.isEnabled ? '○ Letiltás' : '● Engedélyezés'}
        </button>
        <button class="regenerate-btn" on:click={handleRegenerate}>
            🔄 Token regenerálás
        </button>
        <button class="reset-btn" on:click={() => isResetSecretOpen = !isResetSecretOpen}>
            🔑 Secret reset
        </button>
        <button class="delete-btn" on:click={handleDelete}>
            🗑 Törlés
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
        background: #1e1e1e;
        border: 1px solid #333;
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
    }

    .provider-icon { font-size: 1.2rem; }
    .provider { font-weight: bold; font-size: 0.95rem; }
    .repo { color: #888; font-size: 0.9rem; }

    .card-badges {
        display: flex;
        gap: 0.5rem;
    }

    .badge {
        padding: 0.2rem 0.5rem;
        border-radius: 4px;
        font-size: 0.75rem;
        font-weight: bold;
    }

    .verified { background: #1a3a1a; color: #4caf50; }
    .unverified { background: #2a2a1a; color: #f0a500; }
    .enabled { background: #1a3a1a; color: #4caf50; }
    .disabled { background: #2a2a2a; color: #666; }

    .webhook-info {
        display: flex;
        flex-direction: column;
        gap: 0.5rem;
    }

    .info-row {
        display: flex;
        flex-direction: column;
        gap: 0.25rem;
    }

    .info-row label {
        font-size: 0.8rem;
        color: #888;
    }

    .copy-row {
        display: flex;
        gap: 0.5rem;
    }

    .copy-row input {
        flex: 1;
        background: #2a2a2a;
        border: 1px solid #333;
        border-radius: 6px;
        color: #aaa;
        padding: 0.4rem 0.6rem;
        font-size: 0.8rem;
    }

    .copy-btn {
        background: #2a2a2a;
        border: 1px solid #444;
        color: #aaa;
        padding: 0.4rem 0.6rem;
        border-radius: 6px;
        cursor: pointer;
        white-space: nowrap;
        font-size: 0.8rem;
    }

    .copy-btn:hover { background: #333; }

    .guide-toggle {
        background: transparent;
        border: none;
        color: #4a9eff;
        cursor: pointer;
        font-size: 0.85rem;
        text-align: left;
        padding: 0;
    }

    .guide-toggle:hover { text-decoration: underline; }

    .guide {
        background: #2a2a2a;
        border-radius: 6px;
        padding: 0.75rem 1rem;
        font-size: 0.85rem;
        color: #ccc;
    }

    .guide ol {
        margin: 0;
        padding-left: 1.25rem;
        display: flex;
        flex-direction: column;
        gap: 0.4rem;
    }

    .guide code {
        background: #1a1a1a;
        padding: 0.1rem 0.3rem;
        border-radius: 3px;
        font-size: 0.8rem;
        color: #4a9eff;
    }

    .reset-secret-form {
        background: #2a2a2a;
        border-radius: 6px;
        padding: 0.75rem;
        display: flex;
        flex-direction: column;
        gap: 0.5rem;
    }

    .reset-secret-form label {
        font-size: 0.85rem;
        color: #aaa;
    }

    .reset-secret-form input {
        background: #1e1e1e;
        border: 1px solid #444;
        border-radius: 6px;
        color: white;
        padding: 0.5rem;
        font-size: 0.9rem;
        width: 100%;
    }

    .reset-buttons {
        display: flex;
        justify-content: flex-end;
        gap: 0.5rem;
    }

    .confirm-reset-btn {
        background: #1a3a1a;
        border-color: #4caf50;
        color: #4caf50;
    }

    .confirm-reset-btn:hover { background: #2a4a2a; }
    .confirm-reset-btn:disabled { opacity: 0.5; cursor: not-allowed; }

    .card-actions {
        display: flex;
        gap: 0.5rem;
        flex-wrap: wrap;
    }

    button {
        padding: 0.4rem 0.8rem;
        border-radius: 6px;
        cursor: pointer;
        border: 1px solid #444;
        background: #2a2a2a;
        color: #aaa;
        font-size: 0.85rem;
    }

    button:hover { background: #333; }

    .toggle-btn { color: #f0a500; border-color: #f0a500; }
    .toggle-btn:hover { background: #3a2a00; }

    .regenerate-btn { color: #4a9eff; border-color: #4a9eff; }
    .regenerate-btn:hover { background: #1a2a3a; }

    .reset-btn { color: #b39ddb; border-color: #b39ddb; }
    .reset-btn:hover { background: #2a1a3a; }

    .delete-btn { color: #ff5555; border-color: #ff5555; }
    .delete-btn:hover { background: #3a1a1a; }

    .error { color: red; font-size: 0.85rem; }
</style>