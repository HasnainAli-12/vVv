using UnityEngine;

namespace NasCon.Game
{
    /// <summary>
    /// Ambient meteor shower.
    ///
    /// Purely visual - no damage, no collision, nothing the player can interact with. It exists to put
    /// occasional movement and light in the sky above the city, which is what stops a night sky
    /// reading as a flat backdrop.
    ///
    /// Spawns relative to the player on a timer, so it works anywhere in the level rather than only
    /// where an emitter happened to be placed by hand.
    /// </summary>
    public class MeteorEvent : MonoBehaviour
    {
        [Tooltip("Seconds between meteors, chosen at random in this range.")]
        [SerializeField] Vector2 interval = new Vector2(9f, 22f);

        [Tooltip("How high above the player they appear. Must clear the tallest rooftops.")]
        [SerializeField] float spawnHeight = 45f;

        [Tooltip("Horizontal spread around the player.")]
        [SerializeField] float spawnRadius = 60f;

        [SerializeField] float scaleMin = 1f;
        [SerializeField] float scaleMax = 1.8f;

        [Tooltip("Chance of the bigger shower effect instead of a single meteor.")]
        [SerializeField, Range(0f, 1f)] float showerChance = 0.15f;

        [Tooltip("Fall angle in degrees. A slight lean reads as entry rather than a straight drop.")]
        [SerializeField] float entryLean = 22f;

        Transform target;
        float next;

        void Start()
        {
            GameObject player = null;
            try { player = GameObject.FindGameObjectWithTag("Player"); }
            catch { }   // the tag may not exist in a stripped scene - fall back to this object

            target = player != null ? player.transform : transform;
            next = Random.Range(interval.x, interval.y);
        }

        void Update()
        {
            next -= Time.deltaTime;
            if (next > 0f) return;
            next = Random.Range(interval.x, interval.y);

            Spawn();
        }

        /// <summary>Fires one meteor immediately. Public so a scripted event can trigger it.</summary>
        public void Spawn()
        {
            Vector3 origin = target.position;
            Vector2 offset = Random.insideUnitCircle * spawnRadius;

            bool big = Random.value < showerChance;
            string effect = big ? "vfx_meteor_shower" : "vfx_meteor";

            Quaternion rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), entryLean);
            FXV.Spawn(effect,
                      origin + new Vector3(offset.x, spawnHeight, offset.y),
                      rotation,
                      Random.Range(scaleMin, scaleMax));
        }
    }
}
