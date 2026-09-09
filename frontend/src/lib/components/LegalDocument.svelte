<script lang="ts">
    import { push } from 'svelte-spa-router';
    import { ArrowLeft } from 'lucide-svelte';
    import { LEGAL_VERSION, LEGAL_EFFECTIVE_DATE } from '../legal';

    export let title: string;
    export let intro: string = '';

    //Az oldal közvetlen URL-lel is nyitható (a regisztrációs űrlap linkjei új fülre is mehetnek),
    //ilyenkor nincs hova visszalépni - ezért a bejelentkezésre esünk vissza.
    function goBack() {
        if (window.history.length > 1) {
            window.history.back();
        } else {
            push('/');
        }
    }
</script>

<div class="legal-page scroll-x">
    <div class="legal-card">
        <button class="back-btn" on:click={goBack}>
            <ArrowLeft size={16} />
            <span>Vissza</span>
        </button>

        <h1>{title}</h1>

        <p class="meta">
            Verzió: <strong>{LEGAL_VERSION}</strong> &middot;
            Hatályos: {LEGAL_EFFECTIVE_DATE}
        </p>

        {#if intro}
            <p class="intro">{intro}</p>
        {/if}

        <div class="legal-content">
            <slot />
        </div>

        <div class="legal-footer">
            <a href="#/privacy">Adatkezelési tájékoztató</a>
            <span class="sep">&middot;</span>
            <a href="#/terms">Felhasználási feltételek</a>
        </div>
    </div>
</div>

<style>
    .legal-page {
        display: flex;
        justify-content: center;
        min-height: 100vh;
        width: 100%;
        background: var(--bg-primary);
        padding: 2rem 1rem;
        box-sizing: border-box;
    }

    .legal-card {
        background: var(--bg-card);
        border: 1px solid var(--border-subtle);
        border-radius: 12px;
        padding: 2.5rem;
        width: 100%;
        max-width: 820px;
        height: fit-content;
        color: var(--text-secondary);
        font-size: 0.92rem;
        line-height: 1.65;
    }

    @media (max-width: 600px) {
        .legal-card { padding: 1.5rem; }
        .legal-page { padding: 1rem 0.5rem; }
    }

    .back-btn {
        display: inline-flex;
        align-items: center;
        gap: 0.4rem;
        background: transparent;
        border: 1px solid var(--border-hover);
        color: var(--text-secondary);
        padding: 0.4rem 0.75rem;
        border-radius: var(--border-radius);
        cursor: pointer;
        font-size: var(--font-size-sm);
        margin-bottom: 1.5rem;
    }

    .back-btn:hover {
        border-color: var(--text-muted);
        color: var(--text-primary);
    }

    h1 {
        font-size: 1.6rem;
        line-height: 1.3;
        color: var(--text-primary);
        margin: 0 0 0.5rem;
    }

    .meta {
        color: var(--text-muted);
        font-size: var(--font-size-sm);
        margin: 0 0 1.5rem;
    }

    .intro {
        margin: 0 0 1.5rem;
        padding-bottom: 1.5rem;
        border-bottom: 1px solid var(--border-subtle);
    }

    .legal-footer {
        margin-top: 2.5rem;
        padding-top: 1.25rem;
        border-top: 1px solid var(--border-subtle);
        font-size: var(--font-size-sm);
        color: var(--text-muted);
    }

    .legal-footer a {
        color: var(--accent-blue);
        text-decoration: none;
    }

    .legal-footer a:hover { text-decoration: underline; }

    .sep { margin: 0 0.4rem; }

    .legal-content :global(h2) {
        font-size: 1.1rem;
        color: var(--text-primary);
        margin: 2rem 0 0.75rem;
        padding-top: 0.5rem;
    }

    .legal-content :global(h2:first-child) {
        margin-top: 0;
        padding-top: 0;
    }

    .legal-content :global(h3) {
        font-size: 0.98rem;
        color: var(--text-primary);
        margin: 1.5rem 0 0.5rem;
    }

    .legal-content :global(p) { margin: 0 0 0.9rem; }

    .legal-content :global(ul),
    .legal-content :global(ol) {
        margin: 0 0 0.9rem;
        padding-left: 1.4rem;
    }

    .legal-content :global(li) { margin-bottom: 0.4rem; }

    .legal-content :global(strong) { color: var(--text-primary); }

    .legal-content :global(a) {
        color: var(--accent-blue);
        text-decoration: none;
    }

    .legal-content :global(a:hover) { text-decoration: underline; }

    .legal-content :global(table) {
        width: 100%;
        border-collapse: collapse;
        margin: 0 0 1.25rem;
        font-size: var(--font-size-sm);
        display: block;
        overflow-x: auto;
    }

    .legal-content :global(th),
    .legal-content :global(td) {
        border: 1px solid var(--border-subtle);
        padding: 0.5rem 0.65rem;
        text-align: left;
        vertical-align: top;
    }

    .legal-content :global(th) {
        background: var(--bg-hover);
        color: var(--text-primary);
        font-weight: 600;
        white-space: nowrap;
    }

    /* Kitöltendő helyőrző: szándékosan feltűnő, hogy éles indulás előtt ne maradjon bent. */
    .legal-content :global(.todo) {
        background: var(--accent-yellow-bg);
        color: var(--accent-yellow);
        padding: 0.1rem 0.35rem;
        border-radius: var(--badge-border-radius);
        font-size: var(--font-size-sm);
        font-weight: 600;
    }
</style>
