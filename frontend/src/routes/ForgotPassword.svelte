<script lang="ts">
    import { forgotPasswordAsync } from '../lib/api/authApi';
    import { push } from 'svelte-spa-router';
    import { Mail } from 'lucide-svelte';

    let email = '';
    let error = '';
    let sent = false;

    async function handleForgotPassword() {
        error = '';
        try {
            await forgotPasswordAsync(email);
            sent = true;
        } catch (e: any) {
            error = 'Hiba történt!';
        }
    }
</script>

<div class="auth-container">
    <div class="auth-card">
        {#if !sent}
            <Mail size={32} color="var(--accent-blue)" />
            <h1>Elfelejtett jelszó</h1>
            <p class="desc">Add meg az email címed és küldünk egy jelszó visszaállítási linket!</p>
            <form on:submit|preventDefault={handleForgotPassword}>
                <div class="input-group">
                    <input type="email" placeholder="Email cím" bind:value={email} />
                </div>
                {#if error}
                    <p id="failed">{error}</p>
                {/if}
                <button type="submit">Visszaállítási link küldése</button>
            </form>
            <button class="secondary-btn" on:click={() => push('/')}>
                Vissza a bejelentkezéshez
            </button>
        {:else}
            <Mail size={32} color="var(--accent-green)" />
            <h1>Email elküldve!</h1>
            <p class="desc">Ha az email cím regisztrált, hamarosan megérkezik a visszaállítási link!</p>
            <button class="secondary-btn" on:click={() => push('/')}>
                Vissza a bejelentkezéshez
            </button>
        {/if}
    </div>
</div>

<style>
    .auth-container {
        display: flex;
        justify-content: center;
        align-items: center;
        min-height: 100vh;
        width: 100vw;
        background: var(--bg-primary);
    }

    .auth-card {
        background: var(--bg-card);
        border-radius: 12px;
        padding: 2.5rem;
        width: 400px;
        display: flex;
        flex-direction: column;
        align-items: center;
        gap: 1rem;
        border: 1px solid var(--border-subtle);
        text-align: center;
    }

    h1 {
        font-size: 1.5rem;
        margin: 0;
    }

    .desc {
        color: var(--text-secondary);
        margin: 0;
        font-size: 0.95rem;
        line-height: 1.5;
    }

    form {
        display: flex;
        flex-direction: column;
        gap: 0.75rem;
        width: 100%;
    }

    .input-group {
        display: flex;
        flex-direction: column;
        gap: 0.25rem;
    }

    input {
        background: var(--bg-input);
        border: 1px solid var(--border-hover);
        border-radius: 6px;
        color: var(--text-primary);
        padding: 0.75rem;
        font-size: 1rem;
        width: 100%;
    }

    input:focus {
        outline: none;
        border-color: var(--text-muted);
    }

    button[type="submit"] {
        background: var(--accent-blue);
        border: none;
        color: white;
        padding: 0.75rem;
        border-radius: 6px;
        cursor: pointer;
        font-size: 1rem;
        width: 100%;
    }

    button[type="submit"]:hover {
        opacity: 0.9;
    }

    .secondary-btn {
        background: transparent;
        border: 1px solid var(--border-hover);
        color: var(--text-secondary);
        padding: 0.75rem;
        border-radius: 6px;
        cursor: pointer;
        font-size: 0.9rem;
        width: 100%;
        text-align: center;
    }

    .secondary-btn:hover {
        border-color: var(--text-muted);
        color: var(--text-primary);
    }

    #failed { color: var(--accent-red); }
</style>