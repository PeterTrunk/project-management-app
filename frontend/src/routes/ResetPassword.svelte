<script lang="ts">
    import { onMount } from 'svelte';
    import { resetPasswordAsync } from '../lib/api/authApi';
    import { push } from 'svelte-spa-router';
    import { KeyRound, CheckCircle } from 'lucide-svelte';
    import { validatePassword } from '../lib/validators';

    let token = '';
    let newPassword = '';
    let newPasswordConfirm = '';
    let error = '';
    let passwordError = '';
    let success = false;

    onMount(() => {
        token = new URLSearchParams(window.location.hash.split('?')[1]).get('token') ?? '';
        if (!token) {
            error = 'Hiányzó token!';
        }
    });

    async function handleResetPassword() {
        error = '';
        passwordError = '';

        const passwordErr = validatePassword(newPassword);
        if (passwordErr) {
            passwordError = passwordErr;
            return;
        }

        if (newPassword !== newPasswordConfirm) {
            passwordError = 'A két jelszó nem egyezik!';
            return;
        }

        try {
            await resetPasswordAsync(token, newPassword);
            success = true;
            setTimeout(() => push('/'), 3000);
        } catch (e: any) {
            error = e.response?.data ?? 'Érvénytelen vagy lejárt token!';
        }
    }
</script>

<div class="auth-container">
    <div class="auth-card">
        {#if success}
            <CheckCircle size={32} color="var(--accent-green)" />
            <h1>Jelszó megváltoztatva!</h1>
            <p class="desc">Sikeresen megváltoztattad a jelszavad! Átirányítás a bejelentkezési oldalra...</p>
        {:else}
            <KeyRound size={32} color="var(--accent-blue)" />
            <h1>Jelszó visszaállítás</h1>
            <p class="desc">Add meg az új jelszavad!</p>
            <form on:submit|preventDefault={handleResetPassword}>
                <div class="input-group">
                    <input 
                        type="password" 
                        placeholder="Új jelszó" 
                        bind:value={newPassword}
                    />
                </div>
                <div class="input-group">
                    <input 
                        type="password" 
                        placeholder="Új jelszó megerősítése" 
                        bind:value={newPasswordConfirm}
                    />
                    {#if passwordError}
                        <p class="field-error">{passwordError}</p>
                    {/if}
                </div>
                {#if error}
                    <p id="failed">{error}</p>
                {/if}
                <button type="submit" disabled={!token}>
                    Jelszó megváltoztatása
                </button>
            </form>
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
        max-width: 95vw;
        display: flex;
        flex-direction: column;
        align-items: center;
        gap: 1rem;
        border: 1px solid var(--border-subtle);
        text-align: center;
    }

    @media (max-width: 480px) {
        .auth-card {
            padding: 1.5rem;
        }
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

    .field-error {
        color: var(--accent-red);
        font-size: 0.8rem;
        margin: 0;
        text-align: left;
        word-break: break-word;
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

    button[type="submit"]:disabled {
        opacity: 0.5;
        cursor: not-allowed;
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

    #failed { color: var(--accent-red); white-space: pre-line; word-break: break-word; }
</style>