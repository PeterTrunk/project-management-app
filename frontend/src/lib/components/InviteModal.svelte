<script lang="ts">
    import { onMount } from 'svelte';
    import { generateInviteLinkAsync, type InviteLinkResponse } from '../api/teamApi';

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
        } catch (e: any) {
            error = e.response?.data ?? 'Hiba történt a meghívó generálásakor!';
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
                        {copied ? 'Másolva!' : 'Másolás'}
                    </button>
                </div>

                <div class="buttons">
                    <button type="button" on:click={() => generatedInvite = null}>
                        Új link generálása
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
        width: 500px;
        max-width: 95vw;
        display: flex;
        flex-direction: column;
        gap: 1.5rem;
    }

    h1 {
        font-size: 1.3rem;
        margin: 0;
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
        color: #ccc;
    }

    .hint {
        font-size: 0.8rem;
        color: #666;
        margin-left: 0.5rem;
    }

    input[type="number"] {
        background: #2a2a2a;
        border: 1px solid #444;
        border-radius: 6px;
        color: white;
        padding: 0.5rem;
        font-size: 1rem;
        width: 100%;
    }

    input[type="number"]:focus {
        outline: none;
        border-color: #666;
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

    .generate-btn {
        background: #1a3a1a;
        border-color: #4caf50;
        color: #4caf50;
    }

    .generate-btn:hover { background: #2a4a2a; }

    .invite-result {
        display: flex;
        flex-direction: column;
        gap: 1rem;
    }

    .invite-info {
        background: #2a2a2a;
        border-radius: 6px;
        padding: 0.75rem 1rem;
        display: flex;
        gap: 2rem;
    }

    .invite-info p {
        margin: 0;
        font-size: 0.9rem;
        color: #aaa;
    }

    .invite-link {
        display: flex;
        gap: 0.5rem;
    }

    .invite-link input {
        flex: 1;
        background: #2a2a2a;
        border: 1px solid #444;
        border-radius: 6px;
        color: #aaa;
        padding: 0.5rem;
        font-size: 0.85rem;
    }

    .copy-btn {
        background: #1a2a3a;
        border-color: #4a9eff;
        color: #4a9eff;
        white-space: nowrap;
    }

    .copy-btn:hover { background: #2a3a4a; }

    .error {
        color: red;
        font-size: 0.85rem;
    }
</style>