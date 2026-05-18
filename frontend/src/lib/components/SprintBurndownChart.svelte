<script lang="ts">
    import { onMount, onDestroy } from 'svelte';
    import * as echarts from 'echarts';
    import type { BurndownDataPoint } from '../api/statisticsApi';

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

    $: if (chart && data) {
        renderChart();
    }

    $: if (chart && mode) {
        renderChart();
    }

    function renderChart() {
        if (!chart) return;

        const dates = data.map(d => new Date(d.date).toLocaleDateString('hu-HU'));

        const series = mode === 'burndown'
            ? [{
                name: 'Maradék taskok',
                type: 'line',
                data: data.map(d => d.remainingTasks),
                smooth: true,
                itemStyle: { color: '#ff5555' },
                areaStyle: { color: 'rgba(255,85,85,0.1)' }
              },
              {
                name: 'Ideális',
                type: 'line',
                data: data.map((_, i) => {
                    const total = data[0]?.totalTasks ?? 0;
                    return Math.round(total - (total / (data.length - 1)) * i);
                }),
                smooth: false,
                lineStyle: { type: 'dashed', color: '#555' },
                itemStyle: { color: '#555' },
                symbol: 'none'
              }]
            : [{
                name: 'Befejezett taskok',
                type: 'line',
                data: data.map(d => d.completedTasks),
                smooth: true,
                itemStyle: { color: '#4caf50' },
                areaStyle: { color: 'rgba(76,175,80,0.1)' }
              },
              {
                name: 'Összes task',
                type: 'line',
                data: data.map(d => d.totalTasks),
                smooth: false,
                lineStyle: { type: 'dashed', color: '#4a9eff' },
                itemStyle: { color: '#4a9eff' },
                symbol: 'none'
              }];

        chart.setOption({
            backgroundColor: 'transparent',
            title: {
                text: mode === 'burndown' ? 'Sprint Burndown' : 'Sprint Burnup',
                left: 'center',
                textStyle: { color: '#ccc', fontSize: 14 }
            },
            tooltip: {
                trigger: 'axis',
                axisPointer: { type: 'cross' }
            },
            legend: {
                bottom: 0,
                textStyle: { color: '#aaa' }
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
                axisLabel: { color: '#aaa', rotate: 45 }
            },
            yAxis: {
                type: 'value',
                axisLabel: { color: '#aaa' },
                splitLine: { lineStyle: { color: '#2a2a2a' } }
            },
            series
        });
    }
</script>

<div bind:this={chartContainer} class="chart-container"></div>

<style>
    .chart-container {
        width: 100%;
        height: 350px;
    }
</style>