<script lang="ts">
    import { onMount } from 'svelte';
    import { createIntegrationAsync } from '../api/integrationApi';
    import { addIntegration } from '../stores/integrationStore';

    export let isOpen = false;
    export let projectId: string;
    export let onClose: () => void = () => {};

    let modalRef: HTMLElement;
    onMount(() => modalRef?.focus());

    let provider = 'GitHub';
    let repoFullName = '';
    let webhookSecret = '';
    let accessToken = '';
    let error = '';
    let loading = false;

    function closeModal() {
        isOpen = false;
        reset();
        onClose();
    }

    function reset() {
        provider = 'GitHub';
        repoFullName = '';
        webhookSecret = '';
        accessToken = '';
        error = '';
        loading = false;
    }

    async function handleCreate() {
        error = '';
        loading = true;
        try {
            const integration = await createIntegrationAsync(projectId, {
                provider,
                repoFullName,
                webhookSecret,
                accessToken: accessToken || null
            });
            addIntegration(integration);
            closeModal();
        } catch (e: any) {
            error = e.response?.data ?? 'Hiba történt az integráció létrehozásakor!';
        } finally {
            loading = false;
        }
    }
</script>

<div class="modal-overlay"
    on:click|self={closeModal}
    bind:this={modalRef}
    on:keydown={(e) => e.key === 'Escape' && closeModal()}
    role="dialog"
    aria-modal="true"
    tabindex="-1"
>
    <div class="modal-content">
        <h1>Git Integráció Hozzáadása</h1>

        <form on:submit|preventDefault={handleCreate}>
            <div class="form-group">
                <label for="provider">Provider</label>
                <select id="provider" bind:value={provider}>
                    <option value="GitHub">🐙 GitHub</option>
                    <option value="GitLab">🦊 GitLab</option>
                </select>
            </div>

            <div class="form-group">
                <label for="repoFullName">Repository neve</label>
                <input
                    id="repoFullName"
                    type="text"
                    placeholder="owner/repo"
                    bind:value={repoFullName}
                />
                <span class="hint">Formátum: tulajdonos/repository-neve</span>
            </div>

            <div class="form-group">
                <label for="webhookSecret">Webhook Secret</label>
                <input
                    id="webhookSecret"
                    type="password"
                    placeholder="Minimum 16 karakter"
                    bind:value={webhookSecret}
                />
                <span class="hint">
                    ⚠️ Ezt a secretet add meg a {provider} webhook beállításánál is!
                    Tárold biztonságos helyen — később nem lesz megjeleníthető!
                </span>
            </div>

            <div class="form-group">
                <label for="accessToken">
                    Access Token
                    <span class="optional">(opcionális)</span>
                </label>
                <input
                    id="accessToken"
                    type="password"
                    placeholder="ghp_xxxxxxxxxxxx"
                    bind:value={accessToken}
                />
                <span class="hint">Jövőbeli funkciókhoz szükséges</span>
            </div>

            {#if error}
                <p class="error">{error}</p>
            {/if}

            <div class="buttons">
                <button type="button" on:click={closeModal}>Mégse</button>
                <button 
                    type="submit" 
                    class="create-btn" 
                    disabled={loading || !repoFullName || !webhookSecret}>
                    {loading ? 'Létrehozás...' : '+ Hozzáadás'}
                </button>
            </div>
        </form>
    </div>
</div>

<style>
    .modal-overlay {
        position: fixed;
        top: 0;
        left: 0;
        width: 100%;
        height: 100%;
        background: rgba(0, 0, 0, 0.5);
        display: flex;
        justify-content: center;
        align-items: center;
        z-index: 1000;
    }

    .modal-content {
        background: #1e1e1e;
        padding: 2rem;
        border-radius: 8px;
        width: 480px;
        max-width: 95vw;
        display: flex;
        flex-direction: column;
        gap: 1.5rem;
    }

    h1 { font-size: 1.3rem; margin: 0; }

    form {
        display: flex;
        flex-direction: column;
        gap: 1rem;
    }

    .form-group {
        display: flex;
        flex-direction: column;
        gap: 0.4rem;
    }

    label { font-size: 0.9rem; color: #ccc; }

    .optional {
        font-size: 0.8rem;
        color: #666;
        margin-left: 0.5rem;
    }

    select, input {
        background: #2a2a2a;
        border: 1px solid #444;
        border-radius: 6px;
        color: white;
        padding: 0.5rem;
        font-size: 0.95rem;
        width: 100%;
    }

    select:focus, input:focus {
        outline: none;
        border-color: #666;
    }

    .hint {
        font-size: 0.75rem;
        color: #555;
    }

    .buttons {
        display: flex;
        justify-content: flex-end;
        gap: 0.75rem;
        margin-top: 0.5rem;
    }

    button {
        padding: 0.5rem 1rem;
        border-radius: 6px;
        cursor: pointer;
        border: 1px solid #444;
        background: #2a2a2a;
        color: white;
        font-size: 0.9rem;
    }

    button:hover { background: #333; }

    .create-btn {
        background: #1a3a1a;
        border-color: #4caf50;
        color: #4caf50;
    }

    .create-btn:hover { background: #2a4a2a; }
    .create-btn:disabled { opacity: 0.5; cursor: not-allowed; }

    .error { color: red; font-size: 0.85rem; }
</style>