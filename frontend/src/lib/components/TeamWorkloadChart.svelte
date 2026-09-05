<script lang="ts">
    import { onMount, onDestroy } from 'svelte';
    import * as echarts from 'echarts';
    import type { WorkloadDataPoint } from '../api/statisticsApi';

    import { getChartColors } from '../utils/cssVars';
    import { themeStore } from '../stores/themeStore';

    $: if (chart && data && $themeStore) {
        renderChart();
    }
    
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

    function renderChart() {
        if (!chart) return;
        const c = getChartColors();

        chart.setOption({
            backgroundColor: 'transparent',
            title: {
                text: 'Team Workload',
                left: 'center',
                textStyle: { color: c.textColor, fontSize: 14 }
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
                axisLabel: { color: c.mutedColor, rotate: 30 }
            },
            yAxis: {
                type: 'value',
                axisLabel: { color: c.mutedColor },
                splitLine: { lineStyle: { color: c.splitLine } },
                minInterval: 1
            },
            series: [{
                type: 'bar',
                data: data.map(d => ({
                    value: d.taskCount,
                    itemStyle: {
                        color: new echarts.graphic.LinearGradient(0, 0, 0, 1, [
                            { offset: 0, color: c.blue },
                            { offset: 1, color: `${c.blue}33` }
                        ])
                    }
                })),
                barMaxWidth: 60,
                label: {
                    show: true,
                    position: 'top',
                    color: c.mutedColor,
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
        min-width: 480px;
        height: 350px;
    }
</style>