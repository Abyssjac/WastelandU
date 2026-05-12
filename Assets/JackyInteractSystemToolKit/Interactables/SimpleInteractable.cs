using System.Collections;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// A concrete, reusable <see cref="BaseInteractable"/> that can trigger any combination of:
/// an Animator trigger, a sound effect, and a UnityEvent command — each with an independent delay.
/// Does not enter the Interacting state; EndInteraction is called immediately so the player
/// can move and interact again right away.
/// </summary>
public class SimpleInteractable : BaseInteractable
{
    // ─────────────────────────────────────────────────────────────
    // Serializable Effect Blocks
    // ─────────────────────────────────────────────────────────────

    [System.Serializable]
    public class AnimationEffect
    {
        [Tooltip("Enable this effect.")]
        public bool enabled = false;

        [Tooltip("The Animator to trigger. If left empty, will try to find one on this GameObject.")]
        public Animator animator;

        [Tooltip("The Animator trigger parameter name to call SetTrigger on.")]
        public string triggerName = "Interact";

        [Tooltip("Delay in seconds before the trigger is fired.")]
        [Min(0f)] public float delay = 0f;
    }

    [System.Serializable]
    public class SoundEffect
    {
        [Tooltip("Enable this effect.")]
        public bool enabled = false;

        [Tooltip("AudioSource used to play the clip. If left empty, will try to find one on this GameObject.")]
        public AudioSource audioSource;

        [Tooltip("The clip to play.")]
        public AudioClip clip;

        [Tooltip("Delay in seconds before the sound is played.")]
        [Min(0f)] public float delay = 0f;
    }

    [System.Serializable]
    public class CommandEffect
    {
        [Tooltip("Enable this effect.")]
        public bool enabled = false;

        [Tooltip("Delay in seconds before the event is invoked.")]
        [Min(0f)] public float delay = 0f;

        [Tooltip("UnityEvent invoked when this object is interacted with.")]
        public UnityEvent onInteract;
    }

    // ── Inspector ─────────────────────────────────────────────────

    [Header("Effects")]
    [SerializeField] private AnimationEffect animationEffect;
    [SerializeField] private SoundEffect     soundEffect;
    [SerializeField] private CommandEffect   commandEffect;

    // ── Runtime ───────────────────────────────────────────────────

    private Animator    _animator;
    private AudioSource _audioSource;

    // ─────────────────────────────────────────────────────────────
    // Lifecycle
    // ─────────────────────────────────────────────────────────────

    protected override void Awake()
    {
        base.Awake();

        // Cache fallback references from this GameObject
        _animator    = GetComponent<Animator>();
        _audioSource = GetComponent<AudioSource>();
    }

    // ─────────────────────────────────────────────────────────────
    // BaseInteractable
    // ─────────────────────────────────────────────────────────────

    public override void Interact(InteractorTargetDetector caller)
    {
        // End interaction immediately — this type never opens a UI panel
        caller.EndInteraction();

        // Fire all enabled effects (each handles its own delay internally)
        if (animationEffect.enabled)
            StartCoroutine(FireAnimationEffect());

        if (soundEffect.enabled)
            StartCoroutine(FireSoundEffect());

        if (commandEffect.enabled)
            StartCoroutine(FireCommandEffect());
    }

    // ─────────────────────────────────────────────────────────────
    // Effect Coroutines
    // ─────────────────────────────────────────────────────────────

    private IEnumerator FireAnimationEffect()
    {
        if (animationEffect.delay > 0f)
            yield return new WaitForSeconds(animationEffect.delay);

        Animator anim = animationEffect.animator != null ? animationEffect.animator : _animator;
        if (anim == null)
        {
            Debug.LogWarning($"[SimpleInteractable] '{gameObject.name}': AnimationEffect is enabled but no Animator found.", this);
            yield break;
        }

        if (string.IsNullOrEmpty(animationEffect.triggerName))
        {
            Debug.LogWarning($"[SimpleInteractable] '{gameObject.name}': AnimationEffect trigger name is empty.", this);
            yield break;
        }

        anim.SetTrigger(animationEffect.triggerName);
    }

    private IEnumerator FireSoundEffect()
    {
        if (soundEffect.delay > 0f)
            yield return new WaitForSeconds(soundEffect.delay);

        AudioSource src = soundEffect.audioSource != null ? soundEffect.audioSource : _audioSource;
        if (src == null)
        {
            Debug.LogWarning($"[SimpleInteractable] '{gameObject.name}': SoundEffect is enabled but no AudioSource found.", this);
            yield break;
        }

        if (soundEffect.clip == null)
        {
            Debug.LogWarning($"[SimpleInteractable] '{gameObject.name}': SoundEffect is enabled but no AudioClip assigned.", this);
            yield break;
        }

        src.PlayOneShot(soundEffect.clip);
    }

    private IEnumerator FireCommandEffect()
    {
        if (commandEffect.delay > 0f)
            yield return new WaitForSeconds(commandEffect.delay);

        commandEffect.onInteract?.Invoke();
    }
}
