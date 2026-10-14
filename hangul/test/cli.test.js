import assert from 'node:assert/strict';
import { spawnSync } from 'node:child_process';
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import test from 'node:test';
import { fileURLToPath } from 'node:url';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const cli = path.join(root, 'cli.js');

function runCli(args) {
  return spawnSync(process.execPath, [cli, ...args], { encoding: 'utf8' });
}

test('cli가 .hangul을 .js로 만들고 node로 실행된다', () => {
  const dir = fs.mkdtempSync(path.join(os.tmpdir(), 'hangul-cli-'));
  const source = path.join(dir, '합.hangul');
  const output = path.join(dir, '합.js');
  fs.writeFileSync(
    source,
    `함수 더하기(가, 나) {
  반환 가 + 나
}
변수 합 = 더하기(1, 2)
출력 합
`,
  );
  const compiled = runCli([source, '-o', output]);
  assert.equal(compiled.status, 0, compiled.stderr);
  assert.equal(fs.existsSync(output), true);
  const ran = spawnSync(process.execPath, [output], { encoding: 'utf8' });
  assert.equal(ran.status, 0, ran.stderr);
  assert.equal(ran.stdout, '3\n');
});

test('cli 기본 출력 이름은 확장자만 바꾼다', () => {
  const dir = fs.mkdtempSync(path.join(os.tmpdir(), 'hangul-cli-'));
  const source = path.join(dir, '하나.hangul');
  fs.writeFileSync(source, '출력 4 - 1\n');
  const compiled = runCli([source]);
  assert.equal(compiled.status, 0, compiled.stderr);
  const output = path.join(dir, '하나.js');
  const ran = spawnSync(process.execPath, [output], { encoding: 'utf8' });
  assert.equal(ran.stdout, '3\n');
});

test('없는 파일과 컴파일 오류는 실패한다', () => {
  const missing = runCli(['없는파일.hangul']);
  assert.notEqual(missing.status, 0);
  assert.match(missing.stderr, /파일을 찾을 수 없습니다/);

  const dir = fs.mkdtempSync(path.join(os.tmpdir(), 'hangul-cli-'));
  const source = path.join(dir, 'bad.hangul');
  fs.writeFileSync(source, '출력\n');
  const bad = runCli([source]);
  assert.notEqual(bad.status, 0);
  assert.match(bad.stderr, /식이 필요합니다/);
});

test('도움말', () => {
  const result = runCli(['--help']);
  assert.equal(result.status, 0, result.stderr);
  assert.match(result.stdout, /사용법/);
});
