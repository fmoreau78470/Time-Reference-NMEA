---
title: Home
---
# Home - Time Reference NMEA

Welcome to the official documentation of the **Time Reference NMEA** project.

This project aims to transform a standard Windows computer into a **High Precision Time Server (Stratum 1)**, capable of disciplining its internal clock with millisecond precision, without relying on an Internet connection.

It was specifically created to meet the needs of **precise timestamping of acquisitions** necessary for the realization of **light curves** for the observation of **occultations**, particularly in mobile situations where **no Internet connection is available**.

## 🎯 Why this project?

In a connected world, time is generally provided by NTP servers on the Internet (Stratum 2 or 3). Although sufficient for office use, this system has limitations:

*   **Variable network latency:** Packet travel time on the Internet fluctuates (Jitter), degrading precision.
*   **Dependency:** Without Internet, the clock drifts quickly.
*   **Security:** Dependence on third parties.

**Time Reference NMEA** solves these problems by using a local hardware source: a **GPS/GNSS** receiver.

### The advantages
*   **Stratum 1 Precision:** Your PC is directly connected to the atomic source of GPS satellites.
*   **Autonomy:** Works perfectly in "Field" mode (Offline).
*   **Stability:** Uses the PPS (Pulse Per Second) signal for ultra-precise synchronization.

## 🚀 Global Operation

The system relies on the synergy between three components:

1.  **The Hardware:** A GPS module (u-blox type) coupled with a microcontroller (RP2040) which converts satellite signals into a data stream understandable by the computer via USB.
2.  **The NTP Service (Meinberg):** The industry standard for time management under Windows. It disciplines the system clock in the background.
3.  **The Control Application (This software):** A modern graphical interface to:
    *   Configure the NTP service without command line.
    *   Visualize GPS reception and signal quality.
    *   Automatically calibrate transmission delays (Fudge).
    *   Monitor the health of your time server.

## 🛠️ Major Implementation Steps

1.  **Hardware Assembly:** Connection of the GPS module to the RP2040 and flashing of the "Stratum 0" firmware.
2.  **Software Installation:** Installation of the Meinberg NTP service and the Time Reference NMEA application.
3.  **Calibration:** Calculation of latency (Fudge) to perfectly align GPS time with reality.
4.  **Production:** The system runs autonomously and maintains precise time.

## 📚 Documentation Organization

*   **NTP Theory:** Understanding basic concepts (Stratum, Jitter, Offset).
*   **Hardware Guide:** Component list and assembly instructions.
*   **Software Manual:** Installation, configuration, and use of the application.
*   **FAQ & Troubleshooting:** Solutions to common problems.

---

## ❤️ Support the project

If this project is useful to you, you can buy me a coffee to support its development!

<a href='https://ko-fi.com/francismoreau ' target='_blank'><img height='36' style='border:0px;height:36px;' src='https://storage.ko-fi.com/cdn/kofi2.png?v=3' border='0' alt='Buy Me a Coffee at ko-fi.com' /></a>
