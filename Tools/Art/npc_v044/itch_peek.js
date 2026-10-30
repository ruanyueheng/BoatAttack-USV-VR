// usage: node itch_peek.js <itchUrl> <shot.png>  -- opens the free download page and lists files
const { chromium } = require('E:/University/Homework/虚拟现实作业/Tools/BrowserAutomation/node_modules/playwright');
(async () => {
  const [url, out] = process.argv.slice(2);
  const b = await chromium.connectOverCDP('http://127.0.0.1:9222');
  const p = await b.contexts()[0].newPage();
  await p.goto(url, { waitUntil: 'domcontentloaded', timeout: 60000 });
  await p.waitForTimeout(3000);
  const btn = p.locator('a.download_btn, .buy_btn').first();
  if (await btn.count()) { await btn.click(); await p.waitForTimeout(3000); }
  const skip = p.locator('a.direct_download_btn'); if (await skip.count()) { await skip.click(); await p.waitForTimeout(5000); }
  await p.screenshot({ path: out });
  console.log(p.url()); console.log((await p.evaluate(() => [...document.querySelectorAll('.upload')].map(e => e.innerText.replace(/\s+/g,' ')).join('\n'))));
  await p.close(); await b.close();
})().catch(e => { console.error(e.message); process.exit(1); });
