import { CompileError } from './errors.js';

const KEYWORDS = new Set(['함수', '변수', '반환', '출력', '만약', '아니면', '동안']);

const TWO_CHAR = new Set(['==', '!=', '<=', '>=']);

function isIdentStart(ch) {
  return /[\p{ID_Start}_$]/u.test(ch);
}

function isIdentContinue(ch) {
  return /[\p{ID_Continue}_$]/u.test(ch);
}

export function tokenize(source) {
  if (source.charCodeAt(0) === 0xfeff) source = source.slice(1);

  const tokens = [];
  let i = 0;
  let line = 1;
  let column = 1;

  function peek(offset = 0) {
    return source[i + offset] ?? '';
  }

  function advance() {
    const ch = source[i++];
    if (ch === '\n') {
      line += 1;
      column = 1;
    } else {
      column += 1;
    }
    return ch;
  }

  function token(type, extra = {}) {
    return { type, line: extra.line, column: extra.column, ...extra };
  }

  while (i < source.length) {
    const ch = peek();
    if (/\s/u.test(ch)) {
      advance();
      continue;
    }

    const start = { line, column };

    if (ch === '/' && peek(1) === '/') {
      while (i < source.length && peek() !== '\n') advance();
      continue;
    }

    if ('(){},'.includes(ch)) {
      tokens.push(token(ch, start));
      advance();
      continue;
    }

    const two = ch + peek(1);
    if (TWO_CHAR.has(two)) {
      if ((two === '==' || two === '!=') && peek(2) === '=') {
        throw new CompileError(`'${two}='는 지원하지 않습니다`, start.line, start.column);
      }
      tokens.push(token(two, start));
      advance();
      advance();
      continue;
    }

    if ('+-*/<>='.includes(ch)) {
      tokens.push(token(ch, start));
      advance();
      continue;
    }

    if (ch >= '0' && ch <= '9') {
      let digits = '';
      while (peek() >= '0' && peek() <= '9') digits += advance();
      if (peek() === '.') {
        throw new CompileError('소수점은 지원하지 않습니다', line, column);
      }
      const value = Number(digits);
      if (!Number.isSafeInteger(value)) {
        throw new CompileError('정수가 너무 큽니다', start.line, start.column);
      }
      tokens.push(token('number', { ...start, value }));
      continue;
    }

    if (isIdentStart(ch)) {
      let name = '';
      while (i < source.length && isIdentContinue(peek())) name += advance();
      if (KEYWORDS.has(name)) tokens.push(token(name, start));
      else tokens.push(token('ident', { ...start, value: name }));
      continue;
    }

    throw new CompileError(`알 수 없는 문자입니다: ${ch}`, start.line, start.column);
  }

  tokens.push(token('eof', { line, column }));
  return tokens;
}
