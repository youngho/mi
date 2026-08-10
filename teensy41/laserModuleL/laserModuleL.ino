/*
 * laserModuleL.ino
 *
 * Teensy 4.1 기반 BB탄/터치 위치 검출 모듈 — Node A (Left / Slave)
 *
 * 스크린 왼쪽 하단에 설치되어, 오른쪽(수평)과 위쪽(수직)으로
 * 레이저를 발사하여 BB탄/손가락 통과 위치를 검출한다.
 *
 * Board : Teensy 4.1
 * Role  : Left-side laser detection module (Slave)
 *
 * ===========================================================================
 * 전체 데이터 흐름
 * ===========================================================================
 *
 *   [ Node A — Teensy #1 Left / Slave ]   [ Node B — Teensy #2 Right / Master ]
 *   ┌─────────────────────────────┐      ┌──────────────────────────────────┐
 *   │ · 0° Sync 감지              │ UART │ · 0° Sync 감지                   │
 *   │ · 터치 각도 θ1 계산         │─────►│ · 터치 각도 θ2 계산              │
 *   │ · θ1 패킷 송신 (Serial1)    │ θ1   │ · 삼각측량 (X,Y) 최종 계산       │
 *   └─────────────────────────────┘      │ · USB HID → PC (X,Y / click)     │
 *                                         └──────────────────────────────────┘
 *
 * 1) Node A (이 스케치) 역할
 *    - 왼쪽 스캐너 0° Sync 대비 HIT 시각 차로 θ1 산출
 *    - θ1 을 Serial1(UART)로 Master(오른쪽)에 전송
 *
 * 2) Node B (laserModuleR.ino) 역할
 *    - 자체 θ2 산출 + θ1 수신 → 삼각측량 → USB HID
 *
 * 3) 보드 간 UART 배선 (Serial1)
 *    Teensy #1 TX1 (Pin 1) ──────────────► Teensy #2 RX1 (Pin 0)
 *    Teensy #1 RX1 (Pin 0) ◄────────────── Teensy #2 TX1 (Pin 1)
 *    Teensy #1 GND         ─────────────── Teensy #2 GND  (공통 필수)
 *
 * 4) 패킷 (ASCII, Serial1 @ LINK_BAUD)
 *    L → R  T1,<centideg>,<t_us>\n
 *      예: T1,4850,12345678  → θ1 = 48.50°, micros() 타임스탬프
 *    R → L  M,1[,<hz>]\n  모터 기동 동기 (hz 생략 시 L 현재/기본 CLK)
 *           M,0\n         모터 정지 동기
 *      ※ USB 시리얼 start/stop 도 그대로 사용 가능 (로컬 우선)
 *    R → L  P,<nonce>\n   링크 ping
 *    L → R  P,<nonce>\n   ping echo (동일 nonce 반환)
 *
 * 5) 삼각측량 (Master 측)
 *    tan(θ1)=Y/X , tan(θ2)=Y/(W-X)
 *    X = W·tan(θ2)/(tan(θ1)+tan(θ2)) , Y = X·tan(θ1)
 *
 * ===========================================================================
 * Polygon Mirror Motor — Ricoh MP C3502 / PWB MOTOR UNIT 7M0502
 * Driver IC: LB11876 (외부 FET + 24V 단전원 구성)
 * ===========================================================================
 * Motor PCB CN1 (실크: C | L | S | G | V)
 *
 *   C (CLK)  → Teensy PIN_MOTOR_CLK  (오픈드레인: LOW만 구동)
 *   L (LD)   → Teensy PIN_MOTOR_LD   (INPUT_PULLUP, LOW=위상 락)
 *   S (S/S)  → Teensy PIN_MOTOR_SS   (오픈드레인: LOW=기동, Hi-Z=정지)
 *   G (GND)  → Teensy GND + 24V 전원 GND (반드시 공통)
 *   V (VCC)  → 외부 24V DC (모터/FET 전원). Teensy에 연결 금지.
 *
 * LB11876 요약:
 *   - 모터 전원(V)은 리코 레이저 유닛터와 동일하게 24V.
 *     (IC 자체 VCC는 8~17V이고, 보드가 V13 션트 레귤로 ~13V를 만들어 씀)
 *   - CLK / S/S : Low=0~1.0V, High=2.0V~VREG, open=High(정지)
 *     → Teensy 3.3V도 VIH(2.0V)를 만족하지만, 보드 풀업을 쓰는
 *       오픈드레인 구동을 유지한다.
 *   - LD : 락 시 오픈콜렉터 LOW
 *   - CLK 0.1~10 kHz. fFG(servo) = fCLK ÷ CLKSEL (1 또는 2)
 *
 * ===========================================================================
 * 전원 구조 (LM2596 → Teensy VIN / 3.3V / 레이저 / 센서)
 * ===========================================================================
 * Teensy 4.1 MCU는 내부 3.3V로 동작한다. 외부 급전 시 5V를 VIN에 넣으면
 * 보드 내장 레귤레이터가 3.3V로 강압한다.
 *
 * USB 포트가 위쪽일 때 우측 상단 핀열:
 *   1행: VIN  (5V 입력)  ← LM2596 OUT+
 *   2행: GND             ← LM2596 OUT-
 *   3행: 3.3V (출력)     ← 레이저/센서 3.3V 급전용
 *
 *   [LM2596 (5.0V 세팅)]
 *      OUT+ (5V) ──► Teensy VIN
 *      OUT- (GND)──► Teensy GND ──► 센서 GND / 레이저 GND 경로 / 모터 GND(공통)
 *
 *   포토다이오드 센서 VCC → Teensy 3.3V 권장
 *     (5V VCC면 DO가 5V일 수 있음. Teensy 4.1 핀은 5V 비내성!)
 *
 * ---------------------------------------------------------------------------
 * 650nm 라인 레이저 (3V~5V) — Pin 9 + NPN 로우사이드 스위칭 (방법 B)
 * ---------------------------------------------------------------------------
 *   레이저 RED  (+) ──► Teensy 3.3V  (또는 LM2596 5V)
 *   레이저 BLACK(-) ──► NPN Collector (2N2222 등)
 *   NPN Emitter     ──► GND (공통)
 *   Teensy Pin 9    ──► 베이스 저항(~1kΩ) ──► NPN Base
 *
 *   Pin9 HIGH → 트랜지스터 ON → 레이저 ON
 *   Pin9 LOW  → 트랜지스터 OFF → 레이저 OFF
 *   ※ 레이저 GND를 Teensy 핀에 직접 물리지 말 것 (과전류 위험)
 *
 * ---------------------------------------------------------------------------
 * Photodiode / LM393 센서 모듈 (레이저 반사·차단 검출)
 * ---------------------------------------------------------------------------
 *   보드 실크 핀 배열 (센서·가변저항 쪽을 위, 4핀 헤더를 아래로 볼 때
 *   왼쪽 → 오른쪽):
 *
 *        [ 포토다이오드 ]     ← 보드 상단
 *        (파란 가변저항 / LM393)
 *     ┌──┬──┬───┬───┐
 *     │AO│DO│GND│VCC│       ← 4핀 헤더 (실크 그대로)
 *     └──┴──┴───┴───┘
 *
 *   Teensy 연결 (필수):
 *     모듈 AO  → Teensy A1   (PIN_SENSOR_AO=15, 아날로그 세기·디버그)
 *                   ※ A0(14)는 TFT BL과 충돌하므로 사용하지 않음
 *     모듈 DO  → Teensy Pin5 (PIN_SENSOR_DO, HIT 디지털)
 *     모듈 GND → Teensy GND  (공통)
 *     모듈 VCC → Teensy 3.3V (※ 5V 금지 — DO가 5V면 Teensy 4.1 손상 위험)
 *
 *   실배선 색 예 (보드마다 점퍼 색은 달라도 됨 — 실크 기준):
 *     AO=노랑, DO=주황, GND=빨강, VCC=갈/밤색
 *
 *   DO-LED: 디지털 출력 상태 / PWR-LED: 전원
 *   가변저항: DO 임계(감도). HIT가 항상 1이면 임계·주변광 조정
 *
 *   보드마다 DO 극성이 다름:
 *     SENSOR_DO_ACTIVE_LOW=true  → DO=LOW 일 때 HIT (흔한 LM393 보드)
 *     SENSOR_DO_ACTIVE_LOW=false → DO=HIGH 일 때 HIT
 *
 * ---------------------------------------------------------------------------
 * Sync (0° 기준) — 폴리곤/광학 Sync 펄스
 * ---------------------------------------------------------------------------
 *   Sync OUT → Teensy Pin6 (PIN_SYNC, INPUT_PULLUP, FALLING=0° 권장)
 *   Sync 미연결 시: USB 시리얼 `theta <deg>` 로 θ1 수동 송신 가능
 *
 * ---------------------------------------------------------------------------
 * ST7789 TFT 로그 디스플레이 — 1.54TFT-SPI-ST7789 (Ver 1.1, 240×240)
 * ---------------------------------------------------------------------------
 * 보드 뒷면 8핀 헤더 (왼쪽 → 오른쪽):
 *     BL | CS | DC | RES | SDA | SCL | VCC | GND
 *
 *   Teensy 4.1 연결 (L/R 동일, 하드웨어 SPI0):
 *     TFT GND  → Teensy GND
 *     TFT VCC  → Teensy 3.3V  (보드 레귤 있음 — 5V도 가능하나 3.3V 권장)
 *     TFT SCL  → Teensy Pin 13  (SCK)
 *     TFT SDA  → Teensy Pin 11  (MOSI)  ※ MISO(12) 불필요
 *     TFT RES  → Teensy Pin 7   (PIN_TFT_RST)
 *     TFT DC   → Teensy Pin 8   (PIN_TFT_DC)
 *     TFT CS   → Teensy Pin 10  (PIN_TFT_CS)
 *     TFT BL   → Teensy Pin 14  (PIN_TFT_BL, HIGH=백라이트 ON)
 *                또는 3.3V에 직결(항상 ON)
 *
 *   라이브러리: Teensyduino 내장 ST7735_t3 / ST7789_t3
 *   동작: USB Serial 로그가 TFT에도 스크롤 표시 (보드 생존 확인용)
 *   ※ logln/logf 작성 규칙 (TftLog.h 참고):
 *        TFT_LOG_COLS=20, TFT_LOG_ROWS=15 (size2) — 한 줄·한 화면 안에 맞출 것
 *        한글은 TFT에 안 보임 → ASCII 위주
 *   ※ 화면이 안 나오면 TftLog.h 의 init(240,240) 를 init(240,240,SPI_MODE2) 로 시도
 * ---------------------------------------------------------------------------
 */

#include <Arduino.h>
#include "TftLog.h"

// ---- Pin map (필요시 변경) ----
static const int PIN_MOTOR_CLK = 2;   // C
static const int PIN_MOTOR_LD  = 3;   // L
static const int PIN_MOTOR_SS  = 4;   // S
static const int PIN_SENSOR_DO = 5;   // 포토다이오드 모듈 DO
static const int PIN_SENSOR_AO = A1;  // 포토다이오드 모듈 AO (A0=14는 TFT BL)
static const int PIN_SYNC      = 6;   // 0° Sync 입력
static const int PIN_LASER     = 9;   // NPN 베이스 구동 (HIGH=레이저 ON)

// DO=LOW 를 HIT로 볼지 여부 (보드마다 다름 — 반대로면 false)
static const bool SENSOR_DO_ACTIVE_LOW = true;

// setup 직후 레이저 기본 상태 (true면 부팅 시 자동 ON)
static const bool LASER_DEFAULT_ON = true;

// Sync FALLING 을 0°로 볼지 (보드/옵토에 맞게)
static const bool SYNC_ACTIVE_FALLING = true;

// 한 Sync 주기 동안 스캔하는 광학 각도 범위 [deg]
static const float SCAN_ANGLE_DEG = 90.0f;

// Node A ↔ Node B UART (Serial1: RX0 / TX1)
static const uint32_t LINK_BAUD = 1000000;

// ---- Motor defaults ----
static const uint32_t CLK_HZ_DEFAULT = 2000;  // 시작용 보수적 값 (1~10 kHz 권장)
static const uint32_t CLK_HZ_MIN     = 100;
static const uint32_t CLK_HZ_MAX     = 10000;

static IntervalTimer g_clkTimer;
static volatile uint32_t g_clkHz = CLK_HZ_DEFAULT;
static volatile bool g_clkHigh = true;
static bool g_running = false;
static bool g_lastLocked = false;
static bool g_lastHit = false;
static bool g_sensorReady = false;
static bool g_laserOn = false;

static volatile uint32_t g_syncUs = 0;
static volatile uint32_t g_periodUs = 0;
static volatile bool g_syncSeen = false;

// 오픈드레인: LOW = 출력 LOW, HIGH = 입력(Hi-Z) → 보드 풀업
static inline void odWrite(int pin, bool high)
{
  if (high) {
    pinMode(pin, INPUT);
  } else {
    pinMode(pin, OUTPUT);
    digitalWrite(pin, LOW);
  }
}

static void clkIsr()
{
  g_clkHigh = !g_clkHigh;
  odWrite(PIN_MOTOR_CLK, g_clkHigh);
}

static void syncIsr()
{
  const uint32_t now = micros();
  if (g_syncSeen) {
    g_periodUs = now - g_syncUs;
  }
  g_syncUs = now;
  g_syncSeen = true;
}

static bool applyClockHz(uint32_t hz)
{
  if (hz < CLK_HZ_MIN || hz > CLK_HZ_MAX) {
    return false;
  }
  g_clkHz = hz;
  const uint32_t halfPeriodUs = 500000UL / hz;
  g_clkTimer.update(halfPeriodUs);
  return true;
}

static void startClock(uint32_t hz)
{
  g_clkHz = constrain(hz, CLK_HZ_MIN, CLK_HZ_MAX);
  const uint32_t halfPeriodUs = 500000UL / g_clkHz;
  g_clkHigh = true;
  odWrite(PIN_MOTOR_CLK, true);
  logf("[clk] start %lu Hz\n", (unsigned long)g_clkHz);
  logf("[clk] half=%lu us\n", (unsigned long)halfPeriodUs);
  g_clkTimer.begin(clkIsr, halfPeriodUs);
  logln("[clk] OD toggle on");
}

static void stopClock()
{
  logln("[clk] timer stop");
  g_clkTimer.end();
  odWrite(PIN_MOTOR_CLK, true);  // Hi-Z
  logln("[clk] pin Hi-Z");
}

static void motorStart(uint32_t hz = 0)
{
  if (hz == 0) {
    hz = g_clkHz;
  }
  if (g_running) {
    // 이미 기동 중이면 CLK만 필요 시 갱신 (R 주기 동기 M,1 중복 무시)
    if (hz != g_clkHz) {
      applyClockHz(hz);
      logf("[motor] run clk=%lu\n", (unsigned long)g_clkHz);
    }
    return;
  }
  logln();
  logln("[motor] START");
  logf("[motor] 1/4 clk=%lu\n", (unsigned long)hz);

  logln("[motor] 2/4 CLK on");
  startClock(hz);
  delay(5);
  logln("[motor] 3/4 S/S LOW");
  odWrite(PIN_MOTOR_SS, false);  // start
  g_running = true;
  g_lastLocked = false;
  logln("[motor] 4/4 wait LD");
  logf("[motor] OK clk=%lu\n", (unsigned long)g_clkHz);
}

static void motorStop()
{
  if (!g_running) {
    return;
  }
  logln();
  logln("[motor] STOP");
  logln("[motor] 1/3 S/S Hi-Z");
  odWrite(PIN_MOTOR_SS, true);  // stop (Hi-Z → 내부 풀업)
  delay(2);
  logln("[motor] 2/3 CLK off");
  stopClock();
  g_running = false;
  g_lastLocked = false;
  logln("[motor] 3/3 cleared");
  logln("[motor] STOP done");
}

static bool isLocked()
{
  return digitalRead(PIN_MOTOR_LD) == LOW;
}

static bool isSensorHit()
{
  const bool doHigh = digitalRead(PIN_SENSOR_DO) == HIGH;
  return SENSOR_DO_ACTIVE_LOW ? !doHigh : doHigh;
}

static void setLaser(bool on)
{
  digitalWrite(PIN_LASER, on ? HIGH : LOW);
  g_laserOn = on;
  logf("[laser] %s pin%d\n", on ? "ON" : "OFF", PIN_LASER);
}

// Sync 이후 HIT 시각으로 θ1 [deg] 산출. 실패 시 false.
static bool computeTheta1(float *outDeg)
{
  if (!g_syncSeen || g_periodUs == 0) {
    return false;
  }
  noInterrupts();
  const uint32_t syncUs = g_syncUs;
  const uint32_t periodUs = g_periodUs;
  interrupts();

  const uint32_t now = micros();
  const uint32_t dt = now - syncUs;
  if (dt >= periodUs) {
    return false;
  }
  *outDeg = (dt / (float)periodUs) * SCAN_ANGLE_DEG;
  return true;
}

// Master로 θ1 전송: T1,<centideg>,<t_us>\n
static void sendTheta1(float deg)
{
  const int32_t centi = (int32_t)lroundf(deg * 100.0f);
  const uint32_t t = micros();
  Serial1.printf("T1,%ld,%lu\n", (long)centi, (unsigned long)t);
  logf("[link] TX T1 %.2f\n", deg);
}

static void pollSensor()
{
  if (!g_sensorReady) {
    return;
  }

  const bool hit = isSensorHit();
  if (hit == g_lastHit) {
    return;
  }
  g_lastHit = hit;

  const int ao = analogRead(PIN_SENSOR_AO);
  if (hit) {
    logf("[hit] DO=%d\n", digitalRead(PIN_SENSOR_DO));
    logf("[hit] AO=%d\n", ao);
    float th = 0.0f;
    if (computeTheta1(&th)) {
      sendTheta1(th);
    } else {
      logln("[hit] no sync");
      logln(" use: theta <deg>");
    }
  } else {
    logf("[clr] DO=%d\n", digitalRead(PIN_SENSOR_DO));
    logf("[clr] AO=%d\n", ao);
  }
}

// Serial1: Master → Slave 모터 동기 M,1[,hz] / M,0  /  ping P,<nonce>
static void handleLinkSerial()
{
  static String line;
  while (Serial1.available() > 0) {
    const char c = (char)Serial1.read();
    if (c == '\r') {
      continue;
    }
    if (c != '\n') {
      if (line.length() < 64) {
        line += c;
      }
      continue;
    }

    line.trim();
    if (line.startsWith("M,")) {
      // M,1  |  M,1,<hz>  |  M,0
      const int c1 = line.indexOf(',');
      const int c2 = line.indexOf(',', c1 + 1);
      const int run = line.substring(c1 + 1, c2 > c1 ? c2 : (int)line.length()).toInt();
      if (run == 0) {
        logln("[link] RX M,0 stop");
        motorStop();
      } else if (run == 1) {
        uint32_t hz = g_clkHz;
        if (c2 > c1) {
          const uint32_t parsed = (uint32_t)line.substring(c2 + 1).toInt();
          if (parsed >= CLK_HZ_MIN && parsed <= CLK_HZ_MAX) {
            hz = parsed;
          }
        }
        logf("[link] RX M,1 %lu\n", (unsigned long)hz);
        motorStart(hz);
      } else {
        logf("[link] bad M: %.12s\n", line.c_str());
      }
    } else if (line == "P" || line.startsWith("P,")) {
      // Master ping → 동일 라인 echo (P,<nonce>)
      if (line == "P") {
        Serial1.print("P,ok\n");
        logln("[link] ping->P,ok");
      } else {
        Serial1.printf("%s\n", line.c_str());
        logf("[link] ping TX %.10s\n", line.c_str());
      }
    } else if (line.length() > 0) {
      logf("[link] ign %.14s\n", line.c_str());
    }
    line = "";
  }
}

static void printHelp()
{
  // ≤ TFT_LOG_ROWS(15) lines, each ≤ TFT_LOG_COLS(20)
  logln();
  logln("L Slave cmds");
  logln(" start [hz]");
  logln(" stop");
  logln(" clk <hz>");
  logln(" status");
  logln(" sensor");
  logln(" laser on|off");
  logln(" theta <deg>");
  logln(" help");
  logln("R: start->M,1");
  logln("R: stop->M,0");
  logln("R: ping->P,n");
  logln();
}

static void handleSerial()
{
  static String line;
  while (Serial.available() > 0) {
    const char c = (char)Serial.read();
    if (c == '\r') {
      continue;
    }
    if (c != '\n') {
      line += c;
      continue;
    }

    line.trim();
    String lower = line;
    lower.toLowerCase();

    if (lower.length() == 0) {
      // keep empty
    } else if (lower == "help" || lower == "?") {
      printHelp();
    } else if (lower == "stop") {
      logln("[cmd] stop");
      motorStop();
    } else if (lower == "status") {
      logln("[cmd] status");
      logf(" run=%d lk=%d\n", g_running ? 1 : 0, isLocked() ? 1 : 0);
      logf(" clk=%lu hit=%d\n",
                    (unsigned long)g_clkHz, isSensorHit() ? 1 : 0);
      logf(" DO=%d AO=%d\n",
                    digitalRead(PIN_SENSOR_DO), analogRead(PIN_SENSOR_AO));
      logf(" sync=%d p=%lu\n",
                    g_syncSeen ? 1 : 0, (unsigned long)g_periodUs);
      logf(" laser=%d\n", g_laserOn ? 1 : 0);
    } else if (lower == "sensor") {
      logln("[cmd] sensor");
      logf(" hit=%d DO=%d\n",
                    isSensorHit() ? 1 : 0, digitalRead(PIN_SENSOR_DO));
      logf(" AO=%d al=%d\n",
                    analogRead(PIN_SENSOR_AO),
                    SENSOR_DO_ACTIVE_LOW ? 1 : 0);
    } else if (lower.startsWith("laser")) {
      const int sp = lower.indexOf(' ');
      if (sp < 0) {
        logln("[err] laser on|off");
      } else {
        const String arg = lower.substring(sp + 1);
        if (arg == "on") {
          setLaser(true);
        } else if (arg == "off") {
          setLaser(false);
        } else {
          logln("[err] laser on|off");
        }
      }
    } else if (lower.startsWith("theta")) {
      const int sp = lower.indexOf(' ');
      if (sp < 0) {
        logln("[err] theta <deg>");
      } else {
        const float deg = lower.substring(sp + 1).toFloat();
        logf("[cmd] theta %.2f\n", deg);
        sendTheta1(deg);
      }
    } else if (lower.startsWith("start")) {
      logln("[cmd] start");
      uint32_t hz = g_clkHz;
      const int sp = lower.indexOf(' ');
      if (sp > 0) {
        hz = (uint32_t)lower.substring(sp + 1).toInt();
      }
      if (hz < CLK_HZ_MIN || hz > CLK_HZ_MAX) {
        logf("[err] clk %lu..%lu\n",
                      (unsigned long)CLK_HZ_MIN, (unsigned long)CLK_HZ_MAX);
      } else {
        motorStart(hz);
      }
    } else if (lower.startsWith("clk")) {
      logln("[cmd] clk");
      const int sp = lower.indexOf(' ');
      if (sp < 0) {
        logln("[err] clk <hz>");
      } else {
        const uint32_t hz = (uint32_t)lower.substring(sp + 1).toInt();
        logf("[clk] try %lu\n", (unsigned long)hz);
        if (!applyClockHz(hz)) {
          logf("[err] clk %lu..%lu\n",
                        (unsigned long)CLK_HZ_MIN, (unsigned long)CLK_HZ_MAX);
        } else if (!g_running) {
          logf("[ok] clk=%lu idle\n", (unsigned long)g_clkHz);
        } else {
          logf("[ok] clk=%lu run\n", (unsigned long)g_clkHz);
        }
      }
    } else {
      logf("[err] %.14s\n", line.c_str());
      printHelp();
    }

    line = "";
  }
}

void setup()
{
  Serial.begin(115200);
  while (!Serial && millis() < 2000) {
    // USB CDC wait (optional)
  }

  Serial1.begin(LINK_BAUD);

  tftLogBegin("Node A / Left");

  logln();
  logln("[setup] L Slave");
  logln("1/8 USB+UART");
  logf(" baud=%lu TX1=1\n", (unsigned long)LINK_BAUD);
  logln("2/8 TFT CS10");

  logln("3/8 LD pullup");
  pinMode(PIN_MOTOR_LD, INPUT_PULLUP);

  logln("4/8 S/S CLK HiZ");
  odWrite(PIN_MOTOR_SS, true);   // stop
  odWrite(PIN_MOTOR_CLK, true);  // idle high (Hi-Z)

  logln("5/8 sensor I/O");
  pinMode(PIN_SENSOR_DO, INPUT);
  g_lastHit = isSensorHit();
  g_sensorReady = true;
  logf(" hit=%d DO=%d\n",
                g_lastHit ? 1 : 0, digitalRead(PIN_SENSOR_DO));

  logln("6/8 Sync IRQ");
  pinMode(PIN_SYNC, INPUT_PULLUP);
  attachInterrupt(digitalPinToInterrupt(PIN_SYNC), syncIsr,
                  SYNC_ACTIVE_FALLING ? FALLING : RISING);

  logln("7/8 laser pin9");
  pinMode(PIN_LASER, OUTPUT);
  setLaser(LASER_DEFAULT_ON);

  logln("8/8 ready");
  // 한 화면(15줄) 안에 들어가게 요약 — help는 `help` 명령으로
  logln("laserModuleL OK");
  logf("CLK=%lu scan=%.0f\n",
                (unsigned long)CLK_HZ_DEFAULT, SCAN_ANGLE_DEG);
  logln("VIN5V GND common");
  logln("AO|DO->A1|5");
  logln("Sync6 Laser9");
  logln("UART 1<->0 GND");
  logln("then: laser on");
  logln("      start");
  logln("or R: start");
  logln("[setup] done");
}

void loop()
{
  handleSerial();
  handleLinkSerial();
  pollSensor();

  const bool locked = isLocked();
  if (g_running && locked != g_lastLocked) {
    g_lastLocked = locked;
    if (locked) {
      logln("[motor] LOCKED");
    } else {
      logln("[motor] unlocked");
    }
  }

  delay(1);
}
