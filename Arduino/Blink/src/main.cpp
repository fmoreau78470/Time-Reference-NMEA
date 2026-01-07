 #include <Arduino.h>
#include <Adafruit_TinyUSB.h>

// Le RP2040-Zero a une LED RGB adressable (Neopixel) sur la pin GP16
#define PIN_NEOPIXEL 16

void setup() {
  // Initialisation nécessaire pour le RP2040-Zero
  Serial.begin(115200);
  
  // Configuration de la LED (simple sortie pour test basique, 
  // pour la vraie couleur il faut la librairie Adafruit_NeoPixel, 
  // mais ceci suffit pour voir si ça compile)
  pinMode(PIN_NEOPIXEL, OUTPUT);
}

void loop() {
  // Clignotement simple
  digitalWrite(PIN_NEOPIXEL, HIGH);
  delay(500);
  digitalWrite(PIN_NEOPIXEL, LOW);
  delay(500);
  Serial.println("Tick");
}
