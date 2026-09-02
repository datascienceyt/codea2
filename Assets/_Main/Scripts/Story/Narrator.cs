using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class Narrator : MonoBehaviour, IStepAction
{
    [SerializeField] private AudioSource audioSource;
    [SerializeField] public int audioListIndex = 0;
    [SerializeField] public List<AudioList> audioList;

    private int index = 0;

    private AudioList currentAudioList;

    private void Awake()
    {
        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();

        SelectList(audioListIndex);
    }

    /// <summary>
    /// Cambia de lista de audios y reinicia el recorrido.
    /// </summary>
    public void SetAudioListIndex(int value)
    {
        SelectList(value);
        index = 0;
    }

    /// <summary>
    /// Antes esto solo guardaba el número: currentAudioList se quedaba apuntando a la lista
    /// de Awake, así que cambiar de lista no tenía ningún efecto.
    /// </summary>
    private void SelectList(int listIndex)
    {
        if (audioList == null || audioList.Count == 0)
        {
            currentAudioList = null;
            return;
        }

        if (listIndex < 0 || listIndex >= audioList.Count)
        {
            Debug.LogWarning($"[Narrator] Lista de audios {listIndex} fuera de rango (hay {audioList.Count}).");
            return;
        }

        audioListIndex = listIndex;
        currentAudioList = audioList[listIndex];
    }

    public void PlayAudio(string name)
    {
        if (!HasClips()) return;

        int foundIndex = currentAudioList.audios.FindIndex(x => x != null && x.name == name);

        if (foundIndex == -1)
        {
            Debug.LogWarning($"Audio con nombre '{name}' no encontrado");
            return;
        }

        PlayAudio(foundIndex);
    }

    public void PlayAudio(int _index)
    {
        if (!HasClips()) return;

        if (_index < 0 || _index >= currentAudioList.audios.Count)
        {
            Debug.LogWarning($"[Narrator] Índice de audio {_index} fuera de rango.");
            return;
        }

        AudioClip clip = currentAudioList.audios[_index];

        if (clip == null)
        {
            Debug.LogWarning($"[Narrator] El audio {_index} de la lista {audioListIndex} está vacío.");
            return;
        }

        index = _index;
        audioSource.clip = clip;
        audioSource.Play();

        Debug.Log("Reproduciendo audio: " + clip.name);
    }

    /// <summary>
    /// Detiene la reproducción. NO anula el AudioSource: hacerlo dejaba el componente
    /// inservible y cualquier llamada posterior lanzaba NullReferenceException.
    /// </summary>
    public void StopAudio()
    {
        if (audioSource != null)
            audioSource.Stop();
    }

    [ContextMenu("Execute")]
    public IEnumerator Execute()
    {
        if (!HasClips())
        {
            Debug.LogWarning($"[Narrator] '{name}' no tiene audios asignados.");
            yield break;
        }

        // Se comprueba el límite ANTES de avanzar: antes se llamaba a PlayAudio(index) a
        // ciegas y la lista desbordaba con IndexOutOfRangeException al agotarse.
        if (index >= currentAudioList.audios.Count)
        {
            Debug.LogWarning($"[Narrator] No quedan audios en la lista {audioListIndex}.");
            yield break;
        }

        PlayAudio(index);
        index++;

        yield return null;
        yield return new WaitWhile(() => audioSource.isPlaying);
    }

    private bool HasClips() =>
        currentAudioList != null &&
        currentAudioList.audios != null &&
        currentAudioList.audios.Count > 0;
}

[Serializable]
public class AudioList
{
    public List<AudioClip> audios;

    public AudioList(List<AudioClip> audios)
    {
        this.audios = audios;
    }
}
