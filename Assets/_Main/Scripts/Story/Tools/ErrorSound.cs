using UnityEngine;

/// <summary>
/// El sonido de "te equivocaste", el mismo en las cuatro salas. Corto y a medio volumen: avisa
/// sin regañar. Suena siempre, aunque el narrador esté hablando y no pueda decir su frase de
/// error.
///
/// No hay que cablear nada en la escena: el clip se carga de Resources/Sonidos y la fuente de
/// audio se crea sola la primera vez. Lo llaman Narrator.PlayErrorLine (donde acaban los fallos
/// de todas las salas) y, en la sala 1, el choque del robot y el uso donde no hay botón, para
/// que el aviso llegue en el paso que falló y no al terminar el programa.
/// </summary>
public static class ErrorSound
{
    private const string ClipPath = "Sonidos/Watery Interface Wrong Button 1";
    private const float Volume = 0.5f;

    // Dos fallos seguidos (un "Avanzar 2" contra un muro, el choque y el fin del intento)
    // suenan una sola vez.
    private const float MinInterval = 0.6f;

    private static AudioSource source;
    private static AudioClip clip;
    private static bool searched;
    private static float lastPlayed = float.NegativeInfinity;

    public static void Play()
    {
        if (Time.unscaledTime - lastPlayed < MinInterval) return;

        if (!searched)
        {
            searched = true;
            clip = Resources.Load<AudioClip>(ClipPath);
            if (clip == null)
                Debug.LogWarning($"[ErrorSound] Falta Resources/{ClipPath}: los fallos no sonarán.");
        }

        if (clip == null) return;

        // La fuente muere con cada escena: se vuelve a crear al cambiar de Setup a Juego.
        if (source == null)
        {
            GameObject holder = new GameObject("ErrorSound");
            source = holder.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
        }

        lastPlayed = Time.unscaledTime;
        source.PlayOneShot(clip, Volume);
    }
}
