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
    let emailError = '';
    let displayNameError = '';
    let passwordError = '';
    let passwordConfirmError = '';

    
    async function handleRegister() {
        error = '';
        emailError = '';
        displayNameError = '';
        passwordError = '';
        passwordConfirmError = '';
        
        let errorOccured = false;

        const emailErr = validateEmail(email);
        const displayNameErr = validateDisplayName(displayName);
        const passwordErr = validatePassword(password);

        if (emailErr) { emailError = emailErr; errorOccured = true; }
        if (displayNameErr) { displayNameError = displayNameErr; errorOccured = true; }
        if (passwordErr) { passwordError = passwordErr; errorOccured = true; }
        if (password !== passwordconfirm) { 
            passwordConfirmError = 'A két jelszó nem egyezik!'; 
            errorOccured = true; 
        }
        
        if (errorOccured) return;

        try {
            const response = await registerAsync({ email, displayName, password });
            success = 'Sikeres regisztráció! Átirányítás...';
            setTimeout(() => push('/'), 2000);
        } catch (e: any) {
            error = 'Hiba történt a regisztráció során!';
        }
    }
</script>

<div class="auth-container">
    <div class="auth-card">
        <h1>Regisztráció</h1>
        <form on:submit|preventDefault={handleRegister}>
            <div class="input-group">
                <input type="email" placeholder="Email" bind:value={email}/>
                {#if emailError}
                    <p class="field-error">{emailError}</p>
                {/if}
            </div>
            <div class="input-group">
                <input type="text" placeholder="Felhasználónév" bind:value={displayName}/>
                {#if displayNameError}
                    <p class="field-error">{displayNameError}</p>
                {/if}
            </div>
            <div class="input-group">
                <input type="password" placeholder="Jelszó" bind:value={password}/>
                {#if passwordError}
                    <p class="field-error">{passwordError}</p>
                {/if}
            </div>
            <div class="input-group">
                <input type="password" placeholder="Jelszó megerősítése" bind:value={passwordconfirm}/>
                {#if passwordConfirmError}
                    <p class="field-error">{passwordConfirmError}</p>
                {/if}
            </div>
            {#if success}
                <p id="success">{success}</p>
            {/if}
            {#if error}
                <p id="failed">{error}</p>
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

    .input-group {
        display: flex;
        flex-direction: column;
        gap: 0.25rem;
    }

    .field-error {
        color: var(--accent-red);
        font-size: 0.8rem;
        margin: 0;
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