using UnityEngine;

namespace Fighters
{
    /// <summary>
    /// The object AutoPortraitBinder aims its camera at (it looks for a child called "HeadAnchor").
    /// Always sits on the head bone and always faces the way the fighter faces, flat on the ground,
    /// so every character model frames the same way no matter how its rig/bones are oriented.
    /// </summary>
    public sealed class PortraitAnchor : MonoBehaviour
    {
        Transform _root;
        Transform _head;
        float _fallbackHeight;

        public void Init(Transform root, Transform headBone)
        {
            _root = root;
            _head = headBone;

            if (_head == null)
            {
                // No humanoid head bone: use the top of the model's bounds instead.
                var rends = root.GetComponentsInChildren<Renderer>();
                if (rends.Length > 0)
                {
                    var b = rends[0].bounds;
                    for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
                    _fallbackHeight = (b.max.y - 0.1f * b.size.y) - root.position.y;
                }
            }

            Place();
            LogReport();
        }

        void LateUpdate() { Place(); }

        void Place()
        {
            if (_root == null) return;

            Vector3 fwd = _root.forward;
            fwd.y = 0f;
            if (fwd.sqrMagnitude < 1e-6f) fwd = Vector3.forward;

            Vector3 pos = _head != null ? _head.position : _root.position + Vector3.up * _fallbackHeight;
            transform.SetPositionAndRotation(pos, Quaternion.LookRotation(fwd.normalized, Vector3.up));
        }

        void LogReport()
        {
            int total = 0, onLayer = 0;
            foreach (var r in _root.GetComponentsInChildren<Renderer>(true))
            {
                total++;
                if (r.gameObject.layer == _root.gameObject.layer) onLayer++;
            }
            Debug.Log("[Portrait] " + _root.name +
                      ": head=" + (_head != null ? _head.name : "none (using fallback)") +
                      "  anchorWorldY=" + transform.position.y.ToString("0.000") +
                      "  rootLayer=" + LayerMask.LayerToName(_root.gameObject.layer) +
                      "  renderersOnRootLayer=" + onLayer + "/" + total);
        }
    }
}