import assert from 'node:assert/strict';
import { spawnSync } from 'node:child_process';
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import test from 'node:test';
import { fileURLToPath } from 'node:url';
import { compile, CompileError } from '../src/compile.js';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');

function run(source) {
  const javascript = compile(source);
  const dir = fs.mkdtempSync(path.join(os.tmpdir(), 'hangul-'));
  const file = path.join(dir, 'out.js');
  fs.writeFileSync(file, javascript);
  return spawnSync(process.execPath, [file], { encoding: 'utf8' });
}

function prints(source) {
  const result = run(source);
  assert.equal(result.status, 0, result.stderr);
  return result.stdout;
}

test('예시: 함수와 변수와 출력', () => {
  const source = fs.readFileSync(path.join(root, 'examples/더하기.hangul'), 'utf8');
  assert.equal(
    compile(source),
    `'use strict';
function 더하기(가, 나) {
  return (가 + 나);
}
let 합 = 더하기(1, 2);
console.log(합);
`,
  );
  assert.equal(prints(source), '3\n');
});

test('사칙연산과 괄호, 부호', () => {
  assert.equal(prints('출력 1 + 2 * 3\n'), '7\n');
  assert.equal(prints('출력 (1 + 2) * 3\n'), '9\n');
  assert.equal(prints('출력 10 - 3 - 2\n'), '5\n');
  assert.equal(prints('출력 1 - -2\n'), '3\n');
  assert.equal(prints('출력 7 / 2\n'), '3\n');
  assert.equal(prints('출력 -7 / 2\n'), '-3\n');
  assert.equal(prints('출력 20 / 3 * 2\n'), '12\n');
});

test('비교는 1 또는 0', () => {
  assert.equal(prints('출력 1 + 2 == 3\n출력 1 != 1\n출력 1 < 2 + 3\n'), '1\n0\n1\n');
});

test('변수 대입', () => {
  assert.equal(
    prints(`변수 가 = 1
가 = 가 + 2
출력 가
`),
    '3\n',
  );
});

test('만약 아니면', () => {
  const source = `함수 부호(수) {
  만약 수 > 0 {
    반환 1
  } 아니면 {
    만약 수 < 0 {
      반환 -1
    } 아니면 {
      반환 0
    }
  }
}
출력 부호(2)
출력 부호(0)
출력 부호(0 - 5)
`;
  assert.equal(prints(source), '1\n0\n-1\n');
});

test('동안', () => {
  const source = `함수 합계(끝) {
  변수 결과 = 0
  변수 수 = 1
  동안 수 <= 끝 {
    결과 = 결과 + 수
    수 = 수 + 1
  }
  반환 결과
}
출력 합계(3)
`;
  assert.equal(prints(source), '6\n');
});

test('재귀와 먼저 호출', () => {
  const source = `출력 팩토리얼(5)
함수 팩토리얼(수) {
  만약 수 <= 1 {
    반환 1
  }
  반환 수 * 팩토리얼(수 - 1)
}
`;
  assert.equal(prints(source), '120\n');
});

test('블록 안에서 이름을 가린다', () => {
  const source = `변수 가 = 1
만약 1 {
  변수 가 = 2
  출력 가
}
출력 가
`;
  assert.equal(prints(source), '2\n1\n');
});

test('주석', () => {
  assert.equal(prints('출력 1 // 무시\n// 출력 9\n출력 2\n'), '1\n2\n');
});

test('0으로 나누면 실패한다', () => {
  const result = run('출력 1 / 0\n');
  assert.notEqual(result.status, 0);
  assert.match(result.stderr, /0으로 나눌 수 없습니다/);
});

test('컴파일 오류', () => {
  const cases = [
    ['출력 없는\n', /선언되지 않은 이름/, 1],
    ['출력 1\n변수 가 = 가\n', /초기화 전에/, 2],
    ['변수 가 = 1\n변수 가 = 2\n', /이미 선언된 이름/, 2],
    ['함수 겹(가, 가) {\n  반환 가\n}\n', /이미 선언된 이름/, 1],
    ['반환 1\n', /함수 안에서만/, 1],
    ['함수 가() { 반환 1 }\n가 = 2\n', /함수에는 값을 넣을 수 없습니다/, 2],
    ['변수 가 = 1\n출력 가()\n', /함수가 아닙니다/, 2],
    ['함수 하나(가) { 반환 가 }\n출력 하나()\n', /인자 개수/, 2],
    ['변수 if = 1\n', /사용할 수 없는 이름/, 1],
    ['출력 1.5\n', /소수점/, 1],
  ];
  for (const [source, pattern, line] of cases) {
    assert.throws(() => compile(source), (error) => {
      assert.ok(error instanceof CompileError, source);
      assert.match(error.message, pattern, source);
      assert.equal(error.line, line, source);
      return true;
    });
  }
});
