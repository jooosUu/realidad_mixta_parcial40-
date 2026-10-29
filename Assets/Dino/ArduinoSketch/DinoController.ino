/*
  DinoController.ino
  Sketch de Arduino para controlar el Carnotauro en Unity
  
  Conexiones sugeridas:
  - Potenciómetro de Dirección (Volante / Manillar): Pin A0
  - Potenciómetro de Velocidad (Palanca / Pedal): Pin A1
  - Botón de Salto / Pisotón: Pin 2 (con resistencia PULLUP interna)
  - Botón de Rugido / Turbo: Pin 3 (con resistencia PULLUP interna)
*/

const int PIN_STEER = A0;      // Potenciómetro Dirección
const int PIN_THROTTLE = A1;   // Potenciómetro Velocidad
const int PIN_JUMP = 2;        // Pulsador Salto
const int PIN_ROAR = 3;        // Pulsador Rugido

void setup() {
  Serial.begin(115200);
  
  pinMode(PIN_JUMP, INPUT_PULLUP);
  pinMode(PIN_ROAR, INPUT_PULLUP);
}

void loop() {
  // 1. Leer valores analógicos (0 a 1023)
  int steerVal = analogRead(PIN_STEER);
  int throttleVal = analogRead(PIN_THROTTLE);
  
  // 2. Leer botones digitales (INPUT_PULLUP: LOW cuando se presiona)
  int jumpVal = (digitalRead(PIN_JUMP) == LOW) ? 1 : 0;
  int roarVal = (digitalRead(PIN_ROAR) == LOW) ? 1 : 0;
  
  // 3. Enviar trama por Serial en formato CSV:
  // "Direccion,Velocidad,Salto,Rugido"
  Serial.print(steerVal);
  Serial.print(",");
  Serial.print(throttleVal);
  Serial.print(",");
  Serial.print(jumpVal);
  Serial.print(",");
  Serial.println(roarVal);
  
  // Enviar a ~50 Hz (cada 20ms) para respuesta ultra fluida sin saturar el buffer
  delay(20);
}
