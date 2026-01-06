# NTP Theory & Synchronization

Understanding the mechanisms that allow your computer to be precisely on time is essential to getting the most out of this project. This page popularizes the key concepts of the NTP protocol and explains why adding a GPS changes the game.

## 1. What is NTP?

**NTP (Network Time Protocol)** is one of the oldest protocols on the Internet. Its role is to synchronize computer clocks across a network with variable latency.

It operates on a hierarchical model organized into **Strata (Stratum)**:

*   **Stratum 0:** The physical time source (Atomic Clock, GPS). It does not connect to the network.
*   **Stratum 1:** A computer directly connected to a Stratum 0 source (via serial/USB cable). This is the "Primary Time Server". **This is the role of your PC with this project.**
*   **Stratum 2:** A computer that requests time from a Stratum 1 server via the Internet.
*   **Stratum 3:** A computer that requests time from a Stratum 2, and so on...

The higher the stratum number, the more precision degrades due to transmission delays.

## 2. Why is GPS superior to the Internet?

Most PCs synchronize over the Internet (Stratum 2 or 3). Although sufficient for daily life, this system suffers from major flaws for astronomy or science:

### A. Variable Latency (Jitter)
On the Internet, data packets travel by different paths and pass through congested routers. The "outbound" travel time is never exactly the same as the "return" time. This unpredictable variation is called **Jitter**.
*   *Internet:* Typical jitter of 10 to 100 ms.
*   *Local GPS:* Near-zero jitter (< 0.005 ms) because the link is direct (USB/Serial cable).

### B. Asymmetry
NTP theoretically assumes that the Outbound travel time is equal to the Return travel time. However, on a consumer ADSL, 4G, or Fiber connection, download and upload speeds are different, creating a systematic calculation error (Offset) impossible to correct via software.

### C. Availability
Without Internet, your PC clock drifts quickly (several seconds per day). GPS works everywhere on Earth, without subscription and without network, guaranteeing total autonomy ("Field" mode).

## 3. Why use Meinberg NTP?

Windows includes a default time service called **W32Time**. Why replace it?

*   **W32Time** was designed for network authentication (Kerberos), which tolerates up to 5 minutes of error. It is not designed for high scientific precision. It often corrects time by brutal "jumps".
*   **Meinberg NTP** is the Windows port of the official NTP daemon (the one used by NASA or CERN Linux servers).
    *   It disciplines the clock gently (speeds up/slows down the frequency) without time jumps.
    *   It achieves microsecond-level precision.
    *   It offers advanced diagnostic tools (`ntpq`) that this software uses.

## 4. NTP Algorithms

NTP is not a simple "time setting". It is a complex servo system.

### The Intersection Algorithm (Marzullo)
If you configure multiple sources (e.g., GPS + 3 Internet servers), NTP must guess who is telling the truth. It compares the time intervals of each source and rejects "liars" (False Tickers) that deviate too much from the consensus. This allows ignoring a faulty Internet server.

### The Discipline Loop (PLL/FLL)
Once the best source is chosen, NTP does not just "reset" the time. It calculates the **natural drift** of your quartz (Drift).
*   If your PC is 10ms late, NTP will slightly increase the clock frequency to catch up gradually.
*   This guarantees a continuous and monotonic time scale (no backward steps possible), essential for data logging (Logs, Databases, Light Curves).

## 5. Glossary of Common Terms

Here are the technical terms you will encounter in the application:

| Term | Definition | Unit | Good values |
| :--- | :--- | :--- | :--- |
| **Offset** | The time difference between your PC and the absolute reference. This is the error to correct. | ms | Close to 0 (e.g., +/- 2ms) |
| **Jitter** | Signal stability (latency variance). A low value indicates a reliable connection. | ms | < 5 ms (GPS) |
| **Drift** | The natural drift of your PC's hardware clock (quartz inaccuracy). | ppm | Stable (e.g., 15.000 ppm) |
| **Reach** | Register (octal) indicating the success of the last 8 connection attempts. This is the health history. | - | **377** (100% success) |
| **Poll** | Interval between two source queries (power of 2). | s | 16s (Poll 4) to 1024s (Poll 10) |
| **PPS** | *Pulse Per Second*. Electrical signal sent by the GPS at each precise second start. This is the ultimate synchronization "tick". | - | - |