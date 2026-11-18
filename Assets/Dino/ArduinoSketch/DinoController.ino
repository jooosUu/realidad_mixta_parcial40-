/*
  DinoController.ino — Mando de DINO WEST: 2 potenciómetros + 2 pulsadores
  Funciona en ESP32 y en Arduino UNO / Nano / Mega.

  Trama por Serial (115200 baudios, ~50 por segundo):
      DW,<pot1>,<pot2>,<boton1>,<boton2>
  pot1 y pot2 van de 0 a 1000 (ya normalizados, sin importar si el ADC es de 10 o 12 bits)
  y botones 1 = pulsado. Unity detecta solo el puerto buscando las líneas que empiezan por "DW".

  EN UNITY
    Juego:  pot1 = volante · pot2 = acelerador (al fondo corre)
            botón 1 = salto · botón 2 = rugido · los 2 botones 1 s = pausa
    Menús:  los potenciómetros mueven el puntero (pot1 horizontal, pot2 vertical)
            botón 1 = aceptar / clic · botón 2 = atrás

  CONEXIONES
    Potenciómetros (3 patas): extremos a 3V3 (ESP32) o 5V (UNO) y GND; pata del centro al pin analógico.
    Pulsadores de 4 patas: las patas vienen unidas de 2 en 2. Usa dos patas en DIAGONAL:
    una al pin digital y la otra a GND. No hace falta resistencia (se usa INPUT_PULLUP).

                     ESP32 (DevKit)      UNO / Nano
    Potenciómetro 1  GPIO 34             A0
    Potenciómetro 2  GPIO 35             A1
    Pulsador 1       GPIO 25             D2
    Pulsador 2       GPIO 26             D3
*/

#if defined(ESP32)
const int PIN_POT1 = 34;
const int PIN_POT2 = 35;
const int PIN_BTN1 = 25;
const int PIN_BTN2 = 26;
const float ADC_MAX = 4095.0;
#else
const int PIN_POT1 = A0;
const int PIN_POT2 = A1;
const int PIN_BTN1 = 2;
const int PIN_BTN2 = 3;
const float ADC_MAX = 1023.0;
#endif

const unsigned long REBOTE_MS = 25;   // antirrebote de los pulsadores
const unsigned long PERIODO_MS = 20;  // ~50 tramas por segundo

// Suavizado de los potenciómetros (quita el ruido del ADC)
float pot1Suave = -1, pot2Suave = -1;

struct Boton {
  int pin;
  bool estable;          // true = pulsado
  bool ultimaLectura;
  unsigned long cambio;
};
Boton boton1 = {PIN_BTN1, false, false, 0};
Boton boton2 = {PIN_BTN2, false, false, 0};

void actualizarBoton(Boton &b) {
  bool lectura = digitalRead(b.pin) == LOW;  // INPUT_PULLUP: LOW = pulsado
  if (lectura != b.ultimaLectura) {
    b.ultimaLectura = lectura;
    b.cambio = millis();
  }
  if (millis() - b.cambio >= REBOTE_MS) b.estable = lectura;
}

float leerPot(int pin, float &suave) {
  // Promedio de 4 lecturas + filtro exponencial
  long suma = 0;
  for (int i = 0; i < 4; i++) suma += analogRead(pin);
  float v = (suma / 4.0) / ADC_MAX * 1000.0;
  suave = (suave < 0) ? v : suave + 0.35 * (v - suave);
  return suave;
}

void setup() {
  Serial.begin(115200);
  pinMode(PIN_BTN1, INPUT_PULLUP);
  pinMode(PIN_BTN2, INPUT_PULLUP);
#if defined(ESP32)
  analogReadResolution(12);
  analogSetAttenuation(ADC_11db);  // rango completo 0-3.3 V
#endif
}

unsigned long ultimoEnvio = 0;

void loop() {
  actualizarBoton(boton1);
  actualizarBoton(boton2);

  unsigned long ahora = millis();
  if (ahora - ultimoEnvio < PERIODO_MS) return;
  ultimoEnvio = ahora;

  int p1 = constrain((int)(leerPot(PIN_POT1, pot1Suave) + 0.5), 0, 1000);
  int p2 = constrain((int)(leerPot(PIN_POT2, pot2Suave) + 0.5), 0, 1000);

  Serial.print("DW,");
  Serial.print(p1);
  Serial.print(',');
  Serial.print(p2);
  Serial.print(',');
  Serial.print(boton1.estable ? 1 : 0);
  Serial.print(',');
  Serial.println(boton2.estable ? 1 : 0);
}
