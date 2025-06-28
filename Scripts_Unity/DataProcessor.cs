using System;
using UnityEngine;
using System.IO.Ports;
using System.Collections.Generic;
using System.Threading;
using System.Collections.Concurrent;
using System.Linq;

public class BluetoothHand : MonoBehaviour
{
    // === Configuración del puerto serie ===
    [Header("Serial Port Settings")]
    public string portName = "COM20";
    public int baudRate = 115200;
    private SerialPort serialPort;

    // === Referencias a huesos de la mano en el modelo 3D ===
    [Header("Hand References")]
    public Transform hand;
    public Transform[] thumbBones = new Transform[3];
    public Transform[] indexBones = new Transform[3];
    public Transform[] middleBones = new Transform[3];
    public Transform[] ringBones = new Transform[3];
    public Transform[] pinkyBones = new Transform[3];

    // === Configuración de sensores flex (dedos) ===
    [Header("Flex Sensor Settings")]
    public float flexMin = 0f;
    public float flexMax = 1023f;
    public float maxFlexAngle = 90f; // Ángulo máximo para la flexión

    // === Configuración del giroscopio ===
    [Header("Rotation - IMU Settings")]
    [Tooltip("Escala de rotación (grados/segundo)")]
    public float rotationScale = 1f;
    [Range(0.01f, 0.3f), Tooltip("Filtro pasa-bajos (mayor = más suave)")]
    public float filterFactor = 0.1f;

    // === Configuración de ejes y signos para la rotación ===
    [Header("Axis Configuration")]
    public IMUAxis pitchAxis = IMUAxis.Y;
    public bool invertPitch = true;
    public IMUAxis yawAxis = IMUAxis.X;
    public bool invertYaw = false;
    public IMUAxis rollAxis = IMUAxis.Z;
    public bool invertRoll = true;
    public enum IMUAxis { X, Y, Z }

    // === Buffers para estadísticas y medidas ===
    private List<float> readIntervals = new List<float>();
    private float lastMessageTime;

    // === Thread para lectura del puerto serie ===
    private Thread serialThread;
    private volatile bool keepReading = true;
    private ConcurrentQueue<string> packetQueue = new ConcurrentQueue<string>();

    // === Variables para movimiento de la mano ===
    private Quaternion baseRotation;
    private Vector3 gyroRaw, gyroFiltered;
    private float[] flexRaw = new float[5];
    private float[] flexFiltered = new float[5];
    private Quaternion[] baseThumbRot, baseIndexRot, baseMiddleRot, baseRingRot, basePinkyRot;

    // Estad�sticas de paquetes
    private List<float> packetTimes = new List<float>();
    private float packetsPerSecondSmooth, smoothFactor = 0.1f;

    // Para estad�sticas tTOTAL
    private List<float> totalTimes = new List<float>();

    // === Cálculo de jitter a partir del timestamp T ===
    struct Sample { public float time; public float dtMs; }
    private List<Sample> intervalSamples = new List<Sample>();
    private float lastTmicro = -1f;

    // === GUI para mostrar datos ===
    private Texture2D bgTexture;
    private float lastGuiUpdate;
    private float guiInterval = 0.25f;
    private DisplayData displayData = new DisplayData();
    private object displayLock = new object();

    private class DisplayData
    {
        public string stats;
        public string[] rawGyro = new string[3];
        public string[] rawFlex = new string[5];
        public string[] filtGyro = new string[3];
        public string[] filtFlex = new string[5];
    }

    void Start()
    {
        // Guardamos la rotación inicial de la mano
        baseRotation = hand.rotation;
        StoreBaseBoneRotations();
        InitializeSerial();

        // Inicialización de tiempos y buffers
        lastMessageTime = Time.realtimeSinceStartup;
        bgTexture = CreateSolidTexture(new Color(0.1f, 0.1f, 0.1f, 0.8f));
        for (int i = 0; i < flexFiltered.Length; i++)
            flexFiltered[i] = 0f;
    }

    Texture2D CreateSolidTexture(Color c)
    {
        var tex = new Texture2D(1, 1);
        tex.SetPixel(0, 0, c);
        tex.Apply();
        return tex;
    }

    // Guarda las rotaciones iniciales de cada hueso de los dedos
    void StoreBaseBoneRotations()
    {
        baseThumbRot = StoreRotations(thumbBones);
        baseIndexRot = StoreRotations(indexBones);
        baseMiddleRot = StoreRotations(middleBones);
        baseRingRot = StoreRotations(ringBones);
        basePinkyRot = StoreRotations(pinkyBones);
    }

    Quaternion[] StoreRotations(Transform[] bones)
    {
        var rots = new Quaternion[bones.Length];
        for (int i = 0; i < bones.Length; i++)
            rots[i] = bones[i].localRotation;
        return rots;
    }

    // Inicia la conexión con el puerto serie y lanza el hilo de lectura
    void InitializeSerial()
    {
        try
        {
            serialPort = new SerialPort(portName, baudRate);
            serialPort.Open();
            Debug.Log($"Connected to {portName}");
            serialThread = new Thread(ReadSerialLoop) { IsBackground = true };
            serialThread.Start();
        }
        catch (Exception e)
        {
            Debug.LogError($"Connection error: {e.Message}");
        }
    }

    // Thread que lee continuamente del puerto serie y guarda los paquetes en una cola
    void ReadSerialLoop()
    {
        string buffer = "";
        while (keepReading)
        {
            try
            {
                if (serialPort != null && serialPort.IsOpen && serialPort.BytesToRead > 0)
                {
                    char c = (char)serialPort.ReadChar();
                    buffer += c;

                    // El carácter '#' indica fin de paquete
                    if (c == '#')
                    {
                        packetQueue.Enqueue(buffer.TrimEnd('#'));
                        buffer = "";
                    }
                }
                else Thread.Sleep(5);
            }
            catch (Exception e)
            {
                Debug.LogError($"Serial thread error: {e.Message}");
            }
        }
    }


    // Procesa los datos de los sensores y actualiza el modelo en tiempo real
    void Update()
    {
        HandleCalibration();

        while (packetQueue.TryDequeue(out string packet))
        {
            float now = Time.realtimeSinceStartup;

            // Lectura intervalos
            float delta = now - lastMessageTime;
            readIntervals.Add(delta);
            lastMessageTime = now;

            // Extrae los valores de sensores del paquete
            ParseSensorData(packet,
                out float gx, out float gy, out float gz,
                out float[] flex, out float T);

            totalTimes.Add(T);

            // jitter
            if (lastTmicro >= 0f)
            {
                float dtMs = (T - lastTmicro) * 1e-3f;
                intervalSamples.Add(new Sample { time = Time.time, dtMs = dtMs });
            }
            lastTmicro = T;

            // paquetes/s
            packetTimes.Add(Time.time);

            // Guardar y filtrar datos del giroscopio
            if (gx != 0 && gy != 0 && gz != 0)
            {
                gyroRaw = new Vector3(gx, gy, gz);
            }

            gyroFiltered = Vector3.Lerp(gyroFiltered, gyroRaw, filterFactor);

            // Guardar y filtrar datos de flexión de dedos
            flexRaw = flex;
            for (int i = 0; i < 5; i++)
                flexFiltered[i] = Mathf.Lerp(flexFiltered[i], flexRaw[i], filterFactor);

            // aplicar rotación mano
            var input = new Vector3(
                MapAxis(pitchAxis, gyroFiltered, invertPitch),
                MapAxis(yawAxis, gyroFiltered, invertYaw),
                MapAxis(rollAxis, gyroFiltered, invertRoll)
            );
            hand.rotation *= Quaternion.Euler(input * (rotationScale * Time.deltaTime));

            // Aplicar rotación a los dedos
            ApplyFingerMovement();
        }

        UpdatePacketRate();
        UpdateGUIData();
    }

ç    // Actualiza la media de paquetes por segundo
    void UpdatePacketRate()
    {
        float cutoff = Time.time - 1f;
        packetTimes.RemoveAll(t => t < cutoff);
        float pps = packetTimes.Count;
        packetsPerSecondSmooth = Mathf.Lerp(packetsPerSecondSmooth, pps, smoothFactor * Time.deltaTime * 60f);
    }

    // Calcula jitter y actualiza datos a mostrar en pantalla
    void UpdateGUIData()
    {
        if (Time.time - lastGuiUpdate < guiInterval) return;
        lastGuiUpdate = Time.time;

        float cutoff = Time.time - 1f;
        var recent = intervalSamples
            .Where(s => s.time >= cutoff)
            .Select(s => s.dtMs)
            .ToList();

        float mean = recent.Count > 0 ? recent.Average() : 0f;
        float jitterMs = recent.Count > 0
            ? recent.Select(dt => Mathf.Abs(dt - mean)).Average()
            : 0f;

        var d = new DisplayData();
        d.stats = $"Packets/s: {packetsPerSecondSmooth:F1}    Avg Jitter: {jitterMs:F1} ms";

        // Raw Gyro
        d.rawGyro[0] = $"Raw Gyro X: {gyroRaw.x:F3}";
        d.rawGyro[1] = $"Raw Gyro Y: {gyroRaw.y:F3}";
        d.rawGyro[2] = $"Raw Gyro Z: {gyroRaw.z:F3}";

        // Raw Flex (thumb?pinky)
        d.rawFlex[4] = $"Raw Thumb:  {flexRaw[4]:F0}";
        d.rawFlex[3] = $"Raw Index:  {flexRaw[3]:F0}";
        d.rawFlex[2] = $"Raw Middle: {flexRaw[2]:F0}";
        d.rawFlex[1] = $"Raw Ring:   {flexRaw[1]:F0}";
        d.rawFlex[0] = $"Raw Pinky:  {flexRaw[0]:F0}";

        // Convertir rotación a ángulos en grados para mostrar
        Vector3 rawEuler = hand.rotation.eulerAngles;
        float sx = rawEuler.x > 180f ? rawEuler.x - 360f : rawEuler.x;
        float sy = rawEuler.y > 180f ? rawEuler.y - 360f : rawEuler.y;
        float sz = rawEuler.z > 180f ? rawEuler.z - 360f : rawEuler.z;
        d.filtGyro[0] = $"Filtered X: {sx:F1}�";
        d.filtGyro[1] = $"Filtered Y: {sy:F1}�";
        d.filtGyro[2] = $"Filtered Z: {sz:F1}�";

        // Convertir valores filtrados de flexión a grados
        {
            float adj = Mathf.Clamp(flexFiltered[4], flexMin, flexMax);
            float angle = Mathf.Lerp(0, maxFlexAngle, Mathf.InverseLerp(flexMin, flexMax, adj));
            d.filtFlex[4] = $"Filtered Thumb:  {angle:F1}�";
        }
        string[] names = { "Pinky", "Ring", "Middle", "Index" };
        for (int i = 3; i >= 0; i--)
        {
            float adj = Mathf.Clamp(flexFiltered[i], flexMin, flexMax);
            float angle = Mathf.Lerp(0, maxFlexAngle, Mathf.InverseLerp(flexMin, flexMax, adj));
            d.filtFlex[i] = $"Filtered {names[i]}: {angle:F1}�";
        }

        lock (displayLock)
            displayData = d;
    }

    // Dibuja los datos en pantalla
    void OnGUI()
    {
        DisplayData d;
        lock (displayLock) d = displayData;

        GUIStyle style = new GUIStyle(GUI.skin.box)
        {
            fontSize = 14,
            fontStyle = FontStyle.Bold,
            normal = { textColor = Color.yellow, background = bgTexture },
            alignment = TextAnchor.UpperLeft,
            padding = new RectOffset(10, 10, 5, 5),
            wordWrap = false
        };

        var lines = new List<string> { d.stats, "" };

        // Gyro
        lines.AddRange(d.rawGyro);
        lines.Add("");


        // Flex raw
        lines.Add(d.rawFlex[4]);
        lines.Add(d.rawFlex[3]);
        lines.Add(d.rawFlex[2]);
        lines.Add(d.rawFlex[1]);
        lines.Add(d.rawFlex[0]);
        lines.Add("");


        // Hand rotation
        lines.AddRange(d.filtGyro);
        lines.Add("");


        // Flex filtered
        lines.Add(d.filtFlex[4]);
        lines.Add(d.filtFlex[3]);
        lines.Add(d.filtFlex[2]);
        lines.Add(d.filtFlex[1]);
        lines.Add(d.filtFlex[0]);

        string content = string.Join("\n", lines);
        float height = lines.Count * 20f + 10;
        Rect boxRect = new Rect(10, 10, 310, height);
        GUI.Box(boxRect, content, style);
    }

    // Cierra el puerto y finaliza el hilo al salir
    void OnApplicationQuit()
    {
        keepReading = false;
        if (serialThread?.IsAlive == true) serialThread.Join();
        if (serialPort?.IsOpen == true) serialPort.Close();

        Debug.Log("---- Intervalos de lectura (s) ----");
        for (int i = 0; i < readIntervals.Count; i++)
            Debug.Log($"[{i}] {readIntervals[i]:F4}");
        int skip = 6;
        int cnt = Math.Max(0, readIntervals.Count - skip);
        float sum = readIntervals.Skip(skip).Sum();
        float avgInt = cnt > 0 ? sum / cnt : 0f;
        Debug.Log($"Media intervalos (desde #{skip + 1}): {avgInt:F4}s ({avgInt * 1000f:F2}ms)");

        if (totalTimes.Count > 1)
        {
            var times = totalTimes.Skip(1).ToArray();
            float minT = times.Min(), maxT = times.Max(), avgT = times.Average();
            Debug.Log("---- Estad�sticas de tTOTAL (�s) ----");
            Debug.Log($"M�nimo: {minT:F0} �s ({minT / 1000f:F3} ms)");
            Debug.Log($"M�ximo: {maxT:F0} �s ({maxT / 1000f:F3} ms)");
            Debug.Log($"Media:  {avgT:F1} �s ({avgT / 1000f:F3} ms)");
        }
        else Debug.Log("No se recibieron valores de tTOTAL.");
    }

    // Devuelve el valor del eje especificado, aplicando inversión si procede
    float MapAxis(IMUAxis axis, Vector3 data, bool inv)
    {
        float v = axis == IMUAxis.X ? data.x
                : axis == IMUAxis.Y ? data.y
                                   : data.z;
        return inv ? -v : v;
    }

    // Resetea la rotación de la mano si se pulsa la tecla C
    void HandleCalibration()
    {
        if (Input.GetKeyDown(KeyCode.C))
        {
            hand.rotation = baseRotation;
            Debug.Log("Rotation reset");
        }
    }

    // Extrae datos de un paquete recibido
    void ParseSensorData(string data,
                         out float gx, out float gy, out float gz,
                         out float[] flex, out float tMicro)
    {
        gx = gy = gz = 0f;
        flex = new float[5];
        tMicro = 0f;
        foreach (var e in data.Split(','))
        {
            if (e.Length < 2) continue;
            char tag = e[0];
            if (!float.TryParse(e.Substring(1), out float v)) continue;
            switch (tag)
            {
               case 'A': gx = v; break;         // A: eje X giroscopio
                case 'B': gy = v; break;         // B: eje Y giroscopio
                case 'C': gz = v; break;         // C: eje Z giroscopio
                case 'D': flex[0] = v; break;    // D: flexión dedo pequeño
                case 'E': flex[1] = v; break;    // E: flexión anular
                case 'F': flex[2] = v; break;    // F: flexión medio
                case 'G': flex[3] = v; break;    // G: flexión índice
                case 'H': flex[4] = v; break;    // H: flexión pulgar
                case 'T': tMicro = v; break;     // T: timestamp en microsegundos
            }
        }
    }

    // Aplica la rotación a todos los dedos
    void ApplyFingerMovement()
    {
        // Aplicamos rotación a cada hueso de cada dedo
        ApplyFinger(thumbBones, baseThumbRot, flexRaw[4]);
        ApplyFinger(indexBones, baseIndexRot, flexRaw[3]);
        ApplyFinger(middleBones, baseMiddleRot, flexRaw[2]);
        ApplyFinger(ringBones, baseRingRot, flexRaw[1]);
        ApplyFinger(pinkyBones, basePinkyRot, flexRaw[0]);
    }

    // Aplica una rotación interpolada a cada hueso de un dedo
    void ApplyFinger(Transform[] bones, Quaternion[] baseRot, float value)
    {
        // Calculamos ángulo de flexión según valor del sensor
        float adj = Mathf.Clamp(value, flexMin, flexMax);
        float angle = Mathf.Lerp(0, maxFlexAngle, Mathf.InverseLerp(flexMin, flexMax, adj));
        for (int i = 0; i < bones.Length; i++)
        {
            if (bones[i] == null) continue;
            var current = bones[i].localRotation;
            var target = baseRot[i] * Quaternion.Euler(angle, 0, 0);
            float flexF = 10f, relaxF = 20f;
            float factor = (Quaternion.Angle(current, target) > 0f && angle < Quaternion.Angle(baseRot[i], current))
                         ? relaxF : flexF;
            bones[i].localRotation = Quaternion.Slerp(current, target, Time.deltaTime * factor);
        }
    }
}
