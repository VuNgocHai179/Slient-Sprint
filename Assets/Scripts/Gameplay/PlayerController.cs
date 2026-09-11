using UnityEngine;
using SilentSprint.Core;
using SilentSprint.UI;
using DG.Tweening;

namespace SilentSprint.Gameplay
{
    [RequireComponent(typeof(CharacterController))]
    public class PlayerController : Singleton<PlayerController>
    {
        [Header("Movement Settings")]
        public float walkSpeed = 5.0f;
        public float runSpeed = 8.0f;
        public float jumpHeight = 1.5f;
        public float gravity = -9.81f;
        public Joystick movementJoystick;

        [Header("Camera Settings")]
        public float mouseSensitivity = 2.0f;
        public Transform playerCamera;

        [Header("Stamina Settings")]
        public float maxStamina = 100f;
        public float drainRate = 25f;
        public float regenRate = 15f;
        public float currentStamina { get; private set; }
        private bool isExhausted = false;

        [Header("Flashlight Settings")]
        public GameObject flashlightSource;

        [Header("Game Feel (DOTween)")]
        public float normalFOV = 60f;
        public float runFOV = 75f;
        public float fovTweenTime = 0.3f;
        public float landingDipAmount = 0.15f;
        public float landingDipTime = 0.2f;

        private CharacterController controller;
        private Animator animator;
        private Camera camComponent;

        private float cameraPitch = 0.0f;
        private float velocityY = 0.0f;
        private float originalCamLocalY;

        private bool isRunning = false;
        private bool isSwiping = false;
        private bool wasGrounded = true;

        void Start()
        {
            currentStamina = maxStamina;

            controller = GetComponent<CharacterController>();
            animator = GetComponentInChildren<Animator>();

            if (playerCamera != null)
            {
                camComponent = playerCamera.GetComponent<Camera>();
                originalCamLocalY = playerCamera.localPosition.y;
            }
        }

        void Update()
        {
            HandlePCInput();
            HandleCameraLook();
            MovePlayer();
            CheckLanding();
        }

        private void CheckLanding()
        {
            if (!wasGrounded && controller.isGrounded)
            {
                playerCamera.DOKill();
                playerCamera.DOLocalMoveY(originalCamLocalY - landingDipAmount, landingDipTime / 2f)
                    .SetEase(Ease.OutQuad)
                    .OnComplete(() =>
                    {
                        playerCamera.DOLocalMoveY(originalCamLocalY, landingDipTime / 2f).SetEase(Ease.InQuad);
                    });
            }
            wasGrounded = controller.isGrounded;
        }

        private void HandlePCInput()
        {
#if UNITY_EDITOR || UNITY_STANDALONE
            if (Input.GetKeyDown(KeyCode.Space)) Jump();

            if (Input.GetKeyDown(KeyCode.LeftShift)) StartRun();
            else if (Input.GetKeyUp(KeyCode.LeftShift)) StopRun();

            if (Input.GetKeyDown(KeyCode.F)) ToggleFlashlight();
#endif
        }

        void MovePlayer()
        {
            float moveX = 0f;
            float moveZ = 0f;

            if (movementJoystick != null)
            {
                moveX = movementJoystick.Horizontal;
                moveZ = movementJoystick.Vertical;
            }

#if UNITY_EDITOR || UNITY_STANDALONE
            if (moveX == 0 && moveZ == 0)
            {
                moveX = Input.GetAxis("Horizontal");
                moveZ = Input.GetAxis("Vertical");
            }
#endif

            Vector2 inputDir = new Vector2(moveX, moveZ);
            if (inputDir.magnitude > 1f) inputDir.Normalize();

            bool isMoving = inputDir.magnitude > 0.1f;

            if (isRunning && isMoving)
            {
                currentStamina -= drainRate * Time.deltaTime;
                if (currentStamina <= 0f)
                {
                    currentStamina = 0f;
                    isExhausted = true;
                    StopRun();
                }
            }
            else
            {
                if (currentStamina < maxStamina)
                {
                    currentStamina += regenRate * Time.deltaTime;
                    if (currentStamina > maxStamina) currentStamina = maxStamina;

                    if (isExhausted && currentStamina >= 15f)
                    {
                        isExhausted = false;
                    }
                }
            }

            if (animator != null)
            {
                animator.SetBool("isWalking", isMoving && !isRunning);
                animator.SetBool("isRunning", isMoving && isRunning);
                if (velocityY <= 0)
                {
                    animator.SetBool("isGrounded", controller.isGrounded);
                }
            }

            float currentSpeed = isRunning ? runSpeed : walkSpeed;
            Vector3 move = transform.right * inputDir.x + transform.forward * inputDir.y;
            controller.Move(move * currentSpeed * Time.deltaTime);

            if (controller.isGrounded && velocityY < 0)
            {
                velocityY = -2f;
            }

            velocityY += gravity * Time.deltaTime;
            Vector3 gravityMove = new Vector3(0, velocityY, 0);
            controller.Move(gravityMove * Time.deltaTime);
        }

        void HandleCameraLook()
        {
            float lookX = 0f;
            float lookY = 0f;

#if UNITY_EDITOR || UNITY_STANDALONE
            if (Input.GetMouseButtonDown(1)) isSwiping = true;
            else if (Input.GetMouseButtonUp(1)) isSwiping = false;

            if (isSwiping && Input.GetMouseButton(1))
            {
                lookX = Input.GetAxis("Mouse X") * mouseSensitivity;
                lookY = Input.GetAxis("Mouse Y") * mouseSensitivity;
            }
#else
            foreach (Touch touch in Input.touches)
            {
                if (UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject(touch.fingerId)) continue;
                if (touch.phase == TouchPhase.Moved)
                {
                    lookX = touch.deltaPosition.x * mouseSensitivity * 0.1f;
                    lookY = touch.deltaPosition.y * mouseSensitivity * 0.1f;
                    break;
                }
            }
#endif
            cameraPitch -= lookY;
            cameraPitch = Mathf.Clamp(cameraPitch, -90f, 90f);

            playerCamera.localEulerAngles = Vector3.right * cameraPitch;
            transform.Rotate(Vector3.up * lookX);
        }

        public void Jump()
        {
            if (controller.isGrounded)
            {
                velocityY = Mathf.Sqrt(jumpHeight * -2f * gravity);

                if (animator != null)
                {
                    animator.SetTrigger("Jump");
                    animator.SetBool("isGrounded", false);
                }
            }
        }

        public void StartRun()
        {
            if (isExhausted) return;

            isRunning = true;
            if (camComponent != null) camComponent.DOFieldOfView(runFOV, fovTweenTime).SetEase(Ease.OutCubic);
        }

        public void StopRun()
        {
            isRunning = false;
            if (camComponent != null) camComponent.DOFieldOfView(normalFOV, fovTweenTime).SetEase(Ease.OutCubic);
        }

        public void ToggleFlashlight()
        {
            if (flashlightSource != null)
            {
                flashlightSource.SetActive(!flashlightSource.activeSelf);
            }
        }
    }
}