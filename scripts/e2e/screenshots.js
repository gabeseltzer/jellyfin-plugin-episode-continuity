// Captures README screenshots from real jellyfin-web against the devcontainer server.
// Usage: SERVER_ID=... E1=... E3=... node screenshots.js   (same env as e2e.js)
const { chromium } = require('playwright');
const fs = require('fs');
const path = require('path');

const BASE = process.env.JELLYFIN_URL || 'http://jellyfin:8096';
const SERVER_ID = process.env.SERVER_ID;
const E1 = process.env.E1, E3 = process.env.E3;
const PLUGIN_ID = '3f8a1c6e-9d2b-4a7f-b5e4-c1d0e8f7a2b9';
const OUT = process.env.OUT || path.resolve(__dirname, '../../docs/screenshots');
fs.mkdirSync(OUT, { recursive: true });
const sleep = ms => new Promise(r => setTimeout(r, ms));

async function api(pathname, opts = {}, token) {
  const res = await fetch(BASE + pathname, {
    ...opts,
    headers: {
      'Content-Type': 'application/json',
      'X-Emby-Authorization': 'MediaBrowser Client="shots", Device="shots", DeviceId="shots", Version="1"',
      ...(token ? { 'X-Emby-Token': token } : {}),
      ...(opts.headers || {})
    }
  });
  if (!res.ok) throw new Error(`${pathname} -> ${res.status}`);
  const text = await res.text();
  return text ? JSON.parse(text) : null;
}

(async () => {
  const auth = await api('/Users/AuthenticateByName', { method: 'POST', body: JSON.stringify({ Username: 'dev', Pw: 'dev' }) });
  const token = auth.AccessToken;
  const original = await api(`/Plugins/${PLUGIN_ID}/Configuration`, {}, token);
  const setConfig = patch => api(`/Plugins/${PLUGIN_ID}/Configuration`, { method: 'POST', body: JSON.stringify({ ...original, ...patch }) }, token);

  const browser = await chromium.launch({ channel: 'chrome', headless: true, args: ['--autoplay-policy=no-user-gesture-required', '--mute-audio'] });
  const page = await browser.newPage({ viewport: { width: 1280, height: 720 }, colorScheme: 'dark' });
  const shot = name => page.screenshot({ path: path.join(OUT, name + '.png') }).then(() => console.log('saved', name));

  try {
    await setConfig({ Enabled: true, WebFeaturesEnabled: true, UpNextWarningEnabled: true, InterstitialEnabled: true, ServerPushMode: 'Toast', WarningCooldownSeconds: 0 });

    await page.goto(BASE + '/web/', { waitUntil: 'domcontentloaded' });
    await page.waitForSelector('#txtManualName, .btnManual, .cardImageContainer', { timeout: 60000 });
    const manual = await page.$('.btnManual:not(.hide)'); if (manual) await manual.click();
    await page.waitForSelector('#txtManualName', { timeout: 20000 });
    await page.fill('#txtManualName', 'dev');
    await page.fill('#txtManualPassword', 'dev');
    await page.click('button[type=submit]');
    await page.waitForSelector('.section0, .homePage, .emby-scroller', { timeout: 60000 });

    // 1. Pre-play interstitial
    await page.goto(`${BASE}/web/#/details?id=${E3}&serverId=${SERVER_ID}`, { waitUntil: 'domcontentloaded' });
    await page.locator('.btnPlay:visible').first().waitFor({ timeout: 30000 });
    await page.locator('.btnPlay:visible').first().click();
    await page.waitForSelector('.ec-interstitial', { timeout: 30000 });
    await sleep(800);
    await shot('interstitial');
    await page.click('.ec-back');
    await sleep(1500);

    // 2. Up-next warning
    await page.goto(`${BASE}/web/#/details?id=${E1}&serverId=${SERVER_ID}`, { waitUntil: 'domcontentloaded' });
    await page.locator('.btnPlay:visible').first().waitFor({ timeout: 30000 });
    await page.locator('.btnPlay:visible').first().click();
    await page.waitForFunction(() => Array.from(document.querySelectorAll('video')).some(v => !v.paused && v.duration > 600), null, { timeout: 60000 });
    await page.evaluate(() => { const v = Array.from(document.querySelectorAll('video')).find(v => !v.paused); v.currentTime = v.duration - 26; });
    await page.waitForSelector('.ec-upnext-warning', { timeout: 30000 });
    await sleep(600);
    await shot('upnext');
    await page.goto(`${BASE}/web/#/home`, { waitUntil: 'domcontentloaded' });
    await sleep(1500);

    // 3. Server dialog (interstitial off so the browser receives the server warning)
    await setConfig({ InterstitialEnabled: false, ServerPushMode: 'Modal', WarningCooldownSeconds: 0 });
    await page.goto(`${BASE}/web/#/details?id=${E3}&serverId=${SERVER_ID}`, { waitUntil: 'domcontentloaded' });
    await page.locator('.btnPlay:visible').first().waitFor({ timeout: 30000 });
    await page.locator('.btnPlay:visible').first().click();
    await page.waitForSelector('.dialog:not(.hide) .dialogContentInner, .dialog .text', { timeout: 30000 });
    await sleep(800);
    await shot('server-dialog');
    await page.click('.dialog:not(.hide) button').catch(() => {});
    await sleep(500);

    // 4. Server toast
    await setConfig({ InterstitialEnabled: false, ServerPushMode: 'Toast', WarningCooldownSeconds: 0 });
    await page.goto(`${BASE}/web/#/home`, { waitUntil: 'domcontentloaded' });
    await sleep(1500);
    await page.goto(`${BASE}/web/#/details?id=${E3}&serverId=${SERVER_ID}`, { waitUntil: 'domcontentloaded' });
    await page.locator('.btnPlay:visible').first().waitFor({ timeout: 30000 });
    await page.locator('.btnPlay:visible').first().click();
    await page.waitForSelector('.toast', { timeout: 30000 });
    await sleep(600);
    await shot('server-toast');
    await page.goto(`${BASE}/web/#/home`, { waitUntil: 'domcontentloaded' });
    await sleep(1000);

    // 5. Config page
    await setConfig({ InterstitialEnabled: true, ServerPushMode: 'Toast', WarningCooldownSeconds: 60 });
    await page.waitForSelector('.toast', { state: 'detached', timeout: 30000 }).catch(() => {});
    await page.goto(`${BASE}/web/#/configurationpage?name=Episode%20Continuity`, { waitUntil: 'domcontentloaded' });
    await page.waitForSelector('#EpisodeContinuityConfigPage #ServerPushMode', { timeout: 30000 });
    await sleep(1200);
    await page.screenshot({ path: path.join(OUT, 'settings.png'), fullPage: true });
    console.log('saved settings');
  } catch (e) {
    console.error('FAILED:', e.message);
    await page.screenshot({ path: path.join(OUT, 'error.png') }).catch(() => {});
    process.exitCode = 1;
  } finally {
    await setConfig({}).catch(() => {});
    await browser.close();
  }
})();
