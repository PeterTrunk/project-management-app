<script lang="ts">
    import { onMount, onDestroy } from 'svelte';
    import * as echarts from 'echarts';
    import type { BurndownDataPoint } from '../api/statisticsApi';

    import { getChartColors } from '../cssVars';
    import { themeStore } from '../stores/themeStore';  

    $: if (chart && data && $themeStore) {
        renderChart();
    }

    export let data: BurndownDataPoint[] = [];
    export let mode: 'burndown' | 'burnup' = 'burndown';

    let chartContainer: HTMLDivElement;
    let chart: echarts.ECharts | null = null;

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
    
    $: if (chart && mode) {
        renderChart();
    }

    function renderChart() {
        if (!chart) return;
        const c = getChartColors();

        const dates = data.map(d => new Date(d.date).toLocaleDateString('hu-HU'));

        const series = mode === 'burndown'
            ? [{
                name: 'Maradék taskok',
                type: 'line',
                data: data.map(d => d.remainingTasks),
                smooth: true,
                itemStyle: { color: c.red },
                areaStyle: { color: `${c.red}1a` }
            },
            {
                name: 'Ideális',
                type: 'line',
                data: data.map((_, i) => {
                    const total = data[0]?.totalTasks ?? 0;
                    return Math.round(total - (total / (data.length - 1)) * i);
                }),
                smooth: false,
                lineStyle: { type: 'dashed', color: c.mutedColor },
                itemStyle: { color: c.mutedColor },
                symbol: 'none'
            }]
            : [{
                name: 'Befejezett taskok',
                type: 'line',
                data: data.map(d => d.completedTasks),
                smooth: true,
                itemStyle: { color: c.green },
                areaStyle: { color: `${c.green}1a` }
            },
            {
                name: 'Összes task',
                type: 'line',
                data: data.map(d => d.totalTasks),
                smooth: false,
                lineStyle: { type: 'dashed', color: c.blue },
                itemStyle: { color: c.blue },
                symbol: 'none'
            }];

        chart.setOption({
            backgroundColor: 'transparent',
            title: {
                text: mode === 'burndown' ? 'Sprint Burndown' : 'Sprint Burnup',
                left: 'center',
                textStyle: { color: c.textColor, fontSize: 14 }
            },
            tooltip: {
                trigger: 'axis',
                axisPointer: { type: 'cross' }
            },
            legend: {
                bottom: 0,
                textStyle: { color: c.mutedColor }
            },
            grid: {
                left: '3%',
                right: '4%',
                bottom: '10%',
                containLabel: true
            },
            xAxis: {
                type: 'category',
                data: dates,
                axisLabel: { color: c.mutedColor, rotate: 45 }
            },
            yAxis: {
                type: 'value',
                axisLabel: { color: c.mutedColor },
                splitLine: { lineStyle: { color: c.splitLine } }
            },
            series
        });
    }
</script>

<div bind:this={chartContainer} class="chart-container"></div>

<style>
    .chart-container {
        width: 100%;
        min-width: 480px;
        height: 350px;
    }
</style>