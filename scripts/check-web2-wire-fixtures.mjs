// WEB2-01 corpus assertions only. This is not an HTTP/client codec or production validator.
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import test from 'node:test';

const corpus = new URL('../docs/contracts/web2-v1/', import.meta.url);
const read = name => JSON.parse(readFileSync(new URL(name, corpus), 'utf8'));
const manifest = read('manifest.json');
const vectors = read('numeric-vectors.json');
const maxLong = 9223372036854775807n;
const caps = {
  qty: '99999999999999.9999', unitPrice: '999999999999999.9999',
  lineAmount: '999999999999999999999999.9999',
  totalAmount: '999999999999999999999999.9999',
  totalCost: '999999999999999999999999.9999',
  costAmount: '999999999999999999999999.9999',
  grossProfit: '999999999999999999999999.9999',
  postedUnitCost: '99999999999999999999.99999999',
};
const createFields = ['saleDate', 'customerId', 'warehouseId', 'employeeId', 'note', 'lines'];
const saveLineFields = ['saleLineId', 'productId', 'qty', 'unitPrice', 'isGift'];
const detailFields = ['saleId', 'saleNumber', 'saleDate', 'customerId', 'warehouseId',
  'employeeId', 'status', 'note', 'postedAtUtc', 'version', 'lines', 'totalAmount',
  'totalCost', 'grossProfit', 'customerDisplayName', 'customerIsActive',
  'warehouseDisplayName', 'warehouseIsActive', 'employeeDisplayName', 'employeeIsActive'];
const detailLineFields = [...saveLineFields, 'lineNo', 'unit', 'lineAmount',
  'postedUnitCost', 'costAmount', 'productDisplayName', 'productIsActive'];
const listRowFields = ['saleId', 'saleNumber', 'saleDate', 'customerId',
  'customerDisplayName', 'warehouseId', 'warehouseDisplayName', 'employeeId',
  'employeeDisplayName', 'status', 'lineCount', 'totalAmount'];
const object = value => value !== null && typeof value === 'object' && !Array.isArray(value);
const scaled = (value, scale) => {
  const negative = value.startsWith('-');
  const [whole, fraction = ''] = (negative ? value.slice(1) : value).split('.');
  return BigInt(whole + fraction.padEnd(scale, '0')) * (negative ? -1n : 1n);
};
const fixed = (value, scale) => {
  const sign = value < 0n ? '-' : '';
  const digits = (value < 0n ? -value : value).toString().padStart(scale + 1, '0');
  return sign + digits.slice(0, -scale) + '.' + digits.slice(-scale);
};
const revenue = line => line.isGift ? 0n :
  (scaled(line.qty, 4) * scaled(line.unitPrice, 4) + 5000n) / 10000n;

// Deliberately bounded to the checked corpus. Future WEB2-05/08 tests must call real mappers/codecs.
function corpusViolations(name, value) {
  const errors = [];
  const check = (condition, code) => { if (!condition) errors.push(code); };
  const fields = (obj, allowed, required = allowed) => {
    if (!object(obj)) { errors.push('object-required'); return; }
    check(Object.keys(obj).every(key => allowed.includes(key)), 'unknown-field');
    check(required.every(key => Object.hasOwn(obj, key)), 'required-field');
  };
  const int32 = (value, positive = true) => {
    check(typeof value === 'number', 'int32-number');
    check(Number.isInteger(value) && value <= 2147483647 && value >= (positive ? 1 : 0), 'int32-range');
  };
  const long = (value, zero = false) => {
    if (typeof value !== 'string') { errors.push('int64-string'); return; }
    if (!/^(0|[1-9][0-9]*)$/.test(value)) {
      errors.push(value.startsWith('-') ? 'int64-range' : 'int64-canonical'); return;
    }
    check(BigInt(value) <= maxLong && (zero || BigInt(value) > 0n), 'int64-range');
  };
  const decimal = (value, field) => {
    if (typeof value !== 'string') { errors.push('decimal-string'); return; }
    const signed = field === 'grossProfit';
    if (!(signed ? /^-?(0|[1-9][0-9]*)(\.[0-9]+)?$/ : /^(0|[1-9][0-9]*)(\.[0-9]+)?$/).test(value)) {
      errors.push('decimal-canonical'); return;
    }
    const scale = field === 'postedUnitCost' ? 8 : 4;
    if ((value.split('.')[1]?.length ?? 0) > scale) { errors.push('decimal-scale'); return; }
    const n = scaled(value, scale);
    check((n < 0n ? -n : n) <= scaled(caps[field], scale), 'decimal-range');
    if (field === 'qty') check(n > 0n, 'positive-qty');
  };
  const calendar = value => {
    if (typeof value !== 'string' || !/^[0-9]{4}-[0-9]{2}-[0-9]{2}$/.test(value)) {
      errors.push('calendar-date'); return;
    }
    const [year, month, day] = value.split('-').map(Number);
    const leap = year % 4 === 0 && (year % 100 !== 0 || year % 400 === 0);
    const days = [31, leap ? 29 : 28, 31, 30, 31, 30, 31, 31, 30, 31, 30, 31];
    check(year >= 1 && month >= 1 && month <= 12 && day >= 1 && day <= days[month - 1], 'calendar-date');
    check(value !== '0001-01-01', 'required-sale-date');
  };
  const version = value => check(typeof value === 'string' && Buffer.from(value, 'base64').length === 8, 'version-token');
  const timestamp = value => check(typeof value === 'string' && /^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(\.\d{1,7})?Z$/.test(value), 'utc-timestamp');
  const line = (value, detail, create) => {
    fields(value, detail ? detailLineFields : saveLineFields,
      detail ? detailLineFields.filter(f => !['postedUnitCost', 'costAmount', 'productDisplayName'].includes(f)) : saveLineFields);
    long(value.saleLineId, !detail);
    if (create) check(value.saleLineId === '0', 'create-line-zero');
    int32(value.productId); decimal(value.qty, 'qty'); decimal(value.unitPrice, 'unitPrice');
    check(typeof value.isGift === 'boolean', 'boolean');
    if (detail) {
      int32(value.lineNo); decimal(value.lineAmount, 'lineAmount');
      check(typeof value.unit === 'string' && value.unit.length > 0, 'unit');
      check(typeof value.productIsActive === 'boolean', 'boolean');
    }
  };
  if (name.endsWith('-response.json') || name.startsWith('error-')) {
    fields(value, ['data', 'error'], []);
    check(Object.hasOwn(value, 'data') !== Object.hasOwn(value, 'error'), 'envelope-exclusive');
    if (Object.hasOwn(value, 'error')) {
      fields(value.error, ['code', 'message', 'details', 'correlationId'], ['code', 'message', 'details']);
      for (const key of ['code', 'message']) check(typeof value.error[key] === 'string' && value.error[key].length > 0, 'error-text');
      check(Array.isArray(value.error.details), 'error-details');
      for (const detail of value.error.details) fields(detail, ['code', 'message', 'field'], ['code', 'message']);
      return errors;
    }
    check(value.data !== null && value.data !== undefined, 'data-required');
    value = value.data;
  }
  if (name === 'sales-create-request.json' || name === 'sales-update-request.json') {
    fields(value, name.includes('update') ? [...createFields, 'expectedVersion'] : createFields,
      name.includes('update') ? [...createFields.filter(f => f !== 'note'), 'expectedVersion'] : createFields.filter(f => f !== 'note'));
    calendar(value.saleDate);
    for (const field of ['customerId', 'warehouseId', 'employeeId']) int32(value[field]);
    check(Array.isArray(value.lines) && value.lines.length >= 1 && value.lines.length <= 200, 'line-count');
    for (const item of value.lines) line(item, false, name.includes('create'));
    if (name.includes('update')) version(value.expectedVersion);
  } else if (name === 'sales-post-request.json') {
    fields(value, ['expectedVersion']); version(value.expectedVersion);
  } else if (name === 'sales-post-response.json') {
    fields(value, ['saleId', 'status', 'postedAtUtc', 'version']);
    long(value.saleId); check(value.status === 'Posted', 'status'); timestamp(value.postedAtUtc); version(value.version);
  } else if (/^sales-(create|update|get-draft|get-posted)-response.json$/.test(name)) {
    fields(value, detailFields, detailFields.filter(f => !['note', 'postedAtUtc', 'totalCost', 'grossProfit',
      'customerDisplayName', 'warehouseDisplayName', 'employeeDisplayName'].includes(f)));
    long(value.saleId); calendar(value.saleDate); version(value.version); decimal(value.totalAmount, 'totalAmount');
    for (const field of ['customerId', 'warehouseId', 'employeeId']) int32(value[field]);
    check(['Draft', 'Posted'].includes(value.status), 'status');
    for (const item of value.lines) line(item, true, false);
    const posted = value.status === 'Posted';
    for (const field of ['totalCost', 'grossProfit', 'postedAtUtc']) {
      if (posted) {
        check(value[field] !== null && value[field] !== undefined, 'posted-cost-required');
        if (value[field] != null) field === 'postedAtUtc' ? timestamp(value[field]) : decimal(value[field], field);
      } else check(!Object.hasOwn(value, field), 'draft-cost-omitted');
    }
    for (const item of value.lines) for (const field of ['postedUnitCost', 'costAmount']) {
      if (posted) { check(item[field] != null, 'posted-cost-required'); if (item[field] != null) decimal(item[field], field); }
      else check(!Object.hasOwn(item, field), 'draft-cost-omitted');
    }
  } else if (/^sales-list-(response|empty-response).json$/.test(name)) {
    fields(value, ['items', 'nextCursor', 'previousCursor'], ['items']);
    for (const item of value.items) {
      fields(item, listRowFields); long(item.saleId); calendar(item.saleDate); decimal(item.totalAmount, 'totalAmount');
      for (const field of ['customerId', 'warehouseId', 'employeeId', 'lineCount']) int32(item[field]);
    }
    for (const field of ['nextCursor', 'previousCursor']) if (Object.hasOwn(value, field)) check(typeof value[field] === 'string', 'cursor-string');
  } else if (name === 'sales-list-query.json') {
    fields(value, ['fromDate', 'toDate', 'customerId', 'warehouseId', 'employeeId', 'status', 'productId', 'search', 'pageSize', 'cursor'], []);
    for (const field of ['fromDate', 'toDate']) if (value[field]) calendar(value[field]);
    for (const field of ['customerId', 'warehouseId', 'employeeId', 'productId', 'pageSize']) if (Object.hasOwn(value, field)) int32(value[field]);
    check(value.pageSize <= 200, 'page-size');
  } else if (/^catalog-(products|customers|warehouses|employees)-response.json$/.test(name)) {
    check(Array.isArray(value), 'lookup-array');
    for (const item of value) { fields(item, ['id', 'code', 'name', 'isActive']); int32(item.id); check(item.isActive === true, 'active-lookup'); }
  } else if (name === 'catalog-lookup-query.json') {
    fields(value, ['search', 'limit'], ['limit']); int32(value.limit); check(value.limit <= 200, 'lookup-limit');
  } else if (name === 'auth-me-response.json') {
    fields(value, ['userId', 'username', 'displayName']); int32(value.userId);
  } else if (name === 'auth-capabilities-response.json') {
    fields(value, ['read', 'write', 'post']); for (const flag of Object.values(value)) check(typeof flag === 'boolean', 'boolean');
  } else if (name === 'auth-token-response.json') {
    fields(value, ['accessToken', 'expiresAtUtc', 'user']); timestamp(value.expiresAtUtc);
    fields(value.user, ['userId', 'username', 'displayName']); int32(value.user.userId);
  } else if (name === 'auth-login-request.json') fields(value, ['username', 'password']);
  else if (name === 'auth-logout-response.json') fields(value, []);
  else errors.push('unhandled-fixture');
  return errors;
}

for (const item of manifest.fixtures.filter(item => item.kind !== 'corpus-control')) {
  test(`lossless JSON and declared shape: ${item.file}`, () => {
    const value = read(item.file);
    assert.deepEqual(JSON.parse(JSON.stringify(value)), value);
    assert.deepEqual(corpusViolations(item.file, value), []);
  });
}

test('int64 and decimal boundary lexemes survive exact BigInt conversion', () => {
  for (const { value, allowZero } of vectors.int64) {
    const exact = BigInt(value);
    assert.equal(exact.toString(), value);
    assert.ok(exact >= (allowZero ? 0n : 1n) && exact <= maxLong);
  }
  assert.notEqual(BigInt(Number('9007199254740993')), BigInt('9007199254740993'));
  for (const { value, scale, field } of vectors.decimals) {
    assert.equal(fixed(scaled(value, scale), scale), value);
    assert.ok((scaled(value, scale) < 0n ? -scaled(value, scale) : scaled(value, scale)) <= scaled(caps[field], scale));
  }
  for (const line of vectors.revenue) assert.equal(fixed(revenue(line), 4), line.lineAmount);
});

test('ordered duplicate-product and gift lines retain independent saved identities', () => {
  const request = read('sales-create-request.json');
  const saved = read('sales-create-response.json').data;
  const update = read('sales-update-request.json');
  assert.equal(new Set(saved.lines.map(line => line.saleLineId)).size, saved.lines.length);
  assert.equal(new Set(saved.lines.map(line => line.productId)).size, 1);
  request.lines.forEach((line, index) => {
    assert.equal(line.saleLineId, '0');
    assert.equal(saved.lines[index].lineNo, index + 1);
    for (const field of ['productId', 'qty', 'unitPrice', 'isGift']) assert.equal(saved.lines[index][field], line[field]);
    assert.equal(update.lines[index].saleLineId, saved.lines[index].saleLineId);
  });
  assert.equal(update.expectedVersion, saved.version);
  assert.equal(read('sales-post-request.json').expectedVersion, read('sales-update-response.json').data.version);
});

test('authoritative detail/list totals, gift costs, ack and later inactive labels remain coherent', () => {
  for (const file of ['sales-create-response.json', 'sales-update-response.json', 'sales-get-draft-response.json', 'sales-get-posted-response.json']) {
    const detail = read(file).data;
    assert.equal(detail.saleId, manifest.routeSaleId);
    assert.equal(detail.lines.reduce((total, line) => total + revenue(line), 0n), scaled(detail.totalAmount, 4));
    for (const line of detail.lines) assert.equal(revenue(line), scaled(line.lineAmount, 4));
  }
  const posted = read('sales-get-posted-response.json').data;
  const ack = read('sales-post-response.json').data;
  for (const field of Object.keys(ack)) assert.equal(posted[field], ack[field]);
  assert.equal(posted.lines.reduce((sum, line) => sum + scaled(line.costAmount, 4), 0n), scaled(posted.totalCost, 4));
  assert.equal(scaled(posted.totalAmount, 4) - scaled(posted.totalCost, 4), scaled(posted.grossProfit, 4));
  const gift = posted.lines.find(line => line.isGift);
  assert.equal(gift.lineAmount, '0.0000'); assert.ok(scaled(gift.costAmount, 4) > 0n);
  for (const owner of ['customer', 'warehouse', 'employee']) {
    assert.equal(posted[owner + 'IsActive'], false); assert.ok(posted[owner + 'DisplayName']);
  }
  for (const line of posted.lines) { assert.equal(line.productIsActive, false); assert.ok(line.productDisplayName); }
  const row = read('sales-list-response.json').data.items[0];
  for (const field of Object.keys(row).filter(field => field !== 'lineCount')) assert.equal(row[field], posted[field]);
  assert.equal(row.lineCount, posted.lines.length);
  assert.equal(read('auth-capabilities-response.json').data.read, false);
  assert.equal(read('error-403.json').error.code, 'Auth.Forbidden'); // Post ack can precede denied Get.
});

test('null omission, real leap date, and opaque cursor/version are preserved without interpretation', () => {
  const draft = read('sales-get-draft-response.json').data;
  for (const field of ['totalCost', 'grossProfit', 'postedAtUtc']) assert.equal(Object.hasOwn(draft, field), false);
  for (const line of draft.lines) for (const field of ['postedUnitCost', 'costAmount']) assert.equal(Object.hasOwn(line, field), false);
  assert.equal(draft.saleDate, '2024-02-29');
  const cursors = read('opaque-cursor-vectors.json');
  for (const field of ['nextCursor', 'previousCursor']) {
    const query = new URLSearchParams({ ...read(cursors.queryFixture), cursor: cursors[field] });
    assert.equal(query.get('cursor'), cursors[field]);
  }
  assert.notEqual(cursors.nextCursor, cursors.previousCursor);
  assert.equal(JSON.parse(JSON.stringify({ expectedVersion: draft.version })).expectedVersion, draft.version);
  assert.deepEqual(read('sales-list-empty-response.json'), { data: { items: [] } });
});

test('initial underfilled list page obeys existing forward keyset cursor semantics', () => {
  const query = read('sales-list-query.json');
  const page = read('sales-list-response.json').data;
  assert.equal(Object.hasOwn(query, 'cursor'), false);
  // SqlSalesReceiptQueryService: no incoming anchor => hasPrevious=false;
  // forward hasNext comes from Take(pageSize+1) look-ahead, so a short page is terminal.
  assert.equal(Object.hasOwn(page, 'previousCursor'), false);
  assert.ok(page.items.length < query.pageSize);
  assert.equal(Object.hasOwn(page, 'nextCursor'), false);
  assert.equal(BigInt(page.items[0].saleId), maxLong);
  for (let index = 1; index < page.items.length; index++) {
    assert.ok(BigInt(page.items[index - 1].saleId) > BigInt(page.items[index].saleId));
  }
});

test('error HTTP status is transport metadata; indexed and fieldless issues retain order', () => {
  const expected = { 400: ['ValidationError'], 401: ['Auth.Unauthorized', 'Auth.InvalidCredentials'], 403: ['Auth.Forbidden'],
    404: ['SalesReceipt.NotFound'], 409: ['SalesReceipt.ConcurrencyConflict', 'SalesReceipt.AlreadyPosted'] };
  for (const item of manifest.fixtures.filter(item => item.file.startsWith('error-'))) {
    const error = read(item.file).error;
    assert.ok(expected[item.status].includes(error.code));
    assert.equal(Object.hasOwn(error, 'status'), false);
    assert.equal(Object.hasOwn(error, 'statusCode'), false);
    assert.match(error.correlationId, /^[A-Za-z0-9_-]{1,128}$/);
  }
  const details = read('error-400.json').error.details;
  assert.equal(details[0].field, 'lines[1].qty');
  const snapshot = ['client-row-a', 'client-row-b', 'client-row-c'];
  const index = Number(/^lines\[(\d+)\]\.qty$/.exec(details[0].field)[1]);
  assert.equal(snapshot[index], 'client-row-b');
  assert.equal(Object.hasOwn(details[1], 'field'), false);
  for (const code of [401, 403]) assert.deepEqual(read(`error-${code}.json`).error.details, []);
});

test('four catalog endpoints share CatalogLookupDto; unit belongs to Sales detail', () => {
  const detail = read('sales-create-response.json').data;
  for (const [kind, expectedId] of [['products', detail.lines[0].productId], ['customers', detail.customerId],
    ['warehouses', detail.warehouseId], ['employees', detail.employeeId]]) {
    const [item] = read(`catalog-${kind}-response.json`).data;
    assert.equal(item.id, expectedId); assert.equal(Object.hasOwn(item, 'unit'), false);
    const label = kind === 'products' ? detail.lines[0].productDisplayName :
      detail[{ customers: 'customer', warehouses: 'warehouse', employees: 'employee' }[kind] + 'DisplayName'];
    assert.equal(label, `${item.code} - ${item.name}`);
  }
  assert.deepEqual(read('auth-token-response.json').data.user, read('auth-me-response.json').data);
  assert.equal(Object.hasOwn(read('auth-token-response.json').data, 'refreshToken'), false);
});

test('deterministic 200-line expansion preserves IDs, request ordering and exact totals', () => {
  const recipe = read('sales-200-lines-expansion.json');
  const request = read(recipe.baseRequest);
  const response = read(recipe.baseResponse);
  const requestTemplate = request.lines[recipe.lineTemplateIndex];
  const responseTemplate = response.data.lines[recipe.lineTemplateIndex];
  request.lines = Array.from({ length: recipe.count }, () => ({ ...requestTemplate,
    saleLineId: '0', productId: recipe.productId, qty: recipe.qty, unitPrice: recipe.unitPrice, isGift: recipe.isGift }));
  response.data.lines = request.lines.map((line, index) => ({ ...responseTemplate, ...line,
    saleLineId: (BigInt(recipe.firstSaleLineId) + BigInt(index)).toString(), lineNo: index + 1,
    lineAmount: fixed(revenue(line), 4) }));
  response.data.totalAmount = recipe.expectedTotalAmount;
  assert.deepEqual(corpusViolations(recipe.baseRequest, request), []);
  assert.deepEqual(corpusViolations(recipe.baseResponse, response), []);
  assert.equal(new Set(response.data.lines.map(line => line.saleLineId)).size, recipe.count);
  assert.equal(response.data.lines.reduce((sum, line) => sum + scaled(line.lineAmount, 4), 0n), scaled(recipe.expectedTotalAmount, 4));
  assert.deepEqual(JSON.parse(JSON.stringify({ request, response })), { request, response });
});

for (const negative of read('negative-cases.json').cases) {
  test(`negative corpus assertion (not runtime rejection): ${negative.name}`, () => {
    const mutated = read(negative.fixture);
    const path = negative.pointer.slice(1).split('/');
    let target = mutated;
    for (const key of path.slice(0, -1)) target = target[key];
    target[path.at(-1)] = negative.value;
    assert.ok(corpusViolations(negative.fixture, mutated).includes(negative.expectedViolation), negative.expectedViolation);
  });
}
