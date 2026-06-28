// ==UserScript==
// @name         Grabsy Downloader
// @namespace    https://github.com/Sidiusz/yt-dlp-gui
// @version      1.0.0
// @description  One-click background downloads to the Grabsy desktop app, embedded next to the YouTube like bar (and the Shorts action bar).
// @author       Sidiusz
// @match        *://*.youtube.com/*
// @match        *://youtu.be/*
// @grant        GM_xmlhttpRequest
// @grant        GM_openInTab
// @connect      127.0.0.1
// @run-at       document-idle
// @updateURL    https://raw.githubusercontent.com/Sidiusz/yt-dlp-gui/main/Grabsy/Assets/grabsy.user.js
// @downloadURL  https://raw.githubusercontent.com/Sidiusz/yt-dlp-gui/main/Grabsy/Assets/grabsy.user.js
// @supportURL   https://github.com/Sidiusz/yt-dlp-gui/issues
// ==/UserScript==

(function () {
    "use strict";

    const PORT = 47821;
    const BASE = "http://127.0.0.1:" + PORT;
    const RELEASES = "https://github.com/Sidiusz/yt-dlp-gui/releases";
    const ACCENT = "#F8006B";

    const xhr = (typeof GM_xmlhttpRequest !== "undefined")
        ? GM_xmlhttpRequest
        : (typeof GM !== "undefined" ? GM.xmlHttpRequest : null);
    const openTab = (typeof GM_openInTab !== "undefined")
        ? GM_openInTab
        : (u) => window.open(u, "_blank");

    // ---------- progress toast ----------
    function makeToast() {
        const root = document.createElement("div");
        root.style.cssText =
            "position:fixed;right:18px;bottom:18px;z-index:2147483647;width:300px;" +
            "padding:12px 14px;border-radius:10px;background:#1e1f22;color:#f2f3f5;" +
            "font:13px/1.35 system-ui,sans-serif;box-shadow:0 6px 24px rgba(0,0,0,.5);";
        const label = document.createElement("div");
        label.textContent = "Sending to Grabsy…";
        label.style.cssText = "margin-bottom:8px;font-weight:600;";
        const track = document.createElement("div");
        track.style.cssText = "height:6px;border-radius:4px;background:#3a3b3e;overflow:hidden;";
        const bar = document.createElement("div");
        bar.style.cssText = "height:100%;width:0%;background:" + ACCENT + ";transition:width .3s;";
        track.appendChild(bar);
        const cancel = document.createElement("button");
        cancel.textContent = "Cancel";
        cancel.style.cssText =
            "margin-top:10px;padding:5px 12px;border:1px solid #3a3b3e;border-radius:7px;cursor:pointer;" +
            "background:transparent;color:#f2f3f5;font:600 12px system-ui,sans-serif;";
        root.appendChild(label);
        root.appendChild(track);
        root.appendChild(cancel);
        document.body.appendChild(root);
        const t = { root, label, bar, track, cancel, id: null, canceled: false };
        cancel.addEventListener("click", () => cancelJob(t));
        return t;
    }
    function setProgress(t, pct, text) {
        t.bar.style.background = ACCENT;
        t.bar.style.width = Math.max(0, Math.min(100, pct)) + "%";
        t.label.textContent = pct > 0 ? ("Downloading " + pct + "%") : (text || "Starting…");
    }
    function setDone(t) {
        t.bar.style.width = "100%";
        t.bar.style.background = "#23A55A";
        t.label.textContent = "Downloaded ✓";
        t.cancel.remove();
    }
    function setError(t, msg) {
        t.bar.style.background = "#F23F42";
        t.label.textContent = "Error: " + (msg || "download failed");
        t.cancel.remove();
    }
    function removeAfter(t, ms) { setTimeout(() => t.root.remove(), ms); }

    function cancelJob(t) {
        t.canceled = true;
        t.label.textContent = "Canceling…";
        if (t.id && xhr) {
            xhr({ method: "GET", url: BASE + "/cancel?id=" + t.id, timeout: 5000, onload: () => {}, onerror: () => {} });
        }
    }

    function appMissing() {
        if (confirm("Grabsy is not running.\n\nStart the Grabsy app (keep it open), or click OK to open the download page.")) {
            openTab(RELEASES, { active: true });
        }
    }

    // ---------- download flow ----------
    function poll(id, t) {
        xhr({
            method: "GET", url: BASE + "/status?id=" + id, timeout: 5000,
            onload: (r) => {
                if (r.status === 404) { setTimeout(() => poll(id, t), 700); return; } // still starting
                let s; try { s = JSON.parse(r.responseText); } catch (e) { setTimeout(() => poll(id, t), 700); return; }
                if (s.state === "running") {
                    setProgress(t, s.progress, s.status);
                    setTimeout(() => poll(id, t), 700);
                } else if (s.state === "done") {
                    setDone(t); removeAfter(t, 5000);
                } else {
                    setError(t, t.canceled ? "canceled" : s.status); removeAfter(t, 6000);
                }
            },
            onerror: () => { setError(t, "lost connection"); removeAfter(t, 5000); },
            ontimeout: () => { setTimeout(() => poll(id, t), 700); },
        });
    }

    function start(mode, quality) {
        if (!xhr) { appMissing(); return; }
        const t = makeToast();
        const u = BASE + "/download?url=" + encodeURIComponent(location.href) +
            "&mode=" + encodeURIComponent(mode) + "&quality=" + encodeURIComponent(quality);
        xhr({
            method: "GET", url: u, timeout: 5000,
            onload: (r) => {
                let j; try { j = JSON.parse(r.responseText); } catch (e) { }
                if (!j || !j.id) { setError(t, "bad response"); removeAfter(t, 5000); return; }
                t.id = j.id;
                if (t.canceled) { cancelJob(t); }   // user hit Cancel before id arrived
                poll(j.id, t);
            },
            onerror: () => { t.root.remove(); appMissing(); },
            ontimeout: () => { t.root.remove(); appMissing(); },
        });
    }

    // ---------- app config (preferred mode/quality from the app's settings) ----------
    let cfg = { mode: "videoaudio", quality: "best" };
    function loadConfig() {
        if (!xhr) return;
        xhr({
            method: "GET", url: BASE + "/config", timeout: 4000,
            onload: (r) => { try { const c = JSON.parse(r.responseText); if (c.mode) cfg.mode = c.mode; if (c.quality) cfg.quality = c.quality; } catch (e) {} },
            onerror: () => {}, ontimeout: () => {},
        });
    }

    // ---------- popover (mode + quality + download) ----------
    let openPop = null, popAnchor = null;
    function closePop() {
        if (!openPop) return;
        openPop.remove(); openPop = null; popAnchor = null;
        document.removeEventListener("mousedown", onDocDown, true);
        window.removeEventListener("scroll", reposPop, true);
        window.removeEventListener("resize", reposPop, true);
    }
    function onDocDown(e) { if (openPop && !openPop.contains(e.target) && !e.target.closest(".grabsy-trigger")) closePop(); }

    // Keep the popover glued to its button as the page scrolls.
    function reposPop() {
        if (!openPop || !popAnchor) return;
        const r = popAnchor.getBoundingClientRect();
        if (r.width === 0 && r.height === 0) { closePop(); return; }   // anchor gone
        const pw = 210, ph = openPop.offsetHeight || 170;
        let left = r.left, top = r.bottom + 8;
        if (left + pw > window.innerWidth - 8) left = window.innerWidth - pw - 8;
        if (top + ph > window.innerHeight - 8) top = r.top - ph - 8;
        openPop.style.left = Math.max(8, left) + "px";
        openPop.style.top = Math.max(8, top) + "px";
    }

    function sel(options) {
        const s = document.createElement("select");
        s.style.cssText =
            "width:100%;background:#2b2d31;color:#f2f3f5;border:1px solid #3a3b3e;border-radius:8px;" +
            "padding:7px 9px;font:13px system-ui,sans-serif;cursor:pointer;box-sizing:border-box;";
        options.forEach(([v, label]) => {
            const o = document.createElement("option");
            o.value = v; o.textContent = label; s.appendChild(o);
        });
        return s;
    }

    function togglePopover(anchor) {
        if (openPop) { closePop(); return; }
        const pop = document.createElement("div");
        pop.style.cssText =
            "position:fixed;z-index:2147483647;width:210px;display:flex;flex-direction:column;gap:8px;" +
            "padding:12px;border-radius:12px;background:#1e1f22;border:1px solid #2b2d31;" +
            "box-shadow:0 8px 28px rgba(0,0,0,.55);font:13px system-ui,sans-serif;";

        const title = document.createElement("div");
        title.textContent = "Grabsy";
        title.style.cssText = "font-weight:700;color:" + ACCENT + ";";

        const mode = sel([
            ["videoaudio", "Video & Audio"],
            ["videoonly", "Just Video"],
            ["audio", "Just Audio"],
        ]);
        const quality = sel([
            ["best", "Best"], ["2160", "2160p"], ["1440", "1440p"],
            ["1080", "1080p"], ["720", "720p"], ["480", "480p"],
        ]);
        mode.addEventListener("change", () => { quality.style.display = mode.value === "audio" ? "none" : ""; });

        // Default to the app's preferred mode/quality (synced via /config).
        mode.value = cfg.mode; quality.value = cfg.quality;
        quality.style.display = mode.value === "audio" ? "none" : "";

        const btn = document.createElement("button");
        btn.textContent = "↓ Download";
        btn.style.cssText =
            "padding:8px 16px;border:none;border-radius:8px;cursor:pointer;" +
            "font:600 13px system-ui,sans-serif;color:#000;background:" + ACCENT + ";";
        btn.addEventListener("click", () => { start(mode.value, quality.value); closePop(); });

        pop.append(title, mode, quality, btn);
        document.body.appendChild(pop);
        openPop = pop; popAnchor = anchor;
        reposPop();

        setTimeout(() => {
            document.addEventListener("mousedown", onDocDown, true);
            window.addEventListener("scroll", reposPop, true);
            window.addEventListener("resize", reposPop, true);
        }, 0);
    }

    // ---------- watch page: button left of the like bar ----------
    function injectWatch() {
        if (location.pathname !== "/watch") return;
        const row = document.querySelector("ytd-watch-metadata #top-level-buttons-computed")
                 || document.querySelector("#top-level-buttons-computed");
        if (!row || row.querySelector(".grabsy-trigger")) return;

        const b = document.createElement("button");
        b.className = "grabsy-trigger";
        b.textContent = "↓ Grabsy";
        b.title = "Download with Grabsy";
        b.style.cssText =
            "display:inline-flex;align-items:center;height:36px;margin-right:8px;padding:0 16px;border:none;" +
            "border-radius:18px;cursor:pointer;font:600 14px Roboto,system-ui,sans-serif;color:#000;" +
            "background:" + ACCENT + ";white-space:nowrap;flex:none;";
        b.addEventListener("click", (e) => { e.stopPropagation(); togglePopover(b); });
        row.insertBefore(b, row.firstChild);
    }

    // ---------- shorts: button in the right-side action bar ----------
    function injectShorts() {
        if (!location.pathname.startsWith("/shorts")) return;
        const bars = document.querySelectorAll("reel-action-bar-view-model");
        bars.forEach((bar) => {
            if (bar.querySelector(".grabsy-trigger")) return;
            const wrap = document.createElement("div");
            wrap.style.cssText = "display:flex;flex-direction:column;align-items:center;margin-bottom:16px;";
            const b = document.createElement("button");
            b.className = "grabsy-trigger";
            b.textContent = "↓";
            b.title = "Download with Grabsy";
            b.style.cssText =
                "width:48px;height:48px;border:none;border-radius:50%;cursor:pointer;" +
                "font:700 20px system-ui,sans-serif;color:#000;background:" + ACCENT + ";";
            b.addEventListener("click", (e) => { e.stopPropagation(); togglePopover(b); });
            const cap = document.createElement("div");
            cap.textContent = "Grabsy";
            cap.style.cssText = "margin-top:6px;font:600 12px system-ui,sans-serif;color:#fff;";
            wrap.append(b, cap);
            bar.insertBefore(wrap, bar.firstChild);
        });
    }

    function tick() {
        injectWatch();
        injectShorts();
    }

    setInterval(tick, 1500);
    tick();
    loadConfig();
    setInterval(loadConfig, 30000);   // keep defaults in sync with the app
})();
