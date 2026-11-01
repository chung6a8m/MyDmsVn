import test from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import * as oracle from './validate-corpus.mjs';
const fixture = name => JSON.parse(readFileSync(new URL(name, import.meta.url), 'utf8'));
const sale = () => ({operationKind:'Sales.Create',method:'POST',routeScope:'/api/v1/sales-receipts',requestTargetId:null,body:fixture('sales-create-request.json')});
test('canonicalization preserves exact numeric strings and normalizes missing note',()=>{
 const a=sale(); delete a.body.note;
 const b=structuredClone(a);b.body.note=null;b.body.lines[0].qty='01.25';
 assert.equal(oracle.canonicalizeRequest(a),oracle.canonicalizeRequest(b));
 assert.match(oracle.canonicalizeRequest(a),/"qty":"1.2500"/);
});
test('strict parser refuses duplicate decoded names and float integer tokens',()=>{
 assert.throws(()=>oracle.parseStrictJson('{"a":1,"\\u0061":2}'));
 assert.throws(()=>oracle.parseStrictJson('{"customerId":1.0}'));
 assert.deepEqual(oracle.parseStrictJson('{"customerId":101}'),{customerId:101});
});
const vectors=fixture('fingerprint-vectors.json').vectors;
for(const vector of vectors)test(`independent fingerprint: ${vector.name}`,()=>{
 assert.equal(oracle.canonicalizeRequest(vector.intent),vector.canonical);
 assert.equal(oracle.fingerprintRequest(vector.intent),vector.sha256);
});
for(const stem of ['sales-create','sales-update','sales-post','receipt-create','receipt-update','receipt-post'])test(`original outcome and protected identity: ${stem}`,()=>{
 const intent=fixture(stem+'-intent.json'),status=fixture(stem+'-status.json');
 assert.equal(oracle.validateOutcome(intent.operationKind,fixture(stem+'-response.json')),true);
 assert.equal(oracle.validateStatus(status,intent,{operationId:status.data.operationId,deploymentEpoch:status.data.deploymentEpoch}),true);
});
const baseStatus=()=>fixture('sales-create-status.json');
const identity=()=>({operationId:baseStatus().data.operationId,deploymentEpoch:baseStatus().data.deploymentEpoch});
for(const [name,change] of [
 ['wrong operation',d=>d.operationId='bbbbbbbb-bbbb-4ccc-8ddd-eeeeeeeeeeee'],
 ['wrong epoch',d=>d.deploymentEpoch='22222222-2222-4333-8444-555555555555'],
 ['wrong kind',d=>d.operationKind='Receipt.Create'],
 ['wrong method',d=>d.method='PUT'],
 ['wrong route',d=>d.routeScope='/api/v1/goods-receipts'],
 ['created ID mistaken for request target',d=>d.requestTargetId=d.outcome.data.saleId],
 ['wrong fingerprint',d=>d.requestFingerprint='0'.repeat(64)],
 ['missing fingerprint',d=>delete d.requestFingerprint],
 ['unknown future state',d=>d.state='PROCESSING'],
 ['extra future field',d=>d.future=true],
 ['truncated Write result',d=>delete d.outcome.data.lines],
 ['missing original version',d=>delete d.outcome.data.version],
 ['reordered Write result',d=>d.outcome.data.lines.reverse()],
 ['changed Write note',d=>d.outcome.data.note='newer document state'],
 ['changed gift',d=>d.outcome.data.lines[2].isGift=false],
])test(`status rejects ${name}`,()=>{
 const status=baseStatus();change(status.data);
 assert.throws(()=>oracle.validateStatus(status,sale(),identity()));
});
for(const state of ['result-expired','result-unavailable'])test(`${state} remains locked`,()=>{
 assert.equal(oracle.validateStatus(fixture('status-'+state+'.json'),sale(),identity()),false);
});
test('cached local fingerprint does not replace recomputation',()=>{
 const changed=sale();changed.body.note='corrupted local payload';
 assert.throws(()=>oracle.validateStatus(baseStatus(),changed,identity()),/fingerprint/);
});
test('post outcome version is operation-specific',()=>{
 const sales=fixture('sales-post-response.json');delete sales.data.version;
 assert.throws(()=>oracle.validateOutcome('Sales.Post',sales));
 const receipt=fixture('receipt-post-response.json');receipt.data.version='AAAAAAAAAAE=';
 assert.throws(()=>oracle.validateOutcome('Receipt.Post',receipt));
});
test('Receipt response retains explicit nulls and original default flags',()=>{
 const receipt=fixture('receipt-create-response.json');
 assert.equal(receipt.data.warehouseDisplayName,null);assert.equal(receipt.data.warehouseIsActive,true);
 delete receipt.data.warehouseDisplayName;assert.throws(()=>oracle.validateOutcome('Receipt.Create',receipt));
});
for(const raw of ['{"a":1,"a":2}','{"a":1,"\\u0061":2}','{"a":{"x":1,"x":2}}','{"customerId":1e2}','{"customerId":1.0}','{"customerId":-0}','{"a":9007199254740993}','{"a":"\\ud800"}','{"a":"\\udc00"}','\ufeff{}','{} trailing','{"a":NaN}','{"a":1,}','{"a":/*x*/1}'])test(`strict parser rejects ${JSON.stringify(raw)}`,()=>assert.throws(()=>oracle.parseStrictJson(raw)));
test('property order, escaping and whitespace do not change canonical hash',()=>{
 const a=sale(),raw=JSON.stringify(a,null,4).replace('saleDate','sale\\u0044ate');
 const b=oracle.parseStrictJson(raw);b.body=Object.fromEntries(Object.entries(b.body).reverse());
 assert.equal(oracle.fingerprintRequest(a),oracle.fingerprintRequest(b));
});
for(const [name,change] of [
 ['unknown body field',i=>i.body.future=true],['unknown line field',i=>i.body.lines[0].future=true],
 ['numeric int64',i=>i.body.lines[0].saleLineId=0],['nonzero Create line ID',i=>i.body.lines[0].saleLineId='1'],
 ['quantity numeric token',i=>i.body.lines[0].qty=1.25],['fifth fractional zero',i=>i.body.lines[0].qty='1.00000'],
 ['negative zero price',i=>i.body.lines[0].unitPrice='-0'],['quantity zero',i=>i.body.lines[0].qty='0'],
 ['int32 overflow',i=>i.body.customerId=2147483648],['invalid calendar date',i=>i.body.saleDate='2025-02-29'],
 ['NUL note',i=>i.body.note='\0'],['lone surrogate',i=>i.body.note='\ud800'],['long note',i=>i.body.note='x'.repeat(1001)],
 ['201 lines',i=>i.body.lines=Array.from({length:201},()=>i.body.lines[0])],['empty lines',i=>i.body.lines=[]],
 ['route case',i=>i.routeScope='/API/v1/sales-receipts'],['Create target',i=>i.requestTargetId='1'],
 ['extra operation metadata',i=>i.actorUserId=42],['unknown kind',i=>i.operationKind='Sales.Delete']
])test(`request rejects ${name}`,()=>{const i=sale();change(i);assert.throws(()=>oracle.canonicalizeRequest(i));});
test('Receipt duplicate product decodes but is never a successful Write outcome',()=>{
 const i=fixture('receipt-create-intent.json');i.body.lines[1].productId=i.body.lines[0].productId;
 assert.equal(oracle.validateRequest(i),true);
 const out=fixture('receipt-create-response.json');out.data.lines[1].productId=out.data.lines[0].productId;
 assert.throws(()=>oracle.validateOutcome('Receipt.Create',out));
});
const headers=()=>[['Idempotency-Key',identity().operationId],['X-MyDmsVn-Deployment-Epoch',identity().deploymentEpoch]];
test('epoch and key headers admit canonical values',()=>assert.equal(oracle.validateHeaders(headers(),'write',{deploymentEpoch:identity().deploymentEpoch}),true));
for(const [name,change] of [
 ['missing key',h=>h.shift()],['missing epoch',h=>h.pop()],['duplicate header',h=>h.push(['idempotency-key',h[0][1]])],
 ['comma joined key',h=>h[0][1]+=','+h[0][1]],['key whitespace',h=>h[0][1]=' '+h[0][1]],['uppercase key',h=>h[0][1]=h[0][1].toUpperCase()],
 ['nil key',h=>h[0][1]='00000000-0000-0000-0000-000000000000'],['nonv4 key',h=>h[0][1]='aaaaaaaa-bbbb-1ccc-8ddd-eeeeeeeeeeee'],
 ['epoch whitespace',h=>h[1][1]+=' '],['epoch changed',h=>h[1][1]='22222222-2222-4333-8444-555555555555']
])test(`headers reject ${name}`,()=>{const h=headers();change(h);assert.throws(()=>oracle.validateHeaders(h,'write',{deploymentEpoch:identity().deploymentEpoch}));});
for(const expected of [undefined,'A'.repeat(64),'0'.repeat(64),'0'.repeat(63),' '+baseStatus().data.requestFingerprint])test(`status expected fingerprint rejects ${expected}`,()=>{
 const h=[headers()[1]];if(expected!==undefined)h.push(['X-MyDmsVn-Operation-Fingerprint',expected]);
 assert.throws(()=>oracle.validateHeaders(h,'status',{requestFingerprint:baseStatus().data.requestFingerprint}));
});
test('protected expected fingerprint accepts exact match',()=>assert.equal(oracle.validateHeaders([headers()[1],['X-MyDmsVn-Operation-Fingerprint',baseStatus().data.requestFingerprint]],'status',{requestFingerprint:baseStatus().data.requestFingerprint}),true));
for(const row of fixture('fixture-index.json').filter(x=>x.kind==='error'))test(`bounded error ${row.file}`,()=>{
 const body=fixture(row.file);assert.equal(oracle.validateError(body,row.status),true);
 body.error.details.push({code:'leak',message:'private result'});
 if(body.error.code!=='ValidationError')assert.throws(()=>oracle.validateError(body,row.status));
});
test('context requires verified actor identity and current epoch',()=>{
 const c=fixture('context-response.json');assert.equal(oracle.validateContext(c),true);c.data.actorUserId='42';assert.throws(()=>oracle.validateContext(c));
});
import { mkdtempSync, cpSync, writeFileSync, rmSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { fileURLToPath } from 'node:url';
test('complete corpus validates bytes then semantic vectors',()=>{
 const result=oracle.validateCorpus();assert.ok(result.fixtures>0);assert.equal(result.fingerprintVectors,vectors.length);
});
test('consumer detects mutated golden bytes before semantic use',()=>{
 const temporary=mkdtempSync(join(tmpdir(),'web4-corpus-'));
 try {cpSync(fileURLToPath(new URL('.',import.meta.url)),temporary,{recursive:true});
  writeFileSync(join(temporary,'sales-post-response.json'),'{"data":{"saleId":"1"}}\n');
  assert.throws(()=>oracle.validateCorpus(temporary),/Hash mismatch/);
 } finally {rmSync(temporary,{recursive:true,force:true});}
});
for(const [name,change] of [
 ['missing unit',d=>delete d.lines[0].unit],['missing Write label',d=>delete d.customerDisplayName],
 ['inactive Write reference',d=>d.customerIsActive=false],['missing product label',d=>delete d.lines[0].productDisplayName],
 ['empty unit',d=>d.lines[0].unit='']
])test(`original Sales Write rejects ${name}`,()=>{const out=fixture('sales-create-response.json');change(out.data);assert.throws(()=>oracle.validateOutcome('Sales.Create',out));});
test('Receipt original Write rejects enriched Get labels',()=>{const out=fixture('receipt-create-response.json');out.data.warehouseDisplayName='Later catalog label';assert.throws(()=>oracle.validateOutcome('Receipt.Create',out));});
test('source-bounded 200-line Sales Write fits protected replay byte cap',(t)=>{
 const out=fixture('sales-create-response.json');const d=out.data;
 d.note='\u0001'.repeat(1000);
 for(const k of ['customerDisplayName','warehouseDisplayName','employeeDisplayName'])d[k]='\u0001'.repeat(291);
 d.lines=Array.from({length:200},(_,index)=>({...d.lines[0],saleLineId:String(9000+index),lineNo:index+1,unit:'\u0001'.repeat(32),productDisplayName:'\u0001'.repeat(291),qty:'1.0000',unitPrice:'1.0000',lineAmount:'1.0000'}));
 d.totalAmount='200.0000';assert.equal(oracle.validateOutcome('Sales.Create',out),true);
 const byteLength=Buffer.byteLength(JSON.stringify(out),'utf8');
 t.diagnostic(`Sales worst-case constructed response: ${byteLength} UTF-8 bytes`);
 assert.ok(byteLength<1048576,`Protected replay requires ${byteLength} bytes`);
 assert.ok(byteLength>400000,'Control escapes must be measured as serialized UTF-8 bytes');
});
test('source-bounded 200-line Receipt Write fits protected replay byte cap',()=>{
 const out=fixture('receipt-create-response.json');out.data.note='\u0001'.repeat(1000);
 out.data.lines=Array.from({length:200},(_,index)=>({...out.data.lines[0],receiptLineId:String(9000+index),lineNumber:index+1,productId:index+1,quantity:'99999999999999.9999',unitCost:'999999999999999.9999'}));
 assert.equal(oracle.validateOutcome('Receipt.Create',out),true);
 assert.ok(Buffer.byteLength(JSON.stringify(out),'utf8')<1048576);
});
for(const name of ['x-mydmsvn-deployment-epoch','X-MyDmsVn-Deployment-Epoch'])test(`duplicate epoch header rejected: ${name}`,()=>{
 const h=headers();h.push([name,h[1][1]]);assert.throws(()=>oracle.validateHeaders(h,'write'));
});
test('Receipt does not inherit permissive Sales expectedVersion',()=>{
 const i=fixture('receipt-post-intent.json');i.body.expectedVersion=' '+i.body.expectedVersion;
 assert.throws(()=>oracle.validateRequest(i));
});
test('Sales outcome rejects label exceeding SQL source maximum',()=>{
 const out=fixture('sales-create-response.json');out.data.lines[0].productDisplayName='x'.repeat(292);
 assert.throws(()=>oracle.validateOutcome('Sales.Create',out));
});
for(const value of [123,true,null])test(`error correlationId rejects nonstring ${JSON.stringify(value)}`,()=>{
 const payload=fixture('error-auth-forbidden.json');payload.error.correlationId=value;
 assert.throws(()=>oracle.validateError(payload,403));
});
