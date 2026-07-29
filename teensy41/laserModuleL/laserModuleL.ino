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
 *    T1,<centideg>,<t_us>\n
 *      예: T1,4850,12345678  → θ1 = 48.50°, micros() 타임스탬프
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
 * Photodiode / LM393 센서 모듈 (레이저 수신)
 * ---------------------------------------------------------------------------
 *   VCC → Teensy 3.3V
 *   GND → Teensy GND (공통)
 *   DO  → Teensy PIN_SENSOR_DO
 *   AO  → Teensy PIN_SENSOR_AO (선택, 아날로그 세기)
 *
 *   보드마다 DO 극성이 다름:
 *     SENSOR_DO_ACTIVE_LOW=true  → DO=LOW 일 때 HIT (흔한 LM393 보드)
 *     SENSOR_DO_ACTIVE_LOW=false → DO=HIGH 일 때 HIT
 *
 * ---------------------------------------------------------------------------
 * Sync (0° 기준) — 폴리곤/광학 Sync 펄스
 * ---------------------------------------------------------------------------
 *   Sync OUT → Teensy PIN_SYNC (INPUT_PULLUP, FALLING=0° 권장)
 *   Sync 미연결 시: USB 시리얼 `theta <deg>` 로 θ1 수동 송신 가능
 * ---------------------------------------------------------------------------
 */

#include <Arduino.h>

// ---- Pin map (필요시 변경) ----
static const int PIN_MOTOR_CLK = 2;   // C
static const int PIN_MOTOR_LD  = 3;   // L
static const int PIN_MOTOR_SS  = 4;   // S
static const int PIN_SENSOR_DO = 5;   // 포토다이오드 모듈 DO
static const int PIN_SENSOR_AO = A0;  // 포토다이오드 모듈 AO (선택)
static const int PIN_SYNC      = 6;   // 0° Sync 입력
static const int PIN_LASER     = 9;   // NPN 베이스 구동 (HIGH=레이저 ON)

// DO=LOW 를 HIT로 볼지 여부 (보드마다 다름 — 반대로면 false)
static const bool SENSOR_DO_ACTIVE_LOW = true;

// setup 직후 레이저 기본 상태
static const bool LASER_DEFAULT_ON = false;

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
  Serial.printf("[stage] CLK 타이머 시작 요청 (목표 %lu Hz, 반주기 %lu us)\n",
                (unsigned long)g_clkHz, (unsigned long)halfPeriodUs);
  g_clkTimer.begin(clkIsr, halfPeriodUs);
  Serial.println("[stage] CLK 출력 중 (오픈드레인 토글)");
}

static void stopClock()
{
  Serial.println("[stage] CLK 타이머 정지");
  g_clkTimer.end();
  odWrite(PIN_MOTOR_CLK, true);  // Hi-Z
  Serial.println("[stage] CLK 핀 Hi-Z (idle)");
}

static void motorStart(uint32_t hz = 0)
{
  if (hz == 0) {
    hz = g_clkHz;
  }
  Serial.println();
  Serial.println("[stage] ===== 모터 START 시퀀스 시작 =====");
  Serial.printf("[stage] 1/4 목표 CLK = %lu Hz\n", (unsigned long)hz);

  Serial.println("[stage] 2/4 기준 클럭(CLK) 공급");
  startClock(hz);
  delay(5);
  Serial.println("[stage] 3/4 S/S = LOW (기동)");
  odWrite(PIN_MOTOR_SS, false);  // start
  g_running = true;
  g_lastLocked = false;
  Serial.println("[stage] 4/4 PLL 락 대기 중 (LD=LOW 되면 LOCKED)");
  Serial.printf("[motor] START 완료 — clk=%lu Hz, running=1\n",
                (unsigned long)g_clkHz);
}

static void motorStop()
{
  Serial.println();
  Serial.println("[stage] ===== 모터 STOP 시퀀스 시작 =====");
  Serial.println("[stage] 1/3 S/S = Hi-Z (정지, 내부 풀업)");
  odWrite(PIN_MOTOR_SS, true);  // stop (Hi-Z → 내부 풀업)
  delay(2);
  Serial.println("[stage] 2/3 기준 클럭 차단");
  stopClock();
  g_running = false;
  g_lastLocked = false;
  Serial.println("[stage] 3/3 상태 초기화 (running=0, locked=0)");
  Serial.println("[motor] STOP 완료");
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
  Serial.printf("[laser] %s (pin %d)\n", on ? "ON" : "OFF", PIN_LASER);
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
  Serial.printf("[link] TX T1 θ1=%.2f° (%ld cd) t=%lu\n",
                deg, (long)centi, (unsigned long)t);
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
    Serial.printf("[sensor] HIT  (DO=%d AO=%d)\n",
                  digitalRead(PIN_SENSOR_DO), ao);
    float th = 0.0f;
    if (computeTheta1(&th)) {
      sendTheta1(th);
    } else {
      Serial.println("[link] HIT but Sync/period 없음 — θ1 미송신 "
                     "(sync 배선 또는 `theta <deg>` 사용)");
    }
  } else {
    Serial.printf("[sensor] CLEAR (DO=%d AO=%d)\n",
                  digitalRead(PIN_SENSOR_DO), ao);
  }
}

static void printHelp()
{
  Serial.println();
  Serial.println("Commands (Node A / Left / Slave):");
  Serial.println("  start [hz]     - motor start (default/current clk)");
  Serial.println("  stop           - motor stop");
  Serial.println("  clk <hz>       - set clock while running (100~10000)");
  Serial.println("  status         - running / lock / clk / sensor / sync / laser");
  Serial.println("  sensor         - print sensor DO/AO once");
  Serial.println("  laser on|off   - 650nm 라인 레이저 (Pin9 → NPN)");
  Serial.println("  theta <deg>    - 수동 θ1 송신 (UART 테스트용)");
  Serial.println("  help           - this help");
  Serial.println();
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
      Serial.println("[cmd] stop 수신");
      motorStop();
    } else if (lower == "status") {
      Serial.println("[cmd] status 수신");
      Serial.printf("[status] running=%d locked=%d clk=%lu Hz hit=%d DO=%d AO=%d "
                    "sync=%d period=%lu us laser=%d\n",
                    g_running ? 1 : 0,
                    isLocked() ? 1 : 0,
                    (unsigned long)g_clkHz,
                    isSensorHit() ? 1 : 0,
                    digitalRead(PIN_SENSOR_DO),
                    analogRead(PIN_SENSOR_AO),
                    g_syncSeen ? 1 : 0,
                    (unsigned long)g_periodUs,
                    g_laserOn ? 1 : 0);
    } else if (lower == "sensor") {
      Serial.println("[cmd] sensor 수신");
      Serial.printf("[sensor] hit=%d DO=%d AO=%d (active_low=%d)\n",
                    isSensorHit() ? 1 : 0,
                    digitalRead(PIN_SENSOR_DO),
                    analogRead(PIN_SENSOR_AO),
                    SENSOR_DO_ACTIVE_LOW ? 1 : 0);
    } else if (lower.startsWith("laser")) {
      const int sp = lower.indexOf(' ');
      if (sp < 0) {
        Serial.println("[err] usage: laser on|off");
      } else {
        const String arg = lower.substring(sp + 1);
        if (arg == "on") {
          setLaser(true);
        } else if (arg == "off") {
          setLaser(false);
        } else {
          Serial.println("[err] usage: laser on|off");
        }
      }
    } else if (lower.startsWith("theta")) {
      const int sp = lower.indexOf(' ');
      if (sp < 0) {
        Serial.println("[err] usage: theta <deg>");
      } else {
        const float deg = lower.substring(sp + 1).toFloat();
        Serial.printf("[cmd] 수동 θ1=%.2f° 송신\n", deg);
        sendTheta1(deg);
      }
    } else if (lower.startsWith("start")) {
      Serial.println("[cmd] start 수신");
      uint32_t hz = g_clkHz;
      const int sp = lower.indexOf(' ');
      if (sp > 0) {
        hz = (uint32_t)lower.substring(sp + 1).toInt();
      }
      if (hz < CLK_HZ_MIN || hz > CLK_HZ_MAX) {
        Serial.printf("[err] clk out of range (%lu~%lu)\n",
                      (unsigned long)CLK_HZ_MIN, (unsigned long)CLK_HZ_MAX);
      } else {
        motorStart(hz);
      }
    } else if (lower.startsWith("clk")) {
      Serial.println("[cmd] clk 수신");
      const int sp = lower.indexOf(' ');
      if (sp < 0) {
        Serial.println("[err] usage: clk <hz>");
      } else {
        const uint32_t hz = (uint32_t)lower.substring(sp + 1).toInt();
        Serial.printf("[stage] CLK 변경 시도 → %lu Hz\n", (unsigned long)hz);
        if (!applyClockHz(hz)) {
          Serial.printf("[err] clk out of range (%lu~%lu)\n",
                        (unsigned long)CLK_HZ_MIN, (unsigned long)CLK_HZ_MAX);
        } else if (!g_running) {
          Serial.printf("[ok] clk set to %lu Hz (not running yet)\n",
                        (unsigned long)g_clkHz);
        } else {
          Serial.printf("[ok] clk -> %lu Hz (동작 중 반영)\n",
                        (unsigned long)g_clkHz);
        }
      }
    } else {
      Serial.printf("[err] unknown: %s\n", line.c_str());
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

  Serial.println();
  Serial.println("[stage] ===== setup 시작 (Node A / Left / Slave) =====");
  Serial.println("[stage] 1/7 USB Serial 115200 + Serial1 link ready");
  Serial.printf("[stage]    LINK_BAUD=%lu  TX1=Pin1 → Master RX1\n",
                (unsigned long)LINK_BAUD);

  Serial.println("[stage] 2/7 LD 핀 INPUT_PULLUP 설정");
  pinMode(PIN_MOTOR_LD, INPUT_PULLUP);

  Serial.println("[stage] 3/7 S/S·CLK 초기화 (정지 / Hi-Z)");
  odWrite(PIN_MOTOR_SS, true);   // stop
  odWrite(PIN_MOTOR_CLK, true);  // idle high (Hi-Z)

  Serial.println("[stage] 4/7 센서 DO/AO 입력 설정");
  pinMode(PIN_SENSOR_DO, INPUT);
  g_lastHit = isSensorHit();
  g_sensorReady = true;
  Serial.printf("[stage] 센서 초기값 hit=%d DO=%d AO=%d\n",
                g_lastHit ? 1 : 0,
                digitalRead(PIN_SENSOR_DO),
                analogRead(PIN_SENSOR_AO));

  Serial.println("[stage] 5/7 Sync 인터럽트 설정");
  pinMode(PIN_SYNC, INPUT_PULLUP);
  attachInterrupt(digitalPinToInterrupt(PIN_SYNC), syncIsr,
                  SYNC_ACTIVE_FALLING ? FALLING : RISING);

  Serial.println("[stage] 6/7 레이저 Pin9 (NPN) 초기화");
  pinMode(PIN_LASER, OUTPUT);
  setLaser(LASER_DEFAULT_ON);

  Serial.println("[stage] 7/7 초기 상태: running=0, waiting for command");
  Serial.println("laserModuleL / Node A (Slave) ready");
  Serial.printf("Default CLK = %lu Hz, SCAN_ANGLE=%.1f deg\n",
                (unsigned long)CLK_HZ_DEFAULT, SCAN_ANGLE_DEG);
  Serial.println("Power: LM2596 5V→VIN, GND공통 / Laser: 3.3V→RED, Pin9→NPN→BLACK");
  Serial.println("Sensor: VCC=3.3V GND=GND DO=5 AO=A0 Sync=6 Laser=9");
  Serial.println("UART → Master: TX1(1)/RX1(0)/GND — packet T1,<cd>,<us>");
  printHelp();
  Serial.println("Wire V=24V(external), G=GND common, then: laser on / start");
  Serial.println("[stage] ===== setup 완료 =====");
}

void loop()
{
  handleSerial();
  pollSensor();

  const bool locked = isLocked();
  if (g_running && locked != g_lastLocked) {
    g_lastLocked = locked;
    if (locked) {
      Serial.println("[stage] LD=LOW → PLL LOCKED (속도 동기 완료)");
      Serial.println("[motor] LOCKED");
    } else {
      Serial.println("[stage] LD=HIGH → unlocked (동기 해제)");
      Serial.println("[motor] unlocked");
    }
  }

  delay(1);
}
