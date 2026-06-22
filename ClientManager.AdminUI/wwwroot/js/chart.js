let _dotNetRef = null;
let _resizeTimer = null;

const CHART_CARD_SELECTORS = ['.cm-chart-card'];

export function getChartCardWidth(selector) {
    const selectors = selector ? [selector] : CHART_CARD_SELECTORS;
    for (const sel of selectors) {
        const card = document.querySelector(sel);
        if (card?.clientWidth > 0) {
            return card.clientWidth;
        }
    }

    return window.innerWidth;
}

export function register(dotNetRef) {
    _dotNetRef = dotNetRef;
    window.addEventListener('resize', onResize);
}

export function unregister() {
    window.removeEventListener('resize', onResize);
    clearTimeout(_resizeTimer);
    _dotNetRef = null;
}

function onResize() {
    clearTimeout(_resizeTimer);
    _resizeTimer = setTimeout(() => {
        if (_dotNetRef) {
            _dotNetRef.invokeMethodAsync('OnChartResize');
        }
    }, 150);
}
