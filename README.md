# Proyecto Realidad Mixta - Simulador Carnotauro

Entrega de avance correspondiente al parcial (40%).

El proyecto consiste en controlar un dinosaurio (Carnotauro) en Unity 6 como si fuera un vehículo, manejando aceleración, giro y físicas de peso. El objetivo final es controlarlo con un mando físico armado con Arduino mediante comunicación por puerto serial, aunque cuenta con controles por teclado para hacer pruebas rápidas.

## Integrantes
- Juan J. Plazas - jjplazas@uniboyaca.edu.co
- K. J. Barreto - Kjbarreto@uniboyaca.edu.co
- David Rosas - davrosas@uniboyaca.edu.co

---

## Controles para Pruebas (Teclado)
Mientras no esté conectado el Arduino, se puede probar con:
- **W / S**: Acelerar y reversa
- **A / D**: Girar (dirección)
- **Espacio**: Freno
- **F**: Saltar
- **E / Click Izquierdo**: Rugido (turbo momentáneo)
- **Shift**: Correr más rápido
- **R**: Reubicar el dinosaurio en la pista si se vuelca

---

## Conexiones de Arduino
El sketch para cargar a la placa está en `Assets/Dino/ArduinoSketch/DinoController.ino`.
- **Pin A0**: Potenciómetro de dirección (volante)
- **Pin A1**: Potenciómetro de velocidad (acelerador)
- **Pin 2**: Botón de salto
- **Pin 3**: Botón de rugido / turbo
- **Baudrate**: 115200

---

## Estado del Proyecto
- Escena de prueba montada en `Assets/Dino/Scenes/DinoTestTrack.unity` con rampas y cajas con físicas.
- Malla del Carnotauro con silla de montar y texturas configuradas.
- Script de lectura serial multihilo listo para recibir datos de Arduino sin congelar el juego.
- En la rama `simulation` se encuentran pruebas adicionales de vegetación y colisiones con árboles.
