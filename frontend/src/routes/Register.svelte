<script lang="ts">
    import { registerAsync } from '../lib/api/authApi';
    import { push } from 'svelte-spa-router';
    import { validateDisplayName, validatePassword, validateEmail } from '../lib/validators'

    import { setupTotpAsync, verifyTotpAsync } from '../lib/api/authApi';
    import { Copy, Check, ShieldCheck, Mail } from 'lucide-svelte';
    import { login } from '../lib/stores/authStore';
    import QRCode from 'qrcode';

    let showTotpPrompt = false;
    let totpStep: 'emailNotice' | 'prompt' | 'setup' | 'verify' | 'success' = 'emailNotice';
    let totpQrCode = '';
    let totpSetupUri = '';
    let totpToken = '';
    let totpError = '';
    let copied = false;


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
            // Store-ba mentjük a usert
            login(response.token, response.refreshToken, {
                userId: response.userId,
                email: response.email,
                displayName: response.displayName,
                isTotpEnabled: false,
                isEmailVerified: false
            });

            // TOTP prompt megjelenítése
            showTotpPrompt = true;
        } catch (e: any) {
            error = 'Hiba történt a regisztráció során!';
        }
    }

    async function handleSetupTotp() {
        try {
            const response = await setupTotpAsync();
            totpSetupUri = response.otpAuthUri;
            totpQrCode = await QRCode.toDataURL(response.otpAuthUri, { width: 200, margin: 1 });
            totpStep = 'setup';
        } catch (e) {
            totpError = 'Hiba történt a 2FA beállításakor!';
        }
    }

    async function handleVerifyTotp() {
        try {
            await verifyTotpAsync(totpToken);
            totpStep = 'success';
        } catch (e) {
            totpError = 'Érvénytelen TOTP token!';
        }
    }

    function skipTotp() {
        push('/app');
    }
</script>

<div class="auth-container">
    <div class="auth-card">
        {#if !showTotpPrompt}
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
        {/if}
        {#if showTotpPrompt}
            <h1>Sikeres regisztráció!</h1>

            {#if totpStep === 'emailNotice'}
            <div class="totp-prompt">
                <p class="totp-desc">
                    <Mail size={32} color="var(--accent-blue)" />
                </p>
                <p class="totp-desc">
                    Küldtünk egy megerősítő emailt a(z) <strong>{email}</strong> címre. 
                    Kérjük erősítsd meg az email címed!
                </p>
                <button type="button" class="primary-btn" on:click={() => totpStep = 'prompt'}>
                    Tovább
                </button>
            </div>

            {:else if totpStep === 'prompt'}
                <div class="totp-prompt">
                    <p class="totp-desc">Szeretnél extra biztonságot a fiókodhoz? Állíts be kétfaktoros hitelesítést most!</p>
                    <button type="button" class="primary-btn" on:click={handleSetupTotp}>
                        Igen, beállítom most!
                    </button>
                    <button type="button" class="secondary-btn" on:click={skipTotp}>
                        Nem, később
                    </button>
                </div>

            {:else if totpStep === 'setup'}
                <div class="totp-setup">
                    <p class="totp-desc">Scanneld be a QR kódot a Google Authenticator appban!</p>
                    {#if totpQrCode}
                        <img src={totpQrCode} alt="TOTP QR kód" class="qrImg" />
                    {/if}
                    <p class="hint">Vagy add meg manuálisan:</p>
                    <div class="copy-row">
                        <input type="text" readonly value={totpSetupUri} />
                        <button type="button" class="copy-btn" on:click={() => {
                            navigator.clipboard.writeText(totpSetupUri);
                            copied = true;
                            setTimeout(() => copied = false, 2000);
                        }}>
                            {#if copied}<Check size={14} />{:else}<Copy size={14} />{/if}
                        </button>
                    </div>
                    <button type="button" class="primary-btn" on:click={() => totpStep = 'verify'}>
                        Tovább a megerősítéshez
                    </button>
                    <button type="button" class="secondary-btn" on:click={skipTotp}>
                        Kihagyom
                    </button>
                </div>

            {:else if totpStep === 'verify'}
                <div class="totp-verify">
                    <p class="totp-desc">Add meg a Google Authenticator által generált 6 jegyű kódot!</p>
                    <form on:submit|preventDefault={handleVerifyTotp}>
                        <div class="input-group">
                            <input 
                                type="text" 
                                placeholder="6 jegyű kód"
                                bind:value={totpToken}
                                maxlength="6"
                                autocomplete="one-time-code"
                            />
                            {#if totpError}
                                <p class="field-error">{totpError}</p>
                            {/if}
                        </div>
                        <button type="submit" class="primary-btn">Aktiválás</button>
                    </form>
                    <button type="button" class="secondary-btn" on:click={() => totpStep = 'setup'}>
                        Vissza
                    </button>
                    <button type="button" class="secondary-btn" on:click={skipTotp}>
                        Kihagyom
                    </button>
                </div>
            {:else if totpStep === 'success'}
                <div class="totp-verify">
                    <div class="totp-status enabled" style="text-align: center;">
                        <ShieldCheck size={16} />
                        <span>2FA sikeresen aktiválva!</span>
                    </div>
                    <p class="totp-desc">Fiókod most már kétfaktoros hitelesítéssel védett!</p>
                    <button type="button" class="primary-btn" on:click={() => push('/app')}>
                        Tovább az appra!
                    </button>
                </div>
            {/if}
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

    .qrImg {
        width: 100%; 
        max-width: 500px; 
        height: auto;
        margin: auto;
    }

    .primary-btn {
        background: var(--accent-blue);
        border: none;
        color: white;
        padding: 0.75rem;
        border-radius: 6px;
        cursor: pointer;
        font-size: 1rem;
        width: 100%;
        margin-top: 0.5rem;
    }

    .primary-btn:hover {
        opacity: 0.9;
    }

    .totp-prompt,
    .totp-setup,
    .totp-verify {
        display: flex;
        flex-direction: column;
        gap: 0.75rem;
    }

    .totp-desc {
        color: var(--text-secondary);
        text-align: center;
        font-size: 0.95rem;
        line-height: 1.5;
    }

    .hint {
        font-size: 0.8rem;
        color: var(--text-muted);
    }

    .copy-row {
        display: flex;
        align-items: center;
        gap: 0.5rem;
    }

    .copy-row input {
        flex: 1;
        font-size: 0.75rem;
        padding: 0.5rem;
    }

    .copy-btn {
        background: var(--bg-hover);
        border: 1px solid var(--border-hover);
        color: var(--text-primary);
        border-radius: 6px;
        padding: 0.5rem;
        cursor: pointer;
        display: flex;
        align-items: center;
        width: auto;
    }

    .copy-btn:hover {
        background: var(--border-hover);
    }

    #failed { color: var(--accent-red); white-space: pre-line; }
    #success { color: var(--accent-green); white-space: pre-line; }
</style>