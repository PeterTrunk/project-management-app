<script lang="ts">
    import { loginAsync } from '../lib/api/authApi';
    import { login } from '../lib/stores/authStore';
    import { push } from 'svelte-spa-router';

    let email = '';
    let password = '';
    let error = '';

    async function handleLogin() {
        try {
            const response = await loginAsync({ email, password });
            login(response.token, response.refreshToken, {
                userId: response.userId,
                email: response.email,
                displayName: response.displayName
            });
            push('/app');
        } catch (e) {
            error = "Hibás email vagy jelszó!";
        }

        // Pending invite token kezelése
        const pendingToken = localStorage.getItem('pendingInviteToken');
        if (pendingToken) {
            localStorage.removeItem('pendingInviteToken');
            push(`/invite/${pendingToken}`);
        } else {
            push('/app');
        }
    }

    async function goToRegister() {
        push('/register');
    }

</script>

<div class="auth-container">
    <div class="auth-card">
        <h1>Bejelentkezés</h1>
        <form on:submit|preventDefault={handleLogin}>
            <input type="email" placeholder="Email" bind:value={email}/>
            <input type="password" placeholder="Jelszó" bind:value={password}/>
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
    </div>
</div>

<style>
    .auth-container {
        display: flex;
        justify-content: center;
        align-items: center;
        min-height: 100vh;
        width: 100vw;
        background: #121212;
    }

    .auth-card {
        background: #1e1e1e;
        border-radius: 12px;
        padding: 2.5rem;
        width: 400px;
        display: flex;
        flex-direction: column;
        gap: 1rem;
        border: 1px solid #333;
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
        background: #2a2a2a;
        border: 1px solid #444;
        border-radius: 6px;
        color: white;
        padding: 0.75rem;
        font-size: 1rem;
        width: 100%;
    }

    input:focus {
        outline: none;
        border-color: #666;
    }

    button[type="submit"] {
        background: #3a3a3a;
        border: 1px solid #555;
        color: white;
        padding: 0.75rem;
        border-radius: 6px;
        cursor: pointer;
        font-size: 1rem;
        width: 100%;
        margin-top: 0.5rem;
    }

    button[type="submit"]:hover {
        background: #444;
    }

    .divider {
        display: flex;
        align-items: center;
        gap: 1rem;
        color: #555;
        font-size: 0.85rem;
    }

    .divider::before,
    .divider::after {
        content: '';
        flex: 1;
        border-top: 1px solid #333;
    }

    .secondary-btn {
        background: transparent;
        border: 1px solid #444;
        color: #aaa;
        padding: 0.75rem;
        border-radius: 6px;
        cursor: pointer;
        font-size: 0.9rem;
        width: 100%;
        text-align: center;
    }

    .secondary-btn:hover {
        border-color: #666;
        color: white;
    }

    #failed { color: red; white-space: pre-line; }
</style>