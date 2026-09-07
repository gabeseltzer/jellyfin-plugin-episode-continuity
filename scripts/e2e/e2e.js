// End-to-end check of the Episode Continuity web script inside real jellyfin-web.
const { chromium } = require('playwright');
const fs = require('fs');

const BASE = process.env.JELLYFIN_URL || 'http://jellyfin:8096';
const SERVER_ID = process.env.SERVER_ID;
const E1 = process.env.E1, E3 = process.env.E3;
const OUT = __dirname + '/out';
fs.mkdirSync(OUT, { recursive: true });

const results = [];
function check(name, ok, detail) { results.push({ name, ok, detail }); console.log((ok ? 'PASS ' : 'FAIL ') + name + (detail ? '  -- ' + detail : '')); }
const sleep = ms => new Promise(r => setTimeout(r, ms));

(async () => {
  const browser = await chromium.launch({ channel: 'chrome', headless: true, args: ['--autoplay-policy=no-user-gesture-required', '--mute-audio'] });
  const page = await browser.newPage({ viewport: { width: 1280, height: 720 } });
  const logs = [];
  page.on('console', m => { const t = m.text(); if (t.includes('[EpisodeContinuity]') || m.type() === 'error') logs.push(m.type() + ': ' + t); });
  page.on('pageerror', e => logs.push('pageerror: ' + e.message));

  try {
    await page.goto(BASE + '/web/', { waitUntil: 'domcontentloaded' });
    // Login: either the manual form or the user-card list.
    await page.waitForSelector('#txtManualName, .btnManual, .cardImageContainer', { timeout: 60000 });
    const manual = await page.$('.btnManual:not(.hide)'); if (manual) { await manual.click(); }
    await page.waitForSelector('#txtManualName', { timeout: 20000 });
    await page.fill('#txtManualName', 'dev');
    await page.fill('#txtManualPassword', 'dev');
    await page.click('button[type=submit]');
    await page.waitForSelector('.section0, .homePage, .emby-scroller', { timeout: 60000 });
    check('script loaded', await page.evaluate(() => !!window.__episodeContinuityLoaded));

    // ---- Interstitial: start S01E03 directly (S01E02 is missing) ----
    await page.goto(`${BASE}/web/#/details?id=${E3}&serverId=${SERVER_ID}`, { waitUntil: 'domcontentloaded' });
    await page.locator('.btnPlay:visible').first().waitFor({ timeout: 30000 });
    await page.locator('.btnPlay:visible').first().click();
    const inter = await page.waitForSelector('.ec-interstitial', { timeout: 30000 }).catch(() => null);
    check('interstitial shown for E3', !!inter);
    if (inter) {
      await sleep(500);
      await page.screenshot({ path: OUT + '/interstitial.png' });
      const body = await page.textContent('.ec-interstitial .ec-body');
      check('interstitial names S01E02', /S01E02/.test(body), body.trim());
      check('video paused while interstitial shown', await page.evaluate(() => Array.from(document.querySelectorAll('video')).every(v => v.paused)));
      await page.click('.ec-continue');
      await sleep(1500);
      check('interstitial dismissed on Continue', !(await page.$('.ec-interstitial')));
      check('video playing after Continue', await page.evaluate(() => Array.from(document.querySelectorAll('video')).some(v => !v.paused)));
    }
    // stop playback
    await page.goBack().catch(() => {});
    await sleep(1500);

    // ---- Up-next: play S01E01 (11 min), seek near the end ----
    await page.goto(`${BASE}/web/#/details?id=${E1}&serverId=${SERVER_ID}`, { waitUntil: 'domcontentloaded' });
    await page.locator('.btnPlay:visible').first().waitFor({ timeout: 30000 });
    await page.locator('.btnPlay:visible').first().click();
    await page.waitForFunction(() => Array.from(document.querySelectorAll('video')).some(v => !v.paused && v.duration > 600), null, { timeout: 60000 });
    check('no interstitial for E1 (no gap before)', !(await page.$('.ec-interstitial')));
    await page.evaluate(() => { const v = Array.from(document.querySelectorAll('video')).find(v => !v.paused); v.currentTime = v.duration - 26; });
    const upnext = await page.waitForSelector('.upNextDialog:not(.hide)', { timeout: 20000 }).catch(() => null);
    check('jellyfin up-next dialog appeared', !!upnext);
    const warn = await page.waitForSelector('.ec-upnext-warning', { timeout: 10000 }).catch(() => null);
    check('up-next warning injected', !!warn);
    if (warn) {
      await sleep(400);
      await page.screenshot({ path: OUT + '/upnext.png' });
      const t = await page.textContent('.ec-upnext-warning');
      check('up-next warning names S01E02 and S01E03', /S01E02/.test(t) && /S01E03/.test(t), t.trim());
    }
    // Let autoplay roll into E3 and confirm the interstitial is suppressed (already warned).
    await page.waitForFunction(() => Array.from(document.querySelectorAll('video')).some(v => !v.paused && v.duration < 60), null, { timeout: 60000 }).catch(() => {});
    await sleep(2500);
    check('E3 autoplayed', await page.evaluate(() => Array.from(document.querySelectorAll('video')).some(v => v.duration < 60)));
    check('no interstitial after up-next warning', !(await page.$('.ec-interstitial')));
    await page.screenshot({ path: OUT + '/after-autoplay.png' });
  } catch (e) {
    check('script ran to completion', false, e.message);
    await page.screenshot({ path: OUT + '/error.png' }).catch(() => {});
  } finally {
    fs.writeFileSync(OUT + '/console.log', logs.join('\n'));
    await browser.close();
    const failed = results.filter(r => !r.ok).length;
    console.log(`\n${results.length - failed}/${results.length} checks passed`);
    process.exit(failed ? 1 : 0);
  }
})();
