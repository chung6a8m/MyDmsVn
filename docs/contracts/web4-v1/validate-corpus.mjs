// Test-only contract oracle. This does not implement or call an HTTP endpoint.
import assert from 'node:assert/strict';
import { readFileSync, readdirSync } from 'node:fs';
import { createHash } from 'node:crypto';
import { resolve, dirname } from 'node:path';
import { fileURLToPath } from 'node:url';

const longMax = 9223372036854775807n;
const fail = (message) => { throw new Error(message); };
const requireValue = (condition, message) => { if (!condition) fail(message); };
const plain = (value) => value !== null && typeof value === 'object' && !Array.isArray(value);
function shape(value, required, optional = []) {
  requireValue(plain(value), 'Expected object');
  for (const key of required) requireValue(Object.hasOwn(value, key), `Missing ${key}`);
  for (const key of Object.keys(value)) requireValue(required.includes(key) || optional.includes(key), `Unknown ${key}`);
}
function text(value, maximum = Infinity) {
  requireValue(typeof value === 'string' && value.length <= maximum && value.isWellFormed() && !value.includes('\0'), 'Invalid text');
}
function nullableText(value, maximum = Infinity) { if (value !== null) text(value, maximum); }
function int(value, maximum = 2147483647) { requireValue(Number.isInteger(value) && value > 0 && value <= maximum, 'Invalid int32'); }
function long(value, zero = false) {
  requireValue(typeof value === 'string' && value.length <= 19 && /^(0|[1-9][0-9]*)$/.test(value), 'Invalid int64 string');
  const result = BigInt(value);
  requireValue(result >= (zero ? 0n : 1n) && result <= longMax, 'Int64 out of range');
  return result;
}
function decimal(value, precision, scale, { signed = false, positive = false } = {}) {
  requireValue(typeof value === 'string' && value.length <= 64, 'Expected decimal string');
  const pattern = new RegExp(`^${signed ? '-?' : ''}[0-9]+(?:\\.[0-9]{1,${scale}})?$`);
  requireValue(pattern.test(value), 'Invalid decimal grammar/scale');
  const negative = value.startsWith('-');
  const [whole, fraction = ''] = (negative ? value.slice(1) : value).split('.');
  const magnitude = BigInt(whole) * 10n ** BigInt(scale) + BigInt(fraction.padEnd(scale, '0'));
  requireValue(magnitude <= 10n ** BigInt(precision) - 1n && (!positive || magnitude > 0n), 'Decimal out of range');
  requireValue(!negative || magnitude !== 0n, 'Negative zero is not canonical output');
  return negative ? -magnitude : magnitude;
}
function date(value, allowDefault = false) {
  requireValue(typeof value === 'string' && /^[0-9]{4}-[0-9]{2}-[0-9]{2}$/.test(value), 'Invalid calendar date');
  const [year, month, day] = value.split('-').map(Number);
  const leap = year % 4 === 0 && (year % 100 !== 0 || year % 400 === 0);
  const days = [31, leap ? 29 : 28, 31, 30, 31, 30, 31, 31, 30, 31, 30, 31];
  requireValue(year >= 1 && month >= 1 && month <= 12 && day >= 1 && day <= days[month - 1] && (allowDefault || value !== '0001-01-01'), 'Invalid calendar date');
}
function utc(value) {
  requireValue(typeof value === 'string' && /^[0-9]{4}-[0-9]{2}-[0-9]{2}T[0-9]{2}:[0-9]{2}:[0-9]{2}(?:\.[0-9]{1,7})?Z$/.test(value), 'Invalid UTC timestamp');
  date(value.slice(0, 10), true);
  requireValue(Number(value.slice(11, 13)) < 24 && Number(value.slice(14, 16)) < 60 && Number(value.slice(17, 19)) < 60, 'Invalid UTC time');
  // Lexically sortable key retains all seven datetime2 fractional digits, unlike JS Date.
  return value.slice(0, 19) + '.' + (value.slice(19, -1).replace(/^\./, '')).padEnd(7, '0');
}
function version(value) {
  requireValue(typeof value === 'string' && /^[A-Za-z0-9+/]{11}=$/.test(value) && Buffer.from(value, 'base64').length === 8 && Buffer.from(value, 'base64').toString('base64') === value, 'Invalid opaque version');
}
function boolean(value) { requireValue(typeof value === 'boolean', 'Expected boolean'); }
function receiptNumber(value) {
  requireValue(typeof value === 'string' && /^GR[0-9]{10}$/.test(value), 'Invalid receipt number');
  const number = long(BigInt(value.slice(2)).toString());
  requireValue(value === 'GR' + number.toString().padStart(10, '0'), 'Noncanonical receipt number');
}
function lines(values, detailed) {
  requireValue(Array.isArray(values) && values.length >= 1 && values.length <= 200, 'Receipt requires 1..200 lines');
  const products = new Set(), identities = new Set();
  values.forEach((line, index) => {
    shape(line, detailed ? ['receiptLineId', 'lineNumber', 'productId', 'quantity', 'unitCost', 'productDisplayName', 'productIsActive'] : ['productId', 'quantity', 'unitCost']);
    int(line.productId); decimal(line.quantity, 18, 4, { positive: true }); decimal(line.unitCost, 19, 4);
    requireValue(!products.has(line.productId), 'Duplicate Product'); products.add(line.productId);
    if (detailed) {
      long(line.receiptLineId); requireValue(!identities.has(line.receiptLineId), 'Duplicate line ID'); identities.add(line.receiptLineId);
      int(line.lineNumber, 200); requireValue(line.lineNumber === index + 1, 'Noncontiguous or unordered lines');
      nullableText(line.productDisplayName); boolean(line.productIsActive);
    }
  });
}
function detail(value) {
  shape(value, ['receiptId', 'receiptNumber', 'receiptDate', 'warehouseId', 'warehouseDisplayName', 'warehouseIsActive', 'employeeId', 'employeeDisplayName', 'employeeIsActive', 'status', 'note', 'postedAtUtc', 'version', 'lines']);
  long(value.receiptId); receiptNumber(value.receiptNumber); date(value.receiptDate); int(value.warehouseId); int(value.employeeId);
  nullableText(value.warehouseDisplayName); nullableText(value.employeeDisplayName); boolean(value.warehouseIsActive); boolean(value.employeeIsActive);
  requireValue(['Draft', 'Posted'].includes(value.status), 'Invalid receipt status'); nullableText(value.note, 1000); version(value.version); lines(value.lines, true);
  if (value.status === 'Draft') requireValue(value.postedAtUtc === null, 'Draft cannot have posting timestamp'); else utc(value.postedAtUtc);
}
// JSON.parse alone loses duplicate members and accepts lone UTF-16 surrogates.
export function parseStrictJson(source) {
  const integerTokensOnly = true;
  requireValue(typeof source === 'string' && source.isWellFormed() && !source.startsWith('\uFEFF'), 'Invalid JSON Unicode/BOM');
  let index = 0;
  const space = () => { while (/[\t\n\r ]/.test(source[index] ?? 'x')) index++; };
  const string = () => {
    requireValue(source[index] === '"', 'Expected JSON string'); const start = index++;
    while (index < source.length) {
      if (source[index] === '\\') { index += 2; continue; }
      if (source[index++] === '"') { const result = JSON.parse(source.slice(start, index)); requireValue(result.isWellFormed(), 'Unpaired surrogate'); return result; }
    }
    fail('Unterminated JSON string');
  };
  const value = (depth) => {
    requireValue(depth <= 15, 'JSON nesting limit'); space();
    if (source[index] === '"') { string(); return; }
    if (source[index] === '{') {
      index++; space(); const keys = new Set(); if (source[index] === '}') { index++; return; }
      while (true) { space(); const key = string(); requireValue(!keys.has(key), 'Duplicate JSON key'); keys.add(key); space(); requireValue(source[index++] === ':', 'Expected colon'); value(depth + 1); space(); const delimiter = source[index++]; if (delimiter === '}') return; requireValue(delimiter === ',', 'Expected comma'); }
    }
    if (source[index] === '[') {
      index++; space(); if (source[index] === ']') { index++; return; }
      while (true) { value(depth + 1); space(); const delimiter = source[index++]; if (delimiter === ']') return; requireValue(delimiter === ',', 'Expected comma'); }
    }
    const token = (integerTokensOnly ? /^(?:true|false|null|-?(?:0|[1-9][0-9]*))/ : /^(?:true|false|null|-?(?:0|[1-9][0-9]*)(?:\.[0-9]+)?(?:[eE][+-]?[0-9]+)?)/).exec(source.slice(index));
    requireValue(token && (!integerTokensOnly || token[0] !== '-0'), 'Invalid JSON scalar'); if (/^-?[0-9]/.test(token[0])) requireValue(Number.isSafeInteger(Number(token[0])), 'Unsafe numeric token'); index += token[0].length;
  };
  value(0); space(); requireValue(index === source.length, 'Trailing JSON/float/exponent token'); return JSON.parse(source);
}
// The following codec is test-only. Product code must implement the frozen protocol independently.
export const operations = Object.freeze({
  'Sales.Create': ['POST','/api/v1/sales-receipts', 'Sales.Receipts.Write'],
  'Sales.Update': ['PUT','/api/v1/sales-receipts/{saleId}/draft', 'Sales.Receipts.Write'],
  'Sales.Post': ['POST','/api/v1/sales-receipts/{saleId}/post', 'Sales.Receipts.Post'],
  'Receipt.Create': ['POST','/api/v1/goods-receipts', 'Inventory.GoodsReceipts.Write'],
  'Receipt.Update': ['PUT','/api/v1/goods-receipts/{receiptId}/draft', 'Inventory.GoodsReceipts.Write'],
  'Receipt.Post': ['POST','/api/v1/goods-receipts/{receiptId}/post', 'Inventory.GoodsReceipts.Post'],
});
// Sales currently uses Convert.FromBase64String: whitespace and nonzero unused pad bits decode.
function salesVersion(value) {
  text(value,128); const compact=value.replace(/[ \t\r\n]/g,'');
  requireValue(/^[A-Za-z0-9+/]{11}=$/.test(compact),'Invalid Sales version');
  const bytes=Buffer.from(compact,'base64'); requireValue(bytes.length===8,'Invalid Sales version');
  return bytes.toString('base64');
}
function fixed(value, positive=false) {
  const n=decimal(value,positive?18:19,4,{positive});
  return `${n/10000n}.${(n%10000n).toString().padStart(4,'0')}`;
}
function salesSave(value,update) {
  shape(value,['saleDate','customerId','warehouseId','employeeId','lines',...(update?['expectedVersion']:[])],['note']);
  date(value.saleDate);int(value.customerId);int(value.warehouseId);int(value.employeeId);
  if(Object.hasOwn(value,'note')) nullableText(value.note,1000);
  if(update) salesVersion(value.expectedVersion);
  requireValue(Array.isArray(value.lines)&&value.lines.length>=1&&value.lines.length<=200,'Sales requires 1..200 lines');
  for(const line of value.lines) {
    shape(line,['saleLineId','productId','qty','unitPrice','isGift']);
    long(line.saleLineId,true);requireValue(update||line.saleLineId==='0','Create line ID must be zero');
    int(line.productId);decimal(line.qty,18,4,{positive:true});decimal(line.unitPrice,19,4);boolean(line.isGift);
  }
}
export function validateRequest(intent) {
  shape(intent,['operationKind','method','routeScope','requestTargetId','body']);
  requireValue(Object.hasOwn(operations,intent.operationKind),'Unknown operation kind');
  const [method,route]=operations[intent.operationKind];
  requireValue(intent.method===method&&intent.routeScope===route,'Method/route/kind mismatch');
  const create=intent.operationKind.endsWith('.Create'),post=intent.operationKind.endsWith('.Post'),sales=intent.operationKind.startsWith('Sales.');
  if(create) requireValue(intent.requestTargetId===null,'Create original target must be null'); else long(intent.requestTargetId);
  if(post) {shape(intent.body,['expectedVersion']);if(sales)salesVersion(intent.body.expectedVersion);else version(intent.body.expectedVersion);}
  else if(sales)salesSave(intent.body,!create);
  else {
    // Receipt duplicate Product is dynamic Application validation, not strict HTTP wire decoding.
    const copy=structuredClone(intent.body);
    shape(copy,['receiptDate','warehouseId','employeeId','lines',...(!create?['expectedVersion']:[])],['note']);
    date(copy.receiptDate);int(copy.warehouseId);int(copy.employeeId);if(Object.hasOwn(copy,'note'))nullableText(copy.note,1000);
    if(!create)version(copy.expectedVersion);
    requireValue(Array.isArray(copy.lines)&&copy.lines.length>=1&&copy.lines.length<=200,'Receipt requires 1..200 lines');
    for(const line of copy.lines){shape(line,['productId','quantity','unitCost']);int(line.productId);decimal(line.quantity,18,4,{positive:true});decimal(line.unitCost,19,4);}
  }
  return true;
}
// Object keys in the contract are ASCII; do not locale-sort. JSON escaping follows ECMAScript JSON.stringify.
function canonicalJson(value) {
  if(Array.isArray(value))return '['+value.map(canonicalJson).join(',')+']';
  if(plain(value))return '{'+Object.keys(value).sort().map(k=>JSON.stringify(k)+':'+canonicalJson(value[k])).join(',')+'}';
  return JSON.stringify(value);
}
export function canonicalizeRequest(intent) {
  validateRequest(intent);
  const copy=structuredClone(intent),body=copy.body,sales=copy.operationKind.startsWith('Sales.');
  if(!copy.operationKind.endsWith('.Post')) {
    body.note=body.note??null;
    for(const line of body.lines) {
      const qty=sales?'qty':'quantity',price=sales?'unitPrice':'unitCost';
      line[qty]=fixed(line[qty],true);line[price]=fixed(line[price]);
    }
  }
  if(Object.hasOwn(body,'expectedVersion')&&sales)body.expectedVersion=salesVersion(body.expectedVersion);
  return canonicalJson({contract:'web4-v1',...copy});
}
export function fingerprintRequest(intent) {return createHash('sha256').update(canonicalizeRequest(intent),'utf8').digest('hex');}
function guid(value,v4=false) {
  requireValue(typeof value==='string'&&/^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/.test(value)&&value!=='00000000-0000-0000-0000-000000000000','Invalid GUID');
  if(v4)requireValue(value[14]==='4'&&/[89ab]/.test(value[19]),'Expected RFC4122 GUIDv4');
}
function hash(value){requireValue(typeof value==='string'&&/^[a-f0-9]{64}$/.test(value),'Invalid SHA256');}
export function validateHeaders(pairs,mode,{deploymentEpoch,requestFingerprint}={}) {
  requireValue(['write','status','context'].includes(mode)&&Array.isArray(pairs),'Invalid header mode');
  const headers=new Map();
  for(const pair of pairs){requireValue(Array.isArray(pair)&&pair.length===2&&pair.every(x=>typeof x==='string'),'Header pairs required');const key=pair[0].toLowerCase();requireValue(!headers.has(key),'Duplicate header');headers.set(key,pair[1]);}
  if(mode!=='context') {
    const epoch=headers.get('x-mydmsvn-deployment-epoch');guid(epoch);
    if(deploymentEpoch!==undefined)requireValue(epoch===deploymentEpoch,'Deployment.EpochMismatch');
  }
  if(mode==='write')guid(headers.get('idempotency-key'),true);
  if(mode==='status') {const expected=headers.get('x-mydmsvn-operation-fingerprint');hash(expected);if(requestFingerprint!==undefined)requireValue(expected===requestFingerprint,'Idempotency.FingerprintMismatch');}
  // Authorization/cookie handling belongs to the HTTP session pipeline, never these test headers.
  return true;
}
export function validateContext(payload) {
  shape(payload,['data']);shape(payload.data,['deploymentEpoch','actorUserId','contractVersion']);
  guid(payload.data.deploymentEpoch);int(payload.data.actorUserId);requireValue(payload.data.contractVersion==='web4-v1','Unsupported contract');return true;
}
function salesDetail(value) {
  shape(value,['saleId','saleNumber','saleDate','customerId','warehouseId','employeeId','status','version','lines','totalAmount','customerIsActive','warehouseIsActive','employeeIsActive','customerDisplayName','warehouseDisplayName','employeeDisplayName'],['note']);
  long(value.saleId);requireValue(/^SA[0-9]{10}$/.test(value.saleNumber)&&value.saleNumber!=='SA0000000000','Invalid sale number');date(value.saleDate);
  for(const key of ['customerId','warehouseId','employeeId'])int(value[key]);
  for(const key of ['customerIsActive','warehouseIsActive','employeeIsActive'])requireValue(value[key]===true,'Original Write reference must be active');
  for(const key of ['note','customerDisplayName','warehouseDisplayName','employeeDisplayName'])if(Object.hasOwn(value,key))text(value[key],key==='note'?1000:291);
  requireValue(value.status==='Draft','Write result must be original Draft');version(value.version);decimal(value.totalAmount,28,4);
  requireValue(Array.isArray(value.lines)&&value.lines.length>=1&&value.lines.length<=200,'Sales detail line limit');
  const ids=new Set();let total=0n;
  value.lines.forEach((line,index)=>{
    shape(line,['saleLineId','lineNo','productId','qty','unitPrice','isGift','lineAmount','productIsActive','unit','productDisplayName']);
    long(line.saleLineId);requireValue(!ids.has(line.saleLineId),'Duplicate Sales line ID');ids.add(line.saleLineId);
    requireValue(line.lineNo===index+1,'Sales line order mismatch');int(line.productId);boolean(line.isGift);requireValue(line.productIsActive===true,'Original Write product must be active');
    text(line.unit,32);requireValue(line.unit.trim().length>0,'Original Write unit required');text(line.productDisplayName,291);
    const q=decimal(line.qty,18,4,{positive:true}),p=decimal(line.unitPrice,19,4),amount=decimal(line.lineAmount,28,4);
    const product=q*p,expected=line.isGift?0n:product/10000n+(product%10000n>=5000n?1n:0n);
    requireValue(amount===expected,'Sales amount/gift mismatch');total+=amount;
  });
  requireValue(decimal(value.totalAmount,28,4)===total,'Sales total mismatch');
}
export function validateOutcome(kind,payload) {
  requireValue(Object.hasOwn(operations,kind),'Unknown operation');shape(payload,['data']);
  const sales=kind.startsWith('Sales.');
  if(kind.endsWith('.Post')) {
    shape(payload.data,[sales?'saleId':'receiptId','status','postedAtUtc',...(sales?['version']:[])]);
    long(payload.data[sales?'saleId':'receiptId']);requireValue(payload.data.status==='Posted','Post result must be Posted');utc(payload.data.postedAtUtc);if(sales)version(payload.data.version);
  } else if(sales)salesDetail(payload.data);
  else {detail(payload.data);requireValue(payload.data.status==='Draft','Write result must be original Draft');
    for(const key of ['warehouse','employee'])requireValue(payload.data[key+'DisplayName']===null&&payload.data[key+'IsActive']===true,'Original Receipt Write reference defaults');
    for(const line of payload.data.lines)requireValue(line.productDisplayName===null&&line.productIsActive===true,'Original Receipt Write line defaults');
  }
  return true;
}
export function validateStatus(payload,intent,identity) {
  validateRequest(intent);shape(identity,['operationId','deploymentEpoch']);guid(identity.operationId,true);guid(identity.deploymentEpoch);
  shape(payload,['data']);const d=payload.data;
  shape(d,['deploymentEpoch','operationId','operationKind','method','routeScope','requestTargetId','requestFingerprint','state','outcome']);
  guid(d.operationId,true);guid(d.deploymentEpoch);hash(d.requestFingerprint);
  for(const key of ['operationId','deploymentEpoch'])requireValue(d[key]===identity[key],`Status ${key} mismatch`);
  for(const key of ['operationKind','method','routeScope','requestTargetId'])requireValue(d[key]===intent[key],`Status ${key} mismatch`);
  requireValue(d.requestFingerprint===fingerprintRequest(intent),'Status fingerprint mismatch');
  requireValue(['COMMITTED','RESULT_EXPIRED','RESULT_UNAVAILABLE'].includes(d.state),'Unknown state');
  if(d.state!=='COMMITTED'){requireValue(d.outcome===null,'Unavailable outcome must be null');return false;}
  validateOutcome(intent.operationKind,d.outcome);
  const result=d.outcome.data,id=intent.operationKind.startsWith('Sales.')?'saleId':'receiptId';
  if(intent.requestTargetId!==null)requireValue(result[id]===intent.requestTargetId,'Outcome target mismatch');
  if(!intent.operationKind.endsWith('.Post')){
    const sales=intent.operationKind.startsWith('Sales.');
    for(const key of [sales?'saleDate':'receiptDate','warehouseId','employeeId',...(sales?['customerId']:[])])requireValue(result[key]===intent.body[key],'Outcome request field mismatch');
    requireValue((result.note??null)===(intent.body.note??null),'Outcome note mismatch');
    requireValue(result.lines.length===intent.body.lines.length,'Outcome cardinality mismatch');
    result.lines.forEach((line,index)=>{
      const sent=intent.body.lines[index];requireValue(line.productId===sent.productId,'Outcome product/order mismatch');
      const q=sales?'qty':'quantity',p=sales?'unitPrice':'unitCost';
      requireValue(fixed(line[q],true)===fixed(sent[q],true)&&fixed(line[p])===fixed(sent[p]),'Outcome quantity/price mismatch');
      if(sales){requireValue(line.isGift===sent.isGift,'Outcome gift mismatch');if(sent.saleLineId!=='0')requireValue(line.saleLineId===sent.saleLineId,'Outcome Sales line ID mismatch');}
    });
  }
  return true;
}
export const errorStatuses = Object.freeze({
 'ValidationError':400,'Auth.Unauthorized':401,'Auth.Forbidden':403,
 'Deployment.EpochMismatch':409,'Idempotency.FingerprintMismatch':409,'Idempotency.KeyConflict':409,
 'Operation.NotObserved':404,'Operation.ResultExpired':410,'Operation.ResultUnavailable':503,
 'Operation.Unavailable':503,'InternalError':500,
});
export function validateError(payload,status) {
 shape(payload,['error']);shape(payload.error,['code','message','details'],['correlationId']);const e=payload.error;
 requireValue(Object.hasOwn(errorStatuses,e.code)&&errorStatuses[e.code]===status,'Error status/code mismatch');text(e.message,256);
 requireValue(Array.isArray(e.details),'Error details required');
 if(e.code!=='ValidationError')requireValue(e.details.length===0,'Protected errors must not disclose details');
 for(const issue of e.details){shape(issue,['code','message'],['field']);text(issue.code,128);text(issue.message,256);if(Object.hasOwn(issue,'field'))text(issue.field,128);}
 if(Object.hasOwn(e,'correlationId'))requireValue(typeof e.correlationId==='string'&&/^[A-Za-z0-9_-]{1,128}$/.test(e.correlationId),'Invalid correlation');return true;
}

function readStrictFile(directory,file) {
  requireValue(file==='.gitattributes'||/^[a-z0-9][a-z0-9.-]*\.(json|mjs|py|md)$/.test(file)||file==='README.md','Unsafe corpus path');
  const bytes=readFileSync(resolve(directory,file));
  requireValue(!bytes.subarray(0,3).equals(Buffer.from([239,187,191])),'Corpus BOM forbidden');
  const source=new TextDecoder('utf-8',{fatal:true}).decode(bytes);requireValue(!source.includes('\r'),'Corpus must use LF');
  return {bytes,source};
}
export function validateCorpus(directory=dirname(fileURLToPath(import.meta.url))) {
  const manifest=parseStrictJson(readStrictFile(directory,'manifest.json').source);
  shape(manifest,['contractVersion','sourceBaseline','fixtures','artifacts']);
  requireValue(manifest.contractVersion==='web4-v1'&&/^[a-f0-9]{40}$/.test(manifest.sourceBaseline),'Invalid provenance');
  requireValue(Array.isArray(manifest.artifacts)&&Array.isArray(manifest.fixtures),'Invalid manifest');
  const files=new Map();
  for(const item of manifest.artifacts){
    shape(item,['file','sha256']);hash(item.sha256);requireValue(!files.has(item.file)&&item.file!=='manifest.json','Duplicate/self manifest artifact');
    const {bytes,source}=readStrictFile(directory,item.file);
    requireValue(createHash('sha256').update(bytes).digest('hex')===item.sha256,`Hash mismatch: ${item.file}`);files.set(item.file,source);
  }
  for(const entry of readdirSync(directory,{withFileTypes:true})){
    requireValue(entry.isFile()&&!entry.isSymbolicLink(),'Unexpected directory/link in corpus');
    requireValue(entry.name==='manifest.json'||files.has(entry.name),`Unmanifested corpus file: ${entry.name}`);
  }
  const json=file=>{requireValue(files.has(file),'Unmanifested fixture reference');return parseStrictJson(files.get(file));};
  assert.deepEqual(manifest.fixtures,json('fixture-index.json'),'Manifest fixture metadata drift');
  const fixtureFiles=new Set(),kinds=new Set();let statuses=0;
  for(const row of manifest.fixtures){
    shape(row,['file','kind'],['intent','status','operationKind']);requireValue(!fixtureFiles.has(row.file),'Duplicate fixture');fixtureFiles.add(row.file);
    const value=json(row.file);
    if(row.kind==='intent'){validateRequest(value);kinds.add(value.operationKind);}
    else if(row.kind==='body'){const intent=json(row.intent);validateRequest(intent);assert.deepEqual(value,intent.body);requireValue(row.operationKind===intent.operationKind,'Body kind mismatch');}
    else if(row.kind==='outcome'){requireValue(row.status===200,'Outcome HTTP status');validateOutcome(row.operationKind,value);}
    else if(row.kind==='status'){
      requireValue(row.status===200,'Protected status HTTP status');const intent=json(row.intent);
      const settled=validateStatus(value,intent,{operationId:value.data.operationId,deploymentEpoch:json('context-response.json').data.deploymentEpoch});
      requireValue(settled===(value.data.state==='COMMITTED'),'Unexpected settlement');
      if(settled)assert.deepEqual(value.data.outcome,json(row.intent.replace('-intent.json','-response.json')),'Status must retain exact original outcome');statuses++;
    }
    else if(row.kind==='context'){requireValue(row.status===200,'Context HTTP status');validateContext(value);}
    else if(row.kind==='error')validateError(value,row.status);
    else fail('Unknown fixture kind');
  }
  assert.deepEqual([...kinds].sort(),Object.keys(operations).sort(),'Missing operation semantics');
  const controls=json('fingerprint-vectors.json');shape(controls,['algorithm','independentAuthor','vectors']);requireValue(controls.algorithm==='SHA-256','Fingerprint algorithm');
  const names=new Set();let expandedLines=0;
  for(const v of controls.vectors){
    shape(v,['name','intent','canonical','sha256']);text(v.name);requireValue(!names.has(v.name),'Duplicate vector');names.add(v.name);hash(v.sha256);
    assert.equal(canonicalizeRequest(v.intent),v.canonical,`Canonical bytes: ${v.name}`);
    assert.equal(fingerprintRequest(v.intent),v.sha256,`Independent SHA256: ${v.name}`);
    if(v.intent.body.lines?.length===200)expandedLines+=200;
  }
  const scenarios=json('protocol-scenarios.json');shape(scenarios,['scenarios']);
  for(const s of scenarios.scenarios){
    shape(s,['name','status','httpStatus','affirmative','automaticResend','newKeyAllowed','resolution']);text(s.name);text(s.resolution);boolean(s.affirmative);
    requireValue(s.automaticResend===false&&s.newKeyAllowed===false,'Unsafe resolution');
    if(s.affirmative)requireValue(s.status==='COMMITTED'&&s.httpStatus===200,'False positive evidence');
  }
  const receiptCreate=json('receipt-create-response.json').data,receiptUpdate=json('receipt-update-response.json').data;
  requireValue(receiptCreate.receiptId===receiptUpdate.receiptId,'Receipt identity changed');
  requireValue(new Set([...receiptCreate.lines,...receiptUpdate.lines].map(x=>x.receiptLineId)).size===receiptCreate.lines.length+receiptUpdate.lines.length,'Receipt replacement IDs overlap');
  return {fixtures:manifest.fixtures.length,protectedStatuses:statuses,fingerprintVectors:controls.vectors.length,expandedLines,protocolScenarios:scenarios.scenarios.length,artifacts:files.size};
}
if(process.argv[1]&&resolve(process.argv[1])===fileURLToPath(import.meta.url)){
  try{console.log(JSON.stringify(validateCorpus(process.argv[2]),null,2));}catch(error){console.error(error.message);process.exitCode=1;}
}
