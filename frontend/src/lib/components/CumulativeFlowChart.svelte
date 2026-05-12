<script lang="ts">
    import { onMount, onDestroy } from 'svelte';
    import * as echarts from 'echarts';
    import type { CumulativeFlowDataPoint } from '../api/statisticsApi';

    export let data: CumulativeFlowDataPoint[] = [];

    let chartContainer: HTMLDivElement;
    let chart: echarts.ECharts | null = null;

    const statusColors: Record<string, string> = {
        'Backlog': '#555555',
        'ToDo': '#4a9eff',
        'InProgress': '#f0a500',
        'Done': '#4caf50',
        'default': '#b39ddb'
    };

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

    function renderChart() {
        if (!chart || data.length === 0) return;

        const dates = data.map(d => new Date(d.date).toLocaleDateString('hu-HU'));
        const statuses = data[0].statusCounts.map(s => s.status);

        const series = statuses.map(status => ({
            name: status,
            type: 'line',
            stack: 'total',
            smooth: true,
            areaStyle: {
                color: `${statusColors[status] ?? statusColors['default']}99`
            },
            itemStyle: {
                color: statusColors[status] ?? statusColors['default']
            },
            symbol: 'none',
            data: data.map(d =>
                d.statusCounts.find(s => s.status === status)?.count ?? 0
            )
        }));

        chart.setOption({
            backgroundColor: 'transparent',
            title: {
                text: 'Cumulative Flow Diagram',
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
                boundaryGap: false,
                data: dates,
                axisLabel: { color: '#aaa', rotate: 45 }
            },
            yAxis: {
                type: 'value',
                axisLabel: { color: '#aaa' },
                splitLine: { lineStyle: { color: '#2a2a2a' } },
                minInterval: 1
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