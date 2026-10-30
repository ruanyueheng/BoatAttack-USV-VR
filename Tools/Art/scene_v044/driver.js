// usage: node driver.js <port>  — opens ONE new page in the shared Edge (CDP 9222) and holds it;
// POST JS bodies to http://127.0.0.1:<port>/ ; they run as async functions with (page, ctx, fs) and return JSON.
const { chromium } = require('E:/University/Homework/虚拟现实作业/Tools/BrowserAutomation/node_modules/playwright');
const http = require('http'); const fs = require('fs');
(async () => {
  const b = await chromium.connectOverCDP('http://127.0.0.1:9222');
  const ctx = b.contexts()[0]; const page = await ctx.newPage();
  http.createServer((req, res) => { let body = ''; req.on('data', d => body += d); req.on('end', async () => {
    try { const r = await (new Function('page', 'ctx', 'fs', 'return (async()=>{' + body + '})()'))(page, ctx, fs); res.end(JSON.stringify(r ?? null)); }
    catch (e) { res.statusCode = 500; res.end(String(e && e.stack || e)); }
    if (body.trim() === 'QUIT') { await page.close(); process.exit(0); }
  }); }).listen(+process.argv[2], '127.0.0.1', () => console.log('ready'));
})().catch(e => { console.error(e); process.exit(1); });
