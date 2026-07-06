const { describe, it } = require('node:test');
const assert = require('node:assert/strict');
const { recalculateScore, validateScore } = require('./scoreValidator');

describe('scoreValidator', () => {
  it('recalculates target hits', () => {
    const log = [
      { eventType: 'TargetHit', targetId: 'a', timestampMs: 100 },
      { eventType: 'TargetHit', targetId: 'b', timestampMs: 200 },
    ];
    assert.equal(recalculateScore(log), 200);
  });

  it('adds combo bonus on third hit', () => {
    const log = [
      { eventType: 'TargetHit', targetId: 'a', timestampMs: 100 },
      { eventType: 'TargetHit', targetId: 'b', timestampMs: 200 },
      { eventType: 'TargetHit', targetId: 'c', timestampMs: 300 },
    ];
    assert.equal(recalculateScore(log), 350);
  });

  it('rejects score mismatch', () => {
    const body = {
      missionId: 'test',
      finalScore: 9999,
      playTime: 60,
      eventLog: [{ eventType: 'TargetHit', targetId: 'a', timestampMs: 0 }],
    };
    const result = validateScore(body);
    assert.equal(result.valid, false);
    assert.equal(result.serverScore, 100);
  });

  it('accepts matching score', () => {
    const body = {
      missionId: 'test',
      finalScore: 100,
      playTime: 60,
      eventLog: [{ eventType: 'TargetHit', targetId: 'a', timestampMs: 0 }],
    };
    const result = validateScore(body);
    assert.equal(result.valid, true);
  });
});
