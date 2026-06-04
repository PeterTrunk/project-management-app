<script lang="ts">
    import { onMount, onDestroy, afterUpdate } from 'svelte';
    import * as echarts from 'echarts';
    import type { TaskStatusDistribution } from '../api/statisticsApi';

    import { getChartColors } from '../cssVars';
    import { themeStore } from '../stores/themeStore';

        $: if (chart && data && $themeStore) {
        renderChart();
    }

    export let data: TaskStatusDistribution[] = [];

    let chartContainer: HTMLDivElement;
    let chart: echarts.ECharts | null = null;

    function getStatusColor(status: string): string {
        // Előre definiált ismert státuszokhoz fix szín
        const knownColors: Record<string, string> = {
            'Backlog': '#555555',
            'To Do': '#4a9eff',
            'In Progress': '#f0a500',
            'Done': '#4caf50',
            'Testing': '#b39ddb',
            'Review': '#ff9800',
        };

        if (knownColors[status]) return knownColors[status];

        // Ismeretlen státuszhoz hash alapú szín generálás
        let hash = 0;
        for (let i = 0; i < status.length; i++) {
            hash = status.charCodeAt(i) + ((hash << 5) - hash);
        }
        const hue = Math.abs(hash) % 360;
        return `hsl(${hue}, 60%, 50%)`;
    }

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
                text: 'Task Státusz Eloszlás',
                left: 'center',
                textStyle: { color: c.textColor, fontSize: 14 }
            },
            tooltip: {
                trigger: 'item',
                formatter: '{b}: {c} ({d}%)'
            },
            legend: {
                orient: 'vertical',
                right: '5%',
                top: 'middle',
                textStyle: { color: c.mutedColor },
                type: 'scroll',
            },
            series: [{
                type: 'pie',
                radius: ['40%', '65%'],
                center: ['40%', '50%'],
                avoidLabelOverlap: true,
                label: {
                    show: true,
                    formatter: '{b}: {c}',
                    color: c.textColor
                },
                emphasis: {
                    itemStyle: {
                        shadowBlur: 10,
                        shadowOffsetX: 0,
                        shadowColor: 'rgba(0, 0, 0, 0.5)'
                    }
                },
                data: data.map(d => ({
                    name: d.status,
                    value: d.count,
                    itemStyle: {
                        color: getStatusColor(d.status)
                    }
                }))
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