# FAQ & Troubleshooting

## My GPS does not fix (No flashing LED)
*   Ensure you are outdoors or near a window.
*   Check the module power supply (the Power LED must be on).

## The application says "COM Port not found"
*   Check that the RP2040 is properly connected.
*   Check in Device Manager for any driver errors.

## Why are administrator rights required?
The application must stop and restart the Windows Time service (W32Time) or NTPD to apply precision corrections.

## Can NTP be used without GPS?
Yes. The NTP service (Meinberg) is designed to handle multiple sources.
*   If the GPS is disconnected, the service automatically switches to the configured Internet servers (NTP Pool) as a fallback.
*   You lose Stratum 1 precision (microsecond) to return to Stratum 2 precision (millisecond), but your clock remains disciplined and more accurate than via the standard Windows service.
*   The application will simply display that the GPS source is missing, but the NTP service will continue running in the background.