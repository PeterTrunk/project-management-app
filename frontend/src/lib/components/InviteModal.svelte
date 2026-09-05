<script lang="ts">
    import { onMount } from 'svelte';
    import { generateInviteLinkAsync, type InviteLinkResponse } from '../api/teamApi';

    import { Copy, Check, RefreshCw } from 'lucide-svelte';

    import { notify } from '../stores/notificationStore';
    
    export let isInviteModalOpen = false;
    export let projectId: string;
    export let onClose: () => void = () => {};

    let modalRef: HTMLElement;
    onMount(() => modalRef?.focus());

    let maxUses: number | null = null;
    let expiresInDays: number | null = 7;
    let generatedInvite: InviteLinkResponse | null = null;
    let error = '';
    let copied = false;

    function closeModal() {
        isInviteModalOpen = false;
        generatedInvite = null;
        error = '';
        onClose();
    }

    async function handleGenerate() {
        error = '';
        try {
            generatedInvite = await generateInviteLinkAsync(projectId, {
                maxUses,
                expiresInDays
            });
            notify.success('Meghívó link létrehozva!');
        } catch (e: any) {
            const message = e.response?.data ?? e.message ?? 'Hiba történt a meghívó generálásakor!';
            error = message;
            notify.error(message);
        }
    }

    async function handleCopy() {
        if (!generatedInvite) return;
        await navigator.clipboard.writeText(generatedInvite.inviteUrl);
        copied = true;
        setTimeout(() => copied = false, 2000);
    }
</script>

<div class="modal-overlay" on:click|self={closeModal}
    bind:this={modalRef}
    on:keydown={(e) => e.key === 'Escape' && closeModal()}
    role="dialog"
    aria-modal="true"
    tabindex="-1"
>
    <div class="modal-content">
        <h1>Meghívó Link Generálása</h1>

        {#if !generatedInvite}
            <form on:submit|preventDefault={handleGenerate}>
                <div class="form-group">
                    <label for="maxUses">
                        Maximális használatok száma
                        <span class="hint">(üresen hagyva = korlátlan)</span>
                    </label>
                    <input
                        id="maxUses"
                        type="number"
                        min="1"
                        placeholder="Korlátlan"
                        bind:value={maxUses}
                    />
                </div>
                
                <div class="form-group">
                    <label for="expiresInDays">
                        Lejárat (napokban)
                        <span class="hint">(üresen hagyva = soha nem jár le)</span>
                    </label>
                    <input
                        id="expiresInDays"
                        type="number"
                        min="1"
                        max="30"
                        placeholder="Soha nem jár le"
                        bind:value={expiresInDays}
                    />
                </div>

                {#if error}
                    <p class="error">{error}</p>
                {/if}

                <div class="buttons">
                    <button type="button" on:click={closeModal}>Mégse</button>
                    <button type="submit" class="generate-btn">Generálás</button>
                </div>
            </form>
        {:else}
            <div class="invite-result">
                <div class="invite-info">
                    {#if generatedInvite.maxUses}
                        <p>Max használatok: <strong>{generatedInvite.maxUses}</strong></p>
                    {:else}
                        <p>Max használatok: <strong>Korlátlan</strong></p>
                    {/if}
                    {#if generatedInvite.expiresAt}
                        <p>Lejárat: <strong>
                            {new Date(generatedInvite.expiresAt).toLocaleDateString('hu-HU')}
                        </strong></p>
                    {:else}
                        <p>Lejárat: <strong>Soha</strong></p>
                    {/if}
                </div>

                <div class="invite-link">
                    <input 
                        type="text" 
                        readonly 
                        value={generatedInvite.inviteUrl}
                    />
                    <button class="copy-btn" on:click={handleCopy}>
                        {#if copied}
                            <Check size={14} /> Másolva!
                        {:else}
                            <Copy size={14} /> Másolás
                        {/if}
                    </button>
                </div>

                <div class="buttons">
                    <button type="button" on:click={() => generatedInvite = null}>
                        <RefreshCw size={14} /> Új link generálása
                    </button>
                    <button type="button" on:click={closeModal}>Bezárás</button>
                </div>
            </div>
        {/if}
    </div>
</div>

<style>
    .modal-overlay {
        position: fixed;
        top: 0;
        left: 0;
        width: 100%;
        height: 100%;
        background: var(--shadow);
        display: flex;
        justify-content: center;
        align-items: center;
        z-index: 1000;
    }

    .modal-content {
        background: var(--bg-card);
        border: 1px solid var(--border);
        padding: 2rem;
        border-radius: 8px;
        width: 500px;
        max-width: 95vw;
        display: flex;
        flex-direction: column;
        gap: 1.5rem;
    }

    @media (max-width: 480px) {
        .modal-content {
            padding: var(--card-padding);
        }
    }

    h1 {
        font-size: 1.3rem;
        margin: 0;
        color: var(--text-primary);
    }

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

    .form-group label {
        font-size: 0.9rem;
        color: var(--text-secondary);
    }

    .hint {
        font-size: 0.8rem;
        color: var(--text-muted);
        margin-left: 0.5rem;
    }

    input[type="number"] {
        background: var(--bg-input);
        border: 1px solid var(--border-hover);
        border-radius: 6px;
        color: var(--text-primary);
        padding: 0.5rem;
        font-size: 1rem;
        width: 100%;
    }

    input[type="number"]:focus {
        outline: none;
        border-color: var(--accent-blue);
    }

    .buttons {
        display: flex;
        justify-content: flex-end;
        gap: 0.75rem;
        margin-top: 0.5rem;
        flex-wrap: wrap;
    }

    button {
        display: flex;
        align-items: center;
        gap: 0.35rem;
        padding: 0.5rem 1rem;
        border-radius: 6px;
        cursor: pointer;
        border: 1px solid var(--border-hover);
        background: var(--bg-hover);
        color: var(--text-secondary);
        font-size: 0.9rem;
        transition: background 0.15s, color 0.15s;
    }

    button:hover { background: var(--border-hover); color: var(--text-primary); }

    .generate-btn {
        background: var(--accent-green-bg);
        border-color: var(--accent-green);
        color: var(--accent-green);
    }

    .generate-btn:hover { background: var(--accent-green); color: #fff; }

    .invite-result {
        display: flex;
        flex-direction: column;
        gap: 1rem;
    }

    .invite-info {
        background: var(--bg-hover);
        border: 1px solid var(--border);
        border-radius: 6px;
        padding: 0.75rem 1rem;
        display: flex;
        gap: 2rem;
        flex-wrap: wrap;
    }

    .invite-info p {
        margin: 0;
        font-size: 0.9rem;
        color: var(--text-secondary);
    }

    .invite-link {
        display: flex;
        gap: 0.5rem;
        flex-wrap: wrap;
    }

    .invite-link input {
        flex: 1;
        background: var(--bg-hover);
        border: 1px solid var(--border);
        border-radius: 6px;
        color: var(--text-secondary);
        padding: 0.5rem;
        font-size: 0.85rem;
        min-width: 0;
    }

    .copy-btn {
        background: var(--accent-blue-bg);
        border-color: var(--accent-blue);
        color: var(--accent-blue);
        white-space: nowrap;
    }

    .copy-btn:hover { background: var(--accent-blue); color: #fff; }

    .error {
        color: var(--accent-red);
        font-size: 0.85rem;
    }
</style>