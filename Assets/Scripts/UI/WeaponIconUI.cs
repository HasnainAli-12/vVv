using UnityEngine;
using UnityEngine.UI;

namespace NasCon.Game
{
    /// <summary>
    /// Shows the fist icon while unarmed and the sword icon while the blade is drawn.
    ///
    /// Two objects rather than one swapping its sprite, because the icons have different shapes and a
    /// Preserve Aspect image would visibly change size when they swap.
    ///
    /// Finds both icons by name under this object, so adding the component to the HUD is the only
    /// setup step - nothing to wire.
    /// </summary>
    public class WeaponIconUI : MonoBehaviour
    {
        [SerializeField] GameObject fistIcon;
        [SerializeField] GameObject swordIcon;

        PlayerWeapon weapon;
        bool lastArmed;
        bool everApplied;

        void Awake()
        {
            if (fistIcon == null) fistIcon = FindByName("WeaponIcon_Fist");
            if (swordIcon == null) swordIcon = FindByName("WeaponIcon_Sword");
        }

        GameObject FindByName(string n)
        {
            foreach (var t in GetComponentsInChildren<Transform>(true))
                if (t.name == n) return t.gameObject;
            return null;
        }

        void Update()
        {
            if (weapon == null)
            {
                GameObject player = null;
                try { player = GameObject.FindGameObjectWithTag("Player"); } catch { }
                if (player == null) return;

                weapon = player.GetComponentInChildren<PlayerWeapon>();
                if (weapon == null) return;
            }

            bool armed = weapon.IsEquipped;

            // only touch the objects when the state actually changes - SetActive every frame would
            // dirty the canvas and rebuild its batches for nothing
            if (everApplied && armed == lastArmed) return;
            everApplied = true;
            lastArmed = armed;

            if (fistIcon != null) fistIcon.SetActive(!armed);
            if (swordIcon != null) swordIcon.SetActive(armed);
        }
    }
}
