<script lang="ts">
    import { authStore, login } from '../stores/authStore';
    import { changePasswordAsync, updateProfileAsync } from '../api/authApi';
    import { validateDisplayName, validatePassword } from '../validators';

    import { X, User, KeyRound, Pencil } from 'lucide-svelte';

    export let isUserSettingsOpen = false;

    let error = '';
    let success = '';

    let displayName = '';
    let newDisplayName = '';

    let currentPassword = '';
    let newPassword ='';
    let newPasswordConfirm = '';

    let email = '';

    let activeView = 'profile'; // 'profile' | 'password' | 'changeprofile'

    authStore.subscribe(state => {
        displayName = state.user?.displayName ?? '';
        email = state.user?.email ?? '';
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
                displayName: response.displayName
            });
            success = 'Profil frissítve!';
        } catch (e) {
            
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
        background: rgba(0, 0, 0, 0.5);
        display: flex;
        justify-content: center;
        align-items: center;
        z-index: 1000;
    }

    .modal-content {
        position: relative;
        background: #1e1e1e;
        border-radius: 8px;
        width: 700px;
        height: 450px;
        display: flex;
        overflow: hidden;
    }

    .close-btn {
        position: absolute;
        top: 0.75rem;
        right: 0.75rem;
        background: transparent;
        border: none;
        color: #aaa;
        font-size: 1.2rem;
        cursor: pointer;
        z-index: 10;
    }

    .close-btn:hover {
        color: white;
    }

    .sidebar {
        width: 220px;
        min-width: 200px;
        background: #161616;
        padding: 1.5rem 1rem;
        display: flex;
        flex-direction: column;
        border-right: 1px solid #333;
    }

    .sidebar-options {
        display: flex;
        flex-direction: column;
        gap: 0.5rem;
    }

    .sidebar-options h2 {
        font-size: 1rem;
        color: #aaa;
        margin-bottom: 0.5rem;
        padding-bottom: 0.5rem;
        border-bottom: 1px solid #333;
    }

    .sidebar-options button {
        background: transparent;
        border: none;
        color: #ccc;
        text-align: left;
        padding: 0.5rem 0.75rem;
        border-radius: 6px;
        cursor: pointer;
        font-size: 0.95rem;
    }

    .sidebar-options button:hover {
        background: #2a2a2a;
        color: white;
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
        background: #2a2a2a;
        border: 1px solid #444;
        border-radius: 6px;
        color: white;
        padding: 0.5rem;
        font-size: 1rem;
        width: 100%;
    }

    input:focus {
        outline: none;
        border-color: #666;
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

    #success { color: greenyellow; }
    #failed { color: red; white-space: pre-line; }
</style>