// Non-rendering DOM tests only. No browser, navigation, resource loading or network access.
const fs=require('node:fs');
const path=require('node:path');
const vm=require('node:vm');
const assert=require('node:assert/strict');
const {test}=require('node:test');
const {JSDOM}=require(path.resolve('tools/overview-dom-tests/node_modules/jsdom'));
const root=process.cwd(),output=path.join(root,'results/overview-verification');
assert.ok(fs.statSync(path.dirname(output)).isDirectory());
if(!fs.existsSync(output))fs.mkdirSync(output);
const html=fs.readFileSync(path.join(root,'results/overview.html'),'utf8');
const parsed=new JSDOM(html);
const client=Array.from(parsed.window.document.querySelectorAll('script')).find(s=>!s.type).textContent;
const sourceFile=path.join(output,'overview-client.js');fs.writeFileSync(sourceFile,client);
const data=JSON.parse(parsed.window.document.getElementById('benchmark-data').textContent);
parsed.window.close();

// Exercise real DOM APIs, but make no claim about CSS layout or rendered screenshots.
function exercise(code,filename=sourceFile){
  const dom=new JSDOM(html,{runScripts:'outside-only'});
  try{
    vm.runInContext(code,dom.getInternalVMContext(),{filename});
    const w=dom.window,d=w.document,cohort=d.getElementById('cohort'),task=d.getElementById('workload'),size=d.getElementById('size');
    const change=(element,value)=>{element.value=String(value);element.dispatchEvent(new w.Event('change'));};
    let views=0;
    for(const group of data.Cohorts){
      change(cohort,group.Id);
      for(const operation of new Set(group.Cells.map(c=>c.Task))){
        change(task,operation);
        for(const bytes of new Set(group.Cells.filter(c=>c.Task===operation).map(c=>c.InputBytes))){
          change(size,bytes);
          const expected=group.Cells.filter(c=>c.Task===operation&&c.InputBytes===bytes).sort((a,b)=>a.Median-b.Median);
          const rows=Array.from(d.querySelectorAll('#result-table tr'));
          assert.equal(rows.length,expected.length,`${group.Id}/${operation}/${bytes}`);
          for(let i=0;i<rows.length;i++){
            assert.equal(rows[i].children[1].textContent,expected[i].Median.toLocaleString('en-US',{minimumFractionDigits:2,maximumFractionDigits:2}));
            assert.equal(rows[i].children[6].textContent,'7');
          }
          for(const bar of d.querySelectorAll('#chart rect'))assert.ok(Number.isFinite(Number(bar.getAttribute('width')))&&Number(bar.getAttribute('width'))>0);
          assert.ok(d.getElementById('chart').getAttribute('aria-label').includes(group.Title));
          if(operation==='sed-head')assert.match(d.getElementById('chart-note').textContent,/whole-file throughput is not applicable/);
          if(group.Id==='tuning')assert.ok(rows.every(r=>!r.textContent.includes('ripgrep')));
          views++;
        }
      }
    }
    assert.equal(d.querySelectorAll('.experiment').length,9);
    return views;
  }finally{dom.window.close();}
}

test('all cohort/workload/size views preserve session boundaries and sorted measured values',()=>{
  assert.equal(exercise(client),134);
});
test('static artifact is offline, complete, accessible and has valid local evidence links',()=>{
  const dom=new JSDOM(html),d=dom.window.document;
  try{
    assert.equal(data.Measured,2989);assert.equal(data.Warmups,427);
    assert.equal(data.Cohorts.flatMap(c=>c.Cells).length,427);
    assert.equal(d.querySelectorAll('script[src],link[rel="stylesheet"],iframe,img').length,0);
    assert.ok(!html.includes('__DATA__'));assert.ok(!html.includes('__MEASURED__'));
    assert.match(html,/@media\(max-width:650px\)/);assert.match(html,/@media print/);assert.match(html,/prefers-reduced-motion/);
    assert.ok(d.querySelector('html[lang="en"]'));assert.equal(d.querySelectorAll('h1').length,1);
    for(const link of d.querySelectorAll('a[href]')){
      const href=link.getAttribute('href');
      if(href.startsWith('#'))assert.ok(d.getElementById(href.slice(1)),href);
      else if(!/^https?:/.test(href)){
        const [file,fragment]=href.split('#'),target=path.resolve(root,'results',file);
        assert.ok(fs.existsSync(target),href);
        if(fragment){const linked=new JSDOM(fs.readFileSync(target,'utf8'));try{assert.ok(linked.window.document.getElementById(fragment),href);}finally{linked.window.close();}}
      }
    }
    const methods=d.getElementById('test-methods');assert.ok(methods);
    const text=methods.textContent;
    for(const term of ['126 measured invocations','18 warmups','--include-zero','-E','Round=-1','confidence intervals','PCRE2'])assert.ok(text.includes(term),term);
    assert.ok(d.getElementById('ripgrep-methods'));
  }finally{dom.window.close();}
});
test('deliberate descending-rank mutation is detected',()=>{
  const mutated=client.replace('.sort((a,b)=>a.Median-b.Median)','.sort((a,b)=>b.Median-a.Median)');
  assert.notEqual(mutated,client);assert.throws(()=>exercise(mutated,sourceFile+'.mutant-rank'));
});
test('deliberate cross-cohort row-filter mutation is detected',()=>{
  const mutated=client.replace('.filter(r=>r.Task===workload.value&&r.InputBytes===Number(size.value))','.filter(r=>true)');
  assert.notEqual(mutated,client);assert.throws(()=>exercise(mutated,sourceFile+'.mutant-filter'));
});
