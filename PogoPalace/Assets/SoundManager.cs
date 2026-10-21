
using System;
using System.Collections.Generic;
using UnityEngine;

public class SoundManager : MonoBehaviour
{
    public static SoundManager Instance { get; private set; }

    [Serializable]
    public class Sound
    {
        public string name;
        public AudioClip clip;

        [Range(0f, 1f)]
        public float volume = 1f;

        [Range(0.1f, 3f)]
        public float pitch = 1f;
    }

    [Header("Sound Effects")]
    public Sound[] sounds;

    [Header("Momentum Sound")]
    public string momentumSoundName = "Momentum";

    public Rigidbody playerRigidbody;

    public float minMomentumSpeed = 3f;
    public float maxMomentumSpeed = 20f;

    [Range(0f, 1f)]
    public float maxMomentumVolume = 0.8f;

    public float minMomentumPitch = 0.8f;
    public float maxMomentumPitch = 1.3f;

    public float momentumSmoothSpeed = 5f;

    private Dictionary<string, Sound> soundLookup =
        new Dictionary<string, Sound>();

    private AudioSource sfxSource;
    private AudioSource momentumSource;

    private float currentMomentumVolume;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        // Keeps SoundManager between scene loads.
        DontDestroyOnLoad(gameObject);

        sfxSource = gameObject.AddComponent<AudioSource>();
        sfxSource.playOnAwake = false;
        sfxSource.spatialBlend = 0f;

        momentumSource = gameObject.AddComponent<AudioSource>();
        momentumSource.playOnAwake = false;
        momentumSource.loop = true;
        momentumSource.spatialBlend = 0f;
        momentumSource.volume = 0f;

        foreach (Sound sound in sounds)
        {
            if (sound == null ||
                string.IsNullOrWhiteSpace(sound.name))
                continue;

            if (soundLookup.ContainsKey(sound.name))
            {
                Debug.LogWarning(
                    $"Duplicate sound name: {sound.name}"
                );
                continue;
            }

            soundLookup.Add(sound.name, sound);
        }
    }

    private void Start()
    {
        SetupMomentumSound();
    }

    private void Update()
    {
        UpdateMomentumSound();
    }

    public void Play(string soundName)
    {
        if (!soundLookup.TryGetValue(
            soundName,
            out Sound sound))
        {
            Debug.LogWarning(
                $"Sound not found: {soundName}"
            );
            return;
        }

        if (sound.clip == null)
            return;

        sfxSource.pitch = sound.pitch;

        sfxSource.PlayOneShot(
            sound.clip,
            sound.volume
        );
    }

    private void SetupMomentumSound()
    {
        if (!soundLookup.TryGetValue(
            momentumSoundName,
            out Sound sound))
        {
            Debug.LogWarning(
                "Momentum sound not found."
            );
            return;
        }

        if (sound.clip == null)
            return;

        momentumSource.clip = sound.clip;
        momentumSource.loop = true;
        momentumSource.volume = 0f;

        momentumSource.Play();
    }

    private void UpdateMomentumSound()
    {
        if (playerRigidbody == null ||
            momentumSource.clip == null)
            return;

        float speed =
            playerRigidbody.linearVelocity.magnitude;

        float speedPercent = Mathf.InverseLerp(
            minMomentumSpeed,
            maxMomentumSpeed,
            speed
        );

        float targetVolume =
            speedPercent * maxMomentumVolume;

        currentMomentumVolume = Mathf.Lerp(
            currentMomentumVolume,
            targetVolume,
            1f - Mathf.Exp(
                -momentumSmoothSpeed * Time.deltaTime
            )
        );

        momentumSource.volume = currentMomentumVolume;

        momentumSource.pitch = Mathf.Lerp(
            minMomentumPitch,
            maxMomentumPitch,
            speedPercent
        );
    }
}
