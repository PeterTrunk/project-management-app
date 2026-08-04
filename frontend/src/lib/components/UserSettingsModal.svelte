<script lang="ts">
    import { authStore, login } from '../stores/authStore';
    import { changePasswordAsync, updateProfileAsync } from '../api/authApi';
    import { validateDisplayName, validatePassword } from '../validators';
    import { setupTotpAsync, verifyTotpAsync, disableTotpAsync } from '../api/authApi';
    import ConfirmModal from './ConfirmModal.svelte';

    import { themeStore, toggleTheme } from '../stores/themeStore';
    import { X, User, KeyRound, Pencil, Sun, Moon, ShieldCheck, Copy, Check } from 'lucide-svelte';
    import QRCode from 'qrcode';

    let currentTheme = 'dark';
    themeStore.subscribe(t => currentTheme = t);

    export let isUserSettingsOpen = false;
    let isDisableTotpConfirmOpen = false;

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
    let totpSetupUri = '';
    let totpQrCode = '';
    let totpToken = '';
    let totpStep: 'idle' | 'setup' | 'verify' = 'idle';
    let copied = false;

    authStore.subscribe(state => {
        displayName = state.user?.displayName ?? '';
        email = state.user?.email ?? '';
        isTotpEnabled = state.user?.isTotpEnabled ?? false;
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
        const newPasswordError = validatePassword(newPassword);
        if(newPasswordError!=null){
            error = newPasswordError;
            return;            
        }
        try {
            var response = await changePasswordAsync({ currentPassword, newPassword });
            success = 'Sikeres változtatás!';
        } catch (e) {
            error = 'Hiba történt a jelszóváltoztatás közben!';
        }
    }

    async function handleProfileChange() {
        error = '';
        success = '';
        const displayNameError = validateDisplayName(newDisplayName);
        if(displayNameError!=null){
            error = displayNameError;
            return;
        }
        try {
            var response = await updateProfileAsync({ displayName: newDisplayName });
            const token = localStorage.getItem('token') ?? '';
            const refreshToken = localStorage.getItem('refreshToken') ?? '';
            login(token, refreshToken, {
                userId: response.userId,
                email: response.email,
                displayName: response.displayName,
                isTotpEnabled: isTotpEnabled
            });
            success = 'Profil frissítve!';
        } catch (e) {
            
        }
    }

    async function handleSetupTotp() {
        error = '';
        try {
            const response = await setupTotpAsync();
            totpSetupUri = response.otpAuthUri;
            totpQrCode = await QRCode.toDataURL(response.otpAuthUri);
            totpStep = 'setup';
        } catch (e) {
            error = 'Hiba történt a 2FA beállításakor!';
        }
    }

    async function handleVerifyTotp() {
        error = '';
        try {
            await verifyTotpAsync(totpToken);
            isTotpEnabled = true;
            const token = localStorage.getItem('token') ?? '';
            const refreshToken = localStorage.getItem('refreshToken') ?? '';
            login(token, refreshToken, {
                userId: $authStore.user?.userId ?? '',
                email: $authStore.user?.email ?? '',
                displayName: $authStore.user?.displayName ?? '',
                isTotpEnabled: true
            });
            totpStep = 'idle';
            totpToken = '';
            success = '2FA sikeresen aktiválva!';
        } catch (e) {
            error = 'Érvénytelen TOTP token!';
        }
    }

    async function handleDisableTotp() {
        error = '';
        try {
            await disableTotpAsync();
            isTotpEnabled = false;
            const token = localStorage.getItem('token') ?? '';
            const refreshToken = localStorage.getItem('refreshToken') ?? '';
            login(token, refreshToken, {
                userId: $authStore.user?.userId ?? '',
                email: $authStore.user?.email ?? '',
                displayName: $authStore.user?.displayName ?? '',
                isTotpEnabled: false
            });
            success = '2FA kikapcsolva!';
        } catch (e) {
            error = 'Hiba történt a 2FA kikapcsolásakor!';
        }
    }


</script>

<div class="modal-overlay">
    <div class="modal-content">
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
                    <ShieldCheck size={15} /> Kétfaktoros hitelesítés
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
                    <h1>Kétfaktoros hitelesítés</h1>
                    
                    {#if isTotpEnabled}
                        <p class="totp-status enabled"><ShieldCheck size={15} /> 2FA aktív</p>
                        <button class="danger-btn" on:click={() => isDisableTotpConfirmOpen = true}>
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
                {/if}
            </div>
        </div>
    </div>
</div>

{#if isDisableTotpConfirmOpen}
    <ConfirmModal
        bind:isOpen={isDisableTotpConfirmOpen}
        title="2FA kikapcsolása"
        message="Biztosan kikapcsolod a kétfaktoros hitelesítést? Fiókod kevésbé lesz biztonságos!"
        confirmText="Kikapcsolás"
        onConfirm={handleDisableTotp}
    />
{/if}

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
        border-radius: 8px;
        width: 1200px;
        height: 800px;
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
    }

    .sidebar-options {
        display: flex;
        flex-direction: column;
        gap: 0.25rem;
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
    }

    .content {
        flex: 1;
        padding: 2rem;
        display: flex;
        flex-direction: column;
        gap: 1rem;
        overflow-y: auto;
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
        max-width: 500px; 
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
    
    #success { color: var(--accent-green); }
    #failed  { color: var(--accent-red); white-space: pre-line; }
</style>