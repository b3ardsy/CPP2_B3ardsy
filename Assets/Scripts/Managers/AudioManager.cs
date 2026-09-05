using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public enum SoundId
{
    Wand, Shield, Entangle, Lightning, IceTornado,
    PlayerHurt, PlayerDeath, PlayerRespawn,
    ShrineActivation, StaffUnlock, EntangleUnlock, LightningUnlock,
    IceTornadoUnlock, HeartPickup, Healing, UIHover, UIClick,
    MageIdle, RogueIdle, TankIdle, MageAttack,
    RogueSkullAttack, RogueDeathEvilAttack, TankAttack1, TankAttack2,
    ShieldHit, Walking, Running
}

public enum FootstepSurface
{
    Grass,
    Dirt,
    Water,
    Stone
}

[DisallowMultipleComponent]
public class AudioManager : MonoBehaviour
{
    [Serializable]
    public class SoundEntry
    {
        public SoundId id;
        public AudioClip clip;
        [Range(0f, 1f)] public float volume = 1f;
        [Range(0f, 1f)] public float spatialBlend = 1f;
        [Min(0.1f)] public float minDistance = 5f;
        [Min(0.1f)] public float maxDistance = 35f;
    }

    [Serializable]
    public class FootstepSet
    {
        public FootstepSurface surface = FootstepSurface.Grass;
        public AudioClip[] walkClips = Array.Empty<AudioClip>();
        public AudioClip[] runClips = Array.Empty<AudioClip>();

        [Range(0f, 1f)] public float volume = 1f;
        [Range(0f, 1f)] public float spatialBlend = 1f;

        [Range(0.5f, 1.5f)] public float minPitch = 0.95f;
        [Range(0.5f, 1.5f)] public float maxPitch = 1.05f;

        [Min(0.1f)] public float minDistance = 2f;
        [Min(0.1f)] public float maxDistance = 20f;

        [NonSerialized] public int lastWalkIndex = -1;
        [NonSerialized] public int lastRunIndex = -1;
    }

    public static AudioManager Instance { get; private set; }

    [Header("Sounds")]
    [SerializeField] private SoundEntry[] sounds = Array.Empty<SoundEntry>();
    [Range(0f, 1f)][SerializeField] private float sfxVolume = 1f;
    [Range(4, 64)][SerializeField] private int worldVoiceCount = 24;

    [Header("Footsteps")]
    [SerializeField] private FootstepSet[] footstepSets = Array.Empty<FootstepSet>();

    [Header("Ambience")]
    [SerializeField] private AudioClip rainAmbience;
    [SerializeField] private AudioClip windAmbience;
    [SerializeField] private AudioClip treesAmbience;
    [Range(0f, 1f)][SerializeField] private float ambienceVolume = 0.5f;
    [Range(0f, 1f)][SerializeField] private float rainVolume = 1f;
    [Range(0f, 1f)][SerializeField] private float windVolume = 1f;
    [Range(0f, 1f)][SerializeField] private float treesVolume = 1f;
    [SerializeField] private bool playAmbienceOnStart = true;

    [Header("Music")]
    [SerializeField] private AudioClip explorationMusic;
    [SerializeField] private AudioClip combatMusic;
    [Range(0f, 1f)][SerializeField] private float musicVolume = 0.4f;
    [Min(0f)][SerializeField] private float crossfadeSeconds = 2f;
    [SerializeField] private bool playExplorationOnStart = true;

    private AudioSource[] worldVoices;
    private AudioSource uiSource;
    private AudioSource rainSource;
    private AudioSource windSource;
    private AudioSource treesSource;
    private AudioSource explorationSource;
    private AudioSource combatSource;
    private bool inCombat;
    private bool musicEnabled;
    private float explorationGain;
    private float combatGain;

    // Unity calls Reset when this component is first added.
    private void Reset()
    {
        SyncSoundEntries();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        SyncSoundEntries();
    }
#endif

    private void SyncSoundEntries()
    {
        SoundId[] ids = (SoundId[])Enum.GetValues(typeof(SoundId));
        Dictionary<SoundId, SoundEntry> existing = new Dictionary<SoundId, SoundEntry>();

        if (sounds != null)
        {
            foreach (SoundEntry entry in sounds)
            {
                if (entry != null && !existing.ContainsKey(entry.id))
                    existing.Add(entry.id, entry);
            }
        }

        SoundEntry[] synced = new SoundEntry[ids.Length];
        for (int i = 0; i < ids.Length; i++)
        {
            if (existing.TryGetValue(ids[i], out SoundEntry entry))
            {
                synced[i] = entry;
            }
            else
            {
                synced[i] = new SoundEntry
                {
                    id = ids[i],
                    spatialBlend = IsUISound(ids[i]) ? 0f : 1f
                };
            }
        }

        sounds = synced;
    }

    private static bool IsUISound(SoundId id)
    {
        return id == SoundId.UIHover || id == SoundId.UIClick;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        transform.SetParent(null);
        DontDestroyOnLoad(gameObject);

        worldVoices = new AudioSource[Mathf.Clamp(worldVoiceCount, 4, 64)];
        for (int i = 0; i < worldVoices.Length; i++)
            worldVoices[i] = CreateSource("World Sound " + (i + 1));

        uiSource = CreateSource("UI Sounds");
        uiSource.ignoreListenerPause = true;

        rainSource = CreateLoopSource("Rain Ambience", rainAmbience);
        windSource = CreateLoopSource("Wind Ambience", windAmbience);
        treesSource = CreateLoopSource("Trees Ambience", treesAmbience);

        explorationSource = CreateSource("Exploration Music");
        combatSource = CreateSource("Combat Music");
        explorationSource.loop = combatSource.loop = true;
        explorationSource.volume = combatSource.volume = 0f;
        explorationSource.clip = explorationMusic;
        combatSource.clip = combatMusic;

        ApplyAmbienceVolumes();
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private void Start()
    {
        if (Instance != this) return;

        if (playAmbienceOnStart)
            SetAmbienceActive(true);

        if (playExplorationOnStart)
            SetCombatMusic(false);
    }

    private AudioSource CreateSource(string sourceName)
    {
        GameObject child = new GameObject(sourceName);
        child.transform.SetParent(transform, false);
        AudioSource source = child.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = 0f;
        source.dopplerLevel = 0f;
        return source;
    }

    private AudioSource CreateLoopSource(string sourceName, AudioClip clip)
    {
        AudioSource source = CreateSource(sourceName);
        source.loop = true;
        source.clip = clip;
        return source;
    }

    public void Play(SoundId id, Vector3 position)
    {
        if (Instance != this) return;
        SoundEntry entry = FindSound(id);
        if (entry == null || entry.clip == null) return;

        if (IsUISound(id))
        {
            uiSource.PlayOneShot(entry.clip, entry.volume * sfxVolume);
            return;
        }

        // Paused voices must not be mistaken for free voices.
        if (AudioListener.pause) return;
        foreach (AudioSource source in worldVoices)
        {
            if (source.isPlaying) continue;
            source.transform.position = position;
            source.clip = entry.clip;
            source.volume = entry.volume * sfxVolume;
            source.pitch = 1f;
            source.spatialBlend = entry.spatialBlend;
            source.minDistance = Mathf.Max(0.1f, entry.minDistance);
            source.maxDistance = Mathf.Max(source.minDistance, entry.maxDistance);
            source.rolloffMode = AudioRolloffMode.Linear;
            source.Play();
            return;
        }
        // At capacity, skip this sound instead of cutting off an existing one.
    }

    public void PlayFootstep(
        FootstepSurface surface,
        bool running,
        Vector3 position
    )
    {
        if (
            Instance != this ||
            AudioListener.pause
        )
        {
            return;
        }

        FootstepSet set =
            FindFootstepSet(
                surface
            );

        if (set == null)
        {
            return;
        }

        AudioClip[] clips =
            running
                ? set.runClips
                : set.walkClips;

        if (
            clips == null ||
            clips.Length == 0
        )
        {
            return;
        }

        int clipIndex =
            ChooseFootstepIndex(
                clips,
                running
                    ? set.lastRunIndex
                    : set.lastWalkIndex
            );

        if (clipIndex < 0)
        {
            return;
        }

        AudioClip clip =
            clips[clipIndex];

        if (clip == null)
        {
            return;
        }

        if (running)
        {
            set.lastRunIndex =
                clipIndex;
        }
        else
        {
            set.lastWalkIndex =
                clipIndex;
        }

        foreach (
            AudioSource source
            in worldVoices
        )
        {
            if (source.isPlaying)
            {
                continue;
            }

            source.transform.position =
                position;

            source.clip =
                clip;

            source.volume =
                set.volume *
                sfxVolume;

            float lowPitch =
                Mathf.Min(
                    set.minPitch,
                    set.maxPitch
                );

            float highPitch =
                Mathf.Max(
                    set.minPitch,
                    set.maxPitch
                );

            source.pitch =
                UnityEngine.Random.Range(
                    lowPitch,
                    highPitch
                );

            source.spatialBlend =
                set.spatialBlend;

            source.minDistance =
                Mathf.Max(
                    0.1f,
                    set.minDistance
                );

            source.maxDistance =
                Mathf.Max(
                    source.minDistance,
                    set.maxDistance
                );

            source.rolloffMode =
                AudioRolloffMode.Linear;

            source.Play();

            return;
        }
    }

    private FootstepSet FindFootstepSet(
        FootstepSurface surface
    )
    {
        if (footstepSets == null)
        {
            return null;
        }

        foreach (
            FootstepSet set
            in footstepSets
        )
        {
            if (
                set != null &&
                set.surface == surface
            )
            {
                return set;
            }
        }

        return null;
    }

    private static int ChooseFootstepIndex(
        AudioClip[] clips,
        int previousIndex
    )
    {
        if (
            clips == null ||
            clips.Length == 0
        )
        {
            return -1;
        }

        if (clips.Length == 1)
        {
            return
                clips[0] != null
                    ? 0
                    : -1;
        }

        int validClipCount = 0;

        for (
            int i = 0;
            i < clips.Length;
            i++
        )
        {
            if (clips[i] != null)
            {
                validClipCount++;
            }
        }

        if (validClipCount == 0)
        {
            return -1;
        }

        if (validClipCount == 1)
        {
            for (
                int i = 0;
                i < clips.Length;
                i++
            )
            {
                if (clips[i] != null)
                {
                    return i;
                }
            }
        }

        int selectedIndex;

        do
        {
            selectedIndex =
                UnityEngine.Random.Range(
                    0,
                    clips.Length
                );
        }
        while (
            clips[selectedIndex] == null ||
            selectedIndex ==
            previousIndex
        );

        return selectedIndex;
    }

    private SoundEntry FindSound(SoundId id)
    {
        foreach (SoundEntry entry in sounds)
            if (entry != null && entry.id == id) return entry;
        return null;
    }

    public void PlayUIHover() => Play(SoundId.UIHover, Vector3.zero);
    public void PlayUIClick() => Play(SoundId.UIClick, Vector3.zero);

    public void SetAmbienceActive(bool active)
    {
        if (Instance != this) return;

        SetLoopSourceActive(rainSource, active);
        SetLoopSourceActive(windSource, active);
        SetLoopSourceActive(treesSource, active);
    }

    public void SetRainActive(bool active)
    {
        if (Instance != this) return;
        SetLoopSourceActive(rainSource, active);
    }

    public void SetWindActive(bool active)
    {
        if (Instance != this) return;
        SetLoopSourceActive(windSource, active);
    }

    public void SetTreesActive(bool active)
    {
        if (Instance != this) return;
        SetLoopSourceActive(treesSource, active);
    }

    private static void SetLoopSourceActive(AudioSource source, bool active)
    {
        if (source == null || source.clip == null) return;

        if (active)
        {
            if (!source.isPlaying)
                source.Play();
        }
        else
        {
            source.Stop();
        }
    }

    private void ApplyAmbienceVolumes()
    {
        if (rainSource != null)
            rainSource.volume = ambienceVolume * rainVolume;
        if (windSource != null)
            windSource.volume = ambienceVolume * windVolume;
        if (treesSource != null)
            treesSource.volume = ambienceVolume * treesVolume;
    }

    public void SetCombatMusic(bool active)
    {
        if (Instance != this) return;
        AudioSource target = active ? combatSource : explorationSource;
        // Keep the current music if the requested track is not assigned yet.
        if (target.clip == null) return;
        inCombat = active;
        musicEnabled = true;
        if (!target.isPlaying) target.Play();
    }

    private void Update()
    {
        if (Instance != this) return;

        ApplyAmbienceVolumes();

        if (!musicEnabled) return;
        float step = crossfadeSeconds <= 0f
            ? 1f : Time.unscaledDeltaTime / crossfadeSeconds;
        explorationGain = Mathf.MoveTowards(explorationGain, inCombat ? 0f : 1f, step);
        combatGain = Mathf.MoveTowards(combatGain, inCombat ? 1f : 0f, step);
        explorationSource.volume = explorationGain * musicVolume;
        combatSource.volume = combatGain * musicVolume;
        if (inCombat && explorationGain == 0f) explorationSource.Stop();
        if (!inCombat && combatGain == 0f) combatSource.Stop();
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode != LoadSceneMode.Single) return;
        foreach (AudioSource source in worldVoices) source.Stop();
        SetCombatMusic(false);
    }

    [ContextMenu("Test/UI Click (Play Mode)")]
    private void TestClick()
    {
        if (Application.isPlaying && Instance == this) PlayUIClick();
    }

    [ContextMenu("Test/Shield Hit (Play Mode)")]
    private void TestShieldHit()
    {
        if (Application.isPlaying && Instance == this)
            Play(SoundId.ShieldHit, transform.position);
    }

    [ContextMenu("Test/Ambience On (Play Mode)")]
    private void TestAmbienceOn()
    {
        if (Application.isPlaying && Instance == this) SetAmbienceActive(true);
    }

    [ContextMenu("Test/Ambience Off (Play Mode)")]
    private void TestAmbienceOff()
    {
        if (Application.isPlaying && Instance == this) SetAmbienceActive(false);
    }

    [ContextMenu("Test/Combat Music (Play Mode)")]
    private void TestCombat()
    {
        if (Application.isPlaying && Instance == this) SetCombatMusic(true);
    }

    [ContextMenu("Test/Exploration Music (Play Mode)")]
    private void TestExploration()
    {
        if (Application.isPlaying && Instance == this) SetCombatMusic(false);
    }

    private void OnDestroy()
    {
        if (Instance != this) return;
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        Instance = null;
    }
}
