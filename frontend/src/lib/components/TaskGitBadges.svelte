<script lang="ts">
    import { gitLinksByTask, noTaskGitLinks } from '../stores/gitLinkStore';
    import { backfillStore } from '../stores/backfillStore';
    import {
        summarizeTaskGit, formatGitSummary, gitSummaryBadgeClass, detectGitContradictions
    } from '../utils/gitTaskState';

    import { GitCommit, TriangleAlert } from 'lucide-svelte';

    /**
     * A feladat git állapota a kártyán.
     *
     * A kát feladat kártya használja (tábla- és backlog-nézetbeli cardok), és a jelzésnek
     * mindkettőn ugyanazt kell jelentenie. A hivatkozásokat nem propként kapja, hanem a
     * származtatott indexből olvassa: így a szülő kártyák változatlanok maradnak, és a
     * hivatkozások egyetlen gyorsítótárból jönnek, ahogy a részletnézetben is.
     *
     * Az adat NEM új lekérés: a projekt betöltésével már megérkezett, és eddig csak a git
     * nézetben, illetve a részletnézetben látszott - vagyis ott, ahová külön oda kell navigálni.
     * A döntések viszont a táblanézetben születnek.
     */
    export let taskId: string;

    //A feladat a kész (utolsó) oszlopban van (`completedAt`). 
    export let isCompleted: boolean;

    //A feladat a sprint lezárásával lezárult (`closedAt`) - ilyenkor nincs figyelmeztetés, nem sok értelme lenne.
    export let isClosed: boolean = false;

    $: links = $gitLinksByTask.get(taskId) ?? noTaskGitLinks;
    $: summary = summarizeTaskGit(links.commits, links.prs);
    $: summaryText = formatGitSummary(summary);

    //A pótlásra váró push a "nincs commit" jelzést teheti hamissá, ezért a jelzés tud róla
    $: hasOpenBackfill = $backfillStore.backfills.some(row => row.state !== 'Completed');

    $: contradictions = detectGitContradictions({
        isCompleted,
        isClosed,
        commits: links.commits,
        prs: links.prs,
        hasOpenBackfill
    });

    $: warningTitle = contradictions.map(c => `${c.label} — ${c.hint}`).join('\n');
</script>

{#if summaryText !== null || contradictions.length > 0}
    <div class="git-row">
        {#if summaryText !== null}
            <span class="badge {gitSummaryBadgeClass(summary)}" title="Kapcsolt commitok és pull requestek">
                <GitCommit size={11} /> {summaryText}
            </span>
        {/if}

        {#if contradictions.length > 0}
            <span
                class="git-warning"
                class:uncertain={contradictions.every(c => c.uncertain)}
                title={warningTitle}>
                <TriangleAlert size={11} /> {contradictions[0].label}
                {#if contradictions.length > 1}
                    <span class="more">+{contradictions.length - 1}</span>
                {/if}
            </span>
        {/if}
    </div>
{/if}

<style>
    .git-row {
        display: flex;
        align-items: center;
        gap: 0.3rem;
        flex-wrap: wrap;
        min-width: 0;
    }

    .badge {
        display: flex;
        align-items: center;
        gap: 0.25rem;
        padding: 0.1rem 0.35rem;
        border-radius: 4px;
        font-size: 0.68rem;
        white-space: nowrap;
    }

    .git-warning {
        display: flex;
        align-items: center;
        gap: 0.25rem;
        padding: 0.1rem 0.35rem;
        border-radius: 4px;
        font-size: 0.68rem;
        background: var(--accent-yellow-bg);
        color: var(--accent-yellow);
        white-space: nowrap;
        cursor: help;
    }

    .git-warning.uncertain {
        background: var(--bg-hover);
        color: var(--text-muted);
    }

    .more {
        opacity: 0.8;
    }
</style>
