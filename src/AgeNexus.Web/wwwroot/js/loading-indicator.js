(() => {
    const overlay = () => document.getElementById("global-loading-overlay");
    const show = () => overlay()?.classList.add("visible");
    const hide = () => overlay()?.classList.remove("visible");

    document.addEventListener("click", event => {
        const link = event.target.closest("a[href]");
        if (!link || event.defaultPrevented || event.button !== 0 || event.ctrlKey || event.metaKey || event.shiftKey || event.altKey ||
            link.target || link.hasAttribute("download") || link.dataset.noLoading !== undefined) return;

        const target = new URL(link.href, window.location.href);
        if (target.origin !== window.location.origin || (target.pathname === window.location.pathname && target.search === window.location.search && target.hash)) return;
        show();
    }, true);

    document.addEventListener("submit", event => {
        const form = event.target;
        queueMicrotask(() => {
            if (!event.defaultPrevented && form.dataset.noLoading === undefined) show();
        });
    });
    document.addEventListener("enhancedload", hide);
    window.addEventListener("pageshow", hide);
})();
