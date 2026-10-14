import { CompileError } from './errors.js';

const RESERVED = new Set(
  `break case catch class const continue debugger default delete do else
   export extends finally for function if import in instanceof new return
   super switch this throw try typeof var void while with yield enum await
   implements interface package private protected public static
   null true false undefined NaN Infinity arguments eval
   console Math globalThis let`
    .split(/\s+/)
    .filter(Boolean),
);

export function resolve(program) {
  const checker = new Checker();
  checker.visitProgram(program);
}

class Checker {
  constructor() {
    this.scopes = [];
    this.functionDepth = 0;
  }

  visitProgram(program) {
    this.pushScope();
    this.hoistFunctions(program.body);
    for (const statement of program.body) this.visitStatement(statement);
    this.popScope();
  }

  visitFunction(fn) {
    this.functionDepth += 1;
    this.pushScope();
    for (const param of fn.params) this.declare(param.name, 'param', param);
    this.hoistFunctions(fn.body);
    for (const statement of fn.body) this.visitStatement(statement);
    this.popScope();
    this.functionDepth -= 1;
  }

  visitBlock(body) {
    this.pushScope();
    this.hoistFunctions(body);
    for (const statement of body) this.visitStatement(statement);
    this.popScope();
  }

  hoistFunctions(body) {
    for (const statement of body) {
      if (statement.type === 'FuncDecl') {
        this.declare(statement.name, 'func', statement, statement.params.length);
      }
    }
  }

  visitStatement(statement) {
    switch (statement.type) {
      case 'FuncDecl':
        this.visitFunction(statement);
        break;
      case 'VarDecl':
        this.declare(statement.name, 'var', statement, 0, 'init');
        this.visitExpression(statement.init);
        this.current().get(statement.name).state = 'ready';
        break;
      case 'Assign': {
        const binding = this.lookup(statement.name, statement);
        if (binding.kind === 'func') {
          throw new CompileError(
            `함수에는 값을 넣을 수 없습니다: ${statement.name}`,
            statement.line,
            statement.column,
          );
        }
        this.visitExpression(statement.value);
        break;
      }
      case 'If':
        this.visitExpression(statement.test);
        this.visitBlock(statement.consequent);
        if (statement.alternate) this.visitBlock(statement.alternate);
        break;
      case 'While':
        this.visitExpression(statement.test);
        this.visitBlock(statement.body);
        break;
      case 'Return':
        if (this.functionDepth === 0) {
          throw new CompileError(
            '함수 안에서만 반환할 수 있습니다',
            statement.line,
            statement.column,
          );
        }
        this.visitExpression(statement.value);
        break;
      case 'Print':
      case 'ExprStmt':
        this.visitExpression(statement.value ?? statement.expr);
        break;
      default:
        throw new CompileError('알 수 없는 문장입니다', statement.line, statement.column);
    }
  }

  visitExpression(expression) {
    switch (expression.type) {
      case 'Number':
        return;
      case 'Var':
        this.lookup(expression.name, expression);
        return;
      case 'Unary':
        this.visitExpression(expression.argument);
        return;
      case 'Binary':
        this.visitExpression(expression.left);
        this.visitExpression(expression.right);
        return;
      case 'Call': {
        const binding = this.lookup(expression.name, expression);
        if (binding.kind !== 'func') {
          throw new CompileError(
            `함수가 아닙니다: ${expression.name}`,
            expression.line,
            expression.column,
          );
        }
        if (binding.arity !== expression.args.length) {
          throw new CompileError(
            `인자 개수가 맞지 않습니다: ${expression.name} (필요 ${binding.arity}, 실제 ${expression.args.length})`,
            expression.line,
            expression.column,
          );
        }
        for (const arg of expression.args) this.visitExpression(arg);
        return;
      }
      default:
        throw new CompileError('알 수 없는 식입니다', expression.line, expression.column);
    }
  }

  declare(name, kind, loc, arity = 0, state = 'ready') {
    if (RESERVED.has(name)) {
      throw new CompileError(`사용할 수 없는 이름입니다: ${name}`, loc.line, loc.column);
    }
    const scope = this.current();
    if (scope.has(name)) {
      throw new CompileError(`이미 선언된 이름입니다: ${name}`, loc.line, loc.column);
    }
    scope.set(name, { kind, arity, state });
  }

  lookup(name, loc) {
    for (let i = this.scopes.length - 1; i >= 0; i -= 1) {
      const binding = this.scopes[i].get(name);
      if (!binding) continue;
      if (binding.state === 'init') {
        throw new CompileError(
          `초기화 전에 사용할 수 없습니다: ${name}`,
          loc.line,
          loc.column,
        );
      }
      return binding;
    }
    throw new CompileError(`선언되지 않은 이름입니다: ${name}`, loc.line, loc.column);
  }

  pushScope() {
    this.scopes.push(new Map());
  }

  popScope() {
    this.scopes.pop();
  }

  current() {
    return this.scopes[this.scopes.length - 1];
  }
}
