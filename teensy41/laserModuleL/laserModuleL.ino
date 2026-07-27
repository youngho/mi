/*
 * laserModuleL.ino
 *
 * Teensy 4.1 기반 BB탄 위치 검출 모듈 (Left)
 *
 * 스크린 왼쪽 하단에 설치되어, 오른쪽(수평)과 위쪽(수직)으로
 * 레이저를 발사하여 BB탄의 통과 위치를 검출한다.
 *
 * Board : Teensy 4.1
 * Role  : Left-side laser detection module
 *
 * ---------------------------------------------------------------------------
 * Polygon Mirror Motor — Ricoh MP C3502 / PWB MOTOR UNIT 7M0502
 * Driver IC: LB11876 (외부 FET + 24V 단전원 구성)
 * ---------------------------------------------------------------------------
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
 * ---------------------------------------------------------------------------
 * Photodiode / LM393 센서 모듈 (레이저 수신 테스트)
 * ---------------------------------------------------------------------------
 *   VCC → Teensy 3V
 *   GND → Teensy GND (공통)
 *   DO  → Teensy PIN_SENSOR_DO
 *   AO  → Teensy PIN_SENSOR_AO (선택, 아날로그 세기)
 *
 *   모듈마다 DO 극성이 다름:
 *     SENSOR_DO_ACTIVE_LOW=true  → DO=LOW 일 때 HIT (흔한 LM393 보드)
 *     SENSOR_DO_ACTIVE_LOW=false → DO=HIGH 일 때 HIT
 *   반대로 나오면 아래 상수만 바꾸면 된다.
 * ---------------------------------------------------------------------------
 */

// ---- Pin map (필요시 변경) ----
static const int PIN_MOTOR_CLK = 2;   // C
static const int PIN_MOTOR_LD  = 3;   // L
static const int PIN_MOTOR_SS  = 4;   // S
static const int PIN_SENSOR_DO = 5;   // 포토다이오드 모듈 DO
static const int PIN_SENSOR_AO = A0;  // 포토다이오드 모듈 AO (선택)

// DO=LOW 를 HIT로 볼지 여부 (보드마다 다름 — 반대로면 false)
static const bool SENSOR_DO_ACTIVE_LOW = true;

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

static bool applyClockHz(uint32_t hz)
{
  if (hz < CLK_HZ_MIN || hz > CLK_HZ_MAX) {
    return false;
  }
  g_clkHz = hz;
  // 50% duty → 반주기마다 토글
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

  // 1) 기준 클럭 먼저 공급 후 2) S/S LOW로 기동
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
  // LD: 락 시 오픈드레인 LOW
  return digitalRead(PIN_MOTOR_LD) == LOW;
}

static bool isSensorHit()
{
  const bool doHigh = digitalRead(PIN_SENSOR_DO) == HIGH;
  return SENSOR_DO_ACTIVE_LOW ? !doHigh : doHigh;
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
  } else {
    Serial.printf("[sensor] CLEAR (DO=%d AO=%d)\n",
                  digitalRead(PIN_SENSOR_DO), ao);
  }
}

static void printHelp()
{
  Serial.println();
  Serial.println("Commands:");
  Serial.println("  start [hz]  - motor start (default/current clk)");
  Serial.println("  stop        - motor stop");
  Serial.println("  clk <hz>    - set clock while running (100~10000)");
  Serial.println("  status      - print running / lock / clk / sensor");
  Serial.println("  sensor      - print sensor DO/AO once");
  Serial.println("  help        - this help");
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
    line.toLowerCase();

    if (line.length() == 0) {
      // keep empty
    } else if (line == "help" || line == "?") {
      printHelp();
    } else if (line == "stop") {
      Serial.println("[cmd] stop 수신");
      motorStop();
    } else if (line == "status") {
      Serial.println("[cmd] status 수신");
      Serial.printf("[status] running=%d locked=%d clk=%lu Hz hit=%d DO=%d AO=%d\n",
                    g_running ? 1 : 0,
                    isLocked() ? 1 : 0,
                    (unsigned long)g_clkHz,
                    isSensorHit() ? 1 : 0,
                    digitalRead(PIN_SENSOR_DO),
                    analogRead(PIN_SENSOR_AO));
    } else if (line == "sensor") {
      Serial.println("[cmd] sensor 수신");
      Serial.printf("[sensor] hit=%d DO=%d AO=%d (active_low=%d)\n",
                    isSensorHit() ? 1 : 0,
                    digitalRead(PIN_SENSOR_DO),
                    analogRead(PIN_SENSOR_AO),
                    SENSOR_DO_ACTIVE_LOW ? 1 : 0);
    } else if (line.startsWith("start")) {
      Serial.println("[cmd] start 수신");
      uint32_t hz = g_clkHz;
      const int sp = line.indexOf(' ');
      if (sp > 0) {
        hz = (uint32_t)line.substring(sp + 1).toInt();
      }
      if (hz < CLK_HZ_MIN || hz > CLK_HZ_MAX) {
        Serial.printf("[err] clk out of range (%lu~%lu)\n",
                      (unsigned long)CLK_HZ_MIN, (unsigned long)CLK_HZ_MAX);
      } else {
        motorStart(hz);
      }
    } else if (line.startsWith("clk")) {
      Serial.println("[cmd] clk 수신");
      const int sp = line.indexOf(' ');
      if (sp < 0) {
        Serial.println("[err] usage: clk <hz>");
      } else {
        const uint32_t hz = (uint32_t)line.substring(sp + 1).toInt();
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

  Serial.println();
  Serial.println("[stage] ===== setup 시작 =====");
  Serial.println("[stage] 1/5 Serial 115200 ready");

  Serial.println("[stage] 2/5 LD 핀 INPUT_PULLUP 설정");
  pinMode(PIN_MOTOR_LD, INPUT_PULLUP);

  Serial.println("[stage] 3/5 S/S·CLK 초기화 (정지 / Hi-Z)");
  odWrite(PIN_MOTOR_SS, true);   // stop
  odWrite(PIN_MOTOR_CLK, true);  // idle high (Hi-Z)

  Serial.println("[stage] 4/5 센서 DO/AO 입력 설정");
  pinMode(PIN_SENSOR_DO, INPUT);
  // AO는 analogRead 시 자동 설정
  g_lastHit = isSensorHit();
  g_sensorReady = true;
  Serial.printf("[stage] 센서 초기값 hit=%d DO=%d AO=%d\n",
                g_lastHit ? 1 : 0,
                digitalRead(PIN_SENSOR_DO),
                analogRead(PIN_SENSOR_AO));

  Serial.println("[stage] 5/5 초기 상태: running=0, waiting for command");
  Serial.println("laserModuleL / LB11876 polygon motor (Ricoh C3502) ready");
  Serial.printf("Default CLK = %lu Hz\n", (unsigned long)CLK_HZ_DEFAULT);
  Serial.println("Sensor: VCC=3V GND=GND DO=5 AO=A0 — laser on/off → HIT/CLEAR");
  printHelp();
  Serial.println("Wire V=24V(external), G=GND common, then type: start");
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
