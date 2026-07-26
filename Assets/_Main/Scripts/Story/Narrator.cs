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
        if (audioList.Count > 0)
            currentAudioList = audioList[audioListIndex];
    }

    public void PlayAudio(string name)
    {
        int foundIndex = currentAudioList.audios.FindIndex(x => x.name == name);

        if (foundIndex == -1)
        {
            Debug.LogWarning($"Audio con nombre '{name}' no encontrado");
            return;
        }

        PlayAudio(foundIndex);
    }

    public void PlayAudio(int _index)
    {
        index = _index;
        AudioClip clip = currentAudioList.audios[index];
        audioSource.clip = clip;
        audioSource.Play();
        Debug.Log("Reproduciendo audio: " + clip.name);
    }

    public void StopAudio()
    {
        audioSource.Stop();
        audioSource = null;
    }

    [ContextMenu("Execute")]
    public IEnumerator Execute()
    {
        PlayAudio(index);
        index++;

        yield return null;
        yield return new WaitWhile(() => audioSource.isPlaying);
    }

    public void SetAudioListIndex(int value)
    {
        audioListIndex = value;
        index = 0;
    }
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
