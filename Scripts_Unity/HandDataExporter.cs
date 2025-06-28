// HandDataExporter.cs
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Globalization;
using UnityEngine;

public class HandDataExporter : MonoBehaviour
{
    [Header("Configuración de grabación")]
    [Tooltip("Raíz de la mano cuyos huesos se capturan")]
    public Transform handRoot;

    [Tooltip("Frames por segundo deseados")]
    public float targetFPS = 60f;

    [Tooltip("Nombre del fichero CSV de salida")]
    public string fileName = "HandData.csv";

    private List<Transform> boneTransforms;
    private StreamWriter writer;
    private string filePath;
    private int frameIndex = 0;

    void Start()
    {
        if (handRoot == null)
        {
            Debug.LogError("HandDataExporter: asigna handRoot.");
            enabled = false;
            return;
        }

        // 1) Configuro FixedUpdate para que pase cada 1/targetFPS segundos.
        Time.fixedDeltaTime = 1f / targetFPS;

        // 2) Recojo únicamente handRoot y el subárbol "metarig.001"
        boneTransforms = new List<Transform>();

        // 2.1) Añado el propio handRoot
        boneTransforms.Add(handRoot);

        // 2.2) Intento encontrar el hijo directo "metarig.001"
        Transform metaRig = handRoot.Find("metarig.001");

        // Si no está como hijo directo, lo busco recursivamente
        if (metaRig == null)
        {
            foreach (var t in handRoot.GetComponentsInChildren<Transform>())
            {
                if (t.name == "metarig.001")
                {
                    metaRig = t;
                    break;
                }
            }
        }

        // 2.3) Si lo encontramos, lo añado y agrego todos sus descendientes
        if (metaRig != null)
        {
            boneTransforms.Add(metaRig);
            foreach (var t in metaRig.GetComponentsInChildren<Transform>())
            {
                if (t != metaRig)
                    boneTransforms.Add(t);
            }
        }
        else
        {
            Debug.LogWarning("HandDataExporter: no se encontró el hijo 'metarig.001' bajo handRoot.");
        }

        // 2.4) Ordeno por nombre para mantener consistencia en el CSV
        boneTransforms.Sort((a, b) => string.Compare(a.name, b.name, StringComparison.Ordinal));

        // 3) Preparo el fichero CSV
        filePath = Path.Combine(@"C:\Users\robs2\Desktop\TFG\CapturePositions", fileName);
        Directory.CreateDirectory(Path.GetDirectoryName(filePath));
        writer = new StreamWriter(filePath, false, Encoding.UTF8);

        // 4) Escribo cabecera con timestamp + canales de rotación para cada hueso
        var header = new StringBuilder();
        header.Append("timestamp,");
        foreach (var t in boneTransforms)
            header.AppendFormat("{0}_rx,{0}_ry,{0}_rz,", t.name);
        header.Length--; // quito la última coma
        writer.WriteLine(header);
        writer.Flush();

        // 5) Inicio la corrutina de captura
        StartCoroutine(CaptureLoop());

        Debug.Log($"HandDataExporter iniciado en: {filePath} (capturando a {targetFPS} FPS).");
    }

    // Corrutina que se dispara justo después de cada FixedUpdate
    IEnumerator CaptureLoop()
    {
        while (true)
        {
            // Time.fixedTime avanza siempre en pasos de fixedDeltaTime
            CaptureFrame(Time.fixedTime);
            // Espera al siguiente FixedUpdate
            yield return new WaitForFixedUpdate();
        }
    }

    // Genera y escribe una línea CSV para el timestamp dado
    void CaptureFrame(float timestamp)
    {
        var line = new StringBuilder();
        line.AppendFormat("{0},", timestamp.ToString("F4", CultureInfo.InvariantCulture));

        foreach (var t in boneTransforms)
        {
            Vector3 p = t.localPosition;
            Vector3 r = t.localRotation.eulerAngles;
            line.AppendFormat(
                "{0},{1},{2},",
                r.x.ToString("F4", CultureInfo.InvariantCulture),
                r.y.ToString("F4", CultureInfo.InvariantCulture),
                r.z.ToString("F4", CultureInfo.InvariantCulture)
            );
        }

        // Quito la coma final y vuelco al fichero
        line.Length--;
        writer.WriteLine(line);
        writer.Flush();
        frameIndex++;
    }

    void OnApplicationQuit()
    {
        writer?.Close();
    }
}
