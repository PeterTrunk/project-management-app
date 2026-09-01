<script lang="ts">
    import { onMount, onDestroy } from 'svelte';
    import * as echarts from 'echarts';
    import type { VelocityDataPoint } from '../api/statisticsApi';

    import { getChartColors } from '../cssVars';
    import { themeStore } from '../stores/themeStore';

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

    $: if (chart && data && $themeStore) {
        renderChart();
    }

    function renderChart() {
        if (!chart) return;
        const c = getChartColors();

        const avgVelocity = data.length > 0
            ? Math.round(data.reduce((sum, d) => sum + d.completedTasks, 0) / data.length)
            : 0;

        chart.setOption({
            backgroundColor: 'transparent',
            title: {
                text: 'Sprint Velocity',
                left: 'center',
                textStyle: { color: c.textColor, fontSize: 14 }
            },
            tooltip: {
                trigger: 'axis',
                axisPointer: { type: 'shadow' },
                formatter: '{b}: {c} task'
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
                data: data.map(d => d.sprintName),
                axisLabel: { color: c.mutedColor, rotate: 30 }
            },
            yAxis: {
                type: 'value',
                axisLabel: { color: c.mutedColor },
                splitLine: { lineStyle: { color: c.splitLine } },
                minInterval: 1
            },
            series: [
                {
                    name: 'Befejezett taskok',
                    type: 'bar',
                    color: c.green,
                    data: data.map(d => ({
                        value: d.completedTasks,
                        itemStyle: {
                            color: new echarts.graphic.LinearGradient(0, 0, 0, 1, [
                                { offset: 0, color: c.green },
                                { offset: 1, color: `${c.green}33` }
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
                },
                {
                    name: 'Átlag velocity',
                    type: 'line',
                    data: data.map(() => avgVelocity),
                    lineStyle: { type: 'dashed', color: c.yellow },
                    itemStyle: { color: c.yellow },
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
        min-width: 480px;
        height: 350px;
    }
</style>