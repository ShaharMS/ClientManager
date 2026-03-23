let _dotNetRef = null;
let _resizeTimer = null;

export function register(dotNetRef) {
    _dotNetRef = dotNetRef;
    window.addEventListener('resize', onResize);
}

export function unregister() {
    window.removeEventListener('resize', onResize);
    clearTimeout(_resizeTimer);
    _dotNetRef = null;
}

export function getPageSize(fallbackRowHeight) {
    // Measure the actual rendered row height (called after first render so rows exist)
    const firstRow = document.querySelector('.rz-data-grid-data tr');
    const rowHeight = (firstRow && firstRow.offsetHeight > 0) ? firstRow.offsetHeight : fallbackRowHeight;

    // Measure actual app chrome heights instead of hard-coding a constant
    const main = document.querySelector('.cm-main');
    const pageHeader = document.querySelector('.cm-page-header');
    const listHeader = document.querySelector('.cm-list-page__header');
    const card = document.querySelector('.cm-list-page__table-card');

    if (!main || !pageHeader || !card) {
        // Fallback when DOM isn't ready
        return Math.max(5, Math.floor((window.innerHeight - 440) / rowHeight));
    }

    const mainStyle = window.getComputedStyle(main);
    const cardStyle = window.getComputedStyle(card);
    const listHeaderStyle = listHeader ? window.getComputedStyle(listHeader) : null;

    const overhead =
        parseFloat(mainStyle.paddingTop) +
        parseFloat(mainStyle.paddingBottom) +
        pageHeader.offsetHeight +
        parseFloat(window.getComputedStyle(pageHeader).marginBottom) +
        parseFloat(cardStyle.paddingTop) +
        parseFloat(cardStyle.paddingBottom) +
        (listHeader
            ? listHeader.offsetHeight + (listHeaderStyle ? parseFloat(listHeaderStyle.marginBottom) : 0)
            : 60) +
        50 +  // Radzen grid column header row (thead)
        58;   // Radzen pager (.rz-paginator)

    return Math.max(5, Math.floor((window.innerHeight - overhead) / rowHeight));
}

function onResize() {
    clearTimeout(_resizeTimer);
    _resizeTimer = setTimeout(() => {
        if (_dotNetRef) {
            _dotNetRef.invokeMethodAsync('OnWindowResize');
        }
    }, 150);
}
