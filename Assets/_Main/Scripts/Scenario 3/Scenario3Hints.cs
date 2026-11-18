using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// Rótulos de la Sala 3 (16/11/2026): sobre cada destino del brazo, a qué lado queda y qué va
/// ahí: "IZQUIERDA · barriles rojos" y "DERECHA · cajas azules", cada uno en su color.
///
/// La frase 12 ya lo dice de voz, pero una sola vez y antes de empezar; en intermedia el niño
/// tiene que elegir entre Girar Izquierda y Girar Derecha con el dato delante. El lado sale del
/// giro del brazo hacia ese destino (ArmSlot.Yaw), que es lo mismo que hacen esos bloques.
///
/// Lo añade Scenario3Controller al arrancar (Show Hints) y lo quita al resolverse la sala. Se
/// monta en tiempo de ejecución, sin piezas en la escena.
/// </summary>
public class Scenario3Hints : MonoBehaviour
{
    // Dónde va el rótulo: en la línea que une el brazo con el destino, a esta parte del camino
    // (delante de la pila, que crece hasta varios metros) y a esta altura sobre el brazo.
    private const float TowardsDestination = 0.62f;
    private const float HeightOverArm = 1.05f;

    private const float FontSize = 5f;

    private static readonly Color Red = new Color(1f, 0.36f, 0.38f);
    private static readonly Color Blue = new Color(0.4f, 0.8f, 1f);

    private readonly List<Transform> labels = new List<Transform>();

    /// <summary>Un rótulo por destino. `arm` es el punto desde el que gira el brazo.</summary>
    public void Build(IEnumerable<KeyValuePair<ArmItemType, ArmSlot>> destinations, Transform arm)
    {
        foreach (KeyValuePair<ArmItemType, ArmSlot> pair in destinations)
        {
            ArmSlot slot = pair.Value;
            if (slot == null) continue;

            bool left = Mathf.DeltaAngle(0f, slot.Yaw) < 0f;
            bool barrel = pair.Key == ArmItemType.Barril;

            // Hacia donde gira el brazo para ese destino, no hacia el punto exacto de la pila:
            // así el rótulo queda centrado sobre la plataforma, delante de lo que se apile.
            Vector3 from = arm != null ? arm.position : transform.position;
            Quaternion zero = arm != null && arm.parent != null ? arm.parent.rotation : Quaternion.identity;
            Vector3 towards = Vector3.ProjectOnPlane(zero * Quaternion.Euler(0f, slot.Yaw, 0f) * Vector3.forward, Vector3.up).normalized;
            Vector3 offset = Vector3.ProjectOnPlane(slot.transform.position - from, Vector3.up);

            Vector3 position = from + towards * (Mathf.Abs(Vector3.Dot(offset, towards)) * TowardsDestination);
            position.y = from.y + HeightOverArm;

            GameObject label = new GameObject($"Rótulo de destino ({pair.Key})");
            label.transform.SetParent(transform, true);
            label.transform.position = position;

            TextMeshPro text = label.AddComponent<TextMeshPro>();
            text.text = $"{(left ? "IZQUIERDA" : "DERECHA")}\n<size=60%>{(barrel ? "barriles rojos" : "cajas azules")}</size>";
            text.fontSize = FontSize;
            text.fontStyle = FontStyles.Bold;
            text.alignment = TextAlignmentOptions.Center;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.color = barrel ? Red : Blue;
            text.outlineWidth = 0.2f;
            text.outlineColor = Color.black;

            labels.Add(label.transform);
        }
    }

    private void LateUpdate()
    {
        Camera view = Camera.main;
        if (view != null) FaceTowards(view.transform.position);
    }

    /// <summary>
    /// Gira los rótulos, solo en horizontal, de cara a ese punto. Cada fotograma, hacia el jugador;
    /// las capturas para informes lo llaman con su propia cámara, que no es la del jugador.
    /// </summary>
    public void FaceTowards(Vector3 viewer)
    {
        foreach (Transform label in labels)
        {
            if (label == null) continue;

            Vector3 away = label.position - viewer;
            away.y = 0f;
            if (away.sqrMagnitude > 0.001f) label.rotation = Quaternion.LookRotation(away);
        }
    }

    private void OnDestroy()
    {
        foreach (Transform label in labels)
            if (label != null) Destroy(label.gameObject);
    }
}
