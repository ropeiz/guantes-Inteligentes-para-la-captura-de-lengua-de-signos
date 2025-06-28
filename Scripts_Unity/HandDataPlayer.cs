// HandDataPlayer.cs
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;

public class HandDataPlayer : MonoBehaviour
{
    [Header("Configuraci�n de reproducci�n")]
    public Transform handRoot;
    public string fileName = "HandData.csv";
    public bool playOnStart = true;
    [Tooltip("1 = misma velocidad que grabaci�n")]
    public float timeScale = 1f;

    private List<Transform> boneTransforms;
    private List<FrameData> frames;
    private Coroutine playbackCoroutine;
    private string filePath;

    void Start()
    {
        if (handRoot == null)
        {
            Debug.LogError("HandDataPlayer: asigna handRoot.");
            enabled = false;
            return;
        }

        // 1) Configuro la lista de huesos: solo handRoot + sub�rbol "metarig.001"
        boneTransforms = new List<Transform>();
        boneTransforms.Add(handRoot);

        // Busco "metarig.001" entre los hijos directos
        Transform metaRig = handRoot.Find("metarig.001");
        if (metaRig == null)
        {
            // Si no est� directo, lo busco recursivamente
            foreach (var t in handRoot.GetComponentsInChildren<Transform>())
            {
                if (t.name == "metarig.001")
                {
                    metaRig = t;
                    break;
                }
            }
        }

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
            Debug.LogWarning("HandDataPlayer: no se encontr� el hijo 'metarig.001' bajo handRoot.");
        }

        // Ordeno los transforms para coincidir con el CSV
        boneTransforms.Sort((a, b) => string.Compare(a.name, b.name, StringComparison.Ordinal));

        // 2) Cargo el CSV y empiezo reproducci�n
        filePath = Path.Combine(@"C:\Users\robs2\Desktop\TFG\CapturePositions", fileName);
        LoadCsv();

        if (frames == null || frames.Count == 0)
        {
            Debug.LogError("HandDataPlayer: sin frames para reproducir.");
            return;
        }

        // Aplico el primer frame
        ApplyFrame(frames[0]);

        if (playOnStart)
            playbackCoroutine = StartCoroutine(PlayLoop());
    }

    void LoadCsv()
    {
        frames = new List<FrameData>();

        if (!File.Exists(filePath))
        {
            Debug.LogError("HandDataPlayer: no existe el archivo " + filePath);
            return;
        }

        // Si el archivo tiene menos de x linias, mostrar mensaje de error.
        var lines = File.ReadAllLines(filePath);
        if (lines.Length < 2)
        {
            Debug.LogError("HandDataPlayer: el archivo no contiene datos.");
            return;
        }

        // Para cada l�nea de datos (saltando cabecera)
        for (int i = 1; i < lines.Length; i++)
        {
            // Dividir la l�nea en tokens separados por coma
            var tok = lines[i].Split(',');


            var fd = new FrameData
            {
                timestamp = float.Parse(tok[0], CultureInfo.InvariantCulture),
                boneRot = new Vector3[boneTransforms.Count]
            };

            // Para cada hueso, leer sus 3 componentes de rotaci�n (X, Y, Z)
            for (int b = 0; b < boneTransforms.Count; b++)
            {
                int idx = 1 + b * 3;
                float rx = float.Parse(tok[idx + 0], CultureInfo.InvariantCulture);
                float ry = float.Parse(tok[idx + 1], CultureInfo.InvariantCulture);
                float rz = float.Parse(tok[idx + 2], CultureInfo.InvariantCulture);

                fd.boneRot[b] = new Vector3(rx, ry, rz);
            }
            frames.Add(fd);
        }

        Debug.Log($"HandDataPlayer: cargados {frames.Count} frames.");
    }

    // Corutina que recorre todos los fotogramas y aplica la animaci�n con el timing correcto
    IEnumerator PlayLoop()
    {
        for (int i = 0; i < frames.Count; i++)
        {
            ApplyFrame(frames[i]);
            // Si no es el �ltimo fotograma, calcular y esperar el tiempo hasta el siguiente
            if (i < frames.Count - 1)
            {
                float deltaTime = frames[i + 1].timestamp - frames[i].timestamp;
                deltaTime = Mathf.Max(0f, deltaTime) / Mathf.Max(0.0001f, timeScale);
                yield return new WaitForSeconds(deltaTime);
            }
        }
        Debug.Log("HandDataPlayer: reproducci�n completada.");
    }

    // M�todo que aplica las rotaciones almacenadas en un FrameData a los transforms de los huesos
    void ApplyFrame(FrameData f)
    {
        for (int b = 0; b < boneTransforms.Count; b++)
        {
            // Convertir Vector3 de rotaci�n en Quaternion y aplicarlo como rotaci�n local
            boneTransforms[b].localRotation = Quaternion.Euler(f.boneRot[b]);
        }
    }

    //Estructura para almacenar timestamp y rotaciones
    private class FrameData
    {
        public float timestamp;
        public Vector3[] boneRot;
    }

    // M�todo p�blico para iniciar la reproducci�n desde el primer fotograma
    public void Play()
    {
        if (playbackCoroutine != null) StopCoroutine(playbackCoroutine);
        playbackCoroutine = StartCoroutine(PlayLoop());
    }
}