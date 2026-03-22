let _dotNetRef = null;

export function register(dotNetRef) {
    _dotNetRef = dotNetRef;
    document.addEventListener("visibilitychange", onVisibilityChange);
}

export function unregister() {
    document.removeEventListener("visibilitychange", onVisibilityChange);
    _dotNetRef = null;
}

export function isHidden() {
    return document.hidden;
}

function onVisibilityChange() {
    if (_dotNetRef) {
        _dotNetRef.invokeMethodAsync("OnVisibilityChanged", !document.hidden);
    }
}
