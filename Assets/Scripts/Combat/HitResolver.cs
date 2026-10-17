using System.Collections.Generic;
using UnityEngine;

namespace NasCon.Game
{
    /// <summary>
    /// The one place damage is dealt from.
    ///
    /// The player's combo, the enemy's punch and the thrown orb all route through here rather than
    /// each doing their own overlap. That is what keeps the two sides synchronised: there is no
    /// second implementation to drift out of step, and every rule - team filtering, one hit per
    /// character, skipping the dead - is applied identically to everyone.
    ///
    /// A character is a rigged hierarchy, so a single swing touches many colliders. Results are
    /// de-duplicated by Health component, not by collider, or one punch would deal damage five times.
    /// </summary>
    public static class HitResolver
    {
        static readonly Collider[] buffer = new Collider[48];
        static readonly List<Health> alreadyHit = new List<Health>(16);

        /// <summary>
        /// Damages everything hostile inside the sphere. Returns how many characters were hit, so
        /// the caller knows whether to play a reaction, trigger hit-stop, or stay quiet on a whiff.
        /// </summary>
        /// <param name="origin">Centre of the sweep, usually chest height in front of the attacker.</param>
        /// <param name="radius">Sweep radius.</param>
        /// <param name="damage">Applied per character hit.</param>
        /// <param name="attackerTeam">The attacker's side. Anyone on it is skipped.</param>
        /// <param name="instigator">Owner GameObject, used for logging and self-exclusion.</param>
        /// <param name="sourcePoint">Where the hit came from, for knockback direction and feedback.</param>
        /// <param name="results">Optional; receives each damaged Health.</param>
        public static int Sphere(Vector3 origin, float radius, float damage,
                                 Health.Team attackerTeam, GameObject instigator,
                                 Vector3 sourcePoint, List<Health> results = null)
        {
            if (radius <= 0f || damage <= 0f) return 0;

            int count = Physics.OverlapSphereNonAlloc(origin, radius, buffer, ~0, QueryTriggerInteraction.Ignore);
            if (count <= 0) return 0;

            int hits = 0;
            alreadyHit.Clear();

            for (int i = 0; i < count; i++)
            {
                Collider col = buffer[i];
                if (col == null) continue;

                Health target = col.GetComponentInParent<Health>();
                if (target == null) continue;
                if (!target.IsAlive) continue;

                // never damage your own side - this is what stops a projectile hitting its caster
                if (target.Side == attackerTeam) continue;

                // belt and braces: the attacker's own hierarchy is never a valid target
                if (instigator != null && target.transform.IsChildOf(instigator.transform)) continue;

                // one hit per CHARACTER, not per collider
                if (alreadyHit.Contains(target)) continue;

                alreadyHit.Add(target);
                target.TakeDamage(damage, sourcePoint, instigator);
                if (results != null) results.Add(target);
                hits++;
            }

            alreadyHit.Clear();
            return hits;
        }
    }
}
