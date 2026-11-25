<script lang="ts">
    import { onMount, onDestroy } from 'svelte';
    import * as echarts from 'echarts';
    import type { CycleTimeDataPoint } from '../api/statisticsApi';

    import { getChartColors } from '../utils/cssVars';
    import { themeStore } from '../stores/themeStore';
    import { summarizeCycleTime, formatCycleTime, isMeasurable } from '../utils/cycleTime';

    /**
     * Átfutási idő: az első committól a pull request összeolvadásáig.
     *
     * Pontdiagram, nem vonal vagy oszlop, és ez nem esztétikai döntés: minden pont EGY feladat,
     * a vízszintes tengely az összeolvadás napja, a függőleges az eltelt idő. Egy vonaldiagram
     * középértékeket mutatna, és pont azt rejtené el, ami érdekes - a szóródást. A középértéket
     * ezért külön, vízszintes vonal jelzi.
     */
    export let data: CycleTimeDataPoint[] = [];

    let chartContainer: HTMLDivElement;
    let chart: echarts.ECharts | null = null;

    $: summary = summarizeCycleTime(data);

    onMount(() => {
        chart = echarts.init(chartContainer, 'dark');
        renderChart();

        const resizeObserver = new ResizeObserver(() => chart?.resize());
        resizeObserver.observe(chartContainer);

        return () => resizeObserver.disconnect();
    });

    onDestroy(() => {
        chart?.dispose();
    });

    $: if (chart && data && $themeStore) {
        renderChart();
    }

    function renderChart() {
        if (!chart) return;
        const c = getChartColors();

        //A nem értelmezhető sorok nem kerülnek a diagramra: egy negatív pont a tengelyt is
        //elrontaná. A számuk a diagram alatt, szövegben látszik - nem csendben tűnnek el.
        const points = data.filter(isMeasurable).map(point => ({
            value: [point.mergedAt, point.hours],
            taskKey: point.taskKey,
            title: point.title,
            prNumber: point.prNumber,
            commitCount: point.commitCount
        }));

        chart.setOption({
            backgroundColor: 'transparent',
            title: {
                text: 'Átfutási idő (első committól összeolvadásig)',
                left: 'center',
                textStyle: { color: c.textColor, fontSize: 14 }
            },
            tooltip: {
                trigger: 'item',
                backgroundColor: c.tooltipBg,
                borderColor: c.splitLine,
                textStyle: { color: c.textColor },
                formatter: (params: any) => {
                    const d = params.data;
                    const merged = new Date(d.value[0]).toLocaleDateString('hu-HU');

                    return `<strong>${d.taskKey}</strong> — ${d.title}<br/>`
                        + `Átfutás: ${formatCycleTime(d.value[1])}<br/>`
                        + `PR #${d.prNumber} · ${d.commitCount} commit<br/>`
                        + `Összeolvadt: ${merged}`;
                }
            },
            grid: { left: '3%', right: '4%', bottom: '12%', containLabel: true },
            xAxis: {
                type: 'time',
                name: 'Összeolvadás',
                nameLocation: 'middle',
                nameGap: 28,
                nameTextStyle: { color: c.mutedColor },
                axisLabel: {
                    color: c.mutedColor,
                    formatter: (value: number) => new Date(value).toLocaleDateString('hu-HU', {
                        month: '2-digit', day: '2-digit'
                    })
                },
                splitLine: { show: false }
            },
            yAxis: {
                type: 'value',
                name: 'óra',
                nameTextStyle: { color: c.mutedColor },
                axisLabel: { color: c.mutedColor },
                splitLine: { lineStyle: { color: c.splitLine } },
                min: 0
            },
            series: [
                {
                    name: 'Feladatok',
                    type: 'scatter',
                    color: c.blue,

                    //A pont mérete a commitok számával nő, de korlátosan: a gyök miatt egy
                    //ötvencommitos feladat nem nyomja agyon a többit
                    symbolSize: (value: any, params: any) =>
                        8 + Math.min(12, Math.sqrt(params.data.commitCount ?? 1) * 2),
                    data: points,

                    //A középérték vízszintes vonala: a szóródás mellett a tipikus érték
                    markLine: summary.medianHours === null ? undefined : {
                        silent: true,
                        symbol: 'none',
                        lineStyle: { color: c.green, type: 'dashed' },
                        label: {
                            formatter: `Középérték: ${formatCycleTime(summary.medianHours)}`,
                            color: c.green,
                            position: 'insideEndTop'
                        },
                        data: [{ yAxis: summary.medianHours }]
                    }
                }
            ]
        }, true);
    }
</script>

<div class="cycle-time">
    <div class="chart" bind:this={chartContainer}></div>

    <div class="summary">
        {#if summary.count === 0}
            <span class="empty">Nincs mérhető átfutási idő ebben az időszakban.</span>
        {:else}
            <span><strong>{summary.count}</strong> feladat</span>
            <span>Középérték: <strong>{formatCycleTime(summary.medianHours)}</strong></span>
            <span>Átlag: {formatCycleTime(summary.averageHours)}</span>
            {#if summary.longest !== null}
                <span>Leghosszabb: {summary.longest.taskKey} ({formatCycleTime(summary.longest.hours)})</span>
            {/if}
        {/if}

        {#if summary.discarded > 0}
            <!-- Nem csendben hagyjuk ki: a commit időbélyege a fejlesztő gépéről jön, és egy
                 commit utólag is hozzárendelhető egy már összeolvadt pull requesthez -->
            <span class="discarded" title="Az összeolvadás a legkorábbi commit előtt történt — a commit időbélyege a fejlesztő gépéről jön, és utólagos hozzárendelésnél is előfordul.">
                {summary.discarded} feladat kimaradt (nem értelmezhető sorrend)
            </span>
        {/if}
    </div>
</div>

<style>
    .cycle-time {
        display: flex;
        flex-direction: column;
        min-width: 0;
    }

    .chart {
        width: 100%;
        height: 320px;
        min-width: 480px;
    }

    .summary {
        display: flex;
        flex-wrap: wrap;
        gap: 0.25rem 1rem;
        padding: 0.5rem 0.75rem 0;
        font-size: 0.78rem;
        color: var(--text-secondary);
    }

    .summary strong {
        color: var(--text-primary);
    }

    .empty {
        color: var(--text-muted);
    }

    .discarded {
        color: var(--text-muted);
        cursor: help;
    }
</style>
