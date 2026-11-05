using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// Indicaciones de la Sala 1 (sesiones del 22/10/2026: dos de tres estudiantes agotaron el tiempo
/// sin saber dónde van los bloques ni para qué):
///   · el primer hueco libre de la fila se ilumina; con un bloque en la mano, todos los libres
///   · una flecha y un rótulo sobre la salida dicen que el objetivo es abrir la puerta
///
/// Lo añade Scenario1Controller al empezar el reto y lo quita al resolverlo. Se monta en tiempo
/// de ejecución, sin piezas en la escena. Las medidas están en metros y aún hay que verlas en el visor.
/// </summary>
public class Scenario1Hints : MonoBehaviour
{
    private const string GoalText = "OBJETIVO: ABRIR LA PUERTA\n<size=70%>Lleva a Roki al botón verde</size>";

    // Altura de la flecha y del rótulo sobre la casilla de salida, donde queda el botón en la pared.
    private const float ArrowHeight = 1.5f;
    private const float LabelHeight = 1.85f;
    private const float ArrowBob = 0.08f;
    private const float PulseSpeed = 4f;

    private static readonly Color HintColor = new Color(1f, 0.8f, 0.15f);

    private readonly List<Socket> sockets = new List<Socket>();
    private readonly List<GridBlock> blocks = new List<GridBlock>();

    private ProgramRunner runner;
    private GameObject marker;
    private Transform arrow;
    private Material arrowMaterial;
    private Exit exit;

    private void Start()
    {
        runner = GetComponent<ProgramRunner>();

        SocketRow row = GetComponentInChildren<SocketRow>(true);
        for (Socket s = row != null ? row.FirstSocket : null; s != null; s = s.Next)
            sockets.Add(s);

        foreach (GridBlock block in FindObjectsByType<GridBlock>(FindObjectsInactive.Exclude))
            if (block.GetComponentInParent<TutorialController>(true) == null)
                blocks.Add(block);

        if (sockets.Count == 0)
            Debug.LogWarning("[Escenario 1] Indicaciones sin fila de huecos.", this);

        BuildMarker();
    }

    private void Update()
    {
        UpdateSockets();
        UpdateMarker();
    }

    private void UpdateSockets()
    {
        bool running = runner != null && runner.IsRunning;
        bool holding = blocks.Exists(b => b != null && b.IsGrabbed);
        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * PulseSpeed);
        bool firstFree = true;

        foreach (Socket socket in sockets)
        {
            if (!socket.IsEmpty) continue;

            socket.SetHint(!running && (firstFree || holding) ? pulse : 0f, HintColor);
            firstFree = false;
        }
    }

    private void UpdateMarker()
    {
        if (marker == null) return;

        // ReloadLevel destruye la salida y crea otra en cada reintento, en la misma casilla.
        if (exit == null) exit = FindAnyObjectByType<Exit>();
        if (exit != null) marker.transform.position = exit.transform.position;

        arrow.localPosition = Vector3.up * (ArrowHeight + ArrowBob * Mathf.Sin(Time.time * PulseSpeed));

        Camera view = Camera.main;
        if (view == null) return;

        // Rótulo y flecha giran solo en horizontal, de cara al jugador.
        Vector3 away = marker.transform.position - view.transform.position;
        away.y = 0f;
        if (away.sqrMagnitude > 0.001f) marker.transform.rotation = Quaternion.LookRotation(away);
    }

    private void BuildMarker()
    {
        marker = new GameObject("Indicación del objetivo");
        marker.transform.SetParent(transform, false);

        GameObject label = new GameObject("Rótulo");
        label.transform.SetParent(marker.transform, false);
        label.transform.localPosition = Vector3.up * LabelHeight;

        TextMeshPro text = label.AddComponent<TextMeshPro>();
        text.text = GoalText;
        text.fontSize = 2f;
        text.alignment = TextAlignmentOptions.Center;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.color = HintColor;
        text.outlineWidth = 0.2f;
        text.outlineColor = Color.black;

        GameObject arrowObject = new GameObject("Flecha");
        arrowObject.transform.SetParent(marker.transform, false);
        arrow = arrowObject.transform;
        arrowObject.AddComponent<MeshFilter>().sharedMesh = BuildArrowMesh();

        // Del material del hueco: Shader.Find falla en el APK con un shader que nadie referencia.
        Renderer reference = sockets.Count > 0 ? sockets[0].GetComponentInChildren<Renderer>(true) : null;
        if (reference != null && reference.sharedMaterial != null)
        {
            arrowMaterial = new Material(reference.sharedMaterial) { color = HintColor };
            arrowObject.AddComponent<MeshRenderer>().sharedMaterial = arrowMaterial;
        }
    }

    /// <summary>Pirámide de 4 caras con la punta hacia abajo.</summary>
    private static Mesh BuildArrowMesh()
    {
        const float r = 0.12f;
        const float h = 0.24f;

        Vector3 tip = Vector3.down * h;
        Vector3 a = new Vector3(-r, 0f, -r), b = new Vector3(r, 0f, -r);
        Vector3 c = new Vector3(r, 0f, r), d = new Vector3(-r, 0f, r);

        // Vértices sin compartir, para que cada cara tenga su normal.
        Vector3[] vertices = { a, tip, b, b, tip, c, c, tip, d, d, tip, a, a, b, c, a, c, d };
        int[] triangles = new int[vertices.Length];
        for (int i = 0; i < triangles.Length; i++) triangles[i] = i;

        Mesh mesh = new Mesh { name = "Flecha", vertices = vertices, triangles = triangles };
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    private void OnDestroy()
    {
        foreach (Socket socket in sockets)
            if (socket != null) socket.SetHint(0f, HintColor);

        if (marker != null) Destroy(marker);
        if (arrowMaterial != null) Destroy(arrowMaterial);
    }
}
