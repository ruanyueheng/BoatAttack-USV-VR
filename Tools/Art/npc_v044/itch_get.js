// usage: node itch_get.js <itchUrl> <outDir>  -- downloads every file on a free itch.io download page
const { chromium } = require('E:/University/Homework/虚拟现实作业/Tools/BrowserAutomation/node_modules/playwright');
(async () => {
  const [url, dir] = process.argv.slice(2);
  const b = await chromium.connectOverCDP('http://127.0.0.1:9222');
  const p = await b.contexts()[0].newPage();
  await p.goto(url, { waitUntil: 'domcontentloaded', timeout: 60000 });
  await p.waitForTimeout(3000);
  const btn = p.locator('a.download_btn, .buy_btn').first();
  if (await btn.count()) { await btn.click(); await p.waitForTimeout(3000); }
  const skip = p.locator('a.direct_download_btn'); if (await skip.count()) { await skip.click(); await p.waitForTimeout(5000); }
  const links = p.locator('.upload a.download_btn');
  for (let i = 0; i < await links.count(); i++) {
    const [d] = await Promise.all([p.waitForEvent('download', { timeout: 180000 }), links.nth(i).click()]);
    const f = dir + '/' + d.suggestedFilename(); await d.saveAs(f); console.log('saved', f);
  }
  await p.close(); await b.close();
})().catch(e => { console.error(e.message); process.exit(1); });
