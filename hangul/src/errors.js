export class CompileError extends Error {
  constructor(message, line = 0, column = 0) {
    super(line ? `${line}:${column}: ${message}` : message);
    this.name = 'CompileError';
    this.line = line;
    this.column = column;
  }
}
