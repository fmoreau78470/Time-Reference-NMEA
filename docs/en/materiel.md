---
title: Hardware
---
# Hardware & Assembly Guide

This guide details the necessary components and assembly steps to build your Stratum 0 GPS receiver.

## 1. Component Sourcing

The project is designed to be accessible and inexpensive. Here is the list of recommended components:

| Component | Recommended Model | Role |
| :--- | :--- | :--- |
| **Microcontroller** | **Waveshare RP2040-Zero** | USB interface and signal processing. Chosen for its compact size and Arduino compatibility. |
| **GPS Module** | **u-blox NEO-6M** or **NEO-8M** | Satellite reception. Often sold under the reference `GY-GPS6MV2` or `GY-NEO6MV2`. |
| **USB Cable** | USB Type-C (Data) | Connection to the PC. Ensure it transmits data (not just charge). |
![Ma photo](../Photos/RP2040.jpg)
![Ma photo](../Photos/NEO-6M.jpg)



## 2. Inter-module Wiring

The assembly connects the GPS module to the microcontroller via a serial link (UART).
![Ma photo](../Photos/Montage.jpg)

### Connection Diagram

| GPS Pin | RP2040-Zero Pin | Description |
| :--- | :--- | :--- |
| **VCC** | **5V** (or VBUS) | GPS module power supply. |
| **GND** | **GND** | Common ground (Electrical reference). |
| **TX** | **GP1** (RX) | Transmission of NMEA data from GPS to RP2040. |
| **RX** | **GP0** (TX) | Reception of configuration commands. |
| **PPS** | **GP2** | PPS Signal (Pulse per Second). |


## 3. PPS Signal Wiring

This is the most critical connection for the time precision of this project.

Not all GPS modules have a PPS pin.

This is the case for the NEO-6M (see photo). In this case, you must connect to the output of the circuit powering the LED. It is the orange wire.

The NEO-8M has a pin called PPS.

*   **Destination:** Pin **GP2** of the RP2040-Zero.

![Ma photo](../Photos/PPS.jpg)

**Why is it indispensable?**
NMEA data (sent via TX/RX) provides date and time, but with variable latency (Jitter) of several hundred milliseconds due to serial processing.

The **PPS (Pulse Per Second)** signal is an electrical pulse sent physically at the exact beginning of each atomic second. The RP2040 uses this signal to align data transmission to the PC with microsecond precision.

### Visual Indicator (PPS LED)
Most GPS modules (u-blox NEO-6M/8M) have a small integrated LED connected to the PPS signal.

*   **Behavior:** It remains off (or solid depending on the model) while the GPS searches for satellites. It starts flashing as soon as "Fix" is acquired (3D Fix).
*   **Signal characteristics:** It is the rising edge (start of lighting) that marks the precise second.
> **Note:** The electrical pulse lasts exactly 100 ms.


## 4. Positioning and Disturbances

GPS/GNSS signals are extremely weak radio waves (-125 dBm to -160 dBm). The physical environment of the assembly directly impacts reception quality (SNR).

### Avoid interferences
Fast digital electronics (RP2040 processor, USB port, PC) generate RF "noise" that can jam the GPS antenna.

*   **Distance:** Do not stick the GPS antenna directly on the RP2040. Leave at least 5 to 10 cm of cable between them.
*   **Enclosure:** If using a metal case, the antenna must be outside.

### Antenna Orientation
*   The ceramic antenna (flat square) must have a **clear view of the sky**.
*   It works through plastic, glass, or wood, but not through metal or carbon fiber.
*   For indoor use (near a window), precision will be lower (degraded Stratum 1) compared to an active outdoor antenna.


## 5. Firmware Installation (Stratum 0)

Once the hardware is assembled, you must flash the RP2040 to act as a smart interface.

1.  Download the `Stratum0.uf2` file from the **Releases** section of the GitHub project.
2.  Unplug the RP2040 from the PC.
3.  Hold the **BOOT** button on the RP2040 and plug it into the PC.
4.  An `RPI-RP2` drive appears in the file explorer.
5.  Copy the `Stratum0.uf2` file to this drive.
6.  The RP2040 restarts automatically: your hardware is ready.

### 💡 Diagnostic LED (RP2040-Zero)

The internal RGB LED indicates the status of the GPS:

* **Blue:** No data received from GPS (check wiring).

* **Red:** GPS data received, but no satellite fix yet.

* **Green:** GPS Fix acquired, but PPS signal missing (> 5s).

* **White Flash:** PPS signal detected (LED turns off between flashes when PPS is active).

You can verify that your GPS is emitting NMEA frames by analyzing the serial port using software like Putty.

![Ma photo](../PrintScreen/Putty.png)


## 6. Firmware Operation (Going further)

The RP2040 code doesn't just relay data. It transforms the consumer GPS module into a scientific reference clock.

### The Technical Challenge
A GPS module emits two pieces of information:

1.  The **PPS** signal: An ultra-precise electrical pulse at the beginning of the second.
2.  The **NMEA** frame: A text message ("It is 12:00:00") sent via the serial port.

The problem is that the text message arrives **after** the pulse (about 100 to 500ms later). If the computer waits for the message to align with the *next* PPS, it will be one second late.

### The Software Solution
The firmware uses a "Time Adder" algorithm:

1.  It reads the NMEA frame as soon as it arrives.
2.  It **adds 1 second** to the received time (handling minute, hour, day, year rollovers).
3.  It stores this "futuristic" frame in memory.
4.  It waits for the next PPS signal.
5.  As soon as the PPS strikes, it immediately sends the modified frame via USB.

Result: Windows receives the time "12:00:01" at the exact microsecond when second 12:00:01 begins.
