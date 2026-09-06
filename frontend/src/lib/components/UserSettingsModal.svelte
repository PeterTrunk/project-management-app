<script lang="ts">
    import { createEventDispatcher } from 'svelte';
    import { authStore, login } from '../stores/authStore';
    import { changePasswordAsync, updateProfileAsync, resendVerificationAsync, meAsync } from '../api/authApi';
    import { setupTotpAsync, verifyTotpAsync, disableTotpAsync } from '../api/authApi';
    import { tokenStore } from '../stores/tokenStore';

    import { themeStore, toggleTheme } from '../stores/themeStore';
    import { X, User, KeyRound, Pencil, Sun, Moon, ShieldCheck, Copy, Check, ShieldAlert } from 'lucide-svelte';
    import QRCode from 'qrcode';

    import { notify } from '../stores/notificationStore';

    let currentTheme = 'dark';
    themeStore.subscribe(t => currentTheme = t);

    export let isUserSettingsOpen = false;

    //A szerver a jelszó- és 2FA-műveletek után minden munkamenetet érvénytelenít,
    //ezért a szülő komponensnek rendes kijelentkezést kell futtatnia
    const dispatch = createEventDispatcher<{ sessionInvalidated: { reason: string } }>();

    let disableTotpPassword = '';

    let resendSent = false;

    let error = '';
    let success = '';

    let displayName = '';
    let newDisplayName = '';

    let currentPassword = '';
    let newPassword ='';
    let newPasswordConfirm = '';

    let email = '';

    let activeView = 'profile'; // 'profile' | 'password' | 'changeprofile' | 'totp'

    let isTotpEnabled = false;
    let isEmailVerified = false;
    let totpSetupUri = '';
    let totpQrCode = '';
    let totpToken = '';
    let totpStep: 'idle' | 'setup' | 'verify' | 'disable' = 'idle';
    let copied = false;

    $: if (isUserSettingsOpen) {
        refreshUserProfile();
    }

    authStore.subscribe(state => {
        displayName = state.user?.displayName ?? '';
        email = state.user?.email ?? '';
        isTotpEnabled = state.user?.isTotpEnabled ?? false;
        isEmailVerified = state.user?.isEmailVerified ?? false;
    });

    function switchView(view: string) {
        activeView = view;
        error = '';
        success = '';
    }

    async function handlePasswordChange() {
        error = '';
        success = '';
        if(newPassword != newPasswordConfirm){
            error = 'Új jelszó megerősítés sikertelen, jelszavak nem egyeznek!'
            return;
        }
        try {
            await changePasswordAsync({ currentPassword, newPassword });
            currentPassword = '';
            newPassword = '';
            newPasswordConfirm = '';
            success = 'Sikeres változtatás!';
            dispatch('sessionInvalidated', {
                reason: 'A jelszó megváltozott, ezért minden munkamenet lezárult. Jelentkezz be újra!'
            });
        } catch (e: any) {
            const message = e.response?.data ?? e.message ?? 'Hiba történt a jelszóváltoztatás közben!';
            error = message;
            notify.error(message);
        }
    }

    async function handleProfileChange() {
        error = '';
        success = '';
        try {
            var response = await updateProfileAsync({ displayName: newDisplayName });
            login(tokenStore.get() ?? '', {
                userId: response.userId,
                email: response.email,
                displayName: response.displayName,
                isTotpEnabled: isTotpEnabled,
                isEmailVerified: $authStore.user?.isEmailVerified ?? false
            });
            success = 'Profil frissítve!';
            notify.success('Profil módosítva!');
        } catch (e: any) {
            const message = e.response?.data ?? e.message ?? 'Hiba történt a profil módosítása során!';
            error = message;
            notify.error(message);
        }
    }

    async function handleSetupTotp() {
        error = '';
        try {
            const response = await setupTotpAsync();
            totpSetupUri = response.otpAuthUri;
            totpQrCode = await QRCode.toDataURL(response.otpAuthUri);
            totpStep = 'setup';
        } catch (e: any) {
            const message = e.response?.data ?? e.message ?? 'Hiba történt a 2FA beállításakor!';
            error = message;
            notify.error(message);
        }
    }

    async function handleVerifyTotp() {
        error = '';
        try {
            await verifyTotpAsync(totpToken);
            isTotpEnabled = true;
            totpStep = 'idle';
            totpToken = '';
            success = '2FA sikeresen aktiválva!';
            dispatch('sessionInvalidated', {
                reason: '2FA aktiválva. Jelentkezz be újra - most már az authenticator kódjára is szükség lesz!'
            });
        } catch (e: any) {
            const message = e.response?.data ?? e.message ?? 'Érvénytelen TOTP token!';
            error = message;
            notify.error(message);
        }
    }

    function startDisableTotp() {
        error = '';
        success = '';
        disableTotpPassword = '';
        totpStep = 'disable';
    }

    function cancelDisableTotp() {
        error = '';
        disableTotpPassword = '';
        totpStep = 'idle';
    }

    async function handleDisableTotp() {
        error = '';
        try {
            await disableTotpAsync(disableTotpPassword);
            disableTotpPassword = '';
            isTotpEnabled = false;
            totpStep = 'idle';
            success = '2FA kikapcsolva!';
            dispatch('sessionInvalidated', {
                reason: 'A 2FA kikapcsolva, ezért minden munkamenet lezárult. Jelentkezz be újra!'
            });
        } catch (e: any) {
            const message = e.response?.data ?? e.message ?? 'Hiba történt a 2FA kikapcsolásakor!';
            error = message;
            notify.error(message);
        }
    }

    async function handleResendVerification() {
        try {
            await resendVerificationAsync(email);
            resendSent = true;
        } catch (e: any) {
            const message = e.response?.data ?? e.message ?? 'Hiba az email újraküldésekor!';
            error = message;
            notify.error(message);
        }
    }

    async function refreshUserProfile() {
        try {
            const user = await meAsync();
            const currentToken = tokenStore.get() ?? '';
            login(currentToken, {
                userId: user.userId,
                email: user.email,
                displayName: user.displayName,
                isTotpEnabled: user.isTotpEnabled ?? false,
                isEmailVerified: user.isEmailVerified ?? false
            });
        } catch (e: any) {
            notify.error(e.response?.data ?? e.message ?? 'Hiba a profil frissítésekor!');
        }
    }

</script>

<div class="modal-overlay">
    <div class="modal-content stack-480">
        <button class="close-btn" on:click={() => isUserSettingsOpen = false}>
            <X size={16} />
        </button>
        <aside class="sidebar">
            <div class="sidebar-options">
                <h2>{displayName}</h2>
                <button class:active={activeView === 'profile'} on:click={() => switchView('profile')}>
                    <User size={15} /> Profil
                </button>
                <button class:active={activeView === 'changeprofile'} on:click={() => switchView('changeprofile')}>
                    <Pencil size={15} /> Profil szerkesztése
                </button>
                <button class:active={activeView === 'password'} on:click={() => switchView('password')}>
                    <KeyRound size={15} /> Jelszó változtatás
                </button>
                <button class:active={activeView === 'totp'} on:click={() => switchView('totp')}>
                    <ShieldCheck size={15} /> Biztonság
                </button>
                <button class="icon-btn" on:click={toggleTheme} title="Téma váltás">
                    {#if currentTheme === 'dark'}
                        <Sun size={18} />
                        <span>Light mód</span>
                    {:else}
                        <Moon size={18} />
                        <span>Dark mód</span>
                    {/if}
                </button>
            </div>
        </aside>
        <div class="main">
            <div class="content">
                {#if activeView === 'profile'}
                    <!--placeholder img tag-->
                    <h1>{displayName} profilja</h1>
                    <img src="" alt="">
                    <p>{displayName}</p>
                    <p>{email}</p>
                {:else if activeView === 'changeprofile'}
                    <h1>Profil módosítása</h1>
                    <form on:submit|preventDefault={handleProfileChange}>
                        <input type="text" bind:value={newDisplayName} placeholder="Új felhasználónév">
                        {#if error}
                            <p id="failed">{error}</p>
                        {/if}
                        {#if success}
                            <p id="success">{success}</p>
                        {/if}
                        <button>Mentés</button>
                    </form>
                {:else if activeView === 'password'}
                    <h1>Jelszó változtatás</h1>
                    <form on:submit|preventDefault={handlePasswordChange}>
                        Jelenlegi jelszó <input type="password" placeholder="Jelenlegi jelszó" bind:value={currentPassword}>
                        Új jelszó <input type="password" placeholder="Új jelszó" bind:value={newPassword}>
                        Új jelszó megint újra <input type="password" placeholder="Új jelszó" bind:value={newPasswordConfirm}>
                        {#if error}
                            <p id="failed">{error}</p>
                        {/if}
                        {#if success}
                            <p id="success">{success}</p>
                        {/if}
                        <button>Mentés</button>
                    </form>
                {:else if activeView === 'totp'}
                    <h1>Biztonság</h1>
                    <div class="security-section">
                        <!-- Email verification státusz -->
                        <h3>Email megerősítés</h3>
                        {#if $authStore.user?.isEmailVerified}
                            <p class="totp-status enabled">
                                <ShieldCheck size={15} /> Email megerősítve
                            </p>
                        {:else}
                            <p class="totp-status disabled">
                                <ShieldAlert size={15} /> Email nincs megerősítve
                            </p>
                            <button class="secondary-btn" on:click={handleResendVerification}>
                                Megerősítő email újraküldése
                            </button>
                        {/if}

                        <div class="section-divider"></div>

                        <!-- TOTP 2FA szekció -->
                        <h3>Kétfaktoros hitelesítés</h3>
                        {#if isTotpEnabled && totpStep === 'disable'}
                            <p class="totp-status enabled"><ShieldCheck size={15} /> 2FA aktív</p>
                            <p class="hint">
                                A kikapcsoláshoz add meg a jelenlegi jelszavad. A művelet után
                                minden munkameneted lezárul, és újra be kell jelentkezned.
                            </p>
                            <form on:submit|preventDefault={handleDisableTotp}>
                                <input
                                    type="password"
                                    placeholder="Jelenlegi jelszó"
                                    bind:value={disableTotpPassword}
                                    autocomplete="current-password"
                                />
                                <button type="submit" class="danger-btn" disabled={!disableTotpPassword}>
                                    2FA kikapcsolása
                                </button>
                            </form>
                            <button class="secondary-btn" on:click={cancelDisableTotp}>
                                Mégse
                            </button>
                        {:else if isTotpEnabled}
                            <p class="totp-status enabled"><ShieldCheck size={15} /> 2FA aktív</p>
                            <button class="danger-btn" on:click={startDisableTotp}>
                                2FA kikapcsolása
                            </button>
                        {:else if totpStep === 'idle'}
                            <p class="totp-status disabled">2FA nincs bekapcsolva</p>
                            <button on:click={handleSetupTotp}>
                                2FA beállítása
                            </button>
                        {:else if totpStep === 'setup'}
                            <p>Scanneld be a QR kódot a Google Authenticatorral!</p>
                            {#if totpQrCode}
                                <img src={totpQrCode} alt="TOTP QR kód" class="qrImg" />
                            {/if}
                            <p class="hint">Manuális kód:</p>
                            <div class="copy-row">
                                <input type="text" readonly value={totpSetupUri} />
                                <button type="button" on:click={() => {
                                    navigator.clipboard.writeText(totpSetupUri);
                                    copied = true;
                                    setTimeout(() => copied = false, 2000);
                                }}>
                                    {#if copied}
                                        <Check size={14} />
                                    {:else}
                                        <Copy size={14} />
                                    {/if}
                                </button>
                            </div>
                            <button on:click={() => totpStep = 'verify'}>
                                Tovább a megerősítéshez
                            </button>
                        {:else if totpStep === 'verify'}
                            <p>Add meg a Google Authenticator által generált 6 jegyű kódot!</p>
                            <form on:submit|preventDefault={handleVerifyTotp}>
                                <input 
                                    type="text" 
                                    placeholder="6 jegyű kód"
                                    bind:value={totpToken}
                                    maxlength="6"
                                    autocomplete="one-time-code"
                                />
                                <button type="submit">Megerősítés</button>
                            </form>
                            <button class="secondary-btn" on:click={() => totpStep = 'setup'}> 
                                Vissza
                            </button>
                        {/if}

                        {#if error}
                            <p id="failed">{error}</p>
                        {/if}
                        {#if success}
                            <p id="success">{success}</p>
                        {/if}
                    </div>
                {/if}
            </div>
        </div>
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
        position: relative;
        background: var(--bg-card);
        border: 1px solid var(--border);
        border-radius: var(--border-radius-lg);
        width: 1200px;
        max-width: 95vw;
        height: 800px;
        max-height: 90vh;
        display: flex;
        overflow: hidden;
    }

    .close-btn {
        position: absolute;
        top: 0.75rem;
        right: 0.75rem;
        background: transparent;
        border: none;
        color: var(--text-secondary);
        display: flex;
        align-items: center;
        justify-content: center;
        cursor: pointer;
        z-index: 10;
        padding: 0.25rem;
        border-radius: 4px;
    }

    .close-btn:hover {
        color: var(--text-primary);
        background: var(--bg-hover);
    }

    .sidebar {
        width: 220px;
        min-width: 200px;
        background: var(--bg-primary);
        padding: 1.5rem 1rem;
        display: flex;
        flex-direction: column;
        border-right: 1px solid var(--border);
        overflow: hidden;
    }

    @media (max-width: 480px) {
        .sidebar {
            width: 100%;
            min-width: 0;
            border-right: none;
            border-bottom: 1px solid var(--border);
            padding: var(--card-padding);
            max-height: 40vh;
        }
    }

    .sidebar-options {
        display: flex;
        flex-direction: column;
        gap: 0.25rem;
        overflow-y: auto;
        min-height: 0;
        padding: 0.3rem;
    }

    .sidebar-options h2 {
        font-size: 1rem;
        color: var(--text-secondary);
        margin-bottom: 0.5rem;
        padding-bottom: 0.5rem;
        border-bottom: 1px solid var(--border);
    }

    .sidebar-options button {
        display: flex;
        align-items: center;
        gap: 0.5rem;
        background: transparent;
        border: none;
        color: var(--text-secondary);
        text-align: left;
        padding: 0.5rem 0.75rem;
        border-radius: 6px;
        cursor: pointer;
        font-size: 0.9rem;
        transition: background 0.15s, color 0.15s;
    }

    .sidebar-options button:hover {
        background: var(--bg-hover);
        color: var(--text-primary);
    }

    .sidebar-options button.active {
        background: var(--accent-blue-bg);
        color: var(--accent-blue);
    }

    .icon-btn {
        margin-top: 0.5rem;
        border-top: 1px solid var(--border) !important;
        padding-top: 0.75rem !important;
        color: var(--text-secondary) !important;
    }

    .icon-btn:hover {
        color: var(--text-primary) !important;
    }

    .main {
        flex: 1;
        display: flex;
        flex-direction: column;
        min-height: 0;
    }

    .content {
        flex: 1;
        display: flex;
        flex-direction: column;
        gap: 1rem;
        overflow-y: auto;
        padding: 2rem;
        min-height: 0;
    }

    @media (max-width: 480px) {
        .content {
            padding: var(--card-padding);
        }
    }

    .content h1 {
        font-size: 1.4rem;
        margin-bottom: 0.5rem;
    }

    input[type="text"],
    input[type="password"] {
        background: var(--bg-input);
        border: 1px solid var(--border-hover);
        border-radius: 6px;
        color: var(--text-primary);
        padding: 0.5rem;
        font-size: 1rem;
        width: 100%;
    }

    input:focus {
        outline: none;
        border-color: var(--accent-blue);
    }

    form {
        display: flex;
        flex-direction: column;
        gap: 0.75rem;
    }

    form button {
        width: fit-content;
        padding: 0.5rem 1.5rem;
        border-radius: 6px;
        cursor: pointer;
        margin-top: 0.5rem;
    }

    .qrImg {
        width: 100%; 
        max-width: 480px;
        height: auto;
        margin: auto;
    }

    .copy-row {
        display: flex;
        align-items: center;
        gap: 0.5rem;
    }

    .copy-row input {
        flex: 1;
        font-size: 0.8rem;
        min-width: 0;
    }

    .totp-status {
        display: flex;
        align-items: center;
        gap: 0.5rem;
        font-weight: bold;
        padding: 0.5rem 0.75rem;
        border-radius: 6px;
        width: fit-content;
    }

    .totp-status.enabled {
        color: var(--accent-green);
        background: var(--accent-green-bg);
    }

    .totp-status.disabled {
        color: var(--text-muted);
        background: var(--bg-hover);
    }

    .security-section {
        display: flex;
        flex-direction: column;
        gap: 0.5rem;
    }

    .section-divider {
        border-top: 1px solid var(--border-subtle);
        margin: 0.5rem 0;
    }

    h3 {
        font-size: 0.95rem;
        color: var(--text-muted);
        margin: 0;
    }
    
    #success { color: var(--accent-green); }
    #failed  { color: var(--accent-red); white-space: pre-line; }
</style>