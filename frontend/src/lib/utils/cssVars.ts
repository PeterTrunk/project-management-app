export function getCssVar(name: string): string {
    return getComputedStyle(document.documentElement)
        .getPropertyValue(name).trim();
}

export function getChartColors() {
    return {
        textColor:   getCssVar('--text-secondary'),
        mutedColor:  getCssVar('--text-muted'),
        splitLine:   getCssVar('--border'),
        tooltipBg:   getCssVar('--bg-card'),
        red:         getCssVar('--accent-red'),
        green:       getCssVar('--accent-green'),
        blue:        getCssVar('--accent-blue'),
        yellow:      getCssVar('--accent-yellow'),
        purple:      getCssVar('--accent-purple'),
    };
}