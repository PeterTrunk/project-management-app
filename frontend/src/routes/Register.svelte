<script lang="ts">
    import { registerAsync } from '../lib/api/authApi';
    import { push } from 'svelte-spa-router';
    import { validateDisplayName, validatePassword, validateEmail } from '../lib/validators'

    let email = '';
    let password = '';
    let passwordconfirm = '';
    let displayName = '';
    
    let success = '';
    let error = '';
    
    async function handleRegister() {
        error = '';
        let errorOccured: boolean = false;
        const emailError = validateEmail(email);
        const displayNameError = validateDisplayName(displayName);
        const passwordError = validatePassword(password);
        if(emailError!=null){
            error = error + emailError;
            errorOccured = true;
        }
        if(displayNameError!=null){
            error = error + displayNameError;
            errorOccured = true;
        }
        if (passwordError!=null) {
            error = error + passwordError;
            errorOccured = true;
        }
        if (password !== passwordconfirm) {
            error = error + 'A két jelszó nem egyezik!\n';
            errorOccured = true;
        }
        if(errorOccured) {
            return;
        }
        try {
            const response = await registerAsync({ email, displayName, password });
            success = 'Sikeres regisztráció! Átirányítás...';
            setTimeout(() => push('/'), 2000);
        } catch (e: any) {
            console.error('Backend hiba:', e.response?.data);
            error = 'Hiba történt a regisztráció során!';
        }
    }
</script>

<div class="auth-container">
    <div class="auth-card">
        <h1>Regisztráció</h1>
        <form on:submit|preventDefault={handleRegister}>
            <input type="email" placeholder="Email" bind:value={email}/>
            <input type="text" placeholder="Felhasználónév" bind:value={displayName}/>
            <input type="password" placeholder="Jelszó" bind:value={password}/>
            <input type="password" placeholder="Jelszó megerősítése" bind:value={passwordconfirm}/>
            {#if error}
                <p id="failed">{error}</p>
            {/if}
            {#if success}
                <p id="success">{success}</p>
            {/if}
            <button type="submit">Regisztráció</button>
        </form>
        <div class="divider">
            <span>vagy</span>
        </div>
        <button class="secondary-btn" on:click={() => push('/')}>
            Már van fiókod? Jelentkezz be!
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
        background: var(--bg-primary);
    }

    .auth-card {
        background: var(--bg-card);
        border-radius: 12px;
        padding: 2.5rem;
        width: 400px;
        display: flex;
        flex-direction: column;
        gap: 1rem;
        border: 1px solid var(--border-subtle);
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

    #failed { color: var(--accent-red); white-space: pre-line; }
    #success { color: var(--accent-green); white-space: pre-line; }
</style>