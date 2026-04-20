<script lang="ts">
    import { onMount } from 'svelte';
    import { push } from 'svelte-spa-router';
    import { joinProjectAsync } from '../lib/api/teamApi';
    import { authStore } from '../lib/stores/authStore';
    import { getProjectsAsync } from '../lib/api/projectApi';
    import { setProjects, setActiveProject } from '../lib/stores/projectStore';

    export let params: { token: string };

    let loading = true;
    let error = '';
    let success = false;
    let isAuthenticated = false;

    authStore.subscribe(state => {
        isAuthenticated = state.isAuthenticated;
    });

    onMount(async () => {
        if (!isAuthenticated) {
            // Token megőrzése localStorage-ban redirect előtt
            localStorage.setItem('pendingInviteToken', params.token);
            push('/');
            return;
        }

        await handleJoin();
    });

    async function handleJoin() {
        loading = true;
        error = '';
        try {
            await joinProjectAsync(params.token);
            
            // Projektek újratöltése
            const projects = await getProjectsAsync();
            setProjects(projects);
            
            success = true;
            
            // 2 másodperc után redirect az appba
            setTimeout(() => push('/app'), 2000);
        } catch (e: any) {
            error = e.response?.data ?? 'Hiba történt a csatlakozáskor!';
        } finally {
            loading = false;
        }
    }
</script>

<div class="invite-container">
    <div class="invite-card">
        <h1>Projekt Meghívó</h1>

        {#if loading}
            <div class="status">
                <div class="spinner"></div>
                <p>Csatlakozás folyamatban...</p>
            </div>
        {:else if success}
            <div class="status success">
                <span class="icon">✓</span>
                <p>Sikeresen csatlakoztál a projekthez!</p>
                <p class="hint">Átirányítás folyamatban...</p>
            </div>
        {:else if error}
            <div class="status error">
                <span class="icon">✕</span>
                <p>{error}</p>
                <button on:click={() => push('/')}>Vissza a főoldalra</button>
            </div>
        {/if}
    </div>
</div>

<style>
    .invite-container {
        display: flex;
        justify-content: center;
        align-items: center;
        min-height: 100vh;
        background: #121212;
    }

    .invite-card {
        background: #1e1e1e;
        border-radius: 12px;
        padding: 2.5rem;
        width: 400px;
        max-width: 95vw;
        display: flex;
        flex-direction: column;
        gap: 1.5rem;
        border: 1px solid #333;
        text-align: center;
    }

    h1 {
        font-size: 1.5rem;
        margin: 0;
    }

    .status {
        display: flex;
        flex-direction: column;
        align-items: center;
        gap: 0.75rem;
        padding: 1rem;
    }

    .spinner {
        width: 40px;
        height: 40px;
        border: 3px solid #333;
        border-top-color: #4caf50;
        border-radius: 50%;
        animation: spin 0.8s linear infinite;
    }

    @keyframes spin {
        to { transform: rotate(360deg); }
    }

    .icon {
        font-size: 2.5rem;
    }

    .status.success .icon { color: #4caf50; }
    .status.error .icon { color: #ff5555; }

    .hint {
        color: #666;
        font-size: 0.85rem;
    }

    button {
        background: #2a2a2a;
        border: 1px solid #444;
        color: white;
        padding: 0.5rem 1rem;
        border-radius: 6px;
        cursor: pointer;
        margin-top: 0.5rem;
    }

    button:hover { background: #333; }
</style>