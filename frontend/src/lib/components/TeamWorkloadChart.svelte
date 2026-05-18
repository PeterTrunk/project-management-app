<script lang="ts">
    import { onMount, onDestroy } from 'svelte';
    import * as echarts from 'echarts';
    import type { WorkloadDataPoint } from '../api/statisticsApi';

    export let data: WorkloadDataPoint[] = [];

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

        chart.setOption({
            backgroundColor: 'transparent',
            title: {
                text: 'Team Workload',
                left: 'center',
                textStyle: { color: '#ccc', fontSize: 14 }
            },
            tooltip: {
                trigger: 'axis',
                axisPointer: { type: 'shadow' },
                formatter: '{b}: {c} task'
            },
            grid: {
                left: '3%',
                right: '4%',
                bottom: '10%',
                containLabel: true
            },
            xAxis: {
                type: 'category',
                data: data.map(d => d.userName),
                axisLabel: { color: '#aaa', rotate: 30 }
            },
            yAxis: {
                type: 'value',
                axisLabel: { color: '#aaa' },
                splitLine: { lineStyle: { color: '#2a2a2a' } },
                minInterval: 1
            },
            series: [{
                type: 'bar',
                data: data.map(d => ({
                    value: d.taskCount,
                    itemStyle: {
                        color: new echarts.graphic.LinearGradient(0, 0, 0, 1, [
                            { offset: 0, color: '#4a9eff' },
                            { offset: 1, color: '#1a2a3a' }
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
            }]
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