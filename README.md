# Guantes Inteligentes para la Captura de Lengua de Signos

Este repositorio contiene todo el material y código desarrollado en el Trabajo de Fin de Grado (TFG) “Diseño y desarrollo de un prototipo de guantes inteligentes para la captura de gestos en lengua de signos”, realizado en la Escola d’Enginyeria (EE), Universitat Autònoma de Barcelona (UAB).

---

## 📑 Descripción del Proyecto

La comunicación entre personas sordas y oyentes se ve limitada por la dependencia de intérpretes especializados y la falta de soluciones accesibles. Este proyecto propone un prototipo de **guantes inteligentes** que capturan en tiempo real el movimiento y la flexión de la mano mediante sensores, representándolo en un modelo 3D en Unity y exportando los datos para su posterior traducción a voz (módulo de traducción no incluido).

### Objetivos principales

1. **Diseño y construcción del prototipo**  
   — Integración ergonómica de microcontrolador, IMU y sensores resistivos.  
2. **Visualización 3D en Unity**  
   — Script de procesamiento de datos y representación de manos virtuales.  
3. **Transmisión de datos vía Bluetooth**  
   — Envío de paquetes (giroscopio + flexión) a 60 FPS con detección y corrección de errores.  
4. **Exportación y reproducción**  
   — Script de exportación a CSV y script de reproducción de movimiento desde CSV.  
5. **Lectura de sensores con Arduino**  
   — Código de adquisición y envío de datos vía HC‑05.

---

## 📂 Estructura del Repositorio
/
├── Informe_final.pdf # Informe final del TFG
├── Presentacion /
| ├──Presentacion.pptx # Presentación PowerPoint
| ├──Presentacion.pdf # Presentación PDF
├── Scripts_Unity/
│ ├── DataProcessor.cs # Procesamiento y traducción de movimiento
│ ├── HandDataExporter.cs # Exportación de datos a CSV
│ └── HandDataPlayer.cs # Reproducción de movimiento desde CSV
├── Arduino_code/
│ └── SendData.ino # Código Arduino (sensores + Bluetooth)
├── CSV_example/
│ └── Example_CSV.csv # Archivo CSV de ejemplo
├── Videos/ # Carpeta con vídeos de funcionamiento
│ └── Movimientos.mp4 # Tipos de movimientos que puede hacer el guante
│ └── Gracias.mp4 # Gesto "Gracias"
│ ├── De_nada.mp4 # Gesto "De nada"
│ └── Si_o_no.mp4 # Gesto "Si o no"
├── Informes_seguimiento /
├── Informe_inicial.pdf
├── Primer_informe_seguiment.pdf
└── Segon_informe_seguiment.pdf
