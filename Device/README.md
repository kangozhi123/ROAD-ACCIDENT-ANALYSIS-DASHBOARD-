# Vehicle unit

ESP32 + MPU6050 (motion) + NEO-6M (GPS). Detects an impact, reads the GPS
fix, and posts one incident to the dashboard over WiFi.

## Wiring — ESP32 DevKit v1

| Module | Pin | ESP32 |
|---|---|---|
| MPU6050 | VCC | 3V3 |
| | GND | GND |
| | SDA | GPIO21 |
| | SCL | GPIO22 |
| NEO-6M | VCC | 3V3 |
| | GND | GND |
| | TX | GPIO16 |
| | RX | GPIO17 |

The GPS module's **TX** goes to the ESP32's **RX**. Getting this backwards is
the usual reason a GPS "does nothing".

On an ESP32-CAM the camera occupies most of the pins — use GPIO14/15 for I²C
and GPIO12/13 for the GPS, and power it from a supply that holds 5V at 2A.

## Before flashing

1. Install the ESP32 board support in the Arduino IDE
   (Boards Manager → "esp32" by Espressif).
2. Install **TinyGPSPlus** by Mikal Hart (Library Manager). It is the only
   library needed; everything else ships with the ESP32 core.
3. Open `AccidentDetector/AccidentDetector.ino` and set the four values at
   the top: WiFi name, WiFi password, `SERVER_BASE`, and `DEVICE_TOKEN`.

`SERVER_BASE` is the **IP address of the machine running the dashboard**, not
`localhost` — to the ESP32, localhost means itself. Find it with `ipconfig`.

## Letting the ESP32 reach the dashboard

By default the dashboard listens on localhost only, which the ESP32 cannot
reach. Start it bound to every interface:

```bash
dotnet run --project RoadSafety.Web --urls "http://0.0.0.0:5199"
```

Windows Firewall will usually prompt the first time — allow it on private
networks. Both devices must be on the **same 2.4GHz network**; the ESP32
cannot see 5GHz.

## Checking it works

The sketch calls `/api/devices/me` at boot and prints the result, so the
serial monitor (115200 baud) tells you immediately whether WiFi and the token
are good — before you need to demonstrate anything.

You can also test the server without hardware:

```bash
curl -X POST http://localhost:5199/api/incidents \
  -H "X-Device-Token: ZP-DEMO-DEVICE-0001" \
  -H "Content-Type: application/json" \
  -d '{"latitude":-12.8054,"longitude":28.2132,"impactG":6.4,"speedKph":48}'
```

## Tuning the trigger

`IMPACT_THRESHOLD_G` is 2.8g. At rest the sensor reads about 1g (gravity), a
firm shake reaches roughly 2g, and a real collision is far higher. Lower it if
the board is hard to trigger by hand; raise it if it fires while being
handled.

`QUIET_PERIOD_MS` is 15 seconds, so one event produces one incident rather
than thirty as the unit rattles.

## What this does not do

It reports a **suspected** collision. A threshold on acceleration cannot tell
a crash from a dropped unit, which is why the dashboard records every report
as awaiting review rather than as a confirmed crash.

It also has no cellular radio, so it only reports within WiFi range. A
deployed unit would need a GSM module.
