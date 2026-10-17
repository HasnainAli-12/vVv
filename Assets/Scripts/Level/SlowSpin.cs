using UnityEngine;

namespace NasCon.Level
{
    /// <summary>
    /// Constant rotation on one axis. Used to keep the decorative tornadoes turning so they read as
    /// alive rather than as static props.
    /// </summary>
    public class SlowSpin : MonoBehaviour
    {
        [SerializeField] Vector3 degreesPerSecond = new Vector3(0f, 40f, 0f);

        void Update()
        {
            transform.Rotate(degreesPerSecond * Time.deltaTime, Space.Self);
        }
    }
}
