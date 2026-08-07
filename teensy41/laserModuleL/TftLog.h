/*
 * TftLog.h — ST7789 1.54" (240×240) 스크롤 로그
 *
 * Library (Teensyduino 내장): ST7735_t3 / ST7789_t3
 * USB Serial 과 동일 메시지를 TFT에도 표시 (한글은 ASCII만 표시).
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

#ifndef TFT_LOG_COLS
#define TFT_LOG_COLS 40
#endif
#ifndef TFT_LOG_ROWS
#define TFT_LOG_ROWS 28
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
  // trailing space trim
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
  g_tft.setTextSize(1);
  g_tft.setTextWrap(false);
  const uint8_t n = g_tftCount;
  for (uint8_t i = 0; i < n; ++i) {
    const uint8_t idx = (uint8_t)((g_tftHead + TFT_LOG_ROWS - n + i) % TFT_LOG_ROWS);
    g_tft.setCursor(0, (int16_t)(i * 8));
    // 최신 줄 강조
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

// 멀티라인·긴 문자열을 TFT 행 단위로 분할
static void tftAppendText(const char *text)
{
  if (!g_tftOk || text == nullptr) {
    return;
  }

  char ascii[256];
  tftSanitizeAscii(ascii, sizeof(ascii), text);

  const char *p = ascii;
  while (*p) {
    char row[TFT_LOG_COLS + 1];
    size_t n = 0;
    while (p[n] && p[n] != '\n' && n < TFT_LOG_COLS) {
      ++n;
    }
    memcpy(row, p, n);
    row[n] = '\0';
    if (n > 0 || p[n] == '\n') {
      tftPushLine(row);
    }
    p += n;
    if (*p == '\n') {
      ++p;
    }
  }
}

static void tftLogBegin(const char *title)
{
  pinMode(PIN_TFT_BL, OUTPUT);
  digitalWrite(PIN_TFT_BL, HIGH);

  g_tft.init(240, 240);
  g_tft.setRotation(0);
  g_tft.fillScreen(ST77XX_BLACK);
  g_tftOk = true;
  g_tftCount = 0;
  g_tftHead = 0;

  if (title && title[0]) {
    tftPushLine(title);
  }
  tftPushLine("ST7789 240x240 log ready");
}

static void logln(const String &s)
{
  Serial.println(s);
  tftAppendText(s.c_str());
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
