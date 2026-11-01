// Test-only contract oracle. This does not implement or call an HTTP endpoint.
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
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
function save(value, update) {
  shape(value, ['receiptDate', 'warehouseId', 'employeeId', 'lines', ...(update ? ['expectedVersion'] : [])], ['note']);
  date(value.receiptDate); int(value.warehouseId); int(value.employeeId); if (Object.hasOwn(value, 'note')) nullableText(value.note, 1000); lines(value.lines, false);
  if (update) version(value.expectedVersion);
}
function detail(value) {
  shape(value, ['receiptId', 'receiptNumber', 'receiptDate', 'warehouseId', 'warehouseDisplayName', 'warehouseIsActive', 'employeeId', 'employeeDisplayName', 'employeeIsActive', 'status', 'note', 'postedAtUtc', 'version', 'lines']);
  long(value.receiptId); receiptNumber(value.receiptNumber); date(value.receiptDate); int(value.warehouseId); int(value.employeeId);
  nullableText(value.warehouseDisplayName); nullableText(value.employeeDisplayName); boolean(value.warehouseIsActive); boolean(value.employeeIsActive);
  requireValue(['Draft', 'Posted'].includes(value.status), 'Invalid receipt status'); nullableText(value.note, 1000); version(value.version); lines(value.lines, true);
  if (value.status === 'Draft') requireValue(value.postedAtUtc === null, 'Draft cannot have posting timestamp'); else utc(value.postedAtUtc);
}
function query(value, kind) {
  if (kind === 'capabilities-query') {
    shape(value, ['feature']);
    requireValue(['sales', 'goods-receipts', 'inventory-balances', 'inventory-stock-card'].includes(value.feature), 'Unknown capability feature');
    return;
  }
  const required = ['pageNumber', 'pageSize', ...(kind === 'stock-card-query' ? ['warehouseId', 'productId'] : [])];
  const optional = kind === 'receipt-list-query' ? ['warehouseId', 'status', 'search'] : kind === 'balances-query' ? ['warehouseId', 'productId', 'search'] : ['fromUtc', 'toUtc'];
  shape(value, required, optional); int(value.pageNumber); int(value.pageSize, 200);
  for (const key of ['warehouseId', 'productId']) if (Object.hasOwn(value, key)) int(value[key]);
  if (Object.hasOwn(value, 'status')) requireValue(['Draft', 'Posted'].includes(value.status), 'Invalid status filter');
  if (Object.hasOwn(value, 'search')) text(value.search, kind === 'receipt-list-query' ? 128 : 256);
  for (const key of ['fromUtc', 'toUtc']) if (Object.hasOwn(value, key)) utc(value[key]);
  if (value.fromUtc && value.toUtc) requireValue(utc(value.fromUtc) <= utc(value.toUtc), 'Reversed UTC range');
}
const referenceKeys = ['warehouseId', 'warehouseCode', 'warehouseName', 'warehouseIsActive', 'productId', 'productCode', 'productName', 'productIsActive'];
function references(value) {
  int(value.warehouseId); int(value.productId);
  for (const key of ['warehouseCode', 'warehouseName', 'productCode', 'productName']) text(value[key]);
  boolean(value.warehouseIsActive); boolean(value.productIsActive);
}
function balance(value) {
  shape(value, [...referenceKeys, 'quantity', 'inventoryValue', 'averageUnitCost']); references(value);
  const q = decimal(value.quantity, 18, 4), v = decimal(value.inventoryValue, 28, 4);
  if (q === 0n) requireValue(v === 0n && value.averageUnitCost === null, 'Zero stock requires zero value and null average');
  else {
    const avg = decimal(value.averageUnitCost, 28, 8);
    const numerator = v * 100000000n;
    const expected = (numerator / q) + ((numerator % q) * 2n >= q ? 1n : 0n);
    requireValue(avg === expected, 'Average differs from server half-away fixed-point arithmetic');
  }
}
function card(value) {
  shape(value, [...referenceKeys, 'ledgerId', 'postedAtUtc', 'documentType', 'documentId', 'documentNumber', 'quantityChange', 'valueChange']); references(value);
  long(value.ledgerId); long(value.documentId); utc(value.postedAtUtc); text(value.documentType); text(value.documentNumber);
  requireValue(value.documentType.length > 0 && value.documentNumber.length > 0, 'Empty document reference');
  const q = decimal(value.quantityChange, 18, 4, { signed: true }), v = decimal(value.valueChange, 28, 4, { signed: true });
  requireValue(q !== 0n, 'Zero stock movement');
  if (value.documentType === 'GoodsReceipt') requireValue(q > 0n && v >= 0n, 'Receipt movement must be positive');
  if (value.documentType === 'SalesReceipt') requireValue(q < 0n && v <= 0n, 'Sale movement must be negative');
}
function page(value, kind) {
  shape(value, ['items', 'pageNumber', 'pageSize', 'totalCount']); int(value.pageNumber); int(value.pageSize, 200); long(value.totalCount, true);
  requireValue(Array.isArray(value.items) && value.items.length <= value.pageSize, 'Invalid page cardinality');
  if (kind === 'receipt-list-page') requireValue(BigInt(value.items.length) <= BigInt(value.totalCount), 'Receipt count smaller than page');
  // Count and rows may change between inventory SQL statements: do not require a full page or offset/count consistency.
  const row = kind === 'receipt-list-page' ? detail : kind === 'balances-page' ? balance : card;
  value.items.forEach(row);
  const ids = new Set();
  for (let index = 0; index < value.items.length; index++) {
    const item = value.items[index];
    const key = kind === 'receipt-list-page' ? item.receiptId : kind === 'balances-page' ? `${item.warehouseId}/${item.productId}` : item.ledgerId;
    requireValue(!ids.has(key), 'Duplicate page identity'); ids.add(key);
    if (index > 0 && kind === 'receipt-list-page') {
      const previous = value.items[index - 1];
      requireValue(previous.receiptDate > item.receiptDate || (previous.receiptDate === item.receiptDate && BigInt(previous.receiptId) > BigInt(item.receiptId)), 'Receipt list order');
    }
    if (index > 0 && kind === 'stock-card-page') {
      const previous = value.items[index - 1];
      requireValue(utc(previous.postedAtUtc) < utc(item.postedAtUtc) || (utc(previous.postedAtUtc) === utc(item.postedAtUtc) && BigInt(previous.ledgerId) < BigInt(item.ledgerId)), 'Stock card order');
    }
  }
  // Balance string order is SQL-collation owned; JavaScript locale order is not an oracle.
}
function error(value) {
  shape(value, ['error']); shape(value.error, ['code', 'message', 'details'], ['correlationId']); text(value.error.code); text(value.error.message);
  requireValue(Array.isArray(value.error.details), 'Missing error details');
  if (Object.hasOwn(value.error, 'correlationId')) requireValue(typeof value.error.correlationId === 'string' && /^[A-Za-z0-9_-]{1,128}$/.test(value.error.correlationId), 'Invalid correlation');
  for (const issue of value.error.details) {
    shape(issue, ['code', 'message'], ['field']); text(issue.code); text(issue.message);
    if (Object.hasOwn(issue, 'field')) requireValue(typeof issue.field === 'string' && /^[a-z][A-Za-z0-9]*(?:\[[0-9]+\])?(?:\.[a-z][A-Za-z0-9]*(?:\[[0-9]+\])?)*$/.test(issue.field), 'Invalid field path');
  }
  if (['Auth.Unauthorized', 'Auth.Forbidden', 'Auth.RateLimited', 'InternalError'].includes(value.error.code)) requireValue(value.error.details.length === 0, 'Private error details leaked');
}
export function validateFixture(kind, payload) {
  if (kind === 'error') { error(payload); return; }
  if (kind === 'receipt-create-request' || kind === 'receipt-update-request') { save(payload, kind === 'receipt-update-request'); return; }
  if (kind === 'receipt-post-request') { shape(payload, ['expectedVersion']); version(payload.expectedVersion); return; }
  if (kind.endsWith('-query')) { requireValue(['receipt-list-query', 'balances-query', 'stock-card-query', 'capabilities-query'].includes(kind), 'Unknown query kind'); query(payload, kind); return; }
  shape(payload, ['data']); const value = payload.data;
  if (kind === 'receipt-detail') detail(value);
  else if (kind === 'receipt-post-response') { shape(value, ['receiptId', 'status', 'postedAtUtc']); long(value.receiptId); requireValue(value.status === 'Posted', 'Post must acknowledge Posted'); utc(value.postedAtUtc); }
  else if (['receipt-list-page', 'balances-page', 'stock-card-page'].includes(kind)) page(value, kind);
  else if (kind === 'capabilities-receipt' || kind === 'capabilities-read-only') { shape(value, kind === 'capabilities-receipt' ? ['read', 'write', 'post'] : ['read']); Object.values(value).forEach(boolean); }
  else fail(`Unknown fixture kind ${kind}`);
}
export function validateScalar(scalar, value) {
  const validators = {
    'int64-positive': () => long(value), 'int64-count': () => long(value, true), 'int32': () => int(value),
    quantity: () => decimal(value, 18, 4, { positive: true }), 'unit-price': () => decimal(value, 19, 4),
    amount: () => decimal(value, 28, 4), 'signed-amount': () => decimal(value, 28, 4, { signed: true }),
    'average-unit-cost': () => decimal(value, 28, 8), 'signed-quantity': () => decimal(value, 18, 4, { signed: true }),
    date: () => date(value), utc: () => utc(value), version: () => version(value),
  };
  requireValue(Object.hasOwn(validators, scalar), 'Unknown scalar'); validators[scalar]();
}

// JSON.parse alone loses duplicate members and accepts lone UTF-16 surrogates.
export function parseStrictJson(source, { integerTokensOnly = true } = {}) {
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
    requireValue(depth <= 32, 'JSON nesting limit'); space();
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
    requireValue(token && (!integerTokensOnly || token[0] !== '-0'), 'Invalid JSON scalar'); index += token[0].length;
  };
  value(0); space(); requireValue(index === source.length, 'Trailing JSON/float/exponent token'); return JSON.parse(source);
}
export function validateQuery(kind, rawQuery) {
  requireValue(typeof rawQuery === 'string', 'Expected query'); const result = {};
  for (const part of rawQuery.replace(/^\?/, '').split('&')) {
    requireValue(part.length > 0 && part.includes('='), 'Invalid query component');
    const equals = part.indexOf('=');
    const decode = (value) => decodeURIComponent(value.replace(/\+/g, ' '));
    const name = decode(part.slice(0, equals)), value = decode(part.slice(equals + 1));
    requireValue(!Object.hasOwn(result, name), 'Duplicate query key');
    if (['warehouseId', 'productId', 'pageNumber', 'pageSize'].includes(name)) { requireValue(/^[1-9][0-9]*$/.test(value), 'Noncanonical query integer'); result[name] = Number(value); }
    else Object.defineProperty(result, name, { value, enumerable: true });
  }
  validateFixture(kind, result); return result;
}
function applyChange(value, change) {
  let owner = value; for (const segment of change.path.slice(0, -1)) owner = owner[segment];
  const key = change.path.at(-1); if (change.remove) delete owner[key]; else owner[key] = structuredClone(change.value);
}
function readJson(directory, file, expectedHash, integerTokensOnly = true) {
  requireValue(/^[a-z0-9-]+\.json$/.test(file), 'Unsafe fixture path');
  const bytes = readFileSync(resolve(directory, file)); const source = new TextDecoder('utf-8', { fatal: true }).decode(bytes);
  requireValue(!bytes.subarray(0, 3).equals(Buffer.from([239, 187, 191])) && !source.includes('\r'), 'Corpus must use UTF-8 without BOM and LF');
  if (expectedHash) requireValue(createHash('sha256').update(bytes).digest('hex') === expectedHash, `Hash mismatch: ${file}`);
  return parseStrictJson(source, { integerTokensOnly });
}
export function validateCorpus(directory = dirname(fileURLToPath(import.meta.url))) {
  const manifest = readJson(directory, 'manifest.json'); requireValue(manifest.contractVersion === 'web3-v1', 'Wrong corpus version');
  requireValue(/^[a-f0-9]{40}$/.test(manifest.sourceBaseline) && Array.isArray(manifest.fixtures) && manifest.fixtures.length > 0, 'Missing source/fixtures');
  const fixtures = new Map(); let fixtureCount = 0;
  for (const fixture of manifest.fixtures) {
    requireValue(!fixtures.has(fixture.file) && /^[a-f0-9]{64}$/.test(fixture.sha256), 'Duplicate fixture or missing SHA256');
    const payload = readJson(directory, fixture.file, fixture.sha256); validateFixture(fixture.kind, payload);
    if (fixture.status !== undefined) {
      requireValue(Number.isInteger(fixture.status) && fixture.status >= 200 && fixture.status <= 599, 'Invalid transport status');
      requireValue((fixture.status === 200) === (fixture.kind !== 'error'), 'Envelope/status mismatch');
      if (fixture.kind === 'error') {
        const code = payload.error.code;
        const expected = code === 'ValidationError' ? 400 : code === 'Auth.Unauthorized' ? 401 : code === 'Auth.Forbidden' ? 403 : code === 'GoodsReceipt.NotFound' ? 404 : code.startsWith('GoodsReceipt.') ? 409 : code === 'Auth.RateLimited' ? 429 : code === 'InternalError' ? 500 : null;
        requireValue(fixture.status === expected, 'Error code/status mismatch');
      }
    }
    fixtures.set(fixture.file, payload); fixtureCount++;
  }
  // Control values intentionally contain invalid numeric token shapes for scalar tests.
  const vectors = readJson(directory, manifest.controls.file, manifest.controls.sha256, false);
  const names = new Set(); let positive = 0, negative = 0, expanded = 0;
  let scalarCases = 0, roundingCases = 0;
  for (const vector of vectors.scalarVectors) {
    const run = () => validateScalar(vector.scalar, vector.value);
    if (vector.valid) run(); else assert.throws(run, Error, vector.name);
    scalarCases++;
  }
  for (const vector of vectors.roundingVectors) {
    const q = decimal(vector.quantity, 18, 4, { positive: true }), p = decimal(vector.unitCost, 19, 4);
    const product = q * p, rounded = product / 10000n + (product % 10000n >= 5000n ? 1n : 0n);
    assert.equal(decimal(vector.expectedLineAmount, 28, 4), rounded, vector.name); roundingCases++;
  }
  for (const scenario of vectors.cases) {
    requireValue(typeof scenario.name === 'string' && !names.has(scenario.name), 'Duplicate case'); names.add(scenario.name);
    requireValue(typeof scenario.valid === 'boolean' && (!scenario.base || fixtures.has(scenario.base)), 'Invalid case expectation/base');
    let payload = scenario.base ? structuredClone(fixtures.get(scenario.base)) : structuredClone(scenario.value);
    if (scenario.expandLines) {
      const target = scenario.kind === 'receipt-detail' ? payload.data : payload;
      target.lines = Array.from({ length: scenario.expandLines }, (_, index) => ({ ...target.lines[0], productId: index + 1, ...(scenario.kind === 'receipt-detail' ? { receiptLineId: (9007199254741000n + BigInt(index)).toString(), lineNumber: index + 1 } : {}) })); expanded += scenario.expandLines;
    }
    for (const change of scenario.changes ?? []) applyChange(payload, change);
    const run = () => scenario.rawQuery !== undefined ? validateQuery(scenario.kind, scenario.rawQuery) : validateFixture(scenario.kind, scenario.rawJson !== undefined ? parseStrictJson(scenario.rawJson) : payload);
    if (scenario.valid) { run(); positive++; } else { assert.throws(run, Error, scenario.name); negative++; }
  }
  requireValue(positive > 0 && negative > 0 && expanded >= 400, 'Missing semantic corpus coverage');
  // Cross-response identity facts cannot be proved by parsing individual objects.
  const create = fixtures.get('receipt-create-response.json').data, update = fixtures.get('receipt-update-response.json').data;
  assert.equal(create.receiptId, update.receiptId); assert.equal(create.receiptNumber, update.receiptNumber);
  let lifecycleAssertions = 4;
  for (const [response, requestFile] of [[create, 'receipt-create-request.json'], [update, 'receipt-update-request.json']]) {
    const request = fixtures.get(requestFile);
    assert.deepEqual(response.lines.map(({ productId, quantity, unitCost }) => ({ productId, quantity, unitCost })), request.lines); lifecycleAssertions++;
    for (const key of ['receiptDate', 'warehouseId', 'employeeId', 'note']) { assert.equal(response[key], request[key] ?? null); lifecycleAssertions++; }
  }
  assert.equal(new Set([...create.lines, ...update.lines].map(line => line.receiptLineId)).size, create.lines.length + update.lines.length);
  const post = fixtures.get('receipt-post-response.json').data; assert.equal(post.receiptId, update.receiptId); assert.equal(Object.hasOwn(post, 'version'), false);
  lifecycleAssertions++;
  requireValue(scalarCases > 0 && roundingCases > 0, 'Missing numeric coverage');
  return { fixtures: fixtureCount, positiveCases: positive, negativeCases: negative, scalarCases, roundingCases, expandedLines: expanded, lifecycleAssertions };
}
if (process.argv[1] && resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  try { console.log(JSON.stringify(validateCorpus(process.argv[2]), null, 2)); }
  catch (error) { console.error(error.message); process.exitCode = 1; }
}
