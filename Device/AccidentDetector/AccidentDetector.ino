/*
  Road Accident Analysis — vehicle unit
  ------------------------------------------------------------------------
  ESP32 + MPU6050 (motion) + NEO-6M (GPS).

  What it does: samples acceleration continuously, and when the force exceeds
  a threshold it holds the peak for a moment, reads the last GPS fix, and
  posts one incident to the dashboard over WiFi.

  What it does NOT do: decide that a crash occurred. A threshold cannot tell
  a collision from the unit being dropped, so the dashboard records every
  report as "suspected" until an officer confirms it.

  Libraries: only TinyGPS++ needs installing (Library Manager → "TinyGPSPlus"
  by Mikal Hart). WiFi, HTTPClient and Wire ship with the ESP32 core. The
  MPU6050 is driven through its registers directly, so there is no sensor
  library version to go wrong on the day.

  Wiring (ESP32 DevKit v1):
    MPU6050  VCC→3V3  GND→GND  SDA→GPIO21  SCL→GPIO22
    NEO-6M   VCC→3V3  GND→GND  TX →GPIO16  RX →GPIO17
                                (GPS TX goes to the ESP32's RX)

  On an ESP32-CAM the camera takes most of the pins; use GPIO14/15 for I2C
  and GPIO12/13 for the GPS, and power it from a supply that can hold 5V at
  2A — a weak USB port browning out is the most common cause of an
  ESP32-CAM that "randomly reboots".
*/

#include <WiFi.h>
#include <HTTPClient.h>
#include <Wire.h>
#include <TinyGPS++.h>

// ── Configure these four ───────────────────────────────────────────────
const char* WIFI_SSID     = "YOUR_WIFI_NAME";
const char* WIFI_PASSWORD = "YOUR_WIFI_PASSWORD";

// The machine running the dashboard. Not "localhost" — that would mean the
// ESP32 itself. Find it with ipconfig on Windows.
const char* SERVER_BASE   = "http://192.168.1.100:5199";

// Matches the seeded demo device. Register a real one for anything else.
const char* DEVICE_TOKEN  = "ZP-DEMO-DEVICE-0001";

// ── Tuning ─────────────────────────────────────────────────────────────

// Force that counts as an impact, in g. At rest the sensor reads about 1g,
// a firm shake reaches 2g, and a real collision is far higher. 2.8 is high
// enough to ignore handling and low enough to demonstrate by hand.
const float IMPACT_THRESHOLD_G = 2.8;

// After the threshold trips, keep watching this long for a higher peak, so
// the number reported is the impact rather than its leading edge.
const unsigned long PEAK_WINDOW_MS = 250;

// Ignore further triggers for this long, so one event is one incident
// rather than thirty as the unit rattles to a stop.
const unsigned long QUIET_PERIOD_MS = 15000;

// ── Hardware ───────────────────────────────────────────────────────────
const int   MPU_ADDRESS   = 0x68;
const int   GPS_RX_PIN    = 16;   // ESP32 receives on this, GPS TX connects here
const int   GPS_TX_PIN    = 17;
const long  GPS_BAUD      = 9600;

// ±2g range: the sensor reports 16384 counts per g.
const float COUNTS_PER_G  = 16384.0;

TinyGPSPlus gps;
HardwareSerial gpsSerial(2);

unsigned long lastIncidentAt = 0;

// ───────────────────────────────────────────────────────────────────────

void setup() {
  Serial.begin(115200);
  delay(300);
  Serial.println("\n=== Road Accident Analysis — vehicle unit ===");

  Wire.begin();
  wakeMotionSensor();

  gpsSerial.begin(GPS_BAUD, SERIAL_8N1, GPS_RX_PIN, GPS_TX_PIN);
  Serial.println("GPS serial started. A cold fix outdoors takes 30-60 seconds.");

  connectWiFi();
  checkIn();

  Serial.println("Watching for impacts.");
}

void loop() {
  // Feed the GPS parser whatever has arrived. This must run constantly or
  // the fix goes stale.
  while (gpsSerial.available() > 0) {
    gps.encode(gpsSerial.read());
  }

  float force = readForceG();

  if (force >= IMPACT_THRESHOLD_G && millis() - lastIncidentAt > QUIET_PERIOD_MS) {
    float peak = capturePeak(force);
    lastIncidentAt = millis();

    Serial.printf("Impact detected: %.2fg\n", peak);
    reportIncident(peak);
  }

  delay(10);   // roughly 100 samples a second
}

// ── Motion ─────────────────────────────────────────────────────────────

void wakeMotionSensor() {
  Wire.beginTransmission(MPU_ADDRESS);
  Wire.write(0x6B);        // PWR_MGMT_1
  Wire.write(0);           // clear the sleep bit
  byte error = Wire.endTransmission(true);

  if (error == 0) {
    Serial.println("Motion sensor ready.");
  } else {
    Serial.printf("Motion sensor NOT responding (I2C error %d). Check SDA/SCL and 3V3.\n", error);
  }
}

/** Total acceleration in g. Reads about 1.0 sitting still — that is gravity. */
float readForceG() {
  Wire.beginTransmission(MPU_ADDRESS);
  Wire.write(0x3B);        // ACCEL_XOUT_H
  if (Wire.endTransmission(false) != 0) return 0;

  if (Wire.requestFrom(MPU_ADDRESS, 6, true) != 6) return 0;

  int16_t x = (Wire.read() << 8) | Wire.read();
  int16_t y = (Wire.read() << 8) | Wire.read();
  int16_t z = (Wire.read() << 8) | Wire.read();

  float gx = x / COUNTS_PER_G;
  float gy = y / COUNTS_PER_G;
  float gz = z / COUNTS_PER_G;

  return sqrt(gx * gx + gy * gy + gz * gz);
}

/** Keeps sampling briefly so the reported figure is the peak, not the onset. */
float capturePeak(float initial) {
  float peak = initial;
  unsigned long until = millis() + PEAK_WINDOW_MS;

  while (millis() < until) {
    float force = readForceG();
    if (force > peak) peak = force;

    while (gpsSerial.available() > 0) gps.encode(gpsSerial.read());
    delay(5);
  }

  return peak;
}

// ── Network ────────────────────────────────────────────────────────────

void connectWiFi() {
  Serial.printf("Joining %s", WIFI_SSID);
  WiFi.mode(WIFI_STA);
  WiFi.begin(WIFI_SSID, WIFI_PASSWORD);

  unsigned long giveUpAt = millis() + 20000;
  while (WiFi.status() != WL_CONNECTED && millis() < giveUpAt) {
    delay(500);
    Serial.print(".");
  }

  if (WiFi.status() == WL_CONNECTED) {
    Serial.printf("\nConnected. This unit is %s\n", WiFi.localIP().toString().c_str());
  } else {
    Serial.println("\nCould not join. Check the name and password, and that the");
    Serial.println("network is 2.4GHz — the ESP32 cannot see 5GHz networks.");
  }
}

/**
 * Asks the server who it thinks this unit is. Run at boot so a wrong token
 * or unreachable server is obvious immediately, rather than at the moment
 * an incident is being demonstrated.
 */
void checkIn() {
  if (WiFi.status() != WL_CONNECTED) return;

  HTTPClient http;
  http.begin(String(SERVER_BASE) + "/api/devices/me");
  http.addHeader("X-Device-Token", DEVICE_TOKEN);

  int status = http.GET();

  if (status == 200) {
    Serial.println("Server recognises this unit:");
    Serial.println("  " + http.getString());
  } else if (status == 401) {
    Serial.println("Server rejected the device token. Check DEVICE_TOKEN.");
  } else {
    Serial.printf("Could not reach the server (%d). Check SERVER_BASE and that\n", status);
    Serial.println("the dashboard is listening on all interfaces, not just localhost.");
  }

  http.end();
}

void reportIncident(float peakG) {
  if (WiFi.status() != WL_CONNECTED) {
    Serial.println("Not connected — reconnecting before reporting.");
    connectWiFi();
    if (WiFi.status() != WL_CONNECTED) {
      Serial.println("Still offline. Incident not reported.");
      return;
    }
  }

  String body = "{";
  body += "\"impactG\":" + String(peakG, 2);

  // Everything below is optional: a unit with no fix yet still reports the
  // impact, and the server records it without coordinates.
  if (gps.location.isValid()) {
    body += ",\"latitude\":" + String(gps.location.lat(), 6);
    body += ",\"longitude\":" + String(gps.location.lng(), 6);
  } else {
    Serial.println("No GPS fix yet — reporting without coordinates.");
  }

  if (gps.speed.isValid()) {
    body += ",\"speedKph\":" + String(gps.speed.kmph(), 1);
  }

  if (gps.date.isValid() && gps.time.isValid()) {
    char stamp[25];
    snprintf(stamp, sizeof(stamp), "%04d-%02d-%02dT%02d:%02d:%02dZ",
             gps.date.year(), gps.date.month(), gps.date.day(),
             gps.time.hour(), gps.time.minute(), gps.time.second());
    body += ",\"occurredAt\":\"" + String(stamp) + "\"";
  }

  body += "}";

  HTTPClient http;
  http.begin(String(SERVER_BASE) + "/api/incidents");
  http.addHeader("Content-Type", "application/json");
  http.addHeader("X-Device-Token", DEVICE_TOKEN);

  int status = http.POST(body);

  if (status == 201) {
    Serial.println("Reported: " + http.getString());
  } else {
    Serial.printf("Report failed (%d): %s\n", status, http.getString().c_str());
  }

  http.end();
}
