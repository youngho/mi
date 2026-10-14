import { generate } from './codegen.js';
import { CompileError } from './errors.js';
import { tokenize } from './lexer.js';
import { parse } from './parser.js';
import { resolve } from './resolve.js';

export { CompileError };

export function compile(source) {
  if (typeof source !== 'string') {
    throw new CompileError('소스 문자열이 필요합니다');
  }
  const tokens = tokenize(source);
  const program = parse(tokens);
  resolve(program);
  return generate(program);
}
