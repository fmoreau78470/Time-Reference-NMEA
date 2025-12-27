from machine import UART, Pin
import time

# Configuration UART
gps_uart = UART(0, baudrate=9600, tx=Pin(0), rx=Pin(1))
led = Pin(16, Pin.OUT)
pps_pin = Pin(2, Pin.IN, Pin.PULL_DOWN)

pps_triggered = False

def pps_handler(pin):
    global pps_triggered
    pps_triggered = True
    led.value(1) # Flash LED au début de la seconde

pps_pin.irq(trigger=Pin.IRQ_RISING, handler=pps_handler)

last_rmc = ""

print("RP2040 Stratum 0 : Mode Alignement PPS actif")

while True:
    # 1. On lit les données du GPS en continu
    if gps_uart.any():
        line = gps_uart.readline()
        try:
            decoded = line.decode('utf-8')
            # On stocke la trame RMC (la plus importante pour le temps)
            if "$GPRMC" in decoded:
                last_rmc = decoded
            # On laisse passer les autres trames normalement
            else:
                print(decoded, end='')
        except:
            pass

    # 2. Si le PPS vient de tomber, on envoie la trame RMC stockée immédiatement
    if pps_triggered:
        if last_rmc:
            print(last_rmc, end='')
            last_rmc = "" # On vide pour attendre la suivante
        
        time.sleep_ms(50) # On laisse la LED allumée un peu
        led.value(0)
        pps_triggered = False