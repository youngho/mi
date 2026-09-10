/*
 * throughBeamGate.ino — 정적 투과빔 1줄 Go/No-Go
 *
 *  copier 모터 없음. 스캔 없음. HID 없음.
 *
 * mode irq : LM393 DO → Pin5 (느림. 손가락/느린 드라이버만)
 * mode adc : PIN 포토다이오드 → A1 을 빠르게 샘플 (BB 후보)
 *
 * Board : Teensy 4.1
 * USB   : Tools → USB Type → Serial
 *
 * 배선: teensy41/throughBeamGate/README.md
 */

#include <Arduino.h>
#include <string.h>
#include <ADC.h>
#include "TftLog.h"

static const int PIN_LASER = 9;
static const int PIN_SENSOR_DO = 5;
static const int PIN_SENSOR_AO = A1;  // 15. A0=14 는 TFT BL

static volatile bool g_doActiveLow = true;
static const bool LASER_DEFAULT_ON = false;

static const uint32_t BREAK_MIN_US = 4;
static const uint32_t BB_MAX_US = 3000;
static const uint32_t BLOCK_MAX_US = 2000000;

static ADC g_adc;
static uint32_t g_adcSamples = 0;

enum GateMode : uint8_t {
  GATE_IRQ = 0,
  GATE_ADC = 1,
};

static GateMode g_mode = GATE_IRQ;
static bool g_laserOn = false;
static bool g_doIrq = false;

static volatile bool g_beamPresent = false;
static volatile bool g_pulseActive = false;
static volatile uint32_t g_pulseStartUs = 0;
static volatile uint32_t g_hitWidthUs = 0;
static volatile bool g_hitPending = false;

static uint32_t g_bbHits = 0;
static uint32_t g_fingerHits = 0;
static bool g_lastBeamPrint = false;
static uint32_t g_lastStatusMs = 0;

static int g_aoth = 400;
static bool g_adcHitWhenBelow = true;  // 빛=전압 높음, 차단=떨어짐
static bool g_adcPulse = false;
static uint32_t g_adcStartUs = 0;

static char g_cmdBuf[48];
static uint8_t g_cmdLen = 0;

static inline bool beamPresentRaw()
{
  const bool doHigh = digitalReadFast(PIN_SENSOR_DO) == HIGH;
  return g_doActiveLow ? !doHigh : doHigh;
}

static void gateIsr()
{
  const bool beam = beamPresentRaw();
  g_beamPresent = beam;
  const uint32_t now = micros();

  if (!beam && !g_pulseActive) {
    g_pulseStartUs = now;
    g_pulseActive = true;
  } else if (beam && g_pulseActive) {
    const uint32_t w = now - g_pulseStartUs;
    g_pulseActive = false;
    if (w >= BREAK_MIN_US && w <= BLOCK_MAX_US) {
      g_hitWidthUs = w;
      g_hitPending = true;
    }
  }
}

static void attachDoIrq()
{
  if (g_doIrq) {
    return;
  }
  attachInterrupt(digitalPinToInterrupt(PIN_SENSOR_DO), gateIsr, CHANGE);
  g_doIrq = true;
}

static void detachDoIrq()
{
  if (!g_doIrq) {
    return;
  }
  detachInterrupt(digitalPinToInterrupt(PIN_SENSOR_DO));
  g_doIrq = false;
}

static void setLaser(bool on)
{
  g_laserOn = on;
  digitalWrite(PIN_LASER, on ? HIGH : LOW);
  logf("[laser] %s\n", on ? "on" : "off");
}

static void resetPulseState()
{
  noInterrupts();
  g_pulseActive = false;
  g_hitPending = false;
  g_adcPulse = false;
  interrupts();
}

static void emitHit(uint32_t w)
{
  if (w < BREAK_MIN_US || w > BLOCK_MAX_US) {
    return;
  }
  if (w <= BB_MAX_US) {
    ++g_bbHits;
    logf("[bb] w=%luus n=%lu\n", (unsigned long)w, (unsigned long)g_bbHits);
  } else {
    ++g_fingerHits;
    logf("[hand] w=%lums\n", (unsigned long)(w / 1000UL));
  }
}

static bool takeHit(uint32_t *widthUs)
{
  if (!g_hitPending) {
    return false;
  }
  noInterrupts();
  *widthUs = g_hitWidthUs;
  g_hitPending = false;
  interrupts();
  return true;
}

static bool adcIsDark(int v)
{
  return g_adcHitWhenBelow ? (v < g_aoth) : (v > g_aoth);
}

static void pollAdcGate()
{
  const int v = g_adc.adc0->analogRead(PIN_SENSOR_AO);
  const bool dark = adcIsDark(v);
  const uint32_t now = micros();
  ++g_adcSamples;

  if (dark && !g_adcPulse) {
    g_adcPulse = true;
    g_adcStartUs = now;
  } else if (!dark && g_adcPulse) {
    const uint32_t w = now - g_adcStartUs;
    g_adcPulse = false;
    emitHit(w);
  }
}

static void configureAdcFast()
{
  analogReadAveraging(1);
  analogReadResolution(10);
  g_adc.adc0->setAveraging(1);
  g_adc.adc0->setResolution(10);
  g_adc.adc0->setConversionSpeed(ADC_CONVERSION_SPEED::VERY_HIGH_SPEED);
  g_adc.adc0->setSamplingSpeed(ADC_SAMPLING_SPEED::VERY_HIGH_SPEED);
}

static void setMode(GateMode m)
{
  resetPulseState();
  g_mode = m;
  if (m == GATE_ADC) {
    detachDoIrq();
    configureAdcFast();
    g_adcSamples = 0;
    logln("[mode] adc");
    logln(" PD on A1, aoth");
  } else {
    analogReadAveraging(4);
    attachDoIrq();
    g_beamPresent = beamPresentRaw();
    g_lastBeamPrint = g_beamPresent;
    logln("[mode] irq");
  }
}

static void printSensor()
{
  logf(" mode=%s\n", g_mode == GATE_ADC ? "adc" : "irq");
  logf(" beam=%d DO=%d\n", beamPresentRaw() ? 1 : 0, digitalRead(PIN_SENSOR_DO));
  logf(" AO=%d aoth=%d\n", analogRead(PIN_SENSOR_AO), g_aoth);
  logf(" adir=%s al=%d\n", g_adcHitWhenBelow ? "low" : "high", g_doActiveLow ? 1 : 0);
  logf(" bb=%lu fn=%lu\n", (unsigned long)g_bbHits, (unsigned long)g_fingerHits);
}

static void printHelp()
{
  logln("[help] through-beam");
  logln(" laser on | off");
  logln(" mode irq | adc");
  logln(" aoth <0-1023>");
  logln(" sensor | flip");
  logln(" reset | cls");
  logln(" help");
}

static void handleLine(char *line)
{
  if (line == nullptr) {
    return;
  }
  while (*line == ' ' || *line == '\t') {
    ++line;
  }
  if (*line == '\0') {
    return;
  }
  for (char *p = line; *p; ++p) {
    if (*p >= 'A' && *p <= 'Z') {
      *p = (char)(*p - 'A' + 'a');
    }
  }

  if (strcmp(line, "help") == 0 || strcmp(line, "?") == 0) {
    printHelp();
    return;
  }
  if (strcmp(line, "laser on") == 0) {
    setLaser(true);
    return;
  }
  if (strcmp(line, "laser off") == 0) {
    setLaser(false);
    return;
  }
  if (strcmp(line, "sensor") == 0 || strcmp(line, "status") == 0) {
    logln("[cmd] sensor");
    printSensor();
    return;
  }
  if (strcmp(line, "mode irq") == 0) {
    setMode(GATE_IRQ);
    return;
  }
  if (strcmp(line, "mode adc") == 0) {
    setMode(GATE_ADC);
    return;
  }
  if (strcmp(line, "mode") == 0) {
    logf("[mode] %s\n", g_mode == GATE_ADC ? "adc" : "irq");
    return;
  }
  if (strncmp(line, "aoth", 4) == 0) {
    const char *p = line + 4;
    while (*p == ' ') {
      ++p;
    }
    if (*p == '\0') {
      logf("[aoth] %d\n", g_aoth);
      return;
    }
    const int v = atoi(p);
    if (v < 0 || v > 1023) {
      logln("[err] aoth 0..1023");
      return;
    }
    g_aoth = v;
    logf("[aoth] %d\n", g_aoth);
    return;
  }
  if (strcmp(line, "flip") == 0) {
    if (g_mode == GATE_ADC) {
      g_adcHitWhenBelow = !g_adcHitWhenBelow;
      resetPulseState();
      logf("[flip] adir=%s\n", g_adcHitWhenBelow ? "low" : "high");
    } else {
      g_doActiveLow = !g_doActiveLow;
      resetPulseState();
      g_beamPresent = beamPresentRaw();
      logf("[flip] al=%d\n", g_doActiveLow ? 1 : 0);
    }
    return;
  }
  if (strcmp(line, "reset") == 0) {
    g_bbHits = 0;
    g_fingerHits = 0;
    logln("[ok] count 0");
    return;
  }
  if (strcmp(line, "cls") == 0 || strcmp(line, "clear") == 0) {
    tftClear();
    logln("[ok] cls");
    return;
  }

  logln("[err] help");
}

static void pollSerial()
{
  while (Serial.available() > 0) {
    const char c = (char)Serial.read();
    if (c == '\n' || c == '\r') {
      if (g_cmdLen > 0) {
        g_cmdBuf[g_cmdLen] = '\0';
        g_cmdLen = 0;
        handleLine(g_cmdBuf);
      }
    } else if ((size_t)g_cmdLen + 1 < sizeof(g_cmdBuf)) {
      g_cmdBuf[g_cmdLen++] = c;
    }
  }
}

void setup()
{
  Serial.begin(115200);
  pinMode(PIN_LASER, OUTPUT);
  digitalWrite(PIN_LASER, LOW);
  pinMode(PIN_SENSOR_DO, INPUT);
  analogReadResolution(10);
  analogReadAveraging(4);

  tftLogBegin("GATE 1-BEAM");
  attachDoIrq();
  g_beamPresent = beamPresentRaw();
  g_lastBeamPrint = g_beamPresent;

  logln("motor OFF");
  logln("LM393 irq=slow");
  logln("PD? mode adc");
  printHelp();
  if (LASER_DEFAULT_ON) {
    setLaser(true);
  } else {
    logln("type: laser on");
  }
}

void loop()
{
  if (g_mode == GATE_ADC) {
    for (int i = 0; i < 64; ++i) {
      pollAdcGate();
    }
    pollSerial();
    const uint32_t now = millis();
    if (now - g_lastStatusMs >= 1000) {
      g_lastStatusMs = now;
      const int ao = g_adc.adc0->analogRead(PIN_SENSOR_AO);
      Serial.printf(" hb adc ao=%d aoth=%d sps=%lu\n",
                    ao, g_aoth, (unsigned long)g_adcSamples);
      g_adcSamples = 0;
    }
    return;
  }

  uint32_t w = 0;
  if (takeHit(&w)) {
    emitHit(w);
  }

  const bool beam = beamPresentRaw();
  if (beam != g_lastBeamPrint) {
    g_lastBeamPrint = beam;
    logf("[beam] %d\n", beam ? 1 : 0);
  }

  const uint32_t now = millis();
  if (now - g_lastStatusMs >= 1000) {
    g_lastStatusMs = now;
    Serial.printf(" hb beam=%d ao=%d\n", beam ? 1 : 0, analogRead(PIN_SENSOR_AO));
  }

  pollSerial();
}
