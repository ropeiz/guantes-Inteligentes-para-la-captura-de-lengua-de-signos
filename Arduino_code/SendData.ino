#include <Arduino_LSM6DS3.h>  // Librería para el IMU (LSM6DS3)

const int flexPin1 = A0;      // Pin analógico donde está conectado el flex sensor 1
const int flexPin2 = A1;      // Pin analógico donde está conectado el flex sensor 2
const int flexPin3 = A2;      // Pin analógico donde está conectado el flex sensor 3
const int flexPin4 = A3;      // Pin analógico donde está conectado el flex sensor 4
const int flexPin5 = A6;      // Pin analógico donde está conectado el flex sensor 5

unsigned long tPrevTotal = 0; // guardará el tiempo (micros) que tardó en leer + enviar el paquete anterior
bool primeraVez = true;       // flag para el primer loop



void setup() {
  Serial.begin(115200);      // Monitor serie
  Serial1.begin(115200);     // Comunicación con HC-05 (pines D0 y D1)

  delay(1000);

  if (!IMU.begin()) {
    Serial.println("Error de conexión con el LSM6DS3");
    while (1);  // Detener si falla el sensor
  }

}

void loop() {
  
  unsigned long tStart = micros(); 

  // 1) ===== Envío al principio del paquete del tiempo de la iteración anterior =====
  if (primeraVez) {
    // En el primer loop no hay “anterior”, así que envio 0 
    Serial1.print("T0,");
    primeraVez = false;
  } else {

    // Envío del tiempo que tardó en leer+enviar todo el paquete anterior
    Serial1.print("T"); 
    Serial1.print(tPrevTotal); 
    Serial1.print(',');
  }

  // ===== 2) Leer flex sensors =====
  int flexValue1 = analogRead(flexPin1);
  int flexValue2 = analogRead(flexPin2);
  int flexValue3 = analogRead(flexPin3);
  int flexValue4 = analogRead(flexPin4);
  int flexValue5 = analogRead(flexPin5);

  // ===== 3) Leer giroscopio =====
  float gx = 0, gy = 0, gz = 0;
  if (IMU.gyroscopeAvailable()) {
    IMU.readGyroscope(gx, gy, gz);
  }
  
  if (tPrevTotal < 6400){
  // ===== 4) Envío de datos A,B,C (gyro) =====
  Serial1.print("A"); Serial1.print(gx, 4); Serial1.print(',');
  Serial1.print("B"); Serial1.print(gy, 4); Serial1.print(',');
  Serial1.print("C"); Serial1.print(gz, 4); Serial1.print(',');
  }

  // ===== 5) Envío de datos D,E,F,G,H (flex) =====
  Serial1.print("D"); Serial1.print(flexValue1); Serial1.print(',');
  Serial1.print("E"); Serial1.print(flexValue2); Serial1.print(',');
  Serial1.print("F"); Serial1.print(flexValue3); Serial1.print(',');
  Serial1.print("G"); Serial1.print(flexValue4); Serial1.print(',');
  Serial1.print("H"); Serial1.print(flexValue5); Serial1.print(',');


  // ===== 6) Delimitador de paquete con '#' y salto de línea =====
  Serial1.println('#'); Serial1.print(',');

  // ===== 7) Al final calculamos cuánto tardamos en armar+enviar este paquete =====
  unsigned long tEnd = micros();
  tPrevTotal = tEnd - tStart;
}
