using UnityEngine;

namespace GameCamera
{
    public class CameraShaker : MonoBehaviour
    {
        [Header("Defaults")] public float defaultAmplitude = 0.07f;
        public float defaultDuration = 0.12f;

        Vector3 baseLocalPos;
        float t;
        float dur;
        float amp;

        void Awake()
        {
            baseLocalPos = transform.localPosition;
        }

        public void Kick(float amplitude = -1f, float duration = -1f)
        {
            amp = (amplitude > 0f) ? amplitude : defaultAmplitude;
            dur = (duration > 0f) ? duration : defaultDuration;
            t = dur;
        }

        void LateUpdate()
        {
            if (t > 0f)
            {
                t -= Time.deltaTime;
                float k = (t / dur);
                transform.localPosition = baseLocalPos + Random.insideUnitSphere * (amp * k);
            }
            else
            {
                transform.localPosition = Vector3.Lerp(transform.localPosition, baseLocalPos, Time.deltaTime * 8f);
            }
        }
    }
}