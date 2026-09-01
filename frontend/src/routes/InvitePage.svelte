<script lang="ts">
    import { onMount } from 'svelte';
    import { push } from 'svelte-spa-router';
    import { joinProjectAsync } from '../lib/api/teamApi';
    import { authStore } from '../lib/stores/authStore';
    import { getProjectsAsync } from '../lib/api/projectApi';
    import { setProjects, setActiveProject } from '../lib/stores/projectStore';

    import { CircleCheckBig, CircleX, Loader } from 'lucide-svelte';

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
                <Loader size={40} color="var(--accent-green)" style="animation: spin 0.8s linear infinite;" />
                <p>Csatlakozás folyamatban...</p>
            </div>
        {:else if success}
            <div class="status success">
                <CircleCheckBig size={40} color="var(--accent-green)" />
                <p>Sikeresen csatlakoztál a projekthez!</p>
                <p class="hint">Átirányítás folyamatban...</p>
            </div>
        {:else if error}
            <div class="status error">
                <CircleX size={40} color="var(--accent-red)" />
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
        background: var(--bg-primary);
    }

    .invite-card {
        background: var(--bg-card);
        border-radius: 12px;
        padding: 2.5rem;
        width: 400px;
        max-width: 95vw;
        display: flex;
        flex-direction: column;
        gap: 1.5rem;
        border: 1px solid var(--border-subtle);
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

    .status p {
        white-space: pre-line;
        word-break: break-word;
    }
    
    @media (max-width: 480px) {
        .invite-card {
            padding: 1.5rem;
        }
    }

    @keyframes spin {
        to { transform: rotate(360deg); }
    }

    .hint {
        color: var(--text-muted);
        font-size: 0.85rem;
    }

    button {
        background: var(--bg-hover);
        border: 1px solid var(--border-hover);
        color: var(--text-primary);
        padding: 0.5rem 1rem;
        border-radius: 6px;
        cursor: pointer;
        margin-top: 0.5rem;
    }

    button:hover { background: var(--border-hover); }
</style>