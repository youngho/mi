#!/usr/bin/env node
import fs from 'node:fs';
import path from 'node:path';
import { compile, CompileError } from './src/compile.js';

function invokedAs() {
  const relative = path.relative(process.cwd(), process.argv[1]);
  return relative.startsWith('..') ? process.argv[1] : relative || 'cli.js';
}

function usage() {
  return `사용법: node ${invokedAs()} <파일.hangul> [-o 출력.js]

한글 파일을 자바스크립트로 컴파일한다.
-o 를 생략하면 확장자만 .js 로 바꾼 파일을 만든다.
`;
}

function parseArgs(argv) {
  let input;
  let output;
  for (let i = 0; i < argv.length; i += 1) {
    const arg = argv[i];
    if (arg === '-h' || arg === '--help') return { help: true };
    if (arg === '-o') {
      output = argv[i + 1];
      if (!output || output.startsWith('-')) {
        throw new Error('-o 뒤에 출력 파일이 필요합니다');
      }
      i += 1;
      continue;
    }
    if (arg.startsWith('-')) throw new Error(`알 수 없는 옵션입니다: ${arg}`);
    if (input) throw new Error('입력 파일은 하나만 받습니다');
    input = arg;
  }
  if (!input) throw new Error(usage().trim());
  if (!output) {
    if (!input.endsWith('.hangul')) {
      throw new Error('-o 로 출력 파일을 지정하세요');
    }
    output = `${input.slice(0, -'.hangul'.length)}.js`;
  }
  return { input, output };
}

function main() {
  let args;
  try {
    args = parseArgs(process.argv.slice(2));
  } catch (error) {
    console.error(error.message);
    process.exit(1);
  }
  if (args.help) {
    console.log(usage());
    return;
  }

  let source;
  try {
    source = fs.readFileSync(args.input, 'utf8');
  } catch (error) {
    const detail = error.code === 'ENOENT' ? '파일을 찾을 수 없습니다' : error.message;
    console.error(`${args.input}: ${detail}`);
    process.exit(1);
  }

  let javascript;
  try {
    javascript = compile(source);
  } catch (error) {
    if (error instanceof CompileError) console.error(`${args.input}:${error.message}`);
    else console.error(error.message);
    process.exit(1);
  }

  try {
    fs.writeFileSync(args.output, javascript);
  } catch (error) {
    console.error(`${args.output}: ${error.message}`);
    process.exit(1);
  }
}

main();
