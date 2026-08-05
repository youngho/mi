/*
 * laserModuleR.ino
 *
 * Teensy 4.1 기반 BB탄/터치 위치 검출 모듈 — Node B (Right / Master)
 *
 * 스크린 오른쪽 하단에 설치되어, 왼쪽(수평)과 위쪽(수직)으로
 * 레이저를 발사하여 BB탄/손가락 통과 위치를 검출한다.
 *
 * Board : Teensy 4.1
 * Role  : Right-side laser detection module (Master)
 *
 * USB Type (Arduino IDE / Teensyduino) — 필수:
 *   Tools → USB Type → Serial + Keyboard + Mouse + Joystick
 *   (Serial만 선택하면 Mouse 미선언으로 컴파일 실패)
 *   (Mouse.moveTo / Mouse.click 사용)
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
 *                                                      │ USB HID
 *                                                      ▼
 *                                         [ 메인 PC / 노트북 ]
 *
 * 1) Node A (laserModuleL.ino) 역할
 *    - θ1 산출 후 Serial1로 Master에 전송
 *
 * 2) Node B (이 스케치) 역할
 *    - 오른쪽 스캐너 Sync 대비 HIT로 θ2 산출
 *    - Slave로부터 θ1 실시간 수신
 *    - θ1, θ2, 스크린 폭 W 로 (X,Y) 삼각측량
 *    - USB HID Mouse.moveTo / click 으로 PC에 전달
 *      → Unity BDS Check (`BdsCheck` 씬 / TouchInputSource)가 수신·검증
 *      → Game 뷰·플레이어 해상도는 SCREEN_PX(1920×1080)과 맞출 것
 *
 * 3) 보드 간 UART 배선 (Serial1)
 *    Teensy #1 TX1 (Pin 1) ──────────────► Teensy #2 RX1 (Pin 0)
 *    Teensy #1 RX1 (Pin 0) ◄────────────── Teensy #2 TX1 (Pin 1)
 *    Teensy #1 GND         ─────────────── Teensy #2 GND  (공통 필수)
 *
 * 4) 패킷 (ASCII, Serial1 @ LINK_BAUD)
 *    L → R  T1,<centideg>,<t_us>\n
 *      예: T1,4850,12345678  → θ1 = 48.50°
 *    R → L  M,1[,<hz>]\n / M,0\n  — Master 모터 start/stop 시 Slave 동기
 *      (running 중 2초마다 M,1 재전송 → L 늦게 켜져도 따라옴)
 *    R → L  P,<nonce>\n           — 링크 ping (R USB `ping` 명령)
 *    L → R  P,<nonce>\n           — ping echo (양방향·GND 확인용)
 *
 * 5) 삼각측량
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
 * Photodiode / LM393 센서 · Sync
 * ---------------------------------------------------------------------------
 *   센서: VCC=3.3V, GND, DO→PIN_SENSOR_DO, AO→PIN_SENSOR_AO
 *   Sync: PIN_SYNC (INPUT_PULLUP). Sync 없으면 `theta2 <deg>` + 수신 θ1 으로 테스트
 * ---------------------------------------------------------------------------
 */

#include <Arduino.h>
#include <Mouse.h>

// Mouse 객체는 Teensy USB Type에 Mouse가 포함될 때만 존재한다.
// Arduino IDE: Tools → USB Type → "Serial + Keyboard + Mouse + Joystick"
#ifndef MOUSE_INTERFACE
#error "USB Type에 Mouse가 없습니다. Tools → USB Type → Serial + Keyboard + Mouse + Joystick 로 바꾼 뒤 다시 컴파일하세요."
#endif

// ---- Pin map (필요시 변경) ----
static const int PIN_MOTOR_CLK = 2;   // C
static const int PIN_MOTOR_LD  = 3;   // L
static const int PIN_MOTOR_SS  = 4;   // S
static const int PIN_SENSOR_DO = 5;   // 포토다이오드 모듈 DO
static const int PIN_SENSOR_AO = A0;  // 포토다이오드 모듈 AO (선택)
static const int PIN_SYNC      = 6;   // 0° Sync 입력
static const int PIN_LASER     = 9;   // NPN 베이스 구동 (HIGH=레이저 ON)

static const bool SENSOR_DO_ACTIVE_LOW = true;
static const bool SYNC_ACTIVE_FALLING = true;
static const bool LASER_DEFAULT_ON = false;

// 한 Sync 주기 동안 스캔하는 광학 각도 범위 [deg]
static const float SCAN_ANGLE_DEG = 90.0f;

// Node A ↔ Node B UART (Serial1: RX0 / TX1)
static const uint32_t LINK_BAUD = 1000000;

// θ1 신선도: 이 시간(us) 안에 온 값만 삼각측량에 사용
static const uint32_t THETA1_FRESH_US = 50000;  // 50 ms

// 스크린 물리 크기 [mm] — 실측 후 수정
static const float SCREEN_WIDTH_MM  = 2000.0f;  // W
static const float SCREEN_HEIGHT_MM = 1125.0f;

// PC / Unity Game 뷰 해상도 (HID absolute) — BdsCheck ExpectedScreen* 와 동일해야 함
static const int SCREEN_PX_W = 1920;
static const int SCREEN_PX_H = 1080;

// HIT 시 클릭 여부 / 최소 간격
static const bool EMIT_CLICK_ON_HIT = true;
static const uint32_t CLICK_COOLDOWN_MS = 80;

// ---- Motor defaults ----
static const uint32_t CLK_HZ_DEFAULT = 2000;
static const uint32_t CLK_HZ_MIN     = 100;
static const uint32_t CLK_HZ_MAX     = 10000;

static IntervalTimer g_clkTimer;
static volatile uint32_t g_clkHz = CLK_HZ_DEFAULT;
static volatile bool g_clkHigh = true;
static bool g_running = false;
static bool g_lastLocked = false;
static uint32_t g_lastMotorSyncMs = 0;

// Slave(L) 모터 동기 주기 — L이 늦게 켜져도 따라오도록 running 중 재전송
static const uint32_t MOTOR_SYNC_PERIOD_MS = 2000;
static const uint32_t LINK_PING_TIMEOUT_MS = 300;
static bool g_lastHit = false;
static bool g_sensorReady = false;
static bool g_laserOn = false;

// UART ping (R USB `ping` → L echo)
static bool g_linkPongPending = false;
static uint32_t g_linkPongNonce = 0;
static bool g_linkPongGot = false;

static volatile uint32_t g_syncUs = 0;
static volatile uint32_t g_periodUs = 0;
static volatile bool g_syncSeen = false;

// Slave에서 수신한 θ1
static float g_theta1Deg = NAN;
static uint32_t g_theta1RecvUs = 0;
static bool g_theta1Valid = false;

static uint32_t g_lastClickMs = 0;
static bool g_hidEnabled = true;

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
  odWrite(PIN_MOTOR_CLK, true);
  Serial.println("[stage] CLK 핀 Hi-Z (idle)");
}

static void sendMotorSync(bool run)
{
  if (run) {
    Serial1.printf("M,1,%lu\n", (unsigned long)g_clkHz);
    Serial.printf("[link] TX M,1,%lu → Slave start\n", (unsigned long)g_clkHz);
  } else {
    Serial1.print("M,0\n");
    Serial.println("[link] TX M,0 → Slave stop");
  }
  g_lastMotorSyncMs = millis();
}

static void pollMotorSync()
{
  if (!g_running) {
    return;
  }
  if (millis() - g_lastMotorSyncMs < MOTOR_SYNC_PERIOD_MS) {
    return;
  }
  // 주기 재전송 (로그 스팸 줄이려 USB는 생략, UART만)
  Serial1.printf("M,1,%lu\n", (unsigned long)g_clkHz);
  g_lastMotorSyncMs = millis();
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
  odWrite(PIN_MOTOR_SS, false);
  g_running = true;
  g_lastLocked = false;
  Serial.println("[stage] 4/4 PLL 락 대기 중 (LD=LOW 되면 LOCKED)");
  Serial.printf("[motor] START 완료 — clk=%lu Hz, running=1\n",
                (unsigned long)g_clkHz);
  sendMotorSync(true);
}

static void motorStop()
{
  Serial.println();
  Serial.println("[stage] ===== 모터 STOP 시퀀스 시작 =====");
  Serial.println("[stage] 1/3 S/S = Hi-Z (정지, 내부 풀업)");
  odWrite(PIN_MOTOR_SS, true);
  delay(2);
  Serial.println("[stage] 2/3 기준 클럭 차단");
  stopClock();
  g_running = false;
  g_lastLocked = false;
  Serial.println("[stage] 3/3 상태 초기화 (running=0, locked=0)");
  Serial.println("[motor] STOP 완료");
  sendMotorSync(false);
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

static bool computeTheta2(float *outDeg)
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

static bool isTheta1Fresh()
{
  if (!g_theta1Valid) {
    return false;
  }
  return (micros() - g_theta1RecvUs) <= THETA1_FRESH_US;
}

// X,Y in mm. 스크린: 좌하단 원점, X→우, Y→상. θ1=좌스캐너, θ2=우스캐너.
static bool triangulate(float theta1Deg, float theta2Deg, float *outX, float *outY)
{
  if (!(theta1Deg > 0.1f && theta1Deg < 89.9f)) {
    return false;
  }
  if (!(theta2Deg > 0.1f && theta2Deg < 89.9f)) {
    return false;
  }

  const float t1 = tanf(theta1Deg * (float)DEG_TO_RAD);
  const float t2 = tanf(theta2Deg * (float)DEG_TO_RAD);
  const float den = t1 + t2;
  if (fabsf(den) < 1e-6f) {
    return false;
  }

  const float x = (SCREEN_WIDTH_MM * t2) / den;
  const float y = x * t1;

  if (x < 0.0f || x > SCREEN_WIDTH_MM || y < 0.0f || y > SCREEN_HEIGHT_MM * 1.5f) {
    return false;
  }

  *outX = x;
  *outY = y;
  return true;
}

static void emitHid(float xMm, float yMm)
{
  if (!g_hidEnabled) {
    Serial.printf("[hid] disabled — skip (%.1f, %.1f) mm\n", xMm, yMm);
    return;
  }

  // Unity Screen: x=0 왼쪽, y=0 하단. yMm=0을 화면 하단으로 보고 뒤집음.
  int px = (int)lroundf((xMm / SCREEN_WIDTH_MM) * (float)(SCREEN_PX_W - 1));
  int py = (int)lroundf((1.0f - (yMm / SCREEN_HEIGHT_MM)) * (float)(SCREEN_PX_H - 1));
  px = constrain(px, 0, SCREEN_PX_W - 1);
  py = constrain(py, 0, SCREEN_PX_H - 1);

  // Unity BDS Check: TouchInputSource가 leftButton + position 으로 InputHit 생성
  Mouse.moveTo(px, py);
  Serial.printf("[hid] moveTo (%d,%d) px  from (%.1f,%.1f) mm\n", px, py, xMm, yMm);

  if (EMIT_CLICK_ON_HIT) {
    const uint32_t now = millis();
    if (now - g_lastClickMs >= CLICK_COOLDOWN_MS) {
      Mouse.click();
      g_lastClickMs = now;
      Serial.println("[hid] click");
    }
  }
}

static void processHitWithTheta2(float theta2Deg)
{
  Serial.printf("[angle] θ2=%.2f°\n", theta2Deg);

  if (!isTheta1Fresh()) {
    Serial.println("[tri] θ1 없거나 stale — 삼각측량 스킵 "
                   "(Left에서 HIT/`theta` 확인)");
    return;
  }

  const float theta1Deg = g_theta1Deg;
  float x = 0.0f;
  float y = 0.0f;
  if (!triangulate(theta1Deg, theta2Deg, &x, &y)) {
    Serial.printf("[tri] 실패 θ1=%.2f θ2=%.2f\n", theta1Deg, theta2Deg);
    return;
  }

  Serial.printf("[tri] θ1=%.2f° θ2=%.2f° → X=%.1f Y=%.1f mm\n",
                theta1Deg, theta2Deg, x, y);
  emitHid(x, y);
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
    float th2 = 0.0f;
    if (computeTheta2(&th2)) {
      processHitWithTheta2(th2);
    } else {
      Serial.println("[angle] HIT but Sync/period 없음 — θ2 미산출 "
                     "(`theta2 <deg>` 로 테스트 가능)");
    }
  } else {
    Serial.printf("[sensor] CLEAR (DO=%d AO=%d)\n",
                  digitalRead(PIN_SENSOR_DO), ao);
  }
}

// Serial1: T1,<centideg>,<t_us>\n  /  P,<nonce>\n (pong)
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
    if (line.startsWith("T1,")) {
      // T1,<centideg>,<t_us>
      const int c1 = line.indexOf(',');
      const int c2 = line.indexOf(',', c1 + 1);
      if (c1 > 0 && c2 > c1) {
        const long centi = line.substring(c1 + 1, c2).toInt();
        g_theta1Deg = centi / 100.0f;
        g_theta1RecvUs = micros();
        g_theta1Valid = true;
        Serial.printf("[link] RX T1 θ1=%.2f° (%ld cd)\n", g_theta1Deg, centi);
      } else {
        Serial.printf("[link] bad packet: %s\n", line.c_str());
      }
    } else if (line.startsWith("P,")) {
      const uint32_t nonce = (uint32_t)line.substring(2).toInt();
      if (g_linkPongPending && nonce == g_linkPongNonce) {
        g_linkPongGot = true;
      }
      Serial.printf("[link] RX P,%lu (pong)\n", (unsigned long)nonce);
    } else if (line.length() > 0) {
      Serial.printf("[link] ignore: %s\n", line.c_str());
    }
    line = "";
  }
}

// R USB만으로 L UART 왕복 확인: P,<nonce> → echo
static void runLinkPing()
{
  g_linkPongGot = false;
  g_linkPongNonce = (millis() & 0xFFFFUL);
  if (g_linkPongNonce == 0) {
    g_linkPongNonce = 1;
  }
  g_linkPongPending = true;

  Serial1.printf("P,%lu\n", (unsigned long)g_linkPongNonce);
  Serial.printf("[cmd] ping 송신 P,%lu → Slave (timeout %lu ms)\n",
                (unsigned long)g_linkPongNonce,
                (unsigned long)LINK_PING_TIMEOUT_MS);

  const uint32_t t0 = millis();
  while ((millis() - t0) < LINK_PING_TIMEOUT_MS) {
    handleLinkSerial();
    if (g_linkPongGot) {
      Serial.printf("[link] OK  L 응답 (%lu ms) — TX/RX/GND 정상\n",
                    (unsigned long)(millis() - t0));
      g_linkPongPending = false;
      return;
    }
  }

  g_linkPongPending = false;
  Serial.println("[link] FAIL  응답 없음");
  Serial.println("       확인: R Pin1→L Pin0, R Pin0←L Pin1, GND공통,");
  Serial.println("             L 전원 ON, L에 최신 laserModuleL 업로드");
}

static void printHelp()
{
  Serial.println();
  Serial.println("Commands (Node B / Right / Master):");
  Serial.println("  start [hz]      - motor start (+ UART M,1 → Slave L)");
  Serial.println("  stop            - motor stop (+ UART M,0 → Slave L)");
  Serial.println("  clk <hz>        - set clock (100~10000)");
  Serial.println("  status          - running / lock / θ1 / sync / laser");
  Serial.println("  sensor          - print sensor once");
  Serial.println("  laser on|off    - 650nm 라인 레이저 (Pin9 → NPN)");
  Serial.println("  theta2 <deg>    - 수동 θ2 + 최신 θ1 로 삼각측량 테스트");
  Serial.println("  inject <t1> <t2>- θ1·θ2 수동 삼각측량 (UART 없이)");
  Serial.println("  hid on|off      - USB HID 출력 토글");
  Serial.println("  ping            - UART 왕복 확인 (R만으로 L 통신 테스트)");
  Serial.println("  help            - this help");
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
      // empty
    } else if (lower == "help" || lower == "?") {
      printHelp();
    } else if (lower == "stop") {
      Serial.println("[cmd] stop 수신");
      motorStop();
    } else if (lower == "ping") {
      runLinkPing();
    } else if (lower == "status") {
      Serial.println("[cmd] status 수신");
      Serial.printf("[status] running=%d locked=%d clk=%lu Hz hit=%d "
                    "sync=%d period=%lu us θ1=%.2f fresh=%d hid=%d laser=%d\n",
                    g_running ? 1 : 0,
                    isLocked() ? 1 : 0,
                    (unsigned long)g_clkHz,
                    isSensorHit() ? 1 : 0,
                    g_syncSeen ? 1 : 0,
                    (unsigned long)g_periodUs,
                    g_theta1Valid ? g_theta1Deg : -1.0f,
                    isTheta1Fresh() ? 1 : 0,
                    g_hidEnabled ? 1 : 0,
                    g_laserOn ? 1 : 0);
    } else if (lower == "sensor") {
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
    } else if (lower.startsWith("hid")) {
      const int sp = lower.indexOf(' ');
      if (sp < 0) {
        Serial.println("[err] usage: hid on|off");
      } else {
        const String arg = lower.substring(sp + 1);
        if (arg == "on") {
          g_hidEnabled = true;
          Serial.println("[hid] enabled");
        } else if (arg == "off") {
          g_hidEnabled = false;
          Serial.println("[hid] disabled");
        } else {
          Serial.println("[err] usage: hid on|off");
        }
      }
    } else if (lower.startsWith("theta2")) {
      const int sp = lower.indexOf(' ');
      if (sp < 0) {
        Serial.println("[err] usage: theta2 <deg>");
      } else {
        const float deg = lower.substring(sp + 1).toFloat();
        Serial.printf("[cmd] 수동 θ2=%.2f°\n", deg);
        processHitWithTheta2(deg);
      }
    } else if (lower.startsWith("inject")) {
      // inject <theta1> <theta2>
      float t1 = 0.0f;
      float t2 = 0.0f;
      if (sscanf(lower.c_str(), "inject %f %f", &t1, &t2) == 2) {
        g_theta1Deg = t1;
        g_theta1RecvUs = micros();
        g_theta1Valid = true;
        Serial.printf("[cmd] inject θ1=%.2f θ2=%.2f\n", t1, t2);
        processHitWithTheta2(t2);
      } else {
        Serial.println("[err] usage: inject <theta1_deg> <theta2_deg>");
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
      const int sp = lower.indexOf(' ');
      if (sp < 0) {
        Serial.println("[err] usage: clk <hz>");
      } else {
        const uint32_t hz = (uint32_t)lower.substring(sp + 1).toInt();
        if (!applyClockHz(hz)) {
          Serial.printf("[err] clk out of range (%lu~%lu)\n",
                        (unsigned long)CLK_HZ_MIN, (unsigned long)CLK_HZ_MAX);
        } else {
          Serial.printf("[ok] clk -> %lu Hz\n", (unsigned long)g_clkHz);
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
  }

  Serial1.begin(LINK_BAUD);
  Mouse.begin();

  Serial.println();
  Serial.println("[stage] ===== setup 시작 (Node B / Right / Master) =====");
  Serial.println("[stage] 1/7 USB Serial + Serial1 link + Mouse HID");
  Serial.printf("[stage]    LINK_BAUD=%lu  RX1=Pin0 ← Slave TX1\n",
                (unsigned long)LINK_BAUD);
  Serial.printf("[stage]    Screen W=%.0f mm → %dx%d px\n",
                SCREEN_WIDTH_MM, SCREEN_PX_W, SCREEN_PX_H);

  Serial.println("[stage] 2/7 LD 핀 INPUT_PULLUP 설정");
  pinMode(PIN_MOTOR_LD, INPUT_PULLUP);

  Serial.println("[stage] 3/7 S/S·CLK 초기화 (정지 / Hi-Z)");
  odWrite(PIN_MOTOR_SS, true);
  odWrite(PIN_MOTOR_CLK, true);

  Serial.println("[stage] 4/7 센서 DO/AO 입력 설정");
  pinMode(PIN_SENSOR_DO, INPUT);
  g_lastHit = isSensorHit();
  g_sensorReady = true;

  Serial.println("[stage] 5/7 Sync 인터럽트 설정");
  pinMode(PIN_SYNC, INPUT_PULLUP);
  attachInterrupt(digitalPinToInterrupt(PIN_SYNC), syncIsr,
                  SYNC_ACTIVE_FALLING ? FALLING : RISING);

  Serial.println("[stage] 6/7 레이저 Pin9 (NPN) 초기화");
  pinMode(PIN_LASER, OUTPUT);
  setLaser(LASER_DEFAULT_ON);

  Serial.println("[stage] 7/7 ready");
  Serial.println("laserModuleR / Node B (Master) ready");
  Serial.println("USB Type must be Keyboard+Mouse+Joystick for HID");
  Serial.println("Power: LM2596 5V→VIN, GND공통 / Laser: 3.3V→RED, Pin9→NPN→BLACK");
  Serial.println("UART ↔ Slave: RX1(0)/TX1(1)/GND — RX T1,... / TX M,1|M,0 / ping");
  printHelp();
  Serial.println("Wire V=24V(external), G=GND common, then: laser on / start");
  Serial.println("R start → L도 UART로 자동 start (2초마다 M,1 재전송)");
  Serial.println("통신 확인(R만): `ping`  →  [link] OK 면 L 연결 정상");
  Serial.println("또는: Left `theta 48.5` / Right `inject 48.5 52.1`");
  Serial.println("[stage] ===== setup 완료 =====");
}

void loop()
{
  handleSerial();
  handleLinkSerial();
  pollMotorSync();
  pollSensor();

  const bool locked = isLocked();
  if (g_running && locked != g_lastLocked) {
    g_lastLocked = locked;
    if (locked) {
      Serial.println("[stage] LD=LOW → PLL LOCKED");
      Serial.println("[motor] LOCKED");
    } else {
      Serial.println("[stage] LD=HIGH → unlocked");
      Serial.println("[motor] unlocked");
    }
  }

  delay(1);
}
