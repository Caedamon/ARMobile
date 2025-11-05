using UnityEngine;
using Combat;        // for SimpleMeleeAI
using Characters;    // for KaijuBody

namespace Game
{
    public class FightBootstrap : MonoBehaviour
    {
        [Header("Prefabs")] public Transform robotPrefab;   // your “player” kaiju
        public Transform monsterPrefab;                      // your “enemy” kaiju

        [Header("Camera")] public Camera cam;                // if null, will use Camera.main

        [Header("Spawn Setup")] public float spawnDistance = 3.0f;
        public float horizontalOffset = 1.25f;

        void Start()
        {
            if (cam == null) cam = Camera.main;
            if (cam == null || robotPrefab == null || monsterPrefab == null)
            {
                Debug.LogError("FightBootstrap: missing Camera or Prefabs.");
                return;
            }

            Vector3 forwardFlat = cam.transform.forward; forwardFlat.y = 0f;
            if (forwardFlat.sqrMagnitude < 0.0001f) forwardFlat = Vector3.forward;
            forwardFlat.Normalize();

            Vector3 center = cam.transform.position + forwardFlat * spawnDistance;
            Vector3 right = Vector3.Cross(Vector3.up, forwardFlat).normalized;

            Vector3 posA = center - right * horizontalOffset;
            Vector3 posB = center + right * horizontalOffset;

            Transform robot = Instantiate(robotPrefab, posA, Quaternion.identity);
            Transform monster = Instantiate(monsterPrefab, posB, Quaternion.identity);

            Vector3 lookToB = monster.position - robot.position; lookToB.y = 0f;
            Vector3 lookToA = robot.position - monster.position; lookToA.y = 0f;
            if (lookToB.sqrMagnitude > 0.0001f) robot.rotation = Quaternion.LookRotation(lookToB.normalized, Vector3.up);
            if (lookToA.sqrMagnitude > 0.0001f) monster.rotation = Quaternion.LookRotation(lookToA.normalized, Vector3.up);

            int fighterLayer = LayerMask.NameToLayer("Fighter");
            int monsterLayer = LayerMask.NameToLayer("Monster");
            if (fighterLayer == -1 || monsterLayer == -1)
                Debug.LogWarning("FightBootstrap: Create layers 'Fighter' and 'Monster' in Project Settings > Tags and Layers.");

            robot.gameObject.layer = fighterLayer;
            monster.gameObject.layer = monsterLayer;

            var rHP = robot.GetComponent<Health>() ?? robot.gameObject.AddComponent<Health>();
            var mHP = monster.GetComponent<Health>() ?? monster.gameObject.AddComponent<Health>();
            var rAI = robot.GetComponent<SimpleMeleeAI>() ?? robot.gameObject.AddComponent<SimpleMeleeAI>();
            var mAI = monster.GetComponent<SimpleMeleeAI>() ?? monster.gameObject.AddComponent<SimpleMeleeAI>();

            // Kaiju defaults (heavy feel)
            rHP.maxHP = 200f; mHP.maxHP = 200f;
            rAI.moveSpeed = 0.9f; mAI.moveSpeed = 0.9f;
            rAI.attackRange = 1.6f; mAI.attackRange = 1.6f;
            rAI.attackRate = 0.6f; mAI.attackRate = 0.6f;
            rAI.damage = 25f; mAI.damage = 25f;

            if (fighterLayer != -1 && monsterLayer != -1)
            {
                rAI.enemyMask = 1 << monsterLayer;
                mAI.enemyMask = 1 << fighterLayer;
            }

            if (!robot.TryGetComponent<KaijuBody>(out _)) robot.gameObject.AddComponent<KaijuBody>();
            if (!monster.TryGetComponent<KaijuBody>(out _)) monster.gameObject.AddComponent<KaijuBody>();

            var rRb = robot.GetComponent<Rigidbody>();
            var mRb = monster.GetComponent<Rigidbody>();
            if (rRb == null)
            {
                rRb = robot.gameObject.AddComponent<Rigidbody>();
                rRb.isKinematic = true; rRb.useGravity = false;
                rRb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
            }
            if (mRb == null)
            {
                mRb = monster.gameObject.AddComponent<Rigidbody>();
                mRb.isKinematic = true; mRb.useGravity = false;
                mRb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
            }
        }
    }
}
