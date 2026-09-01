<script lang="ts">
    import { loginAsync, loginWithTotpAsync } from '../lib/api/authApi';
    import { login } from '../lib/stores/authStore';
    import { push } from 'svelte-spa-router';

    let email = '';
    let password = '';
    let totpToken = '';
    let error = '';
    let requiresTotp = false;

    async function handleLogin() {
        try {
            const response = await loginAsync({ email, password });
            
            if (response.requiresTotp) {
                requiresTotp = true;
                return;
            }

            finishLogin(response);
        } catch (e: any) {
            error = e.response?.data ?? "Hibás email vagy jelszó!";
        }
    }

    async function handleTotpLogin() {
        try {
            const response = await loginWithTotpAsync({ email, password, totpToken });
            finishLogin(response);
        } catch (e: any) {
            error = e.response?.data ?? "Érvénytelen TOTP token!";
        }
    }

    function finishLogin(response: any) {
        login(response.token, {
            userId: response.userId,
            email: response.email,
            displayName: response.displayName,
            isTotpEnabled: response.isTotpEnabled ?? false,
            isEmailVerified: response.isEmailVerified ?? false
        });

        const pendingToken = localStorage.getItem('pendingInviteToken');
        if (pendingToken) {
            localStorage.removeItem('pendingInviteToken');
            push(`/invite/${pendingToken}`);
        } else {
            push('/app');
        }
    }
</script>

<div class="auth-container">
    <div class="auth-card">
        {#if !requiresTotp}
            <h1>Bejelentkezés</h1>
            <form on:submit|preventDefault={handleLogin}>
                <input type="email" placeholder="Email" bind:value={email}/>
                <input type="password" placeholder="Jelszó" bind:value={password}/>
                <div class="forgot-password">
                    <button type="button" class="link-btn" on:click={() => push('/forgot-password')}>
                        Elfelejtett jelszó?
                    </button>
                </div>
                {#if error}
                    <p id="failed">{error}</p>
                {/if}
                <button type="submit">Bejelentkezés</button>
            </form>
            <div class="divider">
                <span>vagy</span>
            </div>
            <button class="secondary-btn" on:click={() => push('/register')}>
                Még nincs fiókod? Regisztrálj!
            </button>
        {:else}
            <h1>Kétfaktoros hitelesítés</h1>
            <p>Add meg a Google Authenticator kódot!</p>
            <form on:submit|preventDefault={handleTotpLogin}>
                <input 
                    type="text" 
                    placeholder="6 jegyű kód" 
                    bind:value={totpToken}
                    maxlength="6"
                    autocomplete="one-time-code"
                />
                {#if error}
                    <p id="failed">{error}</p>
                {/if}
                <button type="submit">Megerősítés</button>
            </form>
            <button class="secondary-btn" on:click={() => { requiresTotp = false; error = ''; }}>
                Vissza
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
        gap: 1rem;
        border: 1px solid var(--border-subtle);
    }

    @media (max-width: 480px) {
        .auth-card {
            padding: 1.5rem;
        }
    }

    h1 {
        text-align: center;
        font-size: 1.8rem;
        margin-bottom: 0.5rem;
    }

    form {
        display: flex;
        flex-direction: column;
        gap: 0.75rem;
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
        background: var(--bg-hover);
        border: 1px solid var(--border-hover);
        color: var(--text-primary);
        padding: 0.75rem;
        border-radius: 6px;
        cursor: pointer;
        font-size: 1rem;
        width: 100%;
        margin-top: 0.5rem;
    }

    button[type="submit"]:hover {
        background: var(--border-hover);
    }

    .divider {
        display: flex;
        align-items: center;
        gap: 1rem;
        color: var(--text-muted);
        font-size: 0.85rem;
    }

    .divider::before,
    .divider::after {
        content: '';
        flex: 1;
        border-top: 1px solid var(--border-subtle);
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

    .forgot-password {
        display: flex;
        justify-content: flex-end;
    }

    .link-btn {
        background: transparent;
        border: none;
        color: var(--text-muted);
        cursor: pointer;
        font-size: 0.8rem;
        padding: 0;
        text-decoration: underline;
    }

    .link-btn:hover {
        color: var(--text-secondary);
    }

    #failed { color: var(--accent-red); white-space: pre-line; word-break: break-word; }
</style>