// usage: node gpt_image.js <promptFile> <outPng>
// Reuses the user's already-open chatgpt.com/images tab (opening new tabs is blocked), submits one prompt,
// waits for the generated image, saves it, and returns the tab to /images.
const { chromium } = require('E:/University/Homework/虚拟现实作业/Tools/BrowserAutomation/node_modules/playwright');
const fs = require('fs');
(async () => {
  const [promptFile, out] = process.argv.slice(2);
  const prompt = fs.readFileSync(promptFile, 'utf8').trim();
  const b = await chromium.connectOverCDP('http://127.0.0.1:9222');
  const p = b.contexts().flatMap(c => c.pages()).find(x => x.url().includes('chatgpt.com/images'));
  if (!p) throw new Error('chatgpt images tab not found');
  if (!p.url().includes('/images')) { await p.goto('https://chatgpt.com/images'); await p.waitForTimeout(4000); }
  const listImgs = () => p.evaluate(() => [...document.querySelectorAll('img')].filter(i => i.naturalWidth >= 512).map(i => i.src));
  const before = new Set(await listImgs());
  const box = p.locator('[contenteditable=true]').first();
  await box.click(); await p.keyboard.insertText(prompt); await p.waitForTimeout(500); await p.keyboard.press('Enter');
  let src = null;
  for (let t = 0; t < 120 && !src; t++) {
    await p.waitForTimeout(5000);
    const now = (await listImgs()).filter(s => !before.has(s) && !s.startsWith('data:'));
    // the final image stays stable for two polls; partial previews are replaced while streaming
    if (now.length) { await p.waitForTimeout(8000); const again = (await listImgs()).filter(s => !before.has(s)); src = again[again.length - 1]; }
  }
  if (!src) { await p.screenshot({ path: out.replace(/\.png$/, '-timeout.png') }); throw new Error('no image within 10 minutes'); }
  const b64 = await p.evaluate(async s => { const r = await fetch(s); const a = new Uint8Array(await r.arrayBuffer()); let x = ''; for (const v of a) x += String.fromCharCode(v); return btoa(x); }, src);
  fs.writeFileSync(out, Buffer.from(b64, 'base64'));
  console.log('saved', out, p.url());
  await p.goto('https://chatgpt.com/images');
  await b.close();
})().catch(e => { console.error(e.message); process.exit(1); });
