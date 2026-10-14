const COMPARISON = new Set(['==', '!=', '<', '>', '<=', '>=']);

export function generate(program) {
  const writer = new Writer();
  writer.line("'use strict';");
  for (const statement of program.body) writer.statement(statement);
  return `${writer.lines.join('\n')}\n`;
}

class Writer {
  constructor() {
    this.lines = [];
    this.indent = 0;
  }

  line(text) {
    this.lines.push(`${'  '.repeat(this.indent)}${text}`);
  }

  statement(statement) {
    switch (statement.type) {
      case 'FuncDecl': {
        const params = statement.params.map((param) => param.name).join(', ');
        this.line(`function ${statement.name}(${params}) {`);
        this.indent += 1;
        for (const child of statement.body) this.statement(child);
        this.indent -= 1;
        this.line('}');
        break;
      }
      case 'VarDecl':
        this.line(`let ${statement.name} = ${this.expression(statement.init)};`);
        break;
      case 'Assign':
        this.line(`${statement.name} = ${this.expression(statement.value)};`);
        break;
      case 'If':
        this.line(`if (${this.expression(statement.test)}) {`);
        this.indent += 1;
        for (const child of statement.consequent) this.statement(child);
        this.indent -= 1;
        if (statement.alternate) {
          this.line('} else {');
          this.indent += 1;
          for (const child of statement.alternate) this.statement(child);
          this.indent -= 1;
        }
        this.line('}');
        break;
      case 'While':
        this.line(`while (${this.expression(statement.test)}) {`);
        this.indent += 1;
        for (const child of statement.body) this.statement(child);
        this.indent -= 1;
        this.line('}');
        break;
      case 'Return':
        this.line(`return ${this.expression(statement.value)};`);
        break;
      case 'Print':
        this.line(`console.log(${this.expression(statement.value)});`);
        break;
      case 'ExprStmt':
        this.line(`${this.expression(statement.expr)};`);
        break;
      default:
        break;
    }
  }

  expression(expression) {
    switch (expression.type) {
      case 'Number':
        return String(expression.value);
      case 'Var':
        return expression.name;
      case 'Unary':
        return `(-${this.expression(expression.argument)})`;
      case 'Binary':
        return this.binary(expression);
      case 'Call':
        return `${expression.name}(${expression.args.map((arg) => this.expression(arg)).join(', ')})`;
      default:
        return '0';
    }
  }

  binary(expression) {
    const left = this.expression(expression.left);
    const right = this.expression(expression.right);
    if (expression.op === '/') {
      return `((a, b) => { if (b === 0) throw new Error('0으로 나눌 수 없습니다'); return globalThis.Math.trunc(a / b); })(${left}, ${right})`;
    }
    if (COMPARISON.has(expression.op)) {
      return `(${left} ${expression.op} ${right} ? 1 : 0)`;
    }
    return `(${left} ${expression.op} ${right})`;
  }
}
