/*
 * TftLog.h — ST7789 1.54" (240×240) 스크롤 로그
 *
 * Library (Teensyduino 내장): ST7735_t3 / ST7789_t3
 * USB Serial 과 동일 메시지를 TFT에도 표시.
 *
 * === 로그 작성 규칙 (logln / logf) ===
 *   TFT_TEXT_SIZE=2 기준:
 *     TFT_LOG_COLS = 20  (한 줄 최대 글자수, 초과 분은 다음 줄로 잘림)
 *     TFT_LOG_ROWS = 15  (한 화면에 보이는 줄 수)
 *   - 한 줄은 ASCII 기준 TFT_LOG_COLS 글자 이하로 작성할 것.
 *   - help / setup 완료 안내 등 “한 덩어리”는 TFT_LOG_ROWS 줄 안에 맞출 것.
 *   - 한글·기타 UTF-8은 TFT에 표시되지 않음(스킵) → ASCII 위주로 쓸 것.
 *   - 긴 문장은 여러 logln 또는 '\n'으로 직접 나눌 것.
 *
 * 배선은 각 .ino 헤더의 "ST7789 TFT" 섹션 참고.
 */
#pragma once

#include <Arduino.h>
#include <SPI.h>
#include <ST7735_t3.h>
#include <ST7789_t3.h>
#include <stdarg.h>

// Teensy 4.1 SPI0 + 여유 GPIO (모터/센서/레이저/UART 와 비충돌)
#ifndef PIN_TFT_CS
#define PIN_TFT_CS   10
#endif
#ifndef PIN_TFT_DC
#define PIN_TFT_DC    8
#endif
#ifndef PIN_TFT_RST
#define PIN_TFT_RST   7
#endif
#ifndef PIN_TFT_BL
#define PIN_TFT_BL   14
#endif
// SCL→13(SCK), SDA→11(MOSI) — 하드웨어 SPI 고정

// Adafruit 기본 글리프 6×8 — size 2 → 12×16
#ifndef TFT_TEXT_SIZE
#define TFT_TEXT_SIZE 2
#endif
#ifndef TFT_CHAR_W
#define TFT_CHAR_W (6 * (TFT_TEXT_SIZE))
#endif
#ifndef TFT_CHAR_H
#define TFT_CHAR_H (8 * (TFT_TEXT_SIZE))
#endif
#ifndef TFT_LOG_COLS
#define TFT_LOG_COLS (240 / (TFT_CHAR_W))  /* size2 → 20 */
#endif
#ifndef TFT_LOG_ROWS
#define TFT_LOG_ROWS (240 / (TFT_CHAR_H))  /* size2 → 15 */
#endif
// 0=기본, 2=180°(위아래·좌우 반전). 모듈/장착 방향에 맞게 조정.
#ifndef TFT_ROTATION
#define TFT_ROTATION 2
#endif

static ST7789_t3 g_tft = ST7789_t3(PIN_TFT_CS, PIN_TFT_DC, PIN_TFT_RST);
static bool g_tftOk = false;
static char g_tftLines[TFT_LOG_ROWS][TFT_LOG_COLS + 1];
static uint8_t g_tftCount = 0;
static uint8_t g_tftHead = 0;  // 다음 write 위치 (링)

static void tftSanitizeAscii(char *dst, size_t dstLen, const char *src)
{
  if (dstLen == 0) {
    return;
  }
  size_t o = 0;
  for (size_t i = 0; src[i] != '\0' && o + 1 < dstLen; ) {
    const uint8_t c = (uint8_t)src[i];
    if (c < 0x80) {
      if (c == '\r') {
        ++i;
        continue;
      }
      if (c == '\n') {
        break;
      }
      if (c >= 0x20 || c == '\t') {
        dst[o++] = (c == '\t') ? ' ' : (char)c;
      }
      ++i;
    } else if ((c & 0xE0) == 0xC0) {
      i += 2;  // UTF-8 2바이트 스킵 (한글 등)
    } else if ((c & 0xF0) == 0xE0) {
      i += 3;
    } else if ((c & 0xF8) == 0xF0) {
      i += 4;
    } else {
      ++i;
    }
  }
  while (o > 0 && dst[o - 1] == ' ') {
    --o;
  }
  dst[o] = '\0';
}

static void tftRedraw()
{
  if (!g_tftOk) {
    return;
  }
  g_tft.fillScreen(ST77XX_BLACK);
  g_tft.setTextSize(TFT_TEXT_SIZE);
  g_tft.setTextWrap(false);
  const uint8_t n = g_tftCount;
  for (uint8_t i = 0; i < n; ++i) {
    const uint8_t idx = (uint8_t)((g_tftHead + TFT_LOG_ROWS - n + i) % TFT_LOG_ROWS);
    g_tft.setCursor(0, (int16_t)(i * TFT_CHAR_H));
    g_tft.setTextColor((i + 1 == n) ? ST77XX_GREEN : ST77XX_WHITE);
    g_tft.print(g_tftLines[idx]);
  }
}

static void tftPushLine(const char *asciiLine)
{
  if (!g_tftOk) {
    return;
  }
  strncpy(g_tftLines[g_tftHead], asciiLine, TFT_LOG_COLS);
  g_tftLines[g_tftHead][TFT_LOG_COLS] = '\0';
  g_tftHead = (uint8_t)((g_tftHead + 1) % TFT_LOG_ROWS);
  if (g_tftCount < TFT_LOG_ROWS) {
    ++g_tftCount;
  }
  tftRedraw();
}

// 멀티라인·긴 문자열 → 행 단위. 공백 우선 줄바꿈, 없으면 COLS에서 hard wrap.
static void tftAppendText(const char *text)
{
  if (!g_tftOk || text == nullptr) {
    return;
  }

  char ascii[256];
  tftSanitizeAscii(ascii, sizeof(ascii), text);

  const char *p = ascii;
  while (*p) {
    while (*p == ' ') {
      ++p;
    }
    if (!*p) {
      break;
    }

    size_t n = 0;
    size_t lastSpace = 0;
    bool haveSpace = false;
    while (p[n] && p[n] != '\n' && n < TFT_LOG_COLS) {
      if (p[n] == ' ') {
        lastSpace = n;
        haveSpace = true;
      }
      ++n;
    }

    if (p[n] && p[n] != '\n' && haveSpace && lastSpace > 0) {
      n = lastSpace;
    }

    char row[TFT_LOG_COLS + 1];
    memcpy(row, p, n);
    row[n] = '\0';
    if (n > 0 || p[n] == '\n') {
      tftPushLine(row);
    }
    p += n;
    if (*p == '\n' || *p == ' ') {
      ++p;
    }
  }
}

static void tftLogBegin(const char *title)
{
  pinMode(PIN_TFT_BL, OUTPUT);
  digitalWrite(PIN_TFT_BL, HIGH);

  g_tft.init(240, 240);
  g_tft.setRotation(TFT_ROTATION);
  g_tft.fillScreen(ST77XX_BLACK);
  g_tftOk = true;
  g_tftCount = 0;
  g_tftHead = 0;

  if (title && title[0]) {
    tftPushLine(title);
  }
  // "cols=20 rows=15 size=2" — 현재 기본값 안내 (≤20자)
  char info[TFT_LOG_COLS + 1];
  snprintf(info, sizeof(info), "TFT %dx%d sz%d",
           TFT_LOG_COLS, TFT_LOG_ROWS, TFT_TEXT_SIZE);
  tftPushLine(info);
}

static void logln(const char *s)
{
  Serial.println(s ? s : "");
  tftAppendText(s ? s : "");
}

static void logln()
{
  Serial.println();
  tftPushLine("");
}

static void logf(const char *fmt, ...)
{
  char buf[192];
  va_list ap;
  va_start(ap, fmt);
  vsnprintf(buf, sizeof(buf), fmt, ap);
  va_end(ap);
  Serial.print(buf);
  tftAppendText(buf);
}
