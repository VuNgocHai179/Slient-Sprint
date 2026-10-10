using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
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

        /// <summary>
        /// fingerId của những ngón tay đang bấm lên một điều khiển UI (cần gạt, nút nhảy...).
        /// Ngón đó chỉ điều khiển UI, không được xoay camera.
        /// </summary>
        private readonly HashSet<int> lookBlockedFingers = new HashSet<int>();

        /// <summary>Vùng đệm dùng lại cho raycast UI, tránh cấp phát mỗi lần ngón tay chạm xuống.</summary>
        private static readonly List<RaycastResult> uiRaycastResults = new List<RaycastResult>();

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

            // Tiếng chạy chỉ kêu khi THẬT SỰ đang chạy và đang di chuyển: giữ Shift mà đứng yên thì isRunning
            // vẫn bật nhưng chân không bước, nên không có tiếng.
            SetRunSound(isMoving && isRunning);

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
            ReadTouchLook(ref lookX, ref lookY);
#endif
            cameraPitch -= lookY;
            cameraPitch = Mathf.Clamp(cameraPitch, -90f, 90f);

            playerCamera.localEulerAngles = Vector3.right * cameraPitch;
            transform.Rotate(Vector3.up * lookX);
        }

        /// <summary>
        /// Vuốt để xoay camera, nhưng bỏ qua ngón tay đang dùng để bấm/kéo một điều khiển UI
        /// (cần gạt, nút nhảy, nút chạy...). Một ngón tay được phân loại ngay lúc nó chạm xuống
        /// và giữ nguyên phân loại đó cho tới khi nhấc lên: khi kéo cần gạt, ngón tay thường
        /// trượt ra khỏi vùng UI, nếu cứ kiểm tra lại mỗi khung hình thì chính cú kéo cần gạt
        /// lại xoay camera.
        ///
        /// Hàm này chỉ chạy trên thiết bị cảm ứng nhưng vẫn để ngoài #if để Editor cũng biên
        /// dịch và kiểm tra được.
        /// </summary>
        private void ReadTouchLook(ref float lookX, ref float lookY)
        {
            int touchCount = Input.touchCount;

            for (int i = 0; i < touchCount; i++)
            {
                Touch touch = Input.GetTouch(i);

                if (touch.phase == TouchPhase.Began)
                {
                    if (IsTouchOnControl(touch.position)) lookBlockedFingers.Add(touch.fingerId);
                    else lookBlockedFingers.Remove(touch.fingerId);
                }
                else if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled)
                {
                    lookBlockedFingers.Remove(touch.fingerId);
                }
            }

            for (int i = 0; i < touchCount; i++)
            {
                Touch touch = Input.GetTouch(i);

                if (lookBlockedFingers.Contains(touch.fingerId)) continue;
                if (touch.phase != TouchPhase.Moved) continue;

                lookX = touch.deltaPosition.x * mouseSensitivity * 0.1f;
                lookY = touch.deltaPosition.y * mouseSensitivity * 0.1f;
                break;
            }
        }

        /// <summary>
        /// Điểm chạm có nằm trên một điều khiển UI thật sự hay không (cần gạt, nút bấm).
        ///
        /// Không dùng EventSystem.IsPointerOverGameObject cho việc này được: MainHUD phủ kín màn
        /// hình bằng một Image trong suốt có bật Raycast Target, nên mọi điểm chạm đều "ở trên UI"
        /// và phép kiểm tra đó không loại trừ được gì. Muốn biết cú chạm có bị UI lấy mất hay không
        /// thì phải xem EventSystem có tìm được mục tiêu nhấn (IPointerDownHandler) từ điểm đó không.
        /// </summary>
        private static bool IsTouchOnControl(Vector2 screenPosition)
        {
            EventSystem eventSystem = EventSystem.current;
            if (eventSystem == null) return false;

            uiRaycastResults.Clear();
            eventSystem.RaycastAll(new PointerEventData(eventSystem) { position = screenPosition }, uiRaycastResults);

            if (uiRaycastResults.Count == 0) return false;

            // EventSystem cũng chọn mục tiêu nhấn y hệt: lấy kết quả raycast trên cùng rồi đi
            // ngược lên cha để tìm component xử lý PointerDown.
            GameObject hit = uiRaycastResults[0].gameObject;
            return hit != null && ExecuteEvents.GetEventHandler<IPointerDownHandler>(hit) != null;
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

        private void OnDisable()
        {
            // Bị khoá (hết ván, đoạn tỉnh dậy, đoạn kết) thì tiếng chân phải tắt theo, không thì nó kêu mãi.
            AudioManager.StopRun();

            // Lúc bị khoá, UI có thể bị ẩn nên không còn TouchPhase.Ended để dọn; tránh giữ lại
            // fingerId cũ rồi hiểu nhầm ngón tay của lần chơi sau.
            lookBlockedFingers.Clear();
        }

        /// <summary>
        /// Bật/tắt tiếng chạy. Gọi mỗi khung hình cũng được, bên AudioManager tự bỏ qua khi đã đúng trạng thái.
        /// </summary>
        private static void SetRunSound(bool running)
        {
            if (running) AudioManager.StartRun();
            else AudioManager.StopRun();
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