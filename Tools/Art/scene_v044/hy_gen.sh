#!/bin/sh
# usage: hy_gen.sh <refPng-abs> <outGlb-abs>  — one Hunyuan image-to-3D (50k faces) on the driver page (port ${HY_PORT:-9311})
cd "$(dirname "$0")"
sh js.sh ${HY_PORT:-9311} <<JS
const credits=async()=>parseInt((await page.evaluate(()=>document.body.innerText)).match(/\n(\d+)\n/)?.[1]||'-1');
await page.goto('https://3d.hunyuan.tencent.com/creation',{waitUntil:'domcontentloaded'});await page.waitForTimeout(6000);
const before=await credits();
await page.locator('input[type=file]').first().setInputFiles('$1');await page.waitForTimeout(8000);
const l=page.getByText('50k',{exact:true});for(let i=0;i<await l.count();i++){if(await l.nth(i).isVisible()){await l.nth(i).click();break;}}
await page.getByText('立即生成').click();
// done = credit charged and the result panel (vertex count + download) is up
let ok=false;for(let i=0;i<90 && !ok;i++){await page.waitForTimeout(8000);const t=await page.evaluate(()=>document.body.innerText);ok=/顶点数/.test(t)&&!/生成中|排队/.test(t)&&(await credits())<before;}
await page.screenshot({path:'$2.png'});if(!ok)return 'not finished, credits '+before+'->'+(await credits());
const dl=page.waitForEvent('download',{timeout:120000}).catch(()=>null);
await page.getByText('下载',{exact:true}).last().click();await page.waitForTimeout(1200);await page.getByText('GLB',{exact:true}).last().click();
const d=await dl;if(!d)return 'no download';const r=await page.request.get(d.url());fs.writeFileSync('$2',await r.body());return r.status()+' credits '+before+'->'+(await credits())
JS
