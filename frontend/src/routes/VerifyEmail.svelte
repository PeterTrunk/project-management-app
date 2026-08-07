<script lang="ts">
    import { onMount } from 'svelte';
    import { push } from 'svelte-spa-router';
    import { CircleCheckBig, CircleX, Loader } from 'lucide-svelte';
    import apiClient from '../lib/api/client';

    let status: 'loading' | 'success' | 'error' = 'loading';
    let error = '';

    onMount(async () => {
        const token = new URLSearchParams(window.location.hash.split('?')[1]).get('token');
        if (!token) {
            status = 'error';
            error = 'Hiányzó token!';
            return;
        }

        try {
            await apiClient.get(`/auth/verify-email?token=${token}`);
            status = 'success';
            setTimeout(() => push('/'), 3000);
        } catch (e: any) {
            status = 'error';
            error = e.response?.data ?? 'Érvénytelen vagy lejárt token!';
        }
    });
</script>

<div class="auth-container">
    <div class="auth-card">
        {#if status === 'loading'}
            <Loader size={32} />
            <h1>Email megerősítés...</h1>
            <p>Kérjük várj...</p>
        {:else if status === 'success'}
            <CircleCheckBig size={32} color="var(--accent-green)" />
            <h1>Email megerősítve!</h1>
            <p>Az email címed sikeresen megerősítve! Átirányítás a bejelentkezési oldalra...</p>
        {:else if status === 'error'}
            <CircleX size={32} color="var(--accent-red)" />
            <h1>Hiba történt</h1>
            <p>{error}</p>
            <button on:click={() => push('/')}>Vissza a bejelentkezéshez</button>
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
        align-items: center;
        gap: 1rem;
        border: 1px solid var(--border-subtle);
        text-align: center;
    }

    h1 {
        font-size: 1.5rem;
        margin: 0;
    }

    p {
        color: var(--text-secondary);
        margin: 0;
    }

    button {
        background: var(--bg-hover);
        border: 1px solid var(--border-hover);
        color: var(--text-primary);
        padding: 0.75rem 1.5rem;
        border-radius: 6px;
        cursor: pointer;
        font-size: 1rem;
        margin-top: 0.5rem;
    }

    button:hover {
        background: var(--border-hover);
    }
</style>