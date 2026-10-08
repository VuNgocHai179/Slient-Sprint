using UnityEngine;

namespace SilentSprint.Gameplay.AI
{
    /// <summary>
    /// Thu thap "giac quan" cua quai moi frame va luu tri nho ngan han (vi tri cuoi cung cua player).
    ///
    /// Lop nay KHONG ra quyet dinh hanh vi. No chi tra loi cau hoi "quai dang biet gi".
    /// Viec chon trang thai do <see cref="MonsterBrain"/> va lop phan xa trong <c>MonsterAI</c> lo.
    /// </summary>
    public sealed class MonsterPerception
    {
        /// <summary>Goc mo ta hinh non thi giac, tinh bang do o moi ben so voi huong mat.</summary>
        public const float NoiseWalk = 0.4f;
        public const float NoiseRun = 1.0f;
        private const float NoiseThreshold = MonsterHeuristicPolicy.NoiseThreshold;
        private const float LostMemorySeconds = 8f;

        // ------------------------------------------------------------------ Cau hinh
        public float SightDistance = 20f;
        public float SightAngle = 45f;
        public float EyeHeight = 2.5f;
        public float HearingDistance = 25f;
        public float StalkRadius = 25f;
        public LayerMask ObstacleMask;
        public float RunSpeedReference = 8f;

        // ------------------------------------------------------------------ Ket qua moi frame
        public bool HasTarget;
        /// <summary>Tam nhin sau khi da nhan voi he so canh giac.</summary>
        public float EffectiveSightDistance;
        public float DistanceToPlayer;
        public float AngleToPlayer;
        public bool LineOfSightClear;
        public bool InSightCone;
        public bool PlayerVisible;
        public bool PlayerHeard;
        public float PlayerSpeed;
        /// <summary>Van toc ngang cua player - dung de doan truoc vi tri khi cat duong.</summary>
        public Vector3 PlayerVelocity;
        public float PlayerNoise;
        public float PlayerStaminaNorm;
        public bool FlashlightOn;
        public Vector3 RelativeDirection;

        // ------------------------------------------------------------------ Tri nho
        public Vector3 LastKnownPosition;
        public bool HasLastKnownPosition;
        public float TimeSinceLastSeen = float.MaxValue;
        public float TimeSinceLastHeard = float.MaxValue;

        private PlayerController _playerController;
        private CharacterController _characterController;
        private Animator _playerAnimator;
        private Transform _playerTransform;

        /// <summary>Buffer dung lai cho tia kiem tra tam nhin - tranh cap phat moi frame.</summary>
        private readonly RaycastHit[] _lineOfSightHits = new RaycastHit[16];

        private static readonly int IsWalkingHash = Animator.StringToHash("isWalking");
        private static readonly int IsRunningHash = Animator.StringToHash("isRunning");

        /// <summary>
        /// Gan player. Goi lai duoc khi player doi (vi du sau khi respawn).
        /// </summary>
        public void Bind(PlayerController playerController)
        {
            if (_playerController == playerController)
                return;

            _playerController = playerController;
            _characterController = null;
            _playerAnimator = null;
            _playerTransform = null;
            HasLastKnownPosition = false;
            TimeSinceLastSeen = float.MaxValue;
            TimeSinceLastHeard = float.MaxValue;

            if (playerController != null)
            {
                _playerTransform = playerController.transform;
                _characterController = playerController.GetComponent<CharacterController>();
                _playerAnimator = playerController.GetComponentInChildren<Animator>();
            }
        }

        /// <summary>Quen dan vi tri cuoi cung (dung khi quai bi reset / doi ca truc).</summary>
        public void Forget()
        {
            HasLastKnownPosition = false;
            TimeSinceLastSeen = float.MaxValue;
            TimeSinceLastHeard = float.MaxValue;
        }

        /// <summary>
        /// Lay mau giac quan cho frame nay.
        /// </summary>
        /// <param name="self">Transform cua quai.</param>
        /// <param name="alertness">0..1 tu quyet dinh cua frame truoc - lam tam nhin co gian nhe.</param>
        /// <param name="deltaTime">Time.deltaTime.</param>
        public void Sample(Transform self, float alertness, float deltaTime)
        {
            if (TimeSinceLastSeen < float.MaxValue) TimeSinceLastSeen += deltaTime;
            if (TimeSinceLastHeard < float.MaxValue) TimeSinceLastHeard += deltaTime;

            EffectiveSightDistance = SightDistance * Mathf.Lerp(0.88f, 1.12f, Mathf.Clamp01(alertness));

            HasTarget = self != null && _playerTransform != null;
            if (!HasTarget)
            {
                PlayerVisible = false;
                PlayerHeard = false;
                LineOfSightClear = false;
                InSightCone = false;
                PlayerSpeed = 0f;
                PlayerVelocity = Vector3.zero;
                PlayerNoise = 0f;
                PlayerStaminaNorm = 1f;
                FlashlightOn = false;
                RelativeDirection = Vector3.forward;
                return;
            }

            Vector3 toPlayer = _playerTransform.position - self.position;
            DistanceToPlayer = toPlayer.magnitude;

            Vector3 flatToPlayer = new Vector3(toPlayer.x, 0f, toPlayer.z);
            AngleToPlayer = flatToPlayer.sqrMagnitude > 0.0001f
                ? Vector3.Angle(self.forward, flatToPlayer)
                : 0f;

            Vector3 localDirection = self.InverseTransformDirection(flatToPlayer);
            if (localDirection.sqrMagnitude > 0.0001f) localDirection.Normalize();
            RelativeDirection = localDirection;

            // --- 1. THI GIAC -------------------------------------------------------------
            InSightCone = AngleToPlayer <= SightAngle;
            LineOfSightClear = false;
            if (DistanceToPlayer <= EffectiveSightDistance && InSightCone)
            {
                Vector3 eyePosition = self.position + Vector3.up * EyeHeight;
                Vector3 aimPoint = _playerTransform.position + Vector3.up * 1f;
                Vector3 eyeToPlayer = aimPoint - eyePosition;
                float rayLength = eyeToPlayer.magnitude;

                if (rayLength <= 0.0001f ||
                    !IsLineOfSightBlocked(self, eyePosition, eyeToPlayer / rayLength, rayLength))
                {
                    LineOfSightClear = true;
                }
            }
            // PlayerVisible dung cung cong thuc voi teacher: thay = co tia nhin + trong hinh non + trong tam.
            PlayerVisible = LineOfSightClear && InSightCone &&
                            DistanceToPlayer <= EffectiveSightDistance;

            if (PlayerVisible)
            {
                LastKnownPosition = _playerTransform.position;
                HasLastKnownPosition = true;
                TimeSinceLastSeen = 0f;
            }

            // --- 2. THINH GIAC -----------------------------------------------------------
            SamplePlayerMotion();

            PlayerHeard = PlayerNoise > NoiseThreshold && DistanceToPlayer <= HearingDistance;
            if (PlayerHeard)
            {
                LastKnownPosition = _playerTransform.position;
                HasLastKnownPosition = true;
                TimeSinceLastHeard = 0f;
            }

            // Nghe thay cung cap vi tri cuoi, nhung khong lam "thay".
            if (!PlayerVisible && PlayerHeard)
            {
                // Da cap nhat LastKnownPosition o tren.
            }
        }

        /// <summary>
        /// Tia tu mat quai toi player co bi vat gi chan khong.
        ///
        /// Bo qua chinh quai (vi du thanh guom co MeshCollider) va bo qua player - player la muc tieu
        /// chu khong phai vat can. Nho vay <see cref="ObstacleMask"/> chi can liet ke layer cua moi truong;
        /// neu lo de layer Player trong mask thi tia se dung ngay tren nguoi player va quai thanh "mu".
        /// </summary>
        private bool IsLineOfSightBlocked(Transform self, Vector3 origin, Vector3 direction, float distance)
        {
            int count = Physics.RaycastNonAlloc(origin, direction, _lineOfSightHits, distance,
                ObstacleMask, QueryTriggerInteraction.Ignore);

            for (int i = 0; i < count; i++)
            {
                Transform hit = _lineOfSightHits[i].collider.transform;

                if (hit == self || hit.IsChildOf(self)) continue;
                if (_playerTransform != null &&
                    (hit == _playerTransform || hit.IsChildOf(_playerTransform))) continue;

                return true;
            }

            return false;
        }

        private void SamplePlayerMotion()
        {
            float walkSpeed = _playerController != null ? _playerController.walkSpeed : 5f;
            float runSpeed = _playerController != null ? _playerController.runSpeed : RunSpeedReference;
            RunSpeedReference = runSpeed;

            // Van toc ngang cua player: dung cho ca viec do "co dang di khong" lan doan truoc vi tri.
            Vector3 flatVelocity = Vector3.zero;
            bool hasVelocity = _characterController != null && _characterController.enabled;
            if (hasVelocity)
            {
                flatVelocity = _characterController.velocity;
                flatVelocity.y = 0f;
            }
            PlayerVelocity = flatVelocity;

            bool isWalking = false;
            bool isRunning = false;
            if (_playerAnimator != null)
            {
                isWalking = _playerAnimator.GetBool(IsWalkingHash);
                isRunning = _playerAnimator.GetBool(IsRunningHash);
            }
            else
            {
                isWalking = flatVelocity.sqrMagnitude > 0.01f;
            }

            float speed = hasVelocity ? flatVelocity.magnitude : 0f;

            if (speed <= 0.05f)
                speed = isRunning ? runSpeed : (isWalking ? walkSpeed : 0f);
            PlayerSpeed = speed;

            if (isRunning)
                PlayerNoise = NoiseRun;
            else if (isWalking)
                PlayerNoise = NoiseWalk;
            else
                PlayerNoise = 0f;

            PlayerStaminaNorm = _playerController != null && _playerController.maxStamina > 0f
                ? Mathf.Clamp01(_playerController.currentStamina / _playerController.maxStamina)
                : 1f;

            GameObject flashlight = _playerController != null ? _playerController.flashlightSource : null;
            FlashlightOn = flashlight != null && flashlight.activeInHierarchy;
        }

        /// <summary>Do mo hoi gian tiep lay tu PlayerController de tranh phu thuoc vao API private.</summary>
        public float PlayerStaminaNormalized => PlayerStaminaNorm;

        /// <summary>Dung de kiem tra xem quai co dang "ngo" hoan toan khong (khong thay, khong nghe, khong nho).</summary>
        public bool HasNoInformation => !PlayerVisible && !PlayerHeard && !HasLastKnownPosition;

        /// <summary>Khoang thoi gian ke tu lan cuoi co thong tin (da chuan hoa 0..1 theo LostMemorySeconds).</summary>
        public float TimeSinceLastSeenNormalized =>
            Mathf.Clamp01(TimeSinceLastSeen / LostMemorySeconds);
    }
}
