<script lang="ts">
    import { onMount, onDestroy } from 'svelte';
    import * as echarts from 'echarts';
    import type { VelocityDataPoint } from '../api/statisticsApi';

    export let data: VelocityDataPoint[] = [];

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

    function renderChart() {
        if (!chart) return;

        const avgVelocity = data.length > 0
            ? Math.round(data.reduce((sum, d) => sum + d.completedTasks, 0) / data.length)
            : 0;

        chart.setOption({
            backgroundColor: 'transparent',
            title: {
                text: 'Sprint Velocity',
                left: 'center',
                textStyle: { color: '#ccc', fontSize: 14 }
            },
            tooltip: {
                trigger: 'axis',
                axisPointer: { type: 'shadow' },
                formatter: '{b}: {c} task'
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
                data: data.map(d => d.sprintName),
                axisLabel: { color: '#aaa', rotate: 30 }
            },
            yAxis: {
                type: 'value',
                axisLabel: { color: '#aaa' },
                splitLine: { lineStyle: { color: '#2a2a2a' } },
                minInterval: 1
            },
            series: [
                {
                    name: 'Befejezett taskok',
                    type: 'bar',
                    color: '#4caf50',
                    data: data.map(d => ({
                        value: d.completedTasks,
                        itemStyle: {
                            color: new echarts.graphic.LinearGradient(0, 0, 0, 1, [
                                { offset: 0, color: '#4caf50' },
                                { offset: 1, color: '#1a3a1a' }
                            ])
                        }
                    })),
                    barMaxWidth: 60,
                    label: {
                        show: true,
                        position: 'top',
                        color: '#aaa',
                        formatter: '{c}'
                    }
                },
                {
                    name: 'Átlag velocity',
                    type: 'line',
                    data: data.map(() => avgVelocity),
                    lineStyle: { type: 'dashed', color: '#f0a500' },
                    itemStyle: { color: '#f0a500' },
                    symbol: 'none'
                }
            ]
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