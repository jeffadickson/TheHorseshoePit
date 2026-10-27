// Non-rendering report checks only: no navigation, resource loading, browser or network.
const fs=require('node:fs'),path=require('node:path'),assert=require('node:assert/strict');
const {test}=require('node:test');
const {JSDOM}=require(path.resolve('tools/overview-dom-tests/node_modules/jsdom'));
const folder=path.resolve('results/diff');
const data=JSON.parse(fs.readFileSync(path.join(folder,'summary.json'),'utf8'));
const html=fs.readFileSync(path.join(folder,'report.html'),'utf8');
const doc=new JSDOM(html).window.document;
test('all qualified summaries appear with matching numerical values',()=>{
  const rows=[...doc.querySelectorAll('tbody tr')];assert.equal(rows.length,data.length);
  for(let i=0;i<rows.length;i++){
    const cells=[...rows[i].cells].map(x=>x.textContent),entry=data[i];
    assert.equal(entry.N,7);assert.equal(cells[0],entry.Engine);
    // Two-decimal display tolerance avoids imposing JavaScript's tie rounding on .NET's formatter.
    assert.ok(Math.abs(Number(cells[1])-entry.Median)<=0.0050001);
    if(entry.Engine.startsWith('git-')){assert.equal(entry.PeakWorkingSetBytes,null);assert.equal(cells[4],'N/A†');}
    else assert.ok(Math.abs(Number(cells[4])-entry.PeakWorkingSetBytes/1048576)<=0.0050001);
    assert.equal(Number(cells[5].replaceAll(',','')),entry.PatchBytes);
    assert.equal(cells[6],`${entry.Added.toLocaleString('en-US')} / ${entry.Deleted.toLocaleString('en-US')}`);
  }
});
test('each table ranks one fixture and size without crossing groups',()=>{
  const groups=new Map();for(const row of data){const key=row.Fixture+'/'+row.Scale;if(!groups.has(key))groups.set(key,[]);groups.get(key).push(row);}
  assert.equal(doc.querySelectorAll('details').length,groups.size);
  for(const group of groups.values())for(let i=1;i<group.length;i++)assert.ok(group[i-1].Median<=group[i].Median);
});
test('offline evidence links resolve and page contains no executable client or external assets',()=>{
  assert.equal(doc.querySelectorAll('script,iframe,link[src],img[src]').length,0);
  for(const a of doc.querySelectorAll('a[href]'))assert.ok(fs.existsSync(path.resolve(folder,a.getAttribute('href'))));
  assert.ok(doc.querySelector('meta[name=viewport]'));assert.match(html,/@media print/);
  assert.match(doc.body.textContent,/not a confidence interval/);assert.match(doc.body.textContent,/not been visually verified/);
});
