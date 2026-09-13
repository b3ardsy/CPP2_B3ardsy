using System;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;

public enum SoundId
{
    // PLAYER
    PlayerHurt,
    PlayerDeath,
    PlayerRespawn,
    PlayerJump,
    PlayerLand,
    PlayerInteractHmm,

    // PLAYER COMBAT
    Wand,
    Shield,
    ShieldHit,

    // ABILITIES / RUNES
    Entangle,
    Lightning,
    IceTornado,

    // ENEMIES - MAGE
    MageIdle,
    MageHurt,
    MageAttack,
    MageDeath,

    // ENEMIES - ROGUE
    RogueIdle,
    RogueHurt,
    RogueSkullAttack,
    RogueDeathEvilAttack,
    RogueDeath,

    // ENEMIES - TANK
    TankIdle,
    TankHurt,
    TankAttack1,
    TankAttack2,
    TankDeath,

    // INTERACTIONS / PICKUPS / PROGRESSION
    ShrineActivation,
    StaffUnlock,
    EntangleUnlock,
    LightningUnlock,
    IceTornadoUnlock,
    HeartPickup,
    Healing,

    // UI / BANNERS
    UIHover,
    UIClick,
    BannerAppear
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
        [Tooltip("One or more variations. A random clip is selected each time.")]
        public AudioClip[] clips = Array.Empty<AudioClip>();
        [Range(0f, 1f)] public float volume = 1f;
        [Range(0f, 1f)] public float spatialBlend = 1f;
        [Range(0.5f, 1.5f)] public float minPitch = 1f;
        [Range(0.5f, 1.5f)] public float maxPitch = 1f;
        [Min(0.1f)] public float minDistance = 5f;
        [Min(0.1f)] public float maxDistance = 35f;
        [NonSerialized] public int lastClipIndex = -1;
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

    private static readonly SoundId[] PlayerSoundIds =
    {
        SoundId.PlayerHurt,
        SoundId.PlayerDeath,
        SoundId.PlayerRespawn,
        SoundId.PlayerJump,
        SoundId.PlayerLand,
        SoundId.PlayerInteractHmm,
        SoundId.Wand,
        SoundId.Shield,
        SoundId.ShieldHit
    };

    private static readonly SoundId[] AbilitySoundIds =
    {
        SoundId.Entangle,
        SoundId.Lightning,
        SoundId.IceTornado
    };

    private static readonly SoundId[] EnemySoundIds =
    {
        SoundId.MageIdle,
        SoundId.MageHurt,
        SoundId.MageAttack,
        SoundId.MageDeath,
        SoundId.RogueIdle,
        SoundId.RogueHurt,
        SoundId.RogueSkullAttack,
        SoundId.RogueDeathEvilAttack,
        SoundId.RogueDeath,
        SoundId.TankIdle,
        SoundId.TankHurt,
        SoundId.TankAttack1,
        SoundId.TankAttack2,
        SoundId.TankDeath
    };

    private static readonly SoundId[] InteractionSoundIds =
    {
        SoundId.ShrineActivation,
        SoundId.StaffUnlock,
        SoundId.EntangleUnlock,
        SoundId.LightningUnlock,
        SoundId.IceTornadoUnlock,
        SoundId.HeartPickup,
        SoundId.Healing
    };

    private static readonly SoundId[] UISoundIds =
    {
        SoundId.UIHover,
        SoundId.UIClick,
        SoundId.BannerAppear
    };

    [Header("Audio Routing")]
    [SerializeField] private AudioMixerGroup musicMixerGroup;
    [SerializeField] private AudioMixerGroup sfxMixerGroup;

    [Header("Playback Pool")]
    [Range(0f, 1f)][SerializeField] private float sfxBalance = 1f;
    [Range(4, 64)][SerializeField] private int worldVoiceCount = 24;

    [Header("Player")]
    [SerializeField] private SoundEntry[] playerSounds = Array.Empty<SoundEntry>();

    [Header("Abilities / Runes")]
    [SerializeField] private SoundEntry[] abilitySounds = Array.Empty<SoundEntry>();

    [Header("Enemies")]
    [SerializeField] private SoundEntry[] enemySounds = Array.Empty<SoundEntry>();

    [Header("Interactions & Pickups")]
    [SerializeField] private SoundEntry[] interactionSounds = Array.Empty<SoundEntry>();

    [Header("UI & Banners")]
    [SerializeField] private SoundEntry[] uiSounds = Array.Empty<SoundEntry>();

    [Header("Footsteps")]
    [SerializeField] private FootstepSet[] footstepSets = Array.Empty<FootstepSet>();

    [Header("Player Breathing")]
    [Tooltip("Loop used after sustained running. The movement system can start/stop this later.")]
    [SerializeField] private AudioClip playerBreathingLoop;
    [Range(0f, 1f)][SerializeField] private float playerBreathingVolume = 0.7f;

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
    [Range(0f, 1f)][SerializeField] private float musicBalance = 0.4f;
    [Min(0f)][SerializeField] private float crossfadeSeconds = 2f;
    [SerializeField] private bool playExplorationOnStart = true;

    private AudioSource[] worldVoices;
    private AudioSource uiSource;
    private AudioSource breathingSource;
    private AudioSource rainSource;
    private AudioSource windSource;
    private AudioSource treesSource;
    private AudioSource explorationSource;
    private AudioSource combatSource;

    private bool inCombat;
    private bool musicEnabled;
    private float explorationGain;
    private float combatGain;

    private void Reset()
    {
        SyncAllSoundBuckets();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        SyncAllSoundBuckets();

        worldVoiceCount = Mathf.Clamp(worldVoiceCount, 4, 64);
        sfxBalance = Mathf.Clamp01(sfxBalance);
        ambienceVolume = Mathf.Clamp01(ambienceVolume);
        rainVolume = Mathf.Clamp01(rainVolume);
        windVolume = Mathf.Clamp01(windVolume);
        treesVolume = Mathf.Clamp01(treesVolume);
        playerBreathingVolume = Mathf.Clamp01(playerBreathingVolume);
        musicBalance = Mathf.Clamp01(musicBalance);
        crossfadeSeconds = Mathf.Max(0f, crossfadeSeconds);
    }
#endif

    private void SyncAllSoundBuckets()
    {
        playerSounds = SyncBucket(playerSounds, PlayerSoundIds, false);
        abilitySounds = SyncBucket(abilitySounds, AbilitySoundIds, false);
        enemySounds = SyncBucket(enemySounds, EnemySoundIds, false);
        interactionSounds = SyncBucket(interactionSounds, InteractionSoundIds, false);
        uiSounds = SyncBucket(uiSounds, UISoundIds, true);
    }

    private static SoundEntry[] SyncBucket(
        SoundEntry[] existingEntries,
        SoundId[] ids,
        bool forceUISettings
    )
    {
        SoundEntry[] synced = new SoundEntry[ids.Length];

        for (int i = 0; i < ids.Length; i++)
        {
            SoundEntry existing = null;

            if (existingEntries != null)
            {
                for (int j = 0; j < existingEntries.Length; j++)
                {
                    SoundEntry candidate = existingEntries[j];

                    if (
                        candidate != null &&
                        candidate.id == ids[i]
                    )
                    {
                        existing = candidate;
                        break;
                    }
                }
            }

            if (existing != null)
            {
                synced[i] = existing;

                if (forceUISettings)
                {
                    synced[i].spatialBlend = 0f;
                    synced[i].minPitch = 1f;
                    synced[i].maxPitch = 1f;
                }

                continue;
            }

            SoundEntry created = new SoundEntry
            {
                id = ids[i],
                clips = Array.Empty<AudioClip>(),
                volume = 1f,
                spatialBlend = forceUISettings ? 0f : 1f,
                minPitch = forceUISettings ? 1f : 0.95f,
                maxPitch = forceUISettings ? 1f : 1.05f,
                minDistance = 2f,
                maxDistance = 25f
            };

            synced[i] = created;
        }

        return synced;
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

        CreateRuntimeSources();
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

    private void Update()
    {
        if (Instance != this) return;

        ApplyAmbienceVolumes();

        if (breathingSource != null)
            breathingSource.volume = playerBreathingVolume * sfxBalance;

        UpdateMusicCrossfade();
    }

    private void OnDestroy()
    {
        if (Instance != this) return;

        SceneManager.sceneLoaded -= HandleSceneLoaded;
        Instance = null;
    }

    private void CreateRuntimeSources()
    {
        worldVoices = new AudioSource[Mathf.Clamp(worldVoiceCount, 4, 64)];

        for (int i = 0; i < worldVoices.Length; i++)
            worldVoices[i] = CreateSource("World Sound " + (i + 1), sfxMixerGroup);

        uiSource = CreateSource("UI Sounds", sfxMixerGroup);
        uiSource.ignoreListenerPause = true;

        breathingSource = CreateLoopSource("Player Breathing", playerBreathingLoop, sfxMixerGroup);

        rainSource = CreateLoopSource("Rain Ambience", rainAmbience, sfxMixerGroup);
        windSource = CreateLoopSource("Wind Ambience", windAmbience, sfxMixerGroup);
        treesSource = CreateLoopSource("Trees Ambience", treesAmbience, sfxMixerGroup);

        explorationSource = CreateSource("Exploration Music", musicMixerGroup);
        combatSource = CreateSource("Combat Music", musicMixerGroup);

        explorationSource.loop = true;
        combatSource.loop = true;
        explorationSource.volume = 0f;
        combatSource.volume = 0f;
        explorationSource.clip = explorationMusic;
        combatSource.clip = combatMusic;
    }

    private AudioSource CreateSource(string sourceName, AudioMixerGroup mixerGroup)
    {
        GameObject child = new GameObject(sourceName);
        child.transform.SetParent(transform, false);

        AudioSource source = child.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = 0f;
        source.dopplerLevel = 0f;
        source.outputAudioMixerGroup = mixerGroup;

        return source;
    }

    private AudioSource CreateLoopSource(string sourceName, AudioClip clip, AudioMixerGroup mixerGroup)
    {
        AudioSource source = CreateSource(sourceName, mixerGroup);
        source.loop = true;
        source.clip = clip;
        return source;
    }

    public void Play(SoundId id, Vector3 position)
    {
        if (Instance != this) return;

        SoundEntry entry = FindSound(id);
        if (entry == null) return;

        AudioClip clip = ChooseClip(entry);
        if (clip == null) return;

        if (IsUISound(id))
        {
            PlayEntryOnSource(uiSource, entry, clip, Vector3.zero);
            return;
        }

        if (AudioListener.pause) return;

        AudioSource source = FindFreeWorldVoice();
        if (source == null) return;

        PlayEntryOnSource(source, entry, clip, position);
    }

    private void PlayEntryOnSource(AudioSource source, SoundEntry entry, AudioClip clip, Vector3 position)
    {
        if (source == null || entry == null || clip == null) return;

        source.transform.position = position;
        source.clip = clip;
        source.volume = entry.volume * sfxBalance;

        float lowPitch = Mathf.Min(entry.minPitch, entry.maxPitch);
        float highPitch = Mathf.Max(entry.minPitch, entry.maxPitch);

        source.pitch = UnityEngine.Random.Range(lowPitch, highPitch);
        source.spatialBlend = IsUISound(entry.id) ? 0f : entry.spatialBlend;
        source.minDistance = Mathf.Max(0.1f, entry.minDistance);
        source.maxDistance = Mathf.Max(source.minDistance, entry.maxDistance);
        source.rolloffMode = AudioRolloffMode.Linear;
        source.Play();
    }

    private AudioSource FindFreeWorldVoice()
    {
        if (worldVoices == null) return null;

        foreach (AudioSource source in worldVoices)
            if (source != null && !source.isPlaying)
                return source;

        return null;
    }

    private SoundEntry FindSound(SoundId id)
    {
        SoundEntry entry = FindSoundInGroup(playerSounds, id);
        if (entry != null) return entry;

        entry = FindSoundInGroup(abilitySounds, id);
        if (entry != null) return entry;

        entry = FindSoundInGroup(enemySounds, id);
        if (entry != null) return entry;

        entry = FindSoundInGroup(interactionSounds, id);
        if (entry != null) return entry;

        return FindSoundInGroup(uiSounds, id);
    }

    private static SoundEntry FindSoundInGroup(SoundEntry[] group, SoundId id)
    {
        if (group == null) return null;

        foreach (SoundEntry entry in group)
            if (entry != null && entry.id == id)
                return entry;

        return null;
    }

    private static AudioClip ChooseClip(SoundEntry entry)
    {
        if (entry == null || entry.clips == null || entry.clips.Length == 0)
            return null;

        int index = ChooseClipIndex(entry.clips, entry.lastClipIndex);
        if (index < 0) return null;

        entry.lastClipIndex = index;
        return entry.clips[index];
    }

    private static int ChooseClipIndex(AudioClip[] clips, int previousIndex)
    {
        if (clips == null || clips.Length == 0)
            return -1;

        int validCount = 0;

        for (int i = 0; i < clips.Length; i++)
            if (clips[i] != null)
                validCount++;

        if (validCount == 0)
            return -1;

        if (validCount == 1)
        {
            for (int i = 0; i < clips.Length; i++)
                if (clips[i] != null)
                    return i;
        }

        int selectedIndex;

        do
        {
            selectedIndex = UnityEngine.Random.Range(0, clips.Length);
        }
        while (
            clips[selectedIndex] == null ||
            selectedIndex == previousIndex
        );

        return selectedIndex;
    }

    private static bool IsUISound(SoundId id)
    {
        return
            id == SoundId.UIHover ||
            id == SoundId.UIClick ||
            id == SoundId.BannerAppear;
    }

    public void PlayUIHover() => Play(SoundId.UIHover, Vector3.zero);
    public void PlayUIClick() => Play(SoundId.UIClick, Vector3.zero);
    public void PlayBanner() => Play(SoundId.BannerAppear, Vector3.zero);

    public void PlayFootstep(FootstepSurface surface, bool running, Vector3 position)
    {
        if (Instance != this || AudioListener.pause)
            return;

        FootstepSet set = FindFootstepSet(surface);
        if (set == null) return;

        AudioClip[] clips = running ? set.runClips : set.walkClips;
        if (clips == null || clips.Length == 0)
            return;

        int previousIndex = running ? set.lastRunIndex : set.lastWalkIndex;
        int clipIndex = ChooseClipIndex(clips, previousIndex);

        if (clipIndex < 0)
            return;

        AudioClip clip = clips[clipIndex];

        if (running)
            set.lastRunIndex = clipIndex;
        else
            set.lastWalkIndex = clipIndex;

        AudioSource source = FindFreeWorldVoice();
        if (source == null) return;

        source.transform.position = position;
        source.clip = clip;
        source.volume = set.volume * sfxBalance;

        float lowPitch = Mathf.Min(set.minPitch, set.maxPitch);
        float highPitch = Mathf.Max(set.minPitch, set.maxPitch);

        source.pitch = UnityEngine.Random.Range(lowPitch, highPitch);
        source.spatialBlend = set.spatialBlend;
        source.minDistance = Mathf.Max(0.1f, set.minDistance);
        source.maxDistance = Mathf.Max(source.minDistance, set.maxDistance);
        source.rolloffMode = AudioRolloffMode.Linear;
        source.Play();
    }

    private FootstepSet FindFootstepSet(FootstepSurface surface)
    {
        if (footstepSets == null) return null;

        foreach (FootstepSet set in footstepSets)
            if (set != null && set.surface == surface)
                return set;

        return null;
    }

    public void StartPlayerBreathing()
    {
        if (
            Instance != this ||
            breathingSource == null ||
            breathingSource.clip == null
        )
        {
            return;
        }

        if (!breathingSource.isPlaying)
            breathingSource.Play();
    }

    public void StopPlayerBreathing()
    {
        if (Instance != this || breathingSource == null)
            return;

        breathingSource.Stop();
    }

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
        if (source == null || source.clip == null)
            return;

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
            rainSource.volume = ambienceVolume * rainVolume * sfxBalance;

        if (windSource != null)
            windSource.volume = ambienceVolume * windVolume * sfxBalance;

        if (treesSource != null)
            treesSource.volume = ambienceVolume * treesVolume * sfxBalance;
    }

    public void SetCombatMusic(bool active)
    {
        if (Instance != this) return;

        AudioSource target = active ? combatSource : explorationSource;

        if (target == null || target.clip == null)
            return;

        inCombat = active;
        musicEnabled = true;

        if (!target.isPlaying)
            target.Play();
    }

    private void UpdateMusicCrossfade()
    {
        if (
            !musicEnabled ||
            explorationSource == null ||
            combatSource == null
        )
        {
            return;
        }

        float step =
            crossfadeSeconds <= 0f
                ? 1f
                : Time.unscaledDeltaTime / crossfadeSeconds;

        explorationGain = Mathf.MoveTowards(
            explorationGain,
            inCombat ? 0f : 1f,
            step
        );

        combatGain = Mathf.MoveTowards(
            combatGain,
            inCombat ? 1f : 0f,
            step
        );

        explorationSource.volume = explorationGain * musicBalance;
        combatSource.volume = combatGain * musicBalance;

        if (inCombat && explorationGain == 0f)
            explorationSource.Stop();

        if (!inCombat && combatGain == 0f)
            combatSource.Stop();
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode != LoadSceneMode.Single)
            return;

        if (worldVoices != null)
        {
            foreach (AudioSource source in worldVoices)
                if (source != null)
                    source.Stop();
        }

        StopPlayerBreathing();
        SetCombatMusic(false);
    }

    [ContextMenu("Test/UI Click (Play Mode)")]
    private void TestClick()
    {
        if (Application.isPlaying && Instance == this)
            PlayUIClick();
    }

    [ContextMenu("Test/Banner (Play Mode)")]
    private void TestBanner()
    {
        if (Application.isPlaying && Instance == this)
            PlayBanner();
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
        if (Application.isPlaying && Instance == this)
            SetAmbienceActive(true);
    }

    [ContextMenu("Test/Ambience Off (Play Mode)")]
    private void TestAmbienceOff()
    {
        if (Application.isPlaying && Instance == this)
            SetAmbienceActive(false);
    }

    [ContextMenu("Test/Combat Music (Play Mode)")]
    private void TestCombat()
    {
        if (Application.isPlaying && Instance == this)
            SetCombatMusic(true);
    }

    [ContextMenu("Test/Exploration Music (Play Mode)")]
    private void TestExploration()
    {
        if (Application.isPlaying && Instance == this)
            SetCombatMusic(false);
    }

}
