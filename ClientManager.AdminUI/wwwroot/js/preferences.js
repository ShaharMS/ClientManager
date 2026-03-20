export function getPreferences() {
    const raw = localStorage.getItem("cm-preferences");
    return raw ? JSON.parse(raw) : null;
}

export function savePreferences(prefs) {
    localStorage.setItem("cm-preferences", JSON.stringify(prefs));
}

export function applyTheme(theme) {
    document.documentElement.setAttribute("data-theme", theme);
}
