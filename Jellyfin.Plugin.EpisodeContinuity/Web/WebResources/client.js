/*
 * Episode Continuity: jellyfin-web client script.
 * Injected into index.html by the plugin. Everything here is defensive: a failure must never
 * break the player. Talks to the plugin's own REST endpoints via the global ApiClient.
 */
(function () {
    'use strict';

    if (window.__episodeContinuityLoaded) {
        return;
    }
    window.__episodeContinuityLoaded = true;

    var LOG = '[EpisodeContinuity]';
    var HELLO_INTERVAL_MS = 10 * 60 * 1000;
    var config = null;
    var configLoadedAt = 0;
    var CONFIG_TTL_MS = 15 * 1000;
    var currentItemId = null;
    var continuityCache = {}; // normalized item id -> Promise<result>
    var lastHello = 0;
    var warnedNextItemId = null; // item we already warned about in the up-next dialog
    var warnedNextAt = 0;
    var interstitial = null;

    function log() {
        try {
            var args = Array.prototype.slice.call(arguments);
            args.unshift(LOG);
            console.debug.apply(console, args);
        } catch (e) { /* ignore */ }
    }

    function normalizeId(id) {
        return (id || '').toString().replace(/-/g, '').toLowerCase();
    }

    function prop(obj, name) {
        if (!obj) {
            return undefined;
        }
        if (obj[name] !== undefined) {
            return obj[name];
        }
        var camel = name.charAt(0).toLowerCase() + name.slice(1);
        return obj[camel];
    }

    function apiClient() {
        return window.ApiClient && typeof window.ApiClient.getUrl === 'function' ? window.ApiClient : null;
    }

    function loggedIn() {
        var api = apiClient();
        try {
            return !!(api && api.getCurrentUserId && api.getCurrentUserId() && api.accessToken && api.accessToken());
        } catch (e) {
            return false;
        }
    }

    function getJSON(path) {
        var api = apiClient();
        return api.getJSON(api.getUrl(path));
    }

    // ---- server communication ------------------------------------------------------------

    function loadConfig() {
        if (!loggedIn()) {
            return Promise.resolve(null);
        }
        return getJSON('EpisodeContinuity/ClientConfig').then(function (c) {
            configLoadedAt = Date.now();
            config = {
                enabled: !!prop(c, 'Enabled'),
                upNext: !!prop(c, 'UpNextWarningEnabled'),
                interstitial: !!prop(c, 'InterstitialEnabled'),
                countdown: Number(prop(c, 'InterstitialCountdownSeconds')) || 10
            };
            log('config', config);
            return config;
        }).catch(function (err) {
            log('config load failed', err);
            return null;
        });
    }

    function ensureConfig(fresh) {
        if (config && !(fresh && Date.now() - configLoadedAt > CONFIG_TTL_MS)) {
            return Promise.resolve(config);
        }
        return loadConfig().then(function (c) { return c || config; });
    }

    function sayHello() {
        var now = Date.now();
        if (now - lastHello < HELLO_INTERVAL_MS || !loggedIn()) {
            return;
        }
        lastHello = now;
        var api = apiClient();
        try {
            api.ajax({
                type: 'POST',
                url: api.getUrl('EpisodeContinuity/Hello'),
                data: JSON.stringify({ DeviceId: api.deviceId() }),
                contentType: 'application/json'
            }).catch(function () { lastHello = 0; });
        } catch (e) {
            lastHello = 0;
        }
    }

    function getContinuity(itemId) {
        var key = normalizeId(itemId);
        if (!continuityCache[key]) {
            continuityCache[key] = getJSON('EpisodeContinuity/Items/' + key + '/Continuity').then(function (r) {
                return {
                    item: prop(r, 'Item') || null,
                    missingBefore: prop(r, 'MissingBefore') || [],
                    missingAfter: prop(r, 'MissingAfter') || [],
                    nextAvailable: prop(r, 'NextAvailable') || null,
                    previousAvailable: prop(r, 'PreviousAvailable') || null
                };
            }).catch(function (err) {
                delete continuityCache[key];
                log('continuity lookup failed', err);
                return null;
            });
        }
        return continuityCache[key];
    }

    // ---- text ----------------------------------------------------------------------------

    function describe(ep) {
        if (!ep) {
            return '';
        }
        var code = prop(ep, 'Code') || ('S' + pad(prop(ep, 'SeasonNumber')) + 'E' + pad(prop(ep, 'EpisodeNumber')));
        var name = prop(ep, 'Name');
        return name ? code + ' “' + name + '”' : code;
    }

    function pad(n) {
        n = Number(n) || 0;
        return n < 10 ? '0' + n : String(n);
    }

    function describeList(list) {
        if (!list || !list.length) {
            return '';
        }
        if (list.length === 1) {
            return describe(list[0]);
        }
        if (list.length <= 3) {
            return list.map(describe).join(', ');
        }
        var first = prop(list[0], 'Code') || describe(list[0]);
        var last = prop(list[list.length - 1], 'Code') || describe(list[list.length - 1]);
        return first + ' to ' + last + ' (' + list.length + ' episodes)';
    }

    function plural(list, one, many) {
        return list.length === 1 ? one : many;
    }

    // ---- playback tracking ---------------------------------------------------------------

    var PLAYBACK_INFO = /\/Items\/([0-9a-fA-F-]{32,36})\/PlaybackInfo(\?|$)/;

    function onPlaybackInfo(itemId) {
        var id = normalizeId(itemId);
        if (!id) {
            return;
        }
        log('playback info for', id);
        currentItemId = id;
        ensureConfig(true).then(function (cfg) {
            if (!cfg || !cfg.enabled) {
                return;
            }
            if (cfg.interstitial) {
                sayHello();
            }
            getContinuity(id).then(function (result) {
                if (!result || currentItemId !== id) {
                    return;
                }
                var recentlyWarned = warnedNextItemId === id && (Date.now() - warnedNextAt) < 3 * 60 * 1000;
                if (cfg.interstitial && result.missingBefore.length && !recentlyWarned) {
                    armInterstitial(id, result, cfg.countdown);
                }
            });
        });
    }

    function hookRequests() {
        try {
            var origFetch = window.fetch;
            if (typeof origFetch === 'function') {
                window.fetch = function (input) {
                    try {
                        var url = typeof input === 'string' ? input : (input && input.url) || '';
                        var m = PLAYBACK_INFO.exec(url);
                        if (m) {
                            onPlaybackInfo(m[1]);
                        }
                    } catch (e) { /* ignore */ }
                    return origFetch.apply(this, arguments);
                };
            }
        } catch (e) {
            log('fetch hook failed', e);
        }
        try {
            var origOpen = XMLHttpRequest.prototype.open;
            XMLHttpRequest.prototype.open = function (method, url) {
                try {
                    var m = PLAYBACK_INFO.exec(url || '');
                    if (m) {
                        onPlaybackInfo(m[1]);
                    }
                } catch (e) { /* ignore */ }
                return origOpen.apply(this, arguments);
            };
        } catch (e) {
            log('xhr hook failed', e);
        }
    }

    // ---- up-next dialog ------------------------------------------------------------------

    function decorateUpNext(dialog) {
        if (!currentItemId || dialog.querySelector('.ec-upnext-warning')) {
            return;
        }
        var id = currentItemId;
        ensureConfig().then(function (cfg) {
            if (!cfg || !cfg.enabled || !cfg.upNext) {
                return;
            }
            getContinuity(id).then(function (result) {
                if (!result || !result.missingAfter.length || !dialog.isConnected || dialog.querySelector('.ec-upnext-warning')) {
                    return;
                }
                var missing = result.missingAfter;
                var text = describeList(missing) + ' ' + plural(missing, 'is', 'are') + ' missing from your library.';
                if (result.nextAvailable) {
                    text += ' Playing next: ' + describe(result.nextAvailable) + '.';
                    warnedNextItemId = normalizeId(prop(result.nextAvailable, 'Id'));
                    warnedNextAt = Date.now();
                }
                var warning = document.createElement('div');
                warning.className = 'ec-upnext-warning';
                warning.innerHTML = '<span class="ec-icon" aria-hidden="true">⚠</span><span class="ec-text"></span>';
                warning.querySelector('.ec-text').textContent = text;
                var title = dialog.querySelector('.upNextDialog-title');
                if (title && title.parentNode) {
                    title.parentNode.insertBefore(warning, title);
                } else {
                    dialog.insertBefore(warning, dialog.firstChild);
                }
                log('up-next warning shown', text);
            });
        });
    }

    function findDialogRoot(node) {
        if (!(node instanceof Element)) {
            return null;
        }
        // jellyfin-web writes the dialog markup into .upNextContainer and adds the upNextDialog class to it.
        if (node.classList.contains('upNextDialog') || node.classList.contains('upNextContainer')) {
            return node;
        }
        if (node.querySelector && node.querySelector('.upNextDialog-title')) {
            return node.closest('.upNextContainer') || node.parentElement || node;
        }
        return null;
    }

    function observeUpNext() {
        var observer = new MutationObserver(function (mutations) {
            for (var i = 0; i < mutations.length; i++) {
                var m = mutations[i];
                if (m.type === 'childList') {
                    for (var j = 0; j < m.addedNodes.length; j++) {
                        var root = findDialogRoot(m.addedNodes[j]);
                        if (root && !root.classList.contains('hide')) {
                            decorateUpNext(root);
                        }
                    }
                } else if (m.type === 'attributes' && m.target instanceof Element) {
                    // show() removes the "hide" class from the container.
                    var t = m.target;
                    if (t.classList.contains('upNextDialog') && !t.classList.contains('hide') && !t.classList.contains('upNextDialog-hidden')) {
                        decorateUpNext(t);
                    }
                }
            }
        });
        observer.observe(document.body, { childList: true, subtree: true, attributes: true, attributeFilter: ['class'] });
    }

    // ---- pre-play interstitial -----------------------------------------------------------

    function findVideo() {
        var videos = document.querySelectorAll('video');
        for (var i = 0; i < videos.length; i++) {
            if (!videos[i].paused && videos[i].readyState >= 2) {
                return videos[i];
            }
        }
        return null;
    }

    function armInterstitial(itemId, result, countdownSeconds) {
        dismissInterstitial();
        var started = Date.now();
        var timer = setInterval(function () {
            if (currentItemId !== itemId || Date.now() - started > 30000) {
                clearInterval(timer);
                return;
            }
            var video = findVideo();
            if (video) {
                clearInterval(timer);
                showInterstitial(video, itemId, result, countdownSeconds);
            }
        }, 200);
    }

    function dismissInterstitial() {
        if (!interstitial) {
            return;
        }
        clearInterval(interstitial.timer);
        if (interstitial.el && interstitial.el.parentNode) {
            interstitial.el.parentNode.removeChild(interstitial.el);
        }
        interstitial = null;
    }

    function showInterstitial(video, itemId, result, countdownSeconds) {
        try {
            video.pause();
        } catch (e) {
            log('could not pause', e);
        }

        var missing = result.missingBefore;
        var el = document.createElement('div');
        el.className = 'ec-interstitial';
        el.setAttribute('role', 'alertdialog');
        el.innerHTML =
            '<div class="ec-card">' +
            '  <div class="ec-card-header"><span class="ec-icon" aria-hidden="true">⚠</span><h2>Missing ' + plural(missing, 'episode', 'episodes') + '</h2></div>' +
            '  <p class="ec-body"></p>' +
            '  <p class="ec-countdown">Continuing in <span class="ec-seconds"></span>s</p>' +
            '  <div class="ec-buttons">' +
            '    <button type="button" class="ec-btn ec-btn-primary ec-continue">Continue</button>' +
            '    <button type="button" class="ec-btn ec-back">Go back</button>' +
            '  </div>' +
            '</div>';
        el.querySelector('.ec-body').textContent =
            describeList(missing) + ' ' + plural(missing, 'is', 'are') + ' missing from your library. ' +
            'You are skipping straight to ' + describe(result.item) + '.';

        var remaining = Math.max(1, Math.round(countdownSeconds));
        var seconds = el.querySelector('.ec-seconds');
        seconds.textContent = String(remaining);

        function resume() {
            dismissInterstitial();
            try {
                video.play();
            } catch (e) {
                log('could not resume', e);
            }
        }

        function goBack() {
            dismissInterstitial();
            try {
                video.pause();
            } catch (e) { /* ignore */ }
            history.back();
        }

        el.querySelector('.ec-continue').addEventListener('click', resume);
        el.querySelector('.ec-back').addEventListener('click', goBack);
        el.addEventListener('keydown', function (ev) {
            if (ev.key === 'Enter' || ev.key === ' ') {
                ev.preventDefault();
                resume();
            } else if (ev.key === 'Escape' || ev.key === 'Backspace') {
                ev.preventDefault();
                goBack();
            }
        });

        var timer = setInterval(function () {
            remaining -= 1;
            if (remaining <= 0) {
                resume();
                return;
            }
            seconds.textContent = String(remaining);
            if (!video.isConnected || currentItemId !== itemId) {
                dismissInterstitial();
            }
        }, 1000);

        interstitial = { el: el, timer: timer };
        document.body.appendChild(el);
        var btn = el.querySelector('.ec-continue');
        if (btn) {
            btn.focus();
        }
        log('interstitial shown for', itemId);
    }

    // ---- styles --------------------------------------------------------------------------

    function injectStyles() {
        var css =
            '.ec-upnext-warning{display:flex;align-items:flex-start;gap:.5em;margin:.25em 0 .5em;padding:.5em .75em;border-radius:.3em;' +
            'background:rgba(255,170,0,.18);border-left:.25em solid #ffaa00;color:#fff;font-size:1em;line-height:1.35}' +
            '.ec-upnext-warning .ec-icon{color:#ffaa00;font-size:1.2em;line-height:1.1}' +
            '.ec-interstitial{position:fixed;inset:0;z-index:100000;display:flex;align-items:center;justify-content:center;' +
            'background:rgba(0,0,0,.72);font-family:inherit;color:#fff}' +
            '.ec-card{max-width:36em;width:calc(100% - 3em);background:#202020;border-radius:.5em;padding:1.5em 1.75em;' +
            'box-shadow:0 .5em 2em rgba(0,0,0,.6);border-top:.3em solid #ffaa00}' +
            '.ec-card-header{display:flex;align-items:center;gap:.6em;margin-bottom:.5em}' +
            '.ec-card-header h2{margin:0;font-size:1.4em;font-weight:500}' +
            '.ec-card-header .ec-icon{color:#ffaa00;font-size:1.6em}' +
            '.ec-body{margin:.25em 0 .75em;font-size:1.05em;line-height:1.45}' +
            '.ec-countdown{margin:0 0 1em;opacity:.75}' +
            '.ec-buttons{display:flex;gap:.75em;flex-wrap:wrap}' +
            '.ec-btn{border:0;border-radius:.3em;padding:.7em 1.4em;font-size:1em;cursor:pointer;background:#3a3a3a;color:#fff}' +
            '.ec-btn:hover,.ec-btn:focus{outline:2px solid #ffaa00;outline-offset:1px}' +
            '.ec-btn-primary{background:#00a4dc}';
        var style = document.createElement('style');
        style.id = 'ec-styles';
        style.textContent = css;
        (document.head || document.documentElement).appendChild(style);
    }

    // ---- bootstrap -----------------------------------------------------------------------

    function start() {
        injectStyles();
        hookRequests();
        if (document.body) {
            observeUpNext();
        } else {
            document.addEventListener('DOMContentLoaded', observeUpNext);
        }
        var attempts = 0;
        var poll = setInterval(function () {
            attempts += 1;
            if (loggedIn()) {
                clearInterval(poll);
                loadConfig();
            } else if (attempts > 600) {
                clearInterval(poll);
            }
        }, 1000);
        // Reload config when the user logs in later or switches users.
        document.addEventListener('viewshow', function () {
            if (!config && loggedIn()) {
                loadConfig();
            }
        });
        log('loaded');
    }

    // Small debug surface for troubleshooting from the browser console.
    window.__episodeContinuity = {
        get currentItemId() { return currentItemId; },
        get config() { return config; },
        get cache() { return continuityCache; },
        onPlaybackInfo: onPlaybackInfo
    };

    try {
        start();
    } catch (e) {
        log('failed to start', e);
    }
})();
