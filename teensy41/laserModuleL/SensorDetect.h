/*
 * SensorDetect.h — LM393 검출 (기본: 재귀반사 차단)
 *
 * 이 모듈은 렌즈/증폭이 없어 원거리 산란광(Diffuse)을 디지털로
 * 뒤집지 못한다. 벽면 테두리 재귀반사 테이프의 강한 귀환광을
 * 평소에 읽고, 손가락/탄이 궤적을 끊는 짧은 소실을 HIT 로 본다.
 *
 * 필요 매크로 (이 헤더 include 전에 정의):
 *   PIN_SENSOR_DO, PIN_SENSOR_AO, SENSOR_DO_ACTIVE_LOW
 * 필요: TftLog.h (logln/logf), Arduino.h
 *
 * USB:
 *   mode int|diff|ao
 *   ao [ms]          AO 스트림 + min/max (TFT 없이 Serial)
 *   aoth <0-1023>
 *   aodir low|high   analog HIT 방향 (high=어두울 때 AO 상승)
 */
#pragma once

#ifndef PIN_SENSOR_DO
#define PIN_SENSOR_DO 5
#endif
#ifndef PIN_SENSOR_AO
#define PIN_SENSOR_AO A1
#endif
#ifndef SENSOR_DO_ACTIVE_LOW
#define SENSOR_DO_ACTIVE_LOW true
#endif

enum SensorMode : uint8_t {
  SENSOR_MODE_INTERRUPT = 0,
  SENSOR_MODE_DIFFUSE   = 1,
  SENSOR_MODE_ANALOG    = 2,
};

static SensorMode g_sensorMode = SENSOR_MODE_INTERRUPT;

// 스캔 중 물체 통과 펄스 폭. 스캔 밖 긴 암흑(엔드 오브 스캔)은 제외.
static const uint32_t SENSOR_BREAK_MIN_US = 2;
static const uint32_t SENSOR_BREAK_MAX_US = 1500;

// `ao` 스트림에서 span 이 이하면 산란/아날로그 검출 불가.
static const int SENSOR_AO_DEAD_SPAN = 8;

static int g_aoThreshold = 512;
// true: AO < th 가 HIT. false: AO > th 가 HIT (전형: 어두움→AO 상승).
static bool g_aoHitWhenBelow = false;

static volatile bool g_beamPresent = false;
static volatile bool g_pulseActive = false;
static volatile uint32_t g_pulseStartUs = 0;
static volatile uint32_t g_hitUs = 0;
static volatile uint32_t g_hitWidthUs = 0;
static volatile bool g_hitPending = false;
static bool g_sensorDoIrqAttached = false;
static bool g_analogLastHit = false;

static inline bool sensorBeamPresentRaw()
{
  const bool doHigh = digitalReadFast(PIN_SENSOR_DO) == HIGH;
  return SENSOR_DO_ACTIVE_LOW ? !doHigh : doHigh;
}

static inline bool sensorPulseLevel(bool beam)
{
  // interrupt: 귀환 소실이 펄스. diffuse: 수광이 펄스.
  return (g_sensorMode == SENSOR_MODE_INTERRUPT) ? !beam : beam;
}

static const char *sensorModeName()
{
  switch (g_sensorMode) {
    case SENSOR_MODE_DIFFUSE: return "diff";
    case SENSOR_MODE_ANALOG:  return "ao";
    default:                  return "int";
  }
}

static void sensorIsr()
{
  const bool beam = sensorBeamPresentRaw();
  g_beamPresent = beam;
  const bool pulse = sensorPulseLevel(beam);
  const uint32_t now = micros();

  if (pulse && !g_pulseActive) {
    g_pulseStartUs = now;
    g_pulseActive = true;
  } else if (!pulse && g_pulseActive) {
    const uint32_t w = now - g_pulseStartUs;
    g_pulseActive = false;
    if (w >= SENSOR_BREAK_MIN_US && w <= SENSOR_BREAK_MAX_US) {
      g_hitUs = g_pulseStartUs;
      g_hitWidthUs = w;
      g_hitPending = true;
    }
  }
}

static void sensorDetachDoIrq()
{
  if (!g_sensorDoIrqAttached) {
    return;
  }
  detachInterrupt(digitalPinToInterrupt(PIN_SENSOR_DO));
  g_sensorDoIrqAttached = false;
}

static void sensorAttachDoIrq()
{
  if (g_sensorDoIrqAttached) {
    return;
  }
  attachInterrupt(digitalPinToInterrupt(PIN_SENSOR_DO), sensorIsr, CHANGE);
  g_sensorDoIrqAttached = true;
}

static void sensorResetPulseState()
{
  noInterrupts();
  g_hitPending = false;
  g_pulseActive = false;
  g_beamPresent = sensorBeamPresentRaw();
  interrupts();
  g_analogLastHit = false;
}

static void sensorApplyMode()
{
  if (g_sensorMode == SENSOR_MODE_ANALOG) {
    sensorDetachDoIrq();
  } else {
    sensorAttachDoIrq();
  }
  sensorResetPulseState();
}

static void sensorBegin()
{
  pinMode(PIN_SENSOR_DO, INPUT);
  analogReadResolution(10);
  sensorApplyMode();
}

static bool isBeamPresent()
{
  if (g_sensorMode == SENSOR_MODE_ANALOG) {
    const int ao = analogRead(PIN_SENSOR_AO);
    return g_aoHitWhenBelow ? (ao >= g_aoThreshold) : (ao <= g_aoThreshold);
  }
  return sensorBeamPresentRaw();
}

static bool isSensorHit()
{
  if (g_sensorMode == SENSOR_MODE_ANALOG) {
    const int ao = analogRead(PIN_SENSOR_AO);
    return g_aoHitWhenBelow ? (ao < g_aoThreshold) : (ao > g_aoThreshold);
  }
  if (g_sensorMode == SENSOR_MODE_INTERRUPT) {
    return !sensorBeamPresentRaw();
  }
  return sensorBeamPresentRaw();
}

static bool sensorTakeHit(uint32_t *hitUs, uint32_t *widthUs)
{
  if (g_sensorMode == SENSOR_MODE_ANALOG) {
    const bool hit = isSensorHit();
    if (hit == g_analogLastHit) {
      return false;
    }
    g_analogLastHit = hit;
    if (!hit) {
      return false;
    }
    *hitUs = micros();
    *widthUs = 0;
    return true;
  }

  if (!g_hitPending) {
    return false;
  }
  noInterrupts();
  *hitUs = g_hitUs;
  *widthUs = g_hitWidthUs;
  g_hitPending = false;
  interrupts();
  return true;
}

static void sensorStreamAo(uint32_t ms)
{
  if (ms < 200) {
    ms = 200;
  }
  if (ms > 20000) {
    ms = 20000;
  }

  logf("[ao] %lums A%d\n", (unsigned long)ms, (int)PIN_SENSOR_AO);
  Serial.println("[ao] pass finger; watch span");

  const uint32_t t0 = millis();
  uint32_t lastPrint = 0;
  int mn = 1023;
  int mx = 0;
  uint32_t n = 0;

  while ((millis() - t0) < ms) {
    const int v = analogRead(PIN_SENSOR_AO);
    ++n;
    if (v < mn) {
      mn = v;
    }
    if (v > mx) {
      mx = v;
    }
    if ((millis() - lastPrint) >= 20) {
      lastPrint = millis();
      Serial.println(v);
    }
  }

  const int span = mx - mn;
  logf("[ao] n=%lu\n", (unsigned long)n);
  logf("[ao] %d..%d\n", mn, mx);
  logf("[ao] span=%d\n", span);
  if (span < SENSOR_AO_DEAD_SPAN) {
    logln("[ao] dead");
    logln(" use mode int");
    logln(" + retro tape");
  } else {
    const int mid = (mn + mx) / 2;
    logln("[ao] ok");
    logf(" try aoth %d\n", mid);
    logln(" then mode ao");
  }
}

static void sensorPrintStatus()
{
  logf(" mode=%s\n", sensorModeName());
  logf(" beam=%d hit=%d\n", isBeamPresent() ? 1 : 0, isSensorHit() ? 1 : 0);
  logf(" DO=%d AO=%d\n", digitalRead(PIN_SENSOR_DO), analogRead(PIN_SENSOR_AO));
  logf(" al=%d aoth=%d\n", SENSOR_DO_ACTIVE_LOW ? 1 : 0, g_aoThreshold);
  logf(" aodir=%s\n", g_aoHitWhenBelow ? "low" : "high");
}

static bool sensorHandleCommand(const String &lower)
{
  if (lower == "sensor") {
    logln("[cmd] sensor");
    sensorPrintStatus();
    return true;
  }

  if (lower == "ao" || lower.startsWith("ao ")) {
    uint32_t ms = 3000;
    const int sp = lower.indexOf(' ');
    if (sp > 0) {
      const long parsed = lower.substring(sp + 1).toInt();
      if (parsed > 0) {
        ms = (uint32_t)parsed;
      }
    }
    sensorStreamAo(ms);
    return true;
  }

  if (lower.startsWith("mode")) {
    const int sp = lower.indexOf(' ');
    if (sp < 0) {
      logf("[mode] %s\n", sensorModeName());
      return true;
    }
    const String arg = lower.substring(sp + 1);
    if (arg == "int" || arg == "interrupt") {
      g_sensorMode = SENSOR_MODE_INTERRUPT;
    } else if (arg == "diff" || arg == "diffuse") {
      g_sensorMode = SENSOR_MODE_DIFFUSE;
    } else if (arg == "ao" || arg == "analog") {
      g_sensorMode = SENSOR_MODE_ANALOG;
    } else {
      logln("[err] mode int|diff|ao");
      return true;
    }
    sensorApplyMode();
    logf("[mode] %s\n", sensorModeName());
    return true;
  }

  if (lower.startsWith("aoth")) {
    const int sp = lower.indexOf(' ');
    if (sp < 0) {
      logf("[aoth] %d\n", g_aoThreshold);
      return true;
    }
    const int v = lower.substring(sp + 1).toInt();
    if (v < 0 || v > 1023) {
      logln("[err] aoth 0..1023");
      return true;
    }
    g_aoThreshold = v;
    logf("[aoth] %d\n", g_aoThreshold);
    return true;
  }

  if (lower.startsWith("aodir")) {
    const int sp = lower.indexOf(' ');
    if (sp < 0) {
      logf("[aodir] %s\n", g_aoHitWhenBelow ? "low" : "high");
      return true;
    }
    const String arg = lower.substring(sp + 1);
    if (arg == "low") {
      g_aoHitWhenBelow = true;
    } else if (arg == "high") {
      g_aoHitWhenBelow = false;
    } else {
      logln("[err] aodir low|high");
      return true;
    }
    logf("[aodir] %s\n", g_aoHitWhenBelow ? "low" : "high");
    return true;
  }

  return false;
}
