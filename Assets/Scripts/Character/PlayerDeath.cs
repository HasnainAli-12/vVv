using UnityEngine;

namespace NasCon.Game
{
    /// <summary>
    /// Ends the run when the player dies.
    ///
    /// Two jobs the HUD deliberately does not do: play the death clip, and stop the world. The death
    /// panel alone left the game running behind it - enemies kept chasing, the player kept moving, and
    /// nothing told the animator anything had happened.
    ///
    /// The freeze is DELAYED on purpose. Stopping time immediately would show the first frame of the
    /// death animation and nothing else; the clip needs a couple of seconds to fall over before the
    /// world is allowed to stop.
    /// </summary>
    public class PlayerDeath : MonoBehaviour
    {
        [Header("Source")]
        [Tooltip("Left empty, found on this object.")]
        [SerializeField] Health health;

        [Header("Animation")]
        [Tooltip("Animator trigger for the Dead state. Must match the controller.")]
        [SerializeField] string deadTrigger = "Dead";

        [Tooltip("Seconds the death clip runs before the world freezes. Match it to the clip length.")]
        [SerializeField] float stopAfter = 2.4f;

        [Header("Shutdown")]
        [Tooltip("Disable enemy brains and in-flight projectiles when the player dies.")]
        [SerializeField] bool freezeEnemies = true;

        Animator animator;
        bool dying;

        void Awake()
        {
            if (health == null) health = GetComponent<Health>();
            animator = GetComponentInChildren<Animator>();
        }

        void OnEnable()
        {
            if (health != null) health.Died += OnDied;
        }

        void OnDisable()
        {
            if (health != null) health.Died -= OnDied;
        }

        void OnDied(Health source, GameObject instigator)
        {
            if (dying) return;
            dying = true;

            if (animator != null)
            {
                // Play the state by NAME rather than by trigger. A trigger has to survive an AnyState
                // transition and a stale one is consumed the instant it is set, which is exactly the
                // race that killed the enemy death animation. Playing directly skips all of it.
                animator.ResetTrigger(deadTrigger);
                animator.Play("Dead", 0, 0f);
                Debug.Log("[PlayerDeath] playing 'Dead' on " + animator.name +
                          "   controller=" + (animator.runtimeAnimatorController == null
                              ? "NULL" : animator.runtimeAnimatorController.name));
            }
            else Debug.LogWarning("[PlayerDeath] no Animator - cannot play the death clip");

            if (freezeEnemies) Freeze();

            // Death is final. Two things have to be shut off or the run continues behind the panel:
            // the pause menu's Resume restores timeScale to 1, and the character brain keeps accepting
            // input. Either one alone lets a dead player walk around.
            SuspendPlayerControl();

            // Free the cursor so the Retry button can actually be clicked. Gameplay locks and hides it;
            // nothing was putting it back, so the death panel was unclickable.
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            Invoke("StopWorld", stopAfter);
            Debug.Log("[PlayerDeath] world stops in " + stopAfter.ToString("F2") + "s");
        }

        /// <summary>Stops anything that could restart the run after death.</summary>
        void SuspendPlayerControl()
        {
            // the character brain is CCP's input driver
            foreach (var brain in FindObjectsByType<Lightbug.CharacterControllerPro.Implementation.CharacterBrain>(
                         FindObjectsSortMode.None))
                if (brain != null) brain.enabled = false;

            // PauseMenu is found by type NAME rather than by type, so this does not need a using for
            // whichever namespace it lives in - and it cannot break if that namespace moves.
            foreach (var mb in FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
            {
                if (mb == null) continue;
                string n = mb.GetType().Name;
                if (n == "PauseMenu") mb.enabled = false;
            }
        }

        void Freeze()
        {
            foreach (var brain in FindObjectsByType<EnemyBrain>(FindObjectsSortMode.None))
                if (brain != null) brain.enabled = false;

            foreach (var shot in FindObjectsByType<FireProjectile>(FindObjectsSortMode.None))
                if (shot != null) shot.enabled = false;
        }

        void StopWorld()
        {
            Debug.Log("[PlayerDeath] stopping the world");
            // PauseMenu, HitStop and the rewind all write timeScale too. They are mutually guarded and
            // PlayerHUD.Retry restores it to 1 on reload, so this does not fight any of them.
            Time.timeScale = 0f;
        }

        void OnDestroy()
        {
            CancelInvoke();
        }
    }
}
