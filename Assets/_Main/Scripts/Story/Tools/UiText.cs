using TMPro;
using UnityEngine.UI;

/// <summary>
/// Escribe en una etiqueta de interfaz sin que quien llama tenga que saber si es un Text de
/// uGUI o un TMP_Text de TextMeshPro.
///
/// Los dos conviven a propósito. Lo montado a mano usa Text, y los botones que vienen de los
/// samples de Meta usan TextMeshPro: obligar a unificarlos costaría rehacer todo lo ya
/// cableado, y ese trabajo no aporta nada al jugador.
///
/// Cada componente expone los dos campos y tú rellenas solo el que tengas. Si rellenas los
/// dos se escriben ambos, que es raro pero no rompe nada.
/// </summary>
public static class UiText
{
    /// <summary>Si hay al menos una etiqueta donde escribir.</summary>
    public static bool Any(Text legacy, TMP_Text tmp) => legacy != null || tmp != null;

    public static void Set(Text legacy, TMP_Text tmp, string value)
    {
        if (legacy != null) legacy.text = value;
        if (tmp != null) tmp.text = value;
    }

    /// <summary>
    /// Permite etiquetas de color en el texto. Lo necesita el Narrator para revelar letra a
    /// letra: el resto de la frase se pinta transparente en vez de no escribirse, y así el
    /// texto no baila al ir creciendo.
    /// </summary>
    public static void EnableRichText(Text legacy, TMP_Text tmp)
    {
        if (legacy != null) legacy.supportRichText = true;
        if (tmp != null) tmp.richText = true;
    }
}
