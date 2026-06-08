using UnityEngine;

namespace ArduinoUnityGame
{
    [RequireComponent(typeof(Rigidbody))]
    public sealed class SerialStarRunnerPlayer : MonoBehaviour
    {
        private static readonly int IsWalkingParameter = Animator.StringToHash("IsWalking");
        private static readonly int JumpParameter = Animator.StringToHash("Jump");

        [SerializeField] private float lateralSpeed = 8f;
        [SerializeField] private float forwardSpeed = 5.8f;
        [SerializeField] private float jumpVelocity = 7.4f;
        [SerializeField] private float dashMultiplier = 1.65f;
        [SerializeField] private float dashDuration = 0.35f;
        [SerializeField] private float visualTurnSpeed = 720f;
        [SerializeField] private Animator animator;

        private SerialInputReader serialInput;
        private SerialStarRunnerGame game;
        private Rigidbody body;
        private Transform visualTransform;
        private float dashTimeRemaining;
        private float hitStunRemaining;
        private float nextJumpTime;
        private bool grounded;
        private bool hasWalkingParameter;
        private bool hasJumpParameter;

        public void Configure(SerialInputReader inputReader, SerialStarRunnerGame runnerGame)
        {
            serialInput = inputReader;
            game = runnerGame;
        }

        public void BindAnimator(Animator runnerAnimator)
        {
            animator = runnerAnimator;
            visualTransform = animator == null ? null : animator.transform;
            RefreshAnimatorParameters();
        }

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            body.constraints = RigidbodyConstraints.FreezeRotation;
            body.collisionDetectionMode = CollisionDetectionMode.Continuous;

            if (animator == null)
            {
                animator = GetComponentInChildren<Animator>();
            }

            BindAnimator(animator);
        }

        private void Update()
        {
            if (game == null || !game.IsPlaying)
            {
                return;
            }

            if (ShouldJump())
            {
                TryJump();
            }

            if (ShouldDash())
            {
                dashTimeRemaining = dashDuration;
            }
        }

        private void FixedUpdate()
        {
            UpdateGrounded();

            if (game == null || !game.IsPlaying)
            {
                Vector3 stopped = GetBodyVelocity();
                stopped.x = 0f;
                stopped.z = 0f;
                SetBodyVelocity(stopped);
                SetWalkingAnimation(false);
                return;
            }

            if (hitStunRemaining > 0f)
            {
                hitStunRemaining -= Time.fixedDeltaTime;
                SetWalkingAnimation(false);
                return;
            }

            float axis = GetControlAxis();
            float currentForwardSpeed = forwardSpeed;
            if (dashTimeRemaining > 0f)
            {
                currentForwardSpeed *= dashMultiplier;
                dashTimeRemaining -= Time.fixedDeltaTime;
            }

            Vector3 velocity = GetBodyVelocity();
            velocity.x = axis * lateralSpeed;
            velocity.z = currentForwardSpeed;
            SetBodyVelocity(velocity);
            SetWalkingAnimation(true);
            RotateVisualTowards(velocity);

            Vector3 position = transform.position;
            position.x = Mathf.Clamp(position.x, -5.1f, 5.1f);
            transform.position = position;
        }

        public void KnockBackFrom(Vector3 hazardPosition)
        {
            Vector3 away = (transform.position - hazardPosition).normalized;
            if (away.sqrMagnitude < 0.01f)
            {
                away = Vector3.back;
            }

            Vector3 velocity = GetBodyVelocity();
            velocity.x = Mathf.Clamp(away.x, -1f, 1f) * 5f;
            velocity.y = 5.5f;
            velocity.z = -3.5f;
            SetBodyVelocity(velocity);
            hitStunRemaining = 0.28f;
        }

        public void StopMotion()
        {
            SetBodyVelocity(Vector3.zero);
        }

        public void RespawnAt(Vector3 position, Quaternion rotation)
        {
            transform.SetPositionAndRotation(position, rotation);
            StopMotion();
            dashTimeRemaining = 0f;
            hitStunRemaining = 0f;
            grounded = false;
            nextJumpTime = Time.time + 0.12f;
            SetWalkingAnimation(false);

            if (visualTransform != null)
            {
                visualTransform.localRotation = Quaternion.identity;
            }
        }

        private bool ShouldJump()
        {
            bool serialJump = serialInput != null && serialInput.JumpPressed;
            return serialJump || UnityEngine.Input.GetKeyDown(KeyCode.Space);
        }

        private bool ShouldDash()
        {
            bool serialDash = serialInput != null && serialInput.DashPressed;
            return serialDash || UnityEngine.Input.GetKeyDown(KeyCode.LeftShift) || UnityEngine.Input.GetKeyDown(KeyCode.RightShift);
        }

        private float GetControlAxis()
        {
            float axis = serialInput == null ? 0f : serialInput.Axis;
            float keyboardAxis = 0f;

            if (UnityEngine.Input.GetKey(KeyCode.A) || UnityEngine.Input.GetKey(KeyCode.LeftArrow))
            {
                keyboardAxis -= 1f;
            }

            if (UnityEngine.Input.GetKey(KeyCode.D) || UnityEngine.Input.GetKey(KeyCode.RightArrow))
            {
                keyboardAxis += 1f;
            }

            if (Mathf.Abs(keyboardAxis) > 0.01f)
            {
                axis = Mathf.Clamp(axis + keyboardAxis, -1f, 1f);
            }

            return axis;
        }

        private void TryJump()
        {
            if (!grounded || Time.time < nextJumpTime)
            {
                return;
            }

            Vector3 velocity = GetBodyVelocity();
            velocity.y = jumpVelocity;
            SetBodyVelocity(velocity);
            grounded = false;
            nextJumpTime = Time.time + 0.18f;
            TriggerJumpAnimation();
        }

        private void UpdateGrounded()
        {
            grounded = Physics.Raycast(transform.position + Vector3.up * 0.1f, Vector3.down, 1.18f, ~0, QueryTriggerInteraction.Ignore);
        }

        private Vector3 GetBodyVelocity()
        {
#if UNITY_6000_0_OR_NEWER
            return body.linearVelocity;
#else
            return body.velocity;
#endif
        }

        private void SetBodyVelocity(Vector3 velocity)
        {
#if UNITY_6000_0_OR_NEWER
            body.linearVelocity = velocity;
#else
            body.velocity = velocity;
#endif
        }

        private void RotateVisualTowards(Vector3 velocity)
        {
            if (visualTransform == null)
            {
                return;
            }

            Vector3 planarVelocity = new Vector3(velocity.x, 0f, velocity.z);
            if (planarVelocity.sqrMagnitude < 0.01f)
            {
                return;
            }

            Quaternion targetRotation = Quaternion.LookRotation(planarVelocity.normalized, Vector3.up);
            visualTransform.rotation = Quaternion.RotateTowards(visualTransform.rotation, targetRotation, visualTurnSpeed * Time.fixedDeltaTime);
        }

        private void SetWalkingAnimation(bool isWalking)
        {
            if (animator != null && hasWalkingParameter)
            {
                animator.SetBool(IsWalkingParameter, isWalking);
            }
        }

        private void TriggerJumpAnimation()
        {
            if (animator != null && hasJumpParameter)
            {
                animator.SetTrigger(JumpParameter);
            }
        }

        private void RefreshAnimatorParameters()
        {
            hasWalkingParameter = false;
            hasJumpParameter = false;

            if (animator == null || animator.runtimeAnimatorController == null)
            {
                return;
            }

            AnimatorControllerParameter[] parameters = animator.parameters;
            for (int i = 0; i < parameters.Length; i++)
            {
                AnimatorControllerParameter parameter = parameters[i];
                if (parameter.nameHash == IsWalkingParameter && parameter.type == AnimatorControllerParameterType.Bool)
                {
                    hasWalkingParameter = true;
                }
                else if (parameter.nameHash == JumpParameter && parameter.type == AnimatorControllerParameterType.Trigger)
                {
                    hasJumpParameter = true;
                }
            }
        }
    }
}
