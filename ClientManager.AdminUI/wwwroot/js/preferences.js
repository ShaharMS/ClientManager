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

const cultureCookieName = ".AspNetCore.Culture";
const cmCultureCookieName = "cm-culture";

export function getPreferencesCulture(prefs) {
    return prefs?.culture ?? prefs?.Culture ?? "";
}

export function getResolvedCulture() {
    const prefs = getPreferences();
    const culture = getPreferencesCulture(prefs);
    if (culture) {
        return culture;
    }
    return window.__resolvedCulture || "en";
}

export function getCultureFromCookie() {
    const cm = document.cookie.match(
        new RegExp("(?:^|; )" + cmCultureCookieName + "=([^;]*)"));
    if (cm) {
        return decodeURIComponent(cm[1]);
    }

    const match = document.cookie.match(
        new RegExp("(?:^|; )" + cultureCookieName.replace(".", "\\.") + "=([^;]*)"));
    if (!match) {
        return null;
    }
    const decoded = decodeURIComponent(match[1]);
    const ui = decoded.split("|").find(p => p.startsWith("uic="));
    return ui ? ui.slice(4) : null;
}

export function setCultureCookie(culture) {
    document.cookie = `${cmCultureCookieName}=${encodeURIComponent(culture)};path=/;max-age=31536000;samesite=lax`;
    const value = encodeURIComponent(`c=${culture}|uic=${culture}`);
    document.cookie = `${cultureCookieName}=${value};path=/;max-age=31536000;samesite=lax`;
}

export function applyCulture(culture, isRtl) {
    const html = document.documentElement;
    html.setAttribute("lang", culture);
    html.setAttribute("dir", isRtl ? "rtl" : "ltr");

    const link = document.getElementById("bootstrap-css");
    if (link) {
        link.href = isRtl
            ? "bootstrap/dist/css/bootstrap.rtl.min.css"
            : "bootstrap/dist/css/bootstrap.min.css";
    }

    setCultureCookie(culture);
}

export function initCulture() {
    const culture = getResolvedCulture();
    const cookieCulture = getCultureFromCookie();

    if (cookieCulture !== culture) {
        setCultureCookie(culture);
        if (cookieCulture !== null || culture !== "en") {
            location.reload();
            return;
        }
    }

    const isRtl = window.__cultureRtl?.[culture] ?? false;
    applyCulture(culture, isRtl);
}

export function reloadPage() {
    location.reload();
}
