export function resolveFontFaceUrls(cssText, stylesheetUrl) {
    return cssText.replace(/url\(\s*(?:(["'])(.*?)\1|([^\s)]+))\s*\)/gi, (match, quote, quotedUrl, unquotedUrl) => {
        const url = quotedUrl ?? unquotedUrl;
        if (!url) return match;
        try {
            return `url("${new URL(url, stylesheetUrl).href}")`;
        } catch {
            return match;
        }
    });
}
