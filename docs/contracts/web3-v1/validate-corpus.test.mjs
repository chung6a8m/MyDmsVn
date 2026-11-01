import test from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync, mkdtempSync, cpSync, rmSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { basename, dirname, join, resolve, sep } from 'node:path';
import { fileURLToPath } from 'node:url';
import { validateCorpus, validateFixture, parseStrictJson, validateQuery } from './validate-corpus.mjs';

const directory = dirname(fileURLToPath(import.meta.url));
const fixture = file => JSON.parse(readFileSync(join(directory, file + '.json'), 'utf8'));

test('canonical corpus executes positive, hostile, scalar, rounding and expanded row cases', () => {
  const result = validateCorpus(directory);
  assert.ok(result.positiveCases > 0 && result.negativeCases > 0 && result.scalarCases > 0 && result.roundingCases > 0);
});
test('receipt response keeps positive fresh line IDs in sent order without sending those IDs', () => {
  const before = fixture('receipt-create-response').data, after = fixture('receipt-update-response').data;
  const request = fixture('receipt-update-request');
  assert.deepEqual(after.lines.map(line => line.productId), request.lines.map(line => line.productId));
  assert.ok(after.lines.every(line => BigInt(line.receiptLineId) > 0n && !before.lines.some(old => old.receiptLineId === line.receiptLineId)));
  const wrong = structuredClone(request); wrong.lines[0].receiptLineId = after.lines[0].receiptLineId;
  assert.throws(() => validateFixture('receipt-update-request', wrong), /Unknown receiptLineId/);
});
test('Post acknowledgement remains a complete success without detail permission or version', () => {
  const ack = fixture('receipt-post-response'), denied = fixture('error-403-forbidden');
  validateFixture('receipt-post-response', ack); validateFixture('error', denied);
  assert.equal(ack.data.status, 'Posted'); assert.equal(Object.hasOwn(ack.data, 'version'), false);
  ack.data.version = 'AAAAAAAAAAM=';
  assert.throws(() => validateFixture('receipt-post-response', ack), /Unknown version/);
});
test('error fields preserve sent indices and fieldless business failures', () => {
  const response = fixture('error-400-fields'); validateFixture('error', response);
  assert.deepEqual(response.error.details.map(detail => detail.field), ['lines[0].quantity', 'lines[1].unitCost']);
  const fieldless = fixture('error-400-valuation-post'); validateFixture('error', fieldless);
  assert.equal(Object.hasOwn(fieldless.error.details[0], 'field'), false);
});
test('strict JSON rejects duplicate decoded members at nested depth', () => {
  assert.throws(() => parseStrictJson('{"lines":[{"productId":7,"product\\u0049d":8}]}'), /Duplicate JSON key/);
  assert.deepEqual(parseStrictJson('{"note":"Tiếng Việt 😀"}'), { note: 'Tiếng Việt 😀' });
  assert.throws(() => parseStrictJson('{"note":"\\ud800"}'), /Unpaired surrogate/);
});
test('query validates decoded duplicate names, integer lexemes and the full seven-digit UTC boundary', () => {
  assert.throws(() => validateQuery('balances-query', 'pageNumber=1&%70ageNumber=2&pageSize=25'), /Duplicate query key/);
  assert.throws(() => validateQuery('balances-query', 'pageNumber=1e0&pageSize=25'), /Noncanonical/);
  assert.throws(() => validateQuery('stock-card-query', 'pageNumber=1&pageSize=25&warehouseId=1&productId=7&fromUtc=2026-11-01T00:00:00.1234568Z&toUtc=2026-11-01T00:00:00.1234567Z'), /Reversed/);
});
test('inventory pages do not invent count/row snapshot isolation', () => {
  const response = fixture('balances-response'); response.data.totalCount = '0';
  validateFixture('balances-page', response);
  const receipt = fixture('receipt-list-response'); receipt.data.totalCount = '0';
  assert.throws(() => validateFixture('receipt-list-page', receipt), /count/);
});
test('a changed pinned fixture fails byte-hash verification before trust', () => {
  const temporary = mkdtempSync(join(tmpdir(), 'mydmsvn-web3-corpus-'));
  try {
    cpSync(directory, temporary, { recursive: true });
    const path = join(temporary, 'receipt-post-response.json');
    writeFileSync(path, readFileSync(path, 'utf8').replace('Posted', 'Draft'));
    assert.throws(() => validateCorpus(temporary), /Hash mismatch/);
  } finally {
    // Only the exact mkdtemp-owned directory is removed; no external SQL/resource ownership.
    assert.ok(resolve(temporary).startsWith(resolve(tmpdir()) + sep) && basename(temporary).startsWith('mydmsvn-web3-corpus-'));
    rmSync(temporary, { recursive: true, force: true });
  }
});
