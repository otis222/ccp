using UnityEngine;

namespace ArduinoUnityGame
{
    public sealed class SerialStarPickup : MonoBehaviour
    {
        [SerializeField] private int points = 10;
        private SerialStarRunnerGame game;
        private bool collected;

        public void Configure(SerialStarRunnerGame runnerGame, int pickupPoints)
        {
            game = runnerGame;
            points = pickupPoints;
        }

        public void ResetPickup()
        {
            collected = false;
            gameObject.SetActive(true);
        }

        private void Update()
        {
            transform.Rotate(0f, 95f * Time.deltaTime, 0f, Space.World);
            transform.position += Vector3.up * Mathf.Sin(Time.time * 4f + transform.position.z) * 0.0015f;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (collected || other.GetComponentInParent<SerialStarRunnerPlayer>() == null)
            {
                return;
            }

            collected = true;
            if (game != null)
            {
                game.CollectCore(points);
            }

            gameObject.SetActive(false);
        }
    }

    public sealed class SerialStarHazard : MonoBehaviour
    {
        private SerialStarRunnerGame game;

        public void Configure(SerialStarRunnerGame runnerGame)
        {
            game = runnerGame;
        }

        private void Update()
        {
            transform.Rotate(0f, 40f * Time.deltaTime, 0f, Space.World);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other.GetComponentInParent<SerialStarRunnerPlayer>() == null)
            {
                return;
            }

            if (game != null)
            {
                game.HitHazard(transform.position);
            }
        }
    }

    public sealed class SerialStarGoal : MonoBehaviour
    {
        private SerialStarRunnerGame game;

        public void Configure(SerialStarRunnerGame runnerGame)
        {
            game = runnerGame;
        }

        private void Update()
        {
            transform.Rotate(0f, 25f * Time.deltaTime, 0f, Space.World);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other.GetComponentInParent<SerialStarRunnerPlayer>() == null)
            {
                return;
            }

            if (game != null)
            {
                game.ReachGoal();
            }
        }
    }

    public sealed class SimpleCameraFollow : MonoBehaviour
    {
        [SerializeField] private Vector3 offset = new Vector3(0f, 7f, -10f);
        [SerializeField] private Vector3 lookOffset = new Vector3(0f, 1f, 4f);
        [SerializeField] private float followSharpness = 6f;
        [SerializeField] private Transform target;

        public void Configure(Transform followTarget)
        {
            target = followTarget;
            SnapToTarget();
        }

        private void LateUpdate()
        {
            if (target == null)
            {
                return;
            }

            Vector3 desiredPosition = target.position + offset;
            transform.position = Vector3.Lerp(transform.position, desiredPosition, 1f - Mathf.Exp(-followSharpness * Time.deltaTime));
            transform.LookAt(target.position + lookOffset);
        }

        public void SnapToTarget()
        {
            if (target == null)
            {
                return;
            }

            transform.position = target.position + offset;
            transform.LookAt(target.position + lookOffset);
        }
    }
}
