import { CompileError } from './errors.js';

const COMPARISON = new Set(['==', '!=', '<', '>', '<=', '>=']);

export function parse(tokens) {
  const parser = new Parser(tokens);
  return parser.parseProgram();
}

class Parser {
  constructor(tokens) {
    this.tokens = tokens;
    this.index = 0;
  }

  peek() {
    return this.tokens[this.index];
  }

  advance() {
    const current = this.peek();
    if (current.type !== 'eof') this.index += 1;
    return current;
  }

  expect(type, message) {
    const current = this.peek();
    if (current.type !== type) {
      throw new CompileError(message, current.line, current.column);
    }
    return this.advance();
  }

  parseProgram() {
    const body = [];
    while (this.peek().type !== 'eof') body.push(this.parseStatement());
    return { type: 'Program', body };
  }

  parseBlock() {
    this.expect('{', "'{'가 필요합니다");
    const body = [];
    while (this.peek().type !== '}' && this.peek().type !== 'eof') {
      body.push(this.parseStatement());
    }
    this.expect('}', "'}'가 필요합니다");
    return body;
  }

  parseStatement() {
    const current = this.peek();
    switch (current.type) {
      case '함수':
        return this.parseFunction();
      case '변수':
        return this.parseVar();
      case '만약':
        return this.parseIf();
      case '동안':
        return this.parseWhile();
      case '반환':
        return this.parseReturn();
      case '출력':
        return this.parsePrint();
      case 'ident':
        if (this.tokens[this.index + 1]?.type === '=') return this.parseAssign();
        return {
          type: 'ExprStmt',
          expr: this.parseExpression(),
          line: current.line,
          column: current.column,
        };
      default:
        throw new CompileError('문장이 필요합니다', current.line, current.column);
    }
  }

  parseFunction() {
    const keyword = this.advance();
    const name = this.expect('ident', '함수 이름이 필요합니다');
    this.expect('(', "'('가 필요합니다");
    const params = [];
    if (this.peek().type !== ')') {
      params.push(this.expectParam());
      while (this.peek().type === ',') {
        this.advance();
        params.push(this.expectParam());
      }
    }
    this.expect(')', "')'가 필요합니다");
    const body = this.parseBlock();
    return {
      type: 'FuncDecl',
      name: name.value,
      params,
      body,
      line: keyword.line,
      column: keyword.column,
    };
  }

  expectParam() {
    const name = this.expect('ident', '매개변수 이름이 필요합니다');
    return { name: name.value, line: name.line, column: name.column };
  }

  parseVar() {
    const keyword = this.advance();
    const name = this.expect('ident', '변수 이름이 필요합니다');
    this.expect('=', "'='가 필요합니다");
    const init = this.parseExpression();
    return {
      type: 'VarDecl',
      name: name.value,
      init,
      line: keyword.line,
      column: name.column,
    };
  }

  parseAssign() {
    const name = this.expect('ident', '이름이 필요합니다');
    this.expect('=', "'='가 필요합니다");
    const value = this.parseExpression();
    return {
      type: 'Assign',
      name: name.value,
      value,
      line: name.line,
      column: name.column,
    };
  }

  parseIf() {
    const keyword = this.advance();
    const test = this.parseExpression();
    const consequent = this.parseBlock();
    let alternate = null;
    if (this.peek().type === '아니면') {
      this.advance();
      alternate = this.parseBlock();
    }
    return {
      type: 'If',
      test,
      consequent,
      alternate,
      line: keyword.line,
      column: keyword.column,
    };
  }

  parseWhile() {
    const keyword = this.advance();
    const test = this.parseExpression();
    const body = this.parseBlock();
    return { type: 'While', test, body, line: keyword.line, column: keyword.column };
  }

  parseReturn() {
    const keyword = this.advance();
    const value = this.parseExpression();
    return { type: 'Return', value, line: keyword.line, column: keyword.column };
  }

  parsePrint() {
    const keyword = this.advance();
    const value = this.parseExpression();
    return { type: 'Print', value, line: keyword.line, column: keyword.column };
  }

  parseExpression() {
    return this.parseComparison();
  }

  parseComparison() {
    let left = this.parseTerm();
    while (COMPARISON.has(this.peek().type)) {
      const op = this.advance();
      const right = this.parseTerm();
      left = { type: 'Binary', op: op.type, left, right, line: op.line, column: op.column };
    }
    return left;
  }

  parseTerm() {
    let left = this.parseFactor();
    while (this.peek().type === '+' || this.peek().type === '-') {
      const op = this.advance();
      const right = this.parseFactor();
      left = { type: 'Binary', op: op.type, left, right, line: op.line, column: op.column };
    }
    return left;
  }

  parseFactor() {
    let left = this.parseUnary();
    while (this.peek().type === '*' || this.peek().type === '/') {
      const op = this.advance();
      const right = this.parseUnary();
      left = { type: 'Binary', op: op.type, left, right, line: op.line, column: op.column };
    }
    return left;
  }

  parseUnary() {
    if (this.peek().type === '-') {
      const op = this.advance();
      return {
        type: 'Unary',
        op: '-',
        argument: this.parseUnary(),
        line: op.line,
        column: op.column,
      };
    }
    return this.parsePrimary();
  }

  parsePrimary() {
    const current = this.peek();
    if (current.type === 'number') {
      this.advance();
      return { type: 'Number', value: current.value, line: current.line, column: current.column };
    }
    if (current.type === 'ident') {
      this.advance();
      if (this.peek().type === '(') {
        this.advance();
        const args = [];
        if (this.peek().type !== ')') {
          args.push(this.parseExpression());
          while (this.peek().type === ',') {
            this.advance();
            args.push(this.parseExpression());
          }
        }
        this.expect(')', "')'가 필요합니다");
        return {
          type: 'Call',
          name: current.value,
          args,
          line: current.line,
          column: current.column,
        };
      }
      return { type: 'Var', name: current.value, line: current.line, column: current.column };
    }
    if (current.type === '(') {
      this.advance();
      const expression = this.parseExpression();
      this.expect(')', "')'가 필요합니다");
      return expression;
    }
    throw new CompileError('식이 필요합니다', current.line, current.column);
  }
}
