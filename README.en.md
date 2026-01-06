# Time Reference NMEA

<div align="center">

[![en](https://img.shields.io/badge/lang-en-red.svg)](https://github.com/fmoreau78470/Time-Reference-NMEA/blob/main/README.en.md)
[![fr](https://img.shields.io/badge/lang-fr-blue.svg)](https://github.com/fmoreau78470/Time-Reference-NMEA/blob/main/README.md)

**Transform your Windows PC into a high-precision Stratum 1 Time Server.**

[![Support on Ko-fi](https://img.shields.io/badge/Ko--fi-Support%20the%20project-blue?style=for-the-badge&logo=kofi)](https://ko-fi.com/francismoreau)
[![Documentation](https://img.shields.io/badge/docs-online-blue?style=for-the-badge&logo=read-the-docs)](https://fmoreau78470.github.io/Time-Reference-NMEA/en/)
[![License](https://img.shields.io/badge/license-MIT-green?style=for-the-badge)](LICENSE)
[![Release](https://img.shields.io/badge/release-latest-orange?style=for-the-badge)](https://github.com/fmoreau78470/Time-reference-NMEA/releases)

</div>

## 🔭 Why this project?

This project was developed to meet a critical need in **astronomy**: precise timestamping of acquisitions for **stellar occultation** light curves. These observations often take place in the field, in mobile situations where no reliable Internet connection is available.

Standard solutions (NTP over Internet) suffer from variable latency (Jitter) and require a connection. **Time Reference NMEA** uses a local hardware source (GPS + PPS Signal) to discipline the Windows clock with **millisecond** precision, completely autonomously.

## ✨ Key Features

*   **Stratum 1 Precision:** Direct synchronization to the atomic clock of GNSS satellites.
*   **Offline Mode:** Works perfectly without Internet.
*   **PPS Technology:** Uses the *Pulse Per Second* signal to eliminate serial transmission jitter.
*   **Control Application (WPF):**
    *   Intuitive "Hand Controller" style interface.
    *   **Automatic Calibration** of hardware delay (Fudge).
    *   Real-time monitoring: Offset, Jitter, System Health.
    *   GPS Signal Quality Analyzer (IQT: SNR, HDOP, Satellites).
    *   "Always on top" Mini Widget Mode.

## 🛠️ Hardware Requirements

The system relies on accessible and inexpensive hardware (< 20€):

1.  **Microcontroller:** Waveshare RP2040-Zero (USB Interface & Processing).
2.  **GPS Module:** u-blox NEO-6M or NEO-8M.
3.  **Connection:** USB-C Data Cable.

*The "Stratum 0" firmware for the RP2040 is available in Releases.*

## 💻 Installation & Prerequisites

### ⚠️ Absolute Prerequisite
This software drives the **official Meinberg NTP** service.
The standard Windows Time service (W32Time) is **NOT** supported as it is insufficient for the targeted precision.

1.  Download and install [NTP for Windows (Meinberg)](https://www.meinbergglobal.com/english/sw/ntp.htm).
2.  Download the installer `TimeReferenceNMEA_Setup.exe` from GitHub Releases.

### Quick Start
1.  Plug in your RP2040/GPS module.
2.  Launch **Time Reference NMEA**.
3.  In settings, select the detected COM port.
4.  The application automatically configures the local NTP service.
5.  Run **Calibration** to compensate for USB delays.

## 📚 Documentation

Complete documentation is available to guide you step by step:
*   NTP Theory
*   Hardware Assembly Guide
*   Software Manual

👉 **Access full documentation**

## 🏗️ Technical Architecture

*   **Firmware (RP2040):** C++ / Arduino. "Time Adder" algorithm to align the NMEA frame with the PPS signal.
*   **PC Software:** C# .NET 6/8 (WPF). Interface with `ntpq` and Windows service management.
*   **Documentation:** MkDocs with Material theme.

## 📄 License

This project is distributed under the MIT license.
