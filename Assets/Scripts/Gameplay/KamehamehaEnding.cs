using System.Collections;
using System.Collections.Generic;
using SilentSprint.Core;
using SilentSprint.Dialogues;
using SilentSprint.UI;
using UnityEngine;

namespace SilentSprint.Gameplay
{
    /// <summary>
    /// Đoạn kết của màn chơi: gom đủ vật phẩm (vật phẩm từ tượng thiên thần và mảnh đá tự do) rồi đi tới
    /// trước cổng là cảnh phim Kamehameha tự chạy — người chơi đứng tụ chiêu, bắn một tia vào cổng,
    /// cổng nổ sập, màn hình mờ dần rồi hiện bảng "phá đảo" và quay về bảng chính.
    ///
    /// Cách dựng: mở scene <b>Gameplay</b> rồi menu <b>Tools ▸ Silent Sprint ▸ Set Up Kamehameha Gate Ending</b>
    /// (xem <c>KamehamehaEndingSetup</c>). Object của script này nằm ở gốc scene; cổng, camera, HUD đều tự dò.
    ///
    /// Ba chỗ cố ý làm khác thường lệ trong project:
    ///  - KHÔNG dùng DOTween: JumpscareController gọi DOTween.KillAll() mỗi lần chơi lại, tween đang chạy
    ///    giữa đoạn kết sẽ chết giữa chừng. Cả đoạn phim tự đếm thời gian thật (unscaledDeltaTime) nên
    ///    bảng tạm dừng có để timeScale = 0 cũng không làm nó đứng hình.
    ///  - Vùng kích hoạt là khoảng cách tới TÂM cổng (giữa cổng vào), không phải trigger collider: cổng nằm trong
    ///    (prefab lồng nhau) nên thêm collider vào đó là phải sửa prefab, mà đoạn kết chỉ cần biết
    ///    "người chơi đang đứng trước cổng".
    ///  - Tư thế nhân vật do code dựng (<see cref="KamehamehaPose"/>) khi ô Charge Pose để trống, vì project
    ///    không có clip tư thế Kamehameha nào. Gắn clip vào ô đó thì clip được ưu tiên, code không đụng tới.
    ///
    /// Ô <b>Giữ Nguyên Góc Máy</b> (<see cref="keepCameraStill"/>) — mặc định BẬT — là chế độ cảnh kết không đụng
    /// tới camera: camera đứng yên đúng góc nó đang có lúc cảnh phim bắt đầu, từ nhịp tụ chiêu tới lúc mờ màn.
    /// Người chơi bị dời ra đứng trước cổng thì camera đi theo (vì là camera của người chơi) nhưng góc nhìn không
    /// đổi. Ba góc máy bên dưới chỉ chạy khi TẮT ô đó.
    ///
    /// Ba góc máy (<see cref="KamehamehaShot"/>), dựng theo kiểu cắt cảnh chứ không nhìn qua vai người chơi:
    ///  1. <b>Tụ chiêu</b> — lệch trái, lùi sau lưng và cao hơn đầu, nhìn xuống theo hướng cổng: thấy trọn người chơi,
    ///     chỗ hai bàn tay khum và cả cánh cổng nằm bên trái khung.
    ///  2. <b>Bắn</b> — cắt sang góc đứng ngang hông: thấy tia phóng ra khỏi khung về phía cổng.
    ///  3. <b>Cổng đổ</b> — cắt sang góc neo vào TRỤC CỦA CỔNG: đứng ở đầu cổng, cao hơn chân cổng, nhìn dọc theo
    ///     cổng xuống chỗ nó sập — thấy trọn cú cổng bật khỏi bản lề rồi nằm xuống. Góc này không lấy theo trục
    ///     người chơi (xem Anchor To Gate) nên người chơi đi tới cổng từ phía nào camera cũng đứng đúng chỗ đó.
    ///
    /// Cảnh kết luôn đứng TRONG map: cả chỗ đứng của người chơi lẫn góc cổng đổ đều lấy phía "trong" theo hướng khu
    /// tường bao ở Map.prefab (xem <see cref="InwardDirection"/>), chứ không lấy theo phía người chơi đang đứng —
    /// đi vòng ra ngoài cổng rồi kích hoạt cũng không đẩy cảnh kết ra ngoài map.
    /// </summary>
    public class KamehamehaEnding : MonoBehaviour
    {
        /// <summary>Đang chạy đoạn kết. Hệ thống khác (ghi bản lưu...) phải đứng ngoài trong lúc này.</summary>
        public static bool IsPlaying { get; private set; }

        [Header("Cổng cần phá")]
        [Tooltip("Cổng sẽ bị Kamehameha phá. Để trống thì tự dò object tên \"Gate\" trong scene.")]
        public Transform gate;

        [Tooltip("Tên object của cổng, dùng khi ô trên để trống. Cổng nằm trong Map.prefab nên gán tay dễ hỏng " +
                 "khi prefab được sửa; tên object là thứ duy nhất còn nhận ra được.")]
        public string gateObjectName = "Gate";

        [Tooltip("Góc cổng đổ khi trúng tia (độ). 90 = nằm hẳn xuống đất.")]
        public float gateFallAngle = 78f;

        [Header("Phía trong map")]
        [Tooltip("Tên object chứa tường bao quanh map (trong Map.prefab). Hướng từ tâm cụm cổng tới tâm khu tường là " +
                 "hướng \"vào trong map\": cảnh kết đặt chỗ đứng người chơi và góc cổng đổ về phía đó. Không tìm " +
                 "thấy tường bao thì người chơi giữ phía đang đứng, còn góc cổng đổ đứng theo trục cổng như thường.")]
        public string mapWallsObjectName = "Walls";

        [Header("Điều kiện kích hoạt")]
        [Tooltip("Phải đủ vật phẩm từ tượng thiên thần (ItemManager: currentQuestItems đạt maxQuestItems).")]
        public bool requireQuestItems = true;

        [Tooltip("Phải đủ mảnh đá tự do (ItemManager: currentFreedomStones đạt maxFreedomStones).")]
        public bool requireFreedomStones = true;

        [Tooltip("Đứng cách TÂM CỔNG (giữa cổng vào) trong bao nhiêu mét thì cảnh phim bắt đầu (m). Đo tới giữa " +
                 "cổng chứ không đo tới mép cụm: cụm cổng dài tới ~86m (hàng rào hai bên cổng vào), đo tới mép thì " +
                 "đứng tận đầu hàng rào cũng kích hoạt được. 6m là vùng kích hoạt nằm gọn trong lòng cổng vào " +
                 "(cửa rộng ~14m), đi qua cổng là kích hoạt.")]
        public float triggerDistance = 6f;

        [Tooltip("Lúc vào cảnh phim, người chơi được dời về đứng cách tâm cổng bao xa cho khung hình đẹp (m). " +
                 "Chỗ đứng luôn nằm về PHÍA TRONG map (xem Map Walls Object Name).")]
        public float standDistance = 7f;

        public float minStandDistance = 4.5f;

        [Header("Nhắc khi chưa đủ điều kiện")]
        [Tooltip("Đứng trước cổng mà chưa đủ vật phẩm thì người bí ẩn nhắc còn thiếu gì.")]
        public bool showLockedHint = true;

        [Tooltip("Hai lần nhắc phải cách nhau ít nhất bao lâu (giây).")]
        public float hintCooldown = 20f;

        [Header("Hiệu ứng Kamehameha")]
        [Tooltip("Prefab hiệu ứng tụ chiêu: kamehameha_charging.prefab (quả cầu + quầng).")]
        public GameObject chargeVfxPrefab;

        [Tooltip("Tên object chứa quả cầu tụ trong prefab hiệu ứng.")]
        public string orbPartName = "Sphere UV 1_0";

        [Tooltip("Tên object chứa quầng trong prefab hiệu ứng (được tắt đi vì nằm lệch hẳn khỏi quả cầu).")]
        public string auraPartName = "Cylinder_1";

        [Tooltip("Hệ số lớn nhất của quả cầu tụ so với cỡ gốc của nó trong prefab. Quả cầu trong prefab " +
                 "to ~1m đường kính, nên 0.45 là quả cầu ~45cm — to hơn nữa là nó nuốt cả người chơi.")]
        public float orbPeakScale = 0.45f;

        [Tooltip("Bán kính tia bắn (m).")]
        public float beamRadius = 0.35f;

        [Tooltip("Cường độ đèn của quả cầu khi tụ xong. Đèn pin trong game đang để 100 để so sánh.")]
        public float chargeLightIntensity = 50f;

        [Tooltip("Cường độ choé sáng ở chỗ cổng bị trúng.")]
        public float impactLightIntensity = 300f;

        [Header("Tư thế nhân vật")]
        [Tooltip("Clip tư thế tụ chiêu. Để trống thì tư thế do code dựng theo hai nhịp bên dưới " +
                 "(Animator bị đóng băng trong lúc đó để nó khỏi ghi đè).")]
        public AnimationClip chargePose;

        [Tooltip("Nhịp TỤ (dùng khi ô Charge Pose để trống): hai tay khum vào nhau ở bên hông phải.")]
        public KamehamehaPoseBeat chargeBeat = new KamehamehaPoseBeat();

        [Tooltip("Nhịp ĐẨY (dùng khi ô Charge Pose để trống): hai tay duỗi ra trước, chạy đúng lúc bắn tia.")]
        public KamehamehaPoseBeat thrustBeat = new KamehamehaPoseBeat
        {
            lean = 18f,
            twist = 0f,
            sideBend = 0f,
            headPitch = 0f,
            hipDrop = 0.04f,
            rightHand = new Vector3(0.10f, 0.28f, 0.60f),
            leftHand = new Vector3(-0.02f, 0.28f, 0.60f),
            rightElbow = new Vector3(0.7f, -0.35f, -0.5f),
            leftElbow = new Vector3(-0.7f, -0.35f, -0.5f),
        };

        [Tooltip("Tốc độ vào dáng tụ (1/giây). Nhỏ = vào dáng chậm và mượt.")]
        public float poseBlendSpeed = 5f;

        [Tooltip("Tốc độ đẩy hai tay ra trước lúc bắn (1/giây). Lớn = giật ra thật nhanh theo tia.")]
        public float poseThrustSpeed = 22f;

        [Header("Góc máy")]
        [Tooltip("Để camera đứng yên đúng góc nó đang có lúc cảnh phim bắt đầu, suốt từ nhịp tụ chiêu tới lúc mờ " +
                 "màn: không cắt sang ba góc máy, không đổi tiêu cự, không rung. Người chơi bị dời ra trước cổng " +
                 "thì camera đi theo nhưng góc nhìn giữ nguyên. Ba góc máy bên dưới chỉ dùng khi TẮT ô này.")]
        public bool keepCameraStill = true;

        [Header("Ép góc máy cố định (Góc lệch 3/4)")]
        [Tooltip("Ép camera về toạ độ cố định bên dưới để không bị lỗi góc nhìn thứ nhất. Chỉ hoạt động nếu BẬT Giữ Nguyên Góc Máy.")]
        public bool forceCustomAngle = true;

        [Tooltip("Nhập Position của Main Camera trong ảnh vào đây")]
        public Vector3 customLocalPosition = new Vector3(1.87f, 1.59f, -1.42f);

        [Tooltip("Nhập Rotation của Main Camera trong ảnh vào đây")]
        public Vector3 customLocalEuler = new Vector3(8.2f, -31.225f, 0f);

        [Tooltip("Camera dùng cho cảnh phim. Để trống thì lấy camera của người chơi.")]
        public Transform cameraTransform;

        [Tooltip("Nhịp TỤ: giữ đúng góc đã ngắm cho camera (lệch trái - sau - cao, nhìn xuống theo hướng cổng). " +
                 "Số mặc định nằm trong KamehamehaShot.ChargeDefault(). Chỉ dùng khi TẮT ô Giữ Nguyên Góc Máy.")]
        public KamehamehaShot chargeShot = KamehamehaShot.ChargeDefault();

        [Tooltip("Nhịp BẮN: đứng ngang hông bên TRÁI người chơi, để cổng vẫn nằm bên trái khung như góc TỤ " +
                 "(không thì cắt cảnh bị nhảy bên). Blend Duration = 0 là cắt thẳng. Chỉ dùng khi TẮT ô " +
                 "Giữ Nguyên Góc Máy.")]
        public KamehamehaShot fireShot = KamehamehaShot.FireDefault();

        [Tooltip("Nhịp CỔNG ĐỔ: neo vào cổng nên cỡ cổng trong khung không đổi dù Stand Distance có chỉnh. " +
                 "Cũng đứng bên TRÁI người chơi cho khớp hai góc kia. Chỉ dùng khi TẮT ô Giữ Nguyên Góc Máy.")]
        public KamehamehaShot impactShot = KamehamehaShot.ImpactDefault();

        /// <summary>
        /// Đặt ba góc máy về bộ số mặc định hiện hành.
        ///
        /// Cần hàm này vì Unity lưu giá trị ĐÃ GHI vào scene: field đã có sẵn trong file scene thì giữ nguyên
        /// giá trị cũ, sửa số mặc định trong code không tự ghi đè (chỉ field MỚI thêm mới lấy số mới). Nên sau
        /// khi code đổi số, phải bấm: chuột phải vào tiêu đề component "KamehamehaEnding" ▸ dòng menu này.
        /// Không đụng tới các ô tham chiếu (hiệu ứng, cổng) nên không phải chạy lại tool cài đặt.
        /// </summary>
        [ContextMenu("Đặt lại ba góc máy theo mặc định")]
        public void ResetShotsToDefaults()
        {
            chargeShot = KamehamehaShot.ChargeDefault();
            fireShot = KamehamehaShot.FireDefault();
            impactShot = KamehamehaShot.ImpactDefault();
        }

        /// <summary>
        /// Đặt lại RIÊNG góc cổng đổ về bộ số mặc định hiện hành.
        ///
        /// Cần hàm này vì Unity giữ giá trị ĐÃ GHI của component đang mở: scene mở từ trước lúc code đổi số thì ô
        /// Impact Shot vẫn là bộ cũ (ví dụ còn bật Auto Frame Gate, fov 48) dù code đã khác — lúc đó cảnh kết vẫn
        /// tự canh khung và đẩy camera ra ngoài map. Lệnh này thay cho việc đặt lại cả ba góc, để không mất số riêng
        /// của góc Tụ chiêu và góc Bắn. Không đụng tới các ô tham chiếu (hiệu ứng, cổng) nên không phải chạy lại
        /// tool cài đặt.
        /// </summary>
        [ContextMenu("Đặt lại góc cổng đổ theo mặc định")]
        public void ResetImpactShotToDefault()
        {
            impactShot = KamehamehaShot.ImpactDefault();
        }

        [Tooltip("Camera rung nhẹ trong lúc tụ chiêu, tăng dần (m).")]
        public float chargeShake = 0.035f;

        [Tooltip("Camera rung mạnh lúc bắn (m).")]
        public float fireShake = 0.22f;

        [Tooltip("Không để camera chui vào tường: có vật cản giữa điểm nhìn và camera thì kéo camera vào sát vật cản.")]
        public bool avoidObstacles = true;

        [Tooltip("Bán kính thân camera khi dò vật cản (m).")]
        public float cameraRadius = 0.3f;

        [Header("Nhịp cảnh phim (giây)")]
        [Tooltip("Tụ chiêu mất bao lâu trước khi bắn.")]
        public float chargeDuration = 2.6f;

        [Tooltip("Từ lúc bắn tới lúc tia trúng cổng.")]
        public float impactDelay = 0.12f;

        [Tooltip("Camera rung sau phát bắn mất bao lâu (choé sáng xảy ra trong khoảng này).")]
        public float fireShakeDuration = 0.8f;

        [Tooltip("Cổng đổ xuống mất bao lâu.")]
        public float gateCollapseDuration = 1.5f;

        [Tooltip("Cổng sập xong thì đứng nhìn thêm bao lâu rồi mới mờ màn.")]
        public float holdAfterCollapse = 0.8f;

        [Tooltip("Tia tắt dần trong bao lâu.")]
        public float beamFadeDuration = 0.4f;

        [Header("Bảng phá đảo")]
        [Tooltip("Scene quay về khi bấm nút ở bảng phá đảo.")]
        public string endingScene = SceneNames.MainMenu;

        [Tooltip("Người nói của thông điệp cuối. Hiện thành dòng trên cùng của bảng (chữ ĐỎ, giống tên người nói " +
                 "trong hộp hội thoại). Mặc định lấy đúng tên người dẫn chuyện của màn chơi " +
                 "(GameplayDialogues.Narrator); để trống thì bảng chỉ còn thông điệp và nút.")]
        public string endingSpeaker = GameplayDialogues.Narrator;

        [Tooltip("Thông điệp cuối của người bí ẩn, hiện ở giữa bảng phá đảo. Đây là lời nhắn cuối trận nên viết dài " +
                 "cũng được — ô chữ tự xuống dòng.")]
        public string endingMessage =
            "Xả stress là cần thiết, nhưng hãy luôn biết điểm dừng: giữ đủ tỉnh táo để tự bảo vệ mình và về nhà " +
            "an toàn giữa đêm khuya, đồng thời giữ lại sức khoẻ để không đánh rơi phong độ cho những mục tiêu " +
            "quan trọng phía trước. Chơi hết mình, nhưng đừng bao giờ vượt quá giới hạn an toàn của chính bạn.";

        [Tooltip("Chữ trên nút ở bảng phá đảo. Tên ô là Button Text (trước đây là Button Label) vì Unity giữ giá " +
                 "trị ĐÃ GHI trong scene: đổi chữ mặc định của một ô đã có trong scene thì phải đổi cả tên ô, " +
                 "không thì bản cũ trong scene vẫn thắng.")]
        public string endingButtonText = "Main Menu";

        [Tooltip("Mờ dần sang màn đen mất bao lâu (giây).")]
        public float endingFadeDuration = 1.1f;

        [Tooltip("Xoá bản lưu khi phá đảo: hết ván rồi thì bảng chính không nên còn nút chơi tiếp.")]
        public bool clearSaveOnEnding = true;

        private HUDController mainHUD;
        private KamehamehaPose pose;
        private Camera cameraComponent;
        private float originalFieldOfView;
        private bool fieldOfViewChanged;
        private Vector3 gateCenter;
        private Bounds gateBounds;
        private bool gateResolved;
        private bool inZone;
        private float nextHintTime;
        private bool compassWasVisible;

        private void Awake()
        {
            ResolveGate();

            if (mainHUD == null) mainHUD = FindAnyObjectByType<HUDController>();
        }

        // Hướng "vào trong map" dò một lần (xem InwardDirection): dựng cảnh kết cần nó ở hai chỗ — chỗ đứng của
        // người chơi và góc cổng đổ — nên không dò lại mỗi lần.
        private bool inwardResolved;
        private Vector3 inwardDirection;

        // Chỗ đứng của góc neo vào cổng chỉ ghi log một lần cho đỡ rác Console (một góc máy hỏi hai lần mỗi nhịp).
        private bool anchoredShotLogged;

        // Góc máy lúc bắt đầu cảnh phim, tính theo trục người chơi (xem CaptureStillPose) — chế độ giữ nguyên
        // góc máy đặt lại đúng góc này mỗi frame.
        private Vector3 stillLocalPosition;
        private Quaternion stillLocalRotation;
        private bool stillPoseCaptured;

        private void Update()
        {
            if (IsPlaying || !gateResolved) return;

            // Đang có hội thoại hay mini game thì người chơi đang bị cất đi chỗ khác, đừng xét gì.
            if (IsBusy())
            {
                inZone = false;
                return;
            }

            PlayerController player = PlayerController.Instance;
            if (player == null || !player.isActiveAndEnabled)
            {
                inZone = false;
                return;
            }

            // Đứng cách TÂM cổng (giữa cổng vào) trong Trigger Distance mét thì cảnh phim bắt đầu. Đo tới giữa
            // cổng chứ không đo tới mép cụm: cụm cổng trong Map.prefab dài tới ~86m (hàng rào hai bên cổng vào),
            // đo tới mép thì người chơi đứng tận đầu hàng rào cũng kích hoạt được cảnh kết.
            if (DistanceToGate(player.transform.position) > triggerDistance)
            {
                // Rời vùng thì lần quay lại sẽ được xét lại (và được nhắc lại điều kiện nếu còn thiếu).
                inZone = false;
                return;
            }

            if (inZone) return;
            inZone = true;

            int missingItems;
            int missingStones;

            if (RequirementsMet(out missingItems, out missingStones))
            {
                Trigger();
                return;
            }

            if (showLockedHint && Time.unscaledTime >= nextHintTime)
            {
                ShowLockedHint(missingItems, missingStones);
            }
        }

        /// <summary>Bắt đầu cảnh phim. Chỉ ăn khi đã dò được cổng và chưa chạy lần nào.</summary>
        public void Trigger()
        {
            if (IsPlaying || !gateResolved) return;

            IsPlaying = true;
            StartCoroutine(PlayRoutine());
        }

        // ------------------------------------------------------------------ cảnh phim

        private IEnumerator PlayRoutine()
        {
            PlayerController player = PlayerController.Instance;
            Transform playerTransform = player != null ? player.transform : null;

            if (playerTransform == null)
            {
                Debug.LogError("[KamehamehaEnding] Không tìm thấy người chơi nên không dựng được cảnh phim.", this);
                Abort();
                yield break;
            }

            // 1. Dọn đường: khoá người chơi, cất HUD, đóng băng quái, thôi ghi bản lưu (từ đây vị trí
            //    người chơi là vị trí dàn dựng, không phải chỗ đứng thật trên map).
            player.enabled = false;
            if (player.movementJoystick != null) player.movementJoystick.ResetInput();
            if (mainHUD != null) mainHUD.gameObject.SetActive(false);

            // Cất la bàn nếu đang mở: la bàn là con của người chơi nên cứ lơ lửng trước mặt suốt đoạn phim.
            compassWasVisible = CompassSystem.Instance != null && CompassSystem.Instance.IsCompassVisible;
            if (compassWasVisible) CompassSystem.Instance.SetCompassVisible(false);

            MonsterAI.PauseAll();
            SetSavingSuspended(true);

            // 2. Đưa người chơi về chỗ đứng trước cổng, quay mặt vào cổng. Cảnh kết là lúc phá cổng để thoát ra nên
            //    người chơi phải đứng TRONG map: lấy phía trong theo hướng khu tường bao chứ không lấy theo phía
            //    người chơi đang đứng — đi vòng ra ngoài cổng rồi kích hoạt thì chỗ đứng vẫn nằm trong sân. Không
            //    dò được tường bao thì đành giữ phía họ đang đứng.
            Vector3 standDirection = InwardDirection();
            bool standInside = standDirection.sqrMagnitude > 0.0001f;

            if (!standInside)
            {
                standDirection = Flat(playerTransform.position - gateCenter);
                if (standDirection.sqrMagnitude < 0.0001f) standDirection = Flat(-gate.forward);
                if (standDirection.sqrMagnitude < 0.0001f) standDirection = Vector3.forward;
                standDirection = standDirection.normalized;
            }

            Vector3 standPosition = gateCenter + standDirection * Mathf.Max(minStandDistance, standDistance);
            standPosition.y = GroundHeight(standPosition, playerTransform.position.y, player.GetComponent<Collider>());

            MovePlayer(playerTransform, standPosition, Quaternion.LookRotation(-standDirection, Vector3.up));

            Debug.Log($"[KamehamehaEnding] Người chơi đứng ở {standPosition} (cách tâm cổng " +
                      $"{Mathf.Max(minStandDistance, standDistance):0.0}m về phía " +
                      (standInside ? "TRONG map)." : "phía họ đang đứng — không dò được tường bao)."), this);

            // 3. Hiệu ứng ở hai bàn tay + tư thế.
            Transform hands = FindHandAnchor(playerTransform);

            var beam = gameObject.AddComponent<KamehamehaBeam>();
            beam.Build(chargeVfxPrefab, hands, orbPartName, auraPartName, orbPeakScale, beamRadius, chargeLightIntensity);

            // Tư thế: có clip Charge Pose thì để Animator lo, không có thì code tự dựng dáng — hai tay khum
            // ở hông rồi đẩy ra trước lúc bắn (xem KamehamehaPose).
            if (!UseChargePoseClip(playerTransform))
            {
                pose = playerTransform.gameObject.AddComponent<KamehamehaPose>();

                if (pose.Initialize(playerTransform, freezeAnimator: true))
                {
                    pose.SetBeat(chargeBeat, poseBlendSpeed);
                }
                else
                {
                    Destroy(pose);
                    pose = null;
                }
            }

            EndingScreenUI ui = EndingScreenUI.GetOrCreate();

            // 4. Góc máy. Hai chế độ:
            //    - Giữ nguyên góc máy (mặc định): chụp lại góc camera đang có lúc này (theo trục người chơi) rồi
            //      đặt lại suốt cảnh phim. Camera là con của người chơi nên nó đi theo khi người chơi được dời ra
            //      trước cổng, nhưng góc nhìn thì không đổi — đúng chỗ người chơi đã ngắm sẵn.
            //    - Ba góc máy dựng sẵn: tụ chiêu → bắn → cổng đổ, có cắt cảnh, tiến/lùi và rung như cũ.
            ResolveCamera(playerTransform);

            Vector3 muzzle = CurrentHands(hands, playerTransform.position + Vector3.up * 1.3f);

            Vector3 chargeStart = Vector3.zero;
            Vector3 chargeEnd = Vector3.zero;
            Quaternion chargeRotation = Quaternion.identity;

            if (keepCameraStill)
            {
                CaptureStillPose(playerTransform);
            }
            else
            {
                Vector3 cameraFrom = cameraTransform != null ? cameraTransform.position : Vector3.zero;
                Quaternion cameraRotationFrom = cameraTransform != null ? cameraTransform.rotation : Quaternion.identity;

                Vector3 chargeLookAt = LookTarget(chargeShot, muzzle, playerTransform);
                chargeStart = ResolveShotPosition(chargeShot, playerTransform, 0f, chargeLookAt);
                chargeEnd = ResolveShotPosition(chargeShot, playerTransform, 1f, chargeLookAt);
                chargeRotation = SafeLookRotation(chargeLookAt - chargeStart);

                ApplyShot(chargeShot);
                yield return MoveCamera(cameraTransform, cameraFrom, cameraRotationFrom, chargeStart, chargeRotation, chargeShot.blendDuration);
            }

            // 5. Tụ chiêu: quả cầu to dần, đèn sáng dần; riêng góc máy thì rung nhẹ và bám theo hai bàn tay đang
            //    nhập dáng — chỉ khi không bật ô Giữ Nguyên Góc Máy.
            float chargeTimer = 0f;

            while (chargeTimer < chargeDuration)
            {
                chargeTimer += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(chargeTimer / Mathf.Max(0.01f, chargeDuration));

                beam.SetOrbScale(k);

                if (keepCameraStill)
                {
                    // Đặt lại mỗi frame: quả cầu, tư thế hay tween nào khác có đẩy camera đi thì góc vẫn y nguyên.
                    HoldStillPose(playerTransform);
                }
                else if (cameraTransform != null)
                {
                    Vector3 lookAt = LookTarget(chargeShot, CurrentHands(hands, muzzle), playerTransform);
                    Vector3 basePosition = Vector3.Lerp(chargeStart, chargeEnd, k);

                    cameraTransform.position = basePosition + Random.insideUnitSphere * (chargeShake * k);
                    cameraTransform.rotation = SafeLookRotation(lookAt - basePosition);
                }

                yield return null;
            }

            // 6. Bắn: tư thế đẩy hai tay ra trước, tia xuất hiện tức thì, camera cắt sang góc đứng ngang (góc máy
            //    cắt hay không là tuỳ ô Giữ Nguyên Góc Máy).
            if (pose != null) pose.SetBeat(thrustBeat, poseThrustSpeed);

            muzzle = CurrentHands(hands, muzzle);

            // Tia nhắm vào PHẦN CỔNG GẦN NGƯỜI CHƠI NHẤT — cũng chính là phần camera quay ở góc cổng đổ.
            // Nhắm vào tâm hộp bao cả cụm (có hàng rào dài và vòm tường cong) thì tia bay vào khoảng không,
            // lệch hẳn khỏi chỗ camera đang nhìn.
            Vector3 impactPoint = NearestGateBounds(playerTransform.position).center;
            beam.AimBeam(muzzle, impactPoint);

            // Tiếng bắn vang lên đúng lúc tia xuất hiện.
            AudioManager.PlayKamehameha();

            // Nhịp tia lao tới cổng và nhịp cổng đổ chạy y như cũ, chỉ khác là giữ nguyên góc máy thì camera
            // không đổi chỗ trong hai nhịp đó.
            if (keepCameraStill)
            {
                yield return Wait(impactDelay);
            }
            else
            {
                Vector3 fireLookAt = LookTarget(fireShot, muzzle, playerTransform);
                Vector3 fireStart = ResolveShotPosition(fireShot, playerTransform, 0f, fireLookAt);
                Vector3 fireEnd = ResolveShotPosition(fireShot, playerTransform, 1f, fireLookAt);
                Quaternion fireRotation = SafeLookRotation(fireLookAt - fireStart);

                ApplyShot(fireShot);
                yield return GoToShot(cameraTransform, fireShot, fireStart, fireRotation);

                // 6a. Nhịp ngắn cho tia lao tới cổng.
                yield return ShakeCamera(cameraTransform, fireStart, fireEnd, fireRotation, impactDelay, fireShake, 1f);
            }

            // Góc cổng đổ: neo vào cổng nên cỡ cổng trong khung không đổi. Giữ nguyên góc máy thì bỏ qua nhịp này,
            // nhưng nhịp cổng đổ bên dưới vẫn phải dài đúng ngần ấy giây.
            Vector3 impactStart = Vector3.zero;
            Vector3 impactEnd = Vector3.zero;
            Quaternion impactRotation = Quaternion.identity;

            // 6b. Trúng cổng: cắt sang góc neo vào cổng để thấy nó bật khỏi bản lề, kèm choé sáng.
            if (!keepCameraStill)
            {
                Vector3 impactLookAt = LookTarget(impactShot, muzzle, playerTransform);
                impactStart = ResolveShotPosition(impactShot, playerTransform, 0f, impactLookAt);
                impactEnd = ResolveShotPosition(impactShot, playerTransform, 1f, impactLookAt);
                impactRotation = SafeLookRotation(impactLookAt - impactStart);

                ApplyShot(impactShot);
                yield return GoToShot(cameraTransform, impactShot, impactStart, impactRotation);
            }

            // Nhịp cổng đổ dài hơn nhịp rung nên cho chạy song song.
            Coroutine collapse = StartCoroutine(CollapseGate(playerTransform));
            beam.SpawnImpact(impactPoint, impactLightIntensity);

            if (keepCameraStill)
                yield return Wait(fireShakeDuration);
            else
                yield return ShakeCamera(cameraTransform, impactStart, impactEnd, impactRotation, fireShakeDuration, fireShake * 0.7f, 0.7f);

            yield return collapse;

            yield return Wait(holdAfterCollapse);

            // 7. Tắt hiệu ứng rồi mờ dần sang bảng phá đảo. Hết game ở đây, không trả quyền điều khiển lại.
            yield return beam.FadeOut(beamFadeDuration);
            beam.Dispose();

            if (pose != null)
            {
                pose.Release();
                pose = null;
            }

            // Chốt lại góc máy: giữ nguyên thì trả về đúng góc đã chụp, còn không thì đứng yên ở góc cổng đổ.
            if (cameraTransform != null)
            {
                if (keepCameraStill) HoldStillPose(playerTransform);
                else cameraTransform.SetPositionAndRotation(impactEnd, impactRotation);
            }

            RestoreFieldOfView();

            ui.ShowEnding(endingSpeaker, endingMessage, endingButtonText, endingFadeDuration,
                          clearSaveOnEnding, endingScene);
        }

        /// <summary>Cổng đổ về phía xa người chơi, quay quanh mép dưới cho giống bị đánh bật khỏi bản lề.</summary>
        private IEnumerator CollapseGate(Transform playerTransform)
        {
            if (gate == null) yield break;

            Vector3 fallDirection = Flat(gateCenter - playerTransform.position).normalized;
            if (fallDirection.sqrMagnitude < 0.0001f) fallDirection = Vector3.forward;

            // Bản lề đặt ở chân cổng: quay quanh mép dưới thì cổng đổ chứ không xoay tại chỗ.
            var pivotObject = new GameObject("Gate_Collapse_Pivot");
            Transform pivot = pivotObject.transform;
            pivot.position = new Vector3(gateCenter.x, gateBounds.min.y, gateCenter.z);
            pivot.rotation = Quaternion.identity;

            gate.SetParent(pivot, true);

            Quaternion from = pivot.rotation;
            Quaternion to = Quaternion.AngleAxis(gateFallAngle, Vector3.Cross(Vector3.up, fallDirection)) * from;

            float elapsed = 0f;
            while (elapsed < gateCollapseDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / Mathf.Max(0.01f, gateCollapseDuration)));
                pivot.rotation = Quaternion.Slerp(from, to, k);
                yield return null;
            }

            pivot.rotation = to;
            gate.gameObject.SetActive(false);

            Destroy(gate.gameObject);
            Destroy(pivotObject);
        }

        // ------------------------------------------------------------------ camera

        /// <summary>Lấy camera của người chơi (và tiêu cự gốc để trả lại sau). Gọi lúc chạy, không phải Awake.</summary>
        private void ResolveCamera(Transform playerTransform)
        {
            if (cameraTransform == null)
            {
                PlayerController player = PlayerController.Instance;
                if (player != null) cameraTransform = player.playerCamera;
            }

            if (cameraTransform == null)
            {
                Debug.LogWarning("[KamehamehaEnding] Không tìm thấy camera nên cảnh phim sẽ không đổi góc máy.", this);
                return;
            }

            cameraComponent = cameraTransform.GetComponent<Camera>();
            if (cameraComponent != null) originalFieldOfView = cameraComponent.fieldOfView;
        }

        /// <summary>
        /// Chụp lại góc camera đang có, hoặc ép nó về góc lệch đã setup trong Inspector.
        ///
        /// Tính theo trục người chơi chứ không lấy toạ độ thế giới: cảnh phim dời người chơi ra đứng trước cổng,
        /// chụp theo toạ độ thế giới thì camera ở lại chỗ cũ và người chơi đi mất khỏi khung. Theo trục người chơi
        /// thì camera đi theo, còn góc nhìn so với người chơi thì giữ nguyên đúng như lúc bắt đầu.
        /// </summary>
        private void CaptureStillPose(Transform playerTransform)
        {
            if (cameraTransform == null || playerTransform == null) return;

            if (forceCustomAngle)
            {
                // Sử dụng chính xác toạ độ trong ảnh của bạn
                stillLocalPosition = customLocalPosition;
                stillLocalRotation = Quaternion.Euler(customLocalEuler);
            }
            else
            {
                // Lấy toạ độ ngẫu nhiên lúc kích hoạt
                stillLocalPosition = playerTransform.InverseTransformPoint(cameraTransform.position);
                stillLocalRotation = Quaternion.Inverse(playerTransform.rotation) * cameraTransform.rotation;
            }

            stillPoseCaptured = true;
        }

        /// <summary>
        /// Đặt camera về đúng góc đã chụp lúc đầu (xem <see cref="CaptureStillPose"/>). Gọi mỗi frame trong lúc
        /// chạy cảnh phim: camera là con của người chơi nên tự đi theo, nhưng vẫn đặt lại cho chắc — tween hạ cánh
        /// của PlayerController hay script khác có đụng vào camera thì góc cũng không lệch.
        /// </summary>
        private void HoldStillPose(Transform playerTransform)
        {
            if (!stillPoseCaptured || cameraTransform == null || playerTransform == null) return;

            cameraTransform.SetPositionAndRotation(
                playerTransform.TransformPoint(stillLocalPosition),
                playerTransform.rotation * stillLocalRotation);
        }

        /// <summary>Sát trong bao nhiêu mét thì coi là "cùng một phần cổng" khi gom phần cổng gần người chơi.</summary>
        private const float NearGateSlack = 3f;

        // Khung đã canh thì nhớ lại: hình học của cảnh phim không đổi từ lúc dàn cảnh, mà mỗi frame phải hỏi
        // cả chỗ đứng lẫn điểm nhìn nên không nhớ thì tính hai lần một frame.
        private bool gateFramingDone;
        private Vector3 gateFramingPosition;
        private Vector3 gateFramingLookAt;

        /// <summary>
        /// Điểm nhìn của một góc máy: mặc định từ hai bàn tay (lệch thêm theo ô Look Offset) tới tâm cổng, còn góc
        /// neo vào trục cổng thì ngắm theo trục cổng (ô Gate Look Offset) — có vậy điểm nhìn mới đứng nguyên một
        /// chỗ cùng với chỗ đứng của camera.
        /// </summary>
        private Vector3 LookTarget(KamehamehaShot shot, Vector3 hands, Transform playerTransform)
        {
            if (shot.autoFrameGate)
            {
                Vector3 position;
                Vector3 lookAt;
                ResolveAutoGateFraming(shot, playerTransform, out position, out lookAt);
                return lookAt;
            }

            if (shot.anchorToGate) return GatePoint(shot.gateLookOffset);

            Vector3 focus = hands + playerTransform.right * shot.lookOffset.x
                                  + Vector3.up * shot.lookOffset.y
                                  + playerTransform.forward * shot.lookOffset.z;

            return Vector3.Lerp(focus, gateCenter, Mathf.Clamp01(shot.lookAtGate));
        }

        /// <summary>
        /// Chỗ đứng của camera cho một góc máy. <paramref name="progress"/> = nhịp tiến/lùi đã chạy được bao xa
        /// (0 = vừa vào góc, 1 = hết nhịp). Góc tự canh khung và góc neo vào trục cổng thì đứng yên, không tiến/lùi.
        /// </summary>
        private Vector3 ResolveShotPosition(KamehamehaShot shot, Transform playerTransform, float progress, Vector3 lookTarget)
        {
            if (shot.autoFrameGate)
            {
                Vector3 framedPosition;
                Vector3 framedLookAt;
                ResolveAutoGateFraming(shot, playerTransform, out framedPosition, out framedLookAt);
                return avoidObstacles ? AvoidObstacles(framedPosition, framedLookAt, playerTransform) : framedPosition;
            }

            if (shot.anchorToGate)
            {
                Vector3 anchoredPosition = GatePoint(shot.gateOffset);

                // Ghi ra một lần: đây là mấy con số quyết định khung hình góc cổng đổ, và cũng là cách biết chắc
                // code mới đã được biên dịch hay chưa (bản cũ ghi log "Góc cổng đổ: ... camera lùi ...m" của cơ
                // chế tự canh khung).
                if (!anchoredShotLogged)
                {
                    anchoredShotLogged = true;
                    Debug.Log($"[KamehamehaEnding] Góc cổng đổ (neo vào cổng): camera ở {anchoredPosition} — " +
                              $"dọc cổng {shot.gateOffset.x:0.0}m, cao {shot.gateOffset.y:0.0}m trên chân cổng, " +
                              $"{(shot.gateOffset.z < 0f ? "lệch ra ngoài" : "ăn vào trong")} {Mathf.Abs(shot.gateOffset.z):0.0}m; " +
                              $"ngắm {lookTarget}, fov {shot.fieldOfView:0}°.", this);
                }

                return avoidObstacles ? AvoidObstacles(anchoredPosition, lookTarget, playerTransform) : anchoredPosition;
            }

            float k = Mathf.Clamp01(progress);
            Vector3 offset = shot.offset * Mathf.Lerp(1f, 1f + shot.dolly, k);

            Vector3 origin = playerTransform.position;
            Vector3 horizontal = playerTransform.right * offset.x + playerTransform.forward * offset.z;

            if (shot.fromGateBack > 0f)
            {
                // Góc máy neo vào CỔNG: lùi về phía người chơi đúng khoảng đã đặt, nên Stand Distance có đổi
                // thì cỡ cổng trong khung vẫn nguyên. Lúc này chỉ dùng x của Offset để lệch ngang.
                origin = gateCenter + Flat(playerTransform.position - gateCenter).normalized * shot.fromGateBack;
                horizontal = playerTransform.right * offset.x;
            }

            Vector3 position = origin + horizontal + Vector3.up * (offset.y + shot.rise * k);

            return avoidObstacles ? AvoidObstacles(position, lookTarget, playerTransform) : position;
        }

        /// <summary>
        /// Một điểm viết theo TRỤC CỦA CỔNG (x = dọc theo cổng, y = cao lên, z = ăn vào trong map) đổi ra toạ độ thế
        /// giới. Dùng cho cả chỗ đứng lẫn điểm nhìn của góc neo vào cổng.
        ///
        /// Gốc object của cổng (Gate.prefab) nằm ở chân cổng, giữa cụm, nên y tính từ mặt đất chân cổng — đổi chỗ gốc
        /// trong prefab thì phải canh lại số trong Gate Offset / Gate Look Offset.
        /// </summary>
        private Vector3 GatePoint(Vector3 gateOffset)
        {
            return gate.position
                 + gate.right * gateOffset.x         // dọc theo cổng
                 + gate.forward * gateOffset.y         // trục đứng của cổng (cổng dựng theo trục đứng của map)
                 + GateSideAxis() * gateOffset.z;      // về phía trong map
        }

        /// <summary>
        /// Trục ngang của cổng, chiều dương chỉ về phía TRONG map (xem <see cref="InwardDirection"/>). Trục này có
        /// hai chiều nên phải chọn theo hướng khu tường bao; không dò được tường bao thì lấy chiều gốc của cổng.
        /// </summary>
        private Vector3 GateSideAxis()
        {
            Vector3 side = gate.up;
            Vector3 inward = InwardDirection();
            if (inward.sqrMagnitude > 0.0001f && Vector3.Dot(side, inward) < 0f) side = -side;

            return side;
        }

        /// <summary>
        /// Góc máy tự canh theo cổng, dùng cho nhịp cổng đổ.
        ///
        /// Camera đứng NGANG trục người chơi–cổng chứ không đứng trên trục đó: đứng trên trục thì cổng đổ
        /// thẳng về phía camera, bị co ngắn lại nên nhìn không ra cú đổ. Đứng ngang thì cổng đổ ngang qua khung.
        ///
        /// Khoảng lùi không tính bằng công thức đóng cho từng trục: cổng rộng nên mép gần của cổng gần camera
        /// hơn mép xa, cùng một độ lệch mà ra góc khác nhau. Thay vào đó dò dần (xem <see cref="FitDistance"/>)
        /// cho tới khi mọi điểm bắt buộc thấy đều nằm trong khung — đúng cho mọi cỡ cổng và mọi tỉ lệ màn hình.
        ///
        /// Chỉ lấy phần cổng GẦN NGƯỜI CHƠI NHẤT: cụm cổng còn có đoạn hàng rào dài và vòm tường cong vươn xa,
        /// canh theo cả cụm thì camera bị đẩy ra rất xa và cổng chỉ còn một chấm trong khung.
        /// </summary>
        private void ResolveAutoGateFraming(KamehamehaShot shot, Transform playerTransform, out Vector3 position, out Vector3 lookAt)
        {
            if (gateFramingDone)
            {
                position = gateFramingPosition;
                lookAt = gateFramingLookAt;
                return;
            }

            Vector3 fallDirection = Flat(gateCenter - playerTransform.position).normalized;
            if (fallDirection.sqrMagnitude < 0.0001f) fallDirection = Vector3.forward;

            Bounds chunk = NearestGateBounds(playerTransform.position);
            float chunkHeight = Mathf.Max(1f, chunk.size.y);

            Vector3 chest = playerTransform.position + Vector3.up * 1f;
            Vector3 head = playerTransform.position + Vector3.up * 1.8f;

            // Chỗ cổng nằm xuống: cổng cao bao nhiêu thì nằm xuống với ra xa ngần ấy.
            Vector3 landing = chunk.center + fallDirection * (chunkHeight * 0.7f);

            float fov = shot.fieldOfView > 0f
                ? shot.fieldOfView
                : cameraComponent != null ? cameraComponent.fieldOfView : 60f;

            float tanVertical = Mathf.Tan(Mathf.Deg2Rad * Mathf.Clamp(fov, 10f, 120f) * 0.5f);
            float aspect = Screen.height > 0 ? (float)Screen.width / Screen.height : 16f / 9f;
            float tanHorizontal = tanVertical * Mathf.Max(0.2f, aspect);
            float margin = Mathf.Max(1f, shot.gateFramingMargin);

            // Tâm khung theo bề DỌC luôn lấy đúng tâm phần cổng: ngắm thấp hơn tâm cổng thì đỉnh cổng bị cắt.
            float centerY = chunk.center.y;

            Vector3 lookAtAll = new Vector3((chest.x + landing.x) * 0.5f, centerY, (chest.z + landing.z) * 0.5f);
            Vector3 lookAtGate = new Vector3((chunk.center.x + landing.x) * 0.5f, centerY, (chunk.center.z + landing.z) * 0.5f);

            // Camera nhô lên trên tâm một chút để nhìn xuống cú cổng đổ, nhưng không lọt xuống dưới mặt đất.
            float ground = GroundHeight(lookAtAll, centerY, playerTransform.GetComponent<Collider>());
            float height = Mathf.Max(centerY + chunkHeight * 0.15f, ground + 1.6f);
            Vector3 heightOffset = Vector3.up * (height - centerY);

            // Đứng bên trái hay bên phải người chơi tuỳ dấu Offset.x (mặc định âm = bên trái, khớp hai góc kia).
            Vector3 sideways = playerTransform.right * (shot.offset.x > 0f ? 1f : -1f);

            Vector3[] gatePoints = GateCornerPoints(chunk);

            Vector3[] scenePoints = new Vector3[gatePoints.Length + 3];
            gatePoints.CopyTo(scenePoints, 0);
            scenePoints[gatePoints.Length] = chest;
            scenePoints[gatePoints.Length + 1] = head;
            scenePoints[gatePoints.Length + 2] = landing;

            Vector3[] gateOnlyPoints = new Vector3[gatePoints.Length + 1];
            gatePoints.CopyTo(gateOnlyPoints, 0);
            gateOnlyPoints[gatePoints.Length] = landing;

            float distanceAll = FitDistance(lookAtAll, sideways, heightOffset, scenePoints, margin, tanHorizontal, tanVertical);
            float distanceGate = FitDistance(lookAtGate, sideways, heightOffset, gateOnlyPoints, margin, tanHorizontal, tanVertical);

            // Hai cách canh khung, chọn cách nào đỡ phải lùi xa hơn hẳn:
            //  - "cả cảnh": người chơi ở một đầu, cổng và chỗ nó nằm xuống ở đầu kia;
            //  - "chỉ cổng": bỏ người chơi ra, chặt khung vào cổng. Màn hình dọc phải lùi rất xa mới chứa cả
            //    cảnh, lúc đó cổng chỉ còn một chấm nên thà chặt vào cổng.
            bool includePlayer = distanceAll <= distanceGate * 1.35f;
            lookAt = includePlayer ? lookAtAll : lookAtGate;
            float distance = includePlayer ? distanceAll : distanceGate;

            position = lookAt + sideways * distance + heightOffset;

            // Ghi ra một lần, ngay lúc canh khung: đây chính là mấy con số quyết định khung hình góc cổng đổ,
            // nên muốn chỉnh rộng/chặt thì nhìn đây (hoặc chỉnh Gate Framing Margin rồi xem lại).
            Debug.Log($"[KamehamehaEnding] Góc cổng đổ: phần cổng gần người chơi cao {chunkHeight:0.0}m rộng {chunk.size.x:0.0}m, " +
                      $"khung {(includePlayer ? "cả người chơi lẫn cổng" : "chỉ cổng (màn hẹp)")}, camera lùi {distance:0.0}m, " +
                      $"cao {height:0.0}m, fov {fov:0}°.", this);

            gateFramingPosition = position;
            gateFramingLookAt = lookAt;
            gateFramingDone = true;
        }

        /// <summary>Số lần nới gấp đôi tối đa khi dò khoảng lùi (1 → 2 → 4 … nới 40 lần là quá thừa).</summary>
        private const int FitDoubleSteps = 40;

        /// <summary>Số lần chia đôi để tìm khoảng lùi sát ngưỡng (24 lần là chính xác tới dưới phần triệu).</summary>
        private const int FitBisectSteps = 24;

        /// <summary>
        /// Khoảng lùi nhỏ nhất để mọi điểm trong <paramref name="points"/> nằm gọn trong khung (còn lề
        /// <paramref name="margin"/>), khi camera đứng lệch theo <paramref name="sideways"/> và nhìn vào
        /// <paramref name="lookAt"/>.
        ///
        /// Dò thay vì tính đóng: chiếu một điểm vào khung phụ thuộc cả độ lệch ngang lẫn khoảng cách tới
        /// camera, mà cổng thì rộng — mép gần và mép xa của cùng một cánh cổng cho góc khác nhau, nên công
        /// thức theo từng trục sẽ hoặc cắt mất mép, hoặc lùi xa quá mức. Trước hết nới gấp đôi cho tới khi
        /// vào khung, rồi chia đôi khoảng cuối để tìm sát ngưỡng — đúng cho mọi cỡ cổng và mọi tỉ lệ màn hình.
        /// </summary>
        private static float FitDistance(Vector3 lookAt, Vector3 sideways, Vector3 heightOffset, Vector3[] points,
                                        float margin, float tanHorizontal, float tanVertical)
        {
            float limit = 1f / Mathf.Max(1f, margin);

            float fits = 1f;
            float tooClose = 0f;

            // 1. Nới gấp đôi cho tới khi mọi điểm đã vào khung.
            for (int step = 0; step < FitDoubleSteps; step++)
            {
                if (FrameUsage(lookAt, sideways, heightOffset, fits, points, tanHorizontal, tanVertical) <= limit) break;

                tooClose = fits;
                fits *= 2f;

                // Hình học lạ thì đành lấy khoảng đã nới, còn hơn trả về con số vô nghĩa.
                if (step == FitDoubleSteps - 1) return fits;
            }

            // 2. Chia đôi khoảng cuối cho tới khi sát ngưỡng.
            for (int step = 0; step < FitBisectSteps; step++)
            {
                float middle = (tooClose + fits) * 0.5f;

                if (FrameUsage(lookAt, sideways, heightOffset, middle, points, tanHorizontal, tanVertical) <= limit)
                    fits = middle;
                else
                    tooClose = middle;
            }

            return fits;
        }

        /// <summary>Phần nửa khung mà điểm lệch nhiều nhất đang dùng (1 = chạm mép, lớn hơn = đã ra ngoài).</summary>
        private static float FrameUsage(Vector3 lookAt, Vector3 sideways, Vector3 heightOffset, float distance,
                                       Vector3[] points, float tanHorizontal, float tanVertical)
        {
            Vector3 position = lookAt + sideways * distance + heightOffset;
            Quaternion rotation = SafeLookRotation(lookAt - position);
            Vector3 forward = rotation * Vector3.forward;
            Vector3 right = rotation * Vector3.right;
            Vector3 up = rotation * Vector3.up;

            float used = 0f;

            foreach (Vector3 point in points)
            {
                Vector3 toPoint = point - position;
                float depth = Vector3.Dot(toPoint, forward);
                if (depth <= 0.05f) return float.MaxValue;

                float x = Mathf.Abs(Vector3.Dot(toPoint, right)) / depth / Mathf.Max(0.05f, tanHorizontal);
                float y = Mathf.Abs(Vector3.Dot(toPoint, up)) / depth / Mathf.Max(0.05f, tanVertical);
                used = Mathf.Max(used, x, y);
            }

            return used;
        }

        /// <summary>Tám đỉnh của hộp bao — dùng để canh khung theo đúng hình dạng cổng, không chỉ tâm.</summary>
        private static Vector3[] GateCornerPoints(Bounds bounds)
        {
            Vector3 min = bounds.min;
            Vector3 max = bounds.max;

            return new[]
            {
                new Vector3(min.x, min.y, min.z), new Vector3(min.x, min.y, max.z),
                new Vector3(min.x, max.y, min.z), new Vector3(min.x, max.y, max.z),
                new Vector3(max.x, min.y, min.z), new Vector3(max.x, min.y, max.z),
                new Vector3(max.x, max.y, min.z), new Vector3(max.x, max.y, max.z),
            };
        }

        /// <summary>
        /// Hộp bao của phần cổng gần người chơi nhất (gom cả những phần nằm sát phần gần nhất, cách nhau không
        /// quá <see cref="NearGateSlack"/>). Nhờ vậy camera không bị đoạn hàng rào dài của cụm cổng kéo ra xa.
        /// </summary>
        private Bounds NearestGateBounds(Vector3 from)
        {
            if (gate == null) return gateBounds;

            Renderer[] renderers = gate.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) return gateBounds;

            float nearest = float.MaxValue;
            foreach (Renderer renderer in renderers)
            {
                if (renderer == null || !renderer.gameObject.activeInHierarchy) continue;

                float distance = (renderer.bounds.ClosestPoint(from) - from).sqrMagnitude;
                if (distance < nearest) nearest = distance;
            }

            if (nearest == float.MaxValue) return gateBounds;

            float limit = nearest + NearGateSlack * NearGateSlack;
            bool hasBounds = false;
            Bounds combined = gateBounds;

            foreach (Renderer renderer in renderers)
            {
                if (renderer == null || !renderer.gameObject.activeInHierarchy) continue;

                float distance = (renderer.bounds.ClosestPoint(from) - from).sqrMagnitude;
                if (distance > limit) continue;

                if (!hasBounds)
                {
                    combined = renderer.bounds;
                    hasBounds = true;
                }
                else
                {
                    combined.Encapsulate(renderer.bounds);
                }
            }

            return hasBounds ? combined : gateBounds;
        }

        /// <summary>
        /// Không để camera chui vào tường: có vật cản giữa điểm nhìn và camera thì kéo camera vào sát vật cản.
        /// Bỏ qua chính nhân vật, vì tia xuất phát từ trong người họ nên lúc nào cũng trúng capsule của họ, và bỏ
        /// qua CỔNG: cổng là chủ thể của cảnh phim nên có nằm giữa camera với điểm nhìn cũng không tính là vật cản
        /// — không thì tia từ tâm cổng ra camera đụng chính cột cổng và camera bị kéo về sát cổng.
        /// </summary>
        private Vector3 AvoidObstacles(Vector3 position, Vector3 lookTarget, Transform playerTransform)
        {
            Vector3 toCamera = position - lookTarget;
            float distance = toCamera.magnitude;
            if (distance < 0.2f) return position;

            Vector3 direction = toCamera / distance;
            float radius = Mathf.Max(0.05f, cameraRadius);

            RaycastHit[] hits = Physics.SphereCastAll(lookTarget, radius, direction, distance,
                                                      ~0, QueryTriggerInteraction.Ignore);

            float nearest = distance;
            foreach (RaycastHit hit in hits)
            {
                if (hit.collider == null) continue;
                if (hit.collider.transform.root == playerTransform.root) continue;
                if (gate != null && hit.collider.transform.IsChildOf(gate)) continue;

                float surface = Mathf.Max(0.2f, hit.distance - radius);
                if (surface < nearest) nearest = surface;
            }

            return lookTarget + direction * nearest;
        }

        /// <summary>Sang góc máy mới: cắt thẳng nếu Blend Duration = 0, còn không thì lướt sang cho mượt.</summary>
        private IEnumerator GoToShot(Transform cameraTransform, KamehamehaShot shot, Vector3 position, Quaternion rotation)
        {
            if (cameraTransform == null) yield break;

            if (shot.blendDuration > 0f)
            {
                yield return MoveCamera(cameraTransform, cameraTransform.position, cameraTransform.rotation,
                                        position, rotation, shot.blendDuration);
                yield break;
            }

            cameraTransform.SetPositionAndRotation(position, rotation);
        }

        /// <summary>Đổi tiêu cự sang tiêu cự của góc máy mới (0 = giữ nguyên tiêu cự đang dùng).</summary>
        private void ApplyShot(KamehamehaShot shot)
        {
            if (cameraComponent == null || shot.fieldOfView <= 0f) return;

            fieldOfViewChanged = true;
            cameraComponent.fieldOfView = shot.fieldOfView;
        }

        /// <summary>Trả tiêu cự về như cũ (chạy lúc hết cảnh phim hoặc khi phải huỷ giữa chừng).</summary>
        private void RestoreFieldOfView()
        {
            if (!fieldOfViewChanged || cameraComponent == null) return;

            cameraComponent.fieldOfView = originalFieldOfView;
            fieldOfViewChanged = false;
        }

        private static Vector3 CurrentHands(Transform hands, Vector3 fallback)
        {
            return hands != null ? hands.position : fallback;
        }

        /// <summary>LookRotation an toàn: hướng rỗng thì trả về hướng gốc thay vì báo lỗi ra Console.</summary>
        private static Quaternion SafeLookRotation(Vector3 direction)
        {
            return direction.sqrMagnitude < 0.000001f
                ? Quaternion.identity
                : Quaternion.LookRotation(direction, Vector3.up);
        }

        private static IEnumerator MoveCamera(Transform cameraTransform, Vector3 from, Quaternion rotationFrom,
                                              Vector3 to, Quaternion rotationTo, float duration)
        {
            float time = Mathf.Max(0f, duration);

            float elapsed = 0f;
            while (elapsed < time)
            {
                elapsed += Time.unscaledDeltaTime;
                float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / time));

                if (cameraTransform != null)
                {
                    cameraTransform.position = Vector3.Lerp(from, to, k);
                    cameraTransform.rotation = Quaternion.Slerp(rotationFrom, rotationTo, k);
                }

                yield return null;
            }

            if (cameraTransform != null) cameraTransform.SetPositionAndRotation(to, rotationTo);
        }

        /// <summary>
        /// Rung camera rồi tắt dần, đồng thời trôi từ <paramref name="baseFrom"/> sang <paramref name="baseTo"/>
        /// theo nhịp tiến/lùi của góc máy. <paramref name="angleScale"/> = 0 thì chỉ rung vị trí.
        /// </summary>
        private static IEnumerator ShakeCamera(Transform cameraTransform, Vector3 baseFrom, Vector3 baseTo,
                                              Quaternion baseRotation, float duration, float positionAmplitude, float angleScale)
        {
            float time = Mathf.Max(0f, duration);

            float elapsed = 0f;
            while (elapsed < time)
            {
                elapsed += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(elapsed / time);
                float left = 1f - k;

                if (cameraTransform != null)
                {
                    cameraTransform.position = Vector3.Lerp(baseFrom, baseTo, k)
                                             + Random.insideUnitSphere * (positionAmplitude * left);

                    float angle = 6f * angleScale * left;
                    cameraTransform.rotation = baseRotation * Quaternion.Euler(
                        Random.Range(-angle, angle), Random.Range(-angle, angle), Random.Range(-angle, angle));
                }

                yield return null;
            }

            if (cameraTransform != null) cameraTransform.SetPositionAndRotation(baseTo, baseRotation);
        }

        // ------------------------------------------------------------------ dò / dựng

        private void ResolveGate()
        {
            if (gate == null)
            {
                GameObject found = GameObject.Find(gateObjectName);
                if (found != null) gate = found.transform;
            }

            if (gate == null)
            {
                Debug.LogWarning(
                    $"[KamehamehaEnding] Không tìm thấy cổng tên \"{gateObjectName}\" trong scene nên đoạn kết sẽ không chạy. " +
                    "Gán ô Gate hoặc đặt lại tên cho cổng.", this);
                return;
            }

            Renderer[] renderers = gate.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
            {
                Debug.LogWarning("[KamehamehaEnding] Cổng không có renderer nào để đo kích thước nên đoạn kết sẽ không chạy.", this);
                return;
            }

            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                // Chỉ lấy phần đang bật: Map.prefab có thể còn object trang trí tắt sẵn nằm cách xa cổng.
                if (renderers[i].gameObject.activeInHierarchy) bounds.Encapsulate(renderers[i].bounds);
            }

            gateBounds = bounds;
            gateCenter = bounds.center;

            gateResolved = true;

            // Ghi ra cho dễ chỉnh: cỡ cụm cổng quyết định khung hình góc "cổng đổ", còn vùng kích hoạt thì
            // quyết định lúc nào cảnh phim bắt đầu.
            Debug.Log($"[KamehamehaEnding] Cổng \"{gate.name}\": hộp bao cả cụm rộng {bounds.size.x:0.0}m, cao {bounds.size.y:0.0}m, " +
                      $"sâu {bounds.size.z:0.0}m | kích hoạt khi đứng cách TÂM cổng {triggerDistance:0.0}m | " +
                      $"người chơi sẽ đứng cách tâm cụm {Mathf.Max(minStandDistance, standDistance):0.0}m.", this);
        }

        /// <summary>
        /// Hướng "vào trong map" ở chỗ cổng: trên mặt phẳng, từ tâm cụm cổng tới tâm khu tường bao (object
        /// <see cref="mapWallsObjectName"/> trong Map.prefab).
        ///
        /// Có hướng này thì cảnh kết biết đâu là trong map mà đứng, không phải đoán theo phía người chơi đi tới:
        /// người chơi hoàn toàn có thể vòng ra ngoài cổng rồi mới kích hoạt (cổng có khe hở ở hai đầu hàng rào),
        /// lúc đó lấy theo phía người chơi là cảnh kết bị đẩy ra ngoài map.
        /// </summary>
        /// <returns>Hướng đã chuẩn hoá, nằm ngang. <see cref="Vector3.zero"/> nếu không dò được tường bao.</returns>
        private Vector3 InwardDirection()
        {
            if (inwardResolved) return inwardDirection;

            inwardResolved = true;
            inwardDirection = Vector3.zero;

            GameObject walls = string.IsNullOrEmpty(mapWallsObjectName) ? null : GameObject.Find(mapWallsObjectName);
            if (walls == null)
            {
                Debug.LogWarning("[KamehamehaEnding] Không tìm thấy object tường bao \"" + mapWallsObjectName +
                                 "\" nên không biết phía nào là trong map: người chơi sẽ đứng theo phía đang đứng, " +
                                 "góc cổng đổ vẫn neo vào trục cổng.", this);
                return inwardDirection;
            }

            // Tâm khu tường = trung bình tâm hộp bao của từng mảnh tường. Với tường bao kín một vòng thì điểm này
            // nằm gọn trong map, nên hướng từ cổng tới nó chính là hướng vào trong.
            Renderer[] renderers = walls.GetComponentsInChildren<Renderer>(true);
            Vector3 sum = Vector3.zero;
            int count = 0;

            foreach (Renderer renderer in renderers)
            {
                if (renderer == null || !renderer.gameObject.activeInHierarchy) continue;

                sum += renderer.bounds.center;
                count++;
            }

            if (count == 0)
            {
                Debug.LogWarning("[KamehamehaEnding] Object \"" + mapWallsObjectName + "\" không có renderer nào " +
                                 "đang bật nên không đo được hướng vào trong map.", this);
                return inwardDirection;
            }

            Vector3 direction = Flat(sum / count - gateCenter);
            if (direction.sqrMagnitude > 0.0001f) inwardDirection = direction.normalized;

            return inwardDirection;
        }

        /// <summary>
        /// Khoảng cách (trên mặt phẳng) từ một điểm tới TÂM cổng — tức giữa cổng vào.
        ///
        /// Đo tới tâm chứ không đo tới mép cụm cổng: cụm cổng trong Map.prefab dài tới ~86m (cánh cổng chính chỉ
        /// ~14m, còn lại là hàng rào hai bên), đo tới mép thì người chơi đứng tận đầu hàng rào cũng kích hoạt được
        /// cảnh kết. Đo tới giữa cổng thì phải tới sát cổng vào mới kích hoạt, đúng như cảnh phim cần.
        /// </summary>
        private float DistanceToGate(Vector3 position)
        {
            return Flat(position - gateCenter).magnitude;
        }

        /// <summary>
        /// Mốc đặt quả cầu tụ: giữa hai bàn tay. Không thấy xương tay thì gắn tạm trước ngực để hiệu ứng
        /// vẫn có chỗ đứng, và mốc bám theo người chơi nên khỏi phải cập nhật lại.
        /// </summary>
        private static Transform FindHandAnchor(Transform playerTransform)
        {
            Transform left = FindDeep(playerTransform, "LeftHand");
            Transform right = FindDeep(playerTransform, "RightHand");

            Transform host = right != null ? right : left;

            var anchorObject = new GameObject("KamehamehaHands");
            Transform anchor = anchorObject.transform;

            if (host == null)
            {
                anchor.SetParent(playerTransform, false);
                anchor.localPosition = new Vector3(0f, 1.25f, 0.35f);
                anchor.localRotation = Quaternion.identity;
                return anchor;
            }

            anchor.SetParent(host, true);
            anchor.position = left != null && right != null
                ? (left.position + right.position) * 0.5f
                : host.position;

            return anchor;
        }

        /// <summary>
        /// Gắn clip tư thế tụ chiêu vào Animator của người chơi.
        /// </summary>
        /// <returns>true nếu clip đã được gắn (lúc đó không dựng tư thế bằng code nữa).</returns>
        private bool UseChargePoseClip(Transform playerTransform)
        {
            if (chargePose == null) return false;

            Animator animator = playerTransform.GetComponentInChildren<Animator>();
            if (animator == null || animator.runtimeAnimatorController == null)
            {
                Debug.LogWarning(
                    "[KamehamehaEnding] Người chơi không có Animator (hoặc chưa gắn Animator Controller) nên không gắn được clip tư thế tụ chiêu. " +
                    "Sẽ dựng tư thế bằng code thay thế.", this);
                return false;
            }

            // Đổi hết clip trong controller thành clip tư thế: nhân vật đang ở trạng thái nào
            // (Idle/Walking/Run/Jump) cũng ra đúng dáng tụ chiêu, khỏi phải đoán state nào đang chạy.
            var overrideController = new AnimatorOverrideController(animator.runtimeAnimatorController);
            var overrides = new List<KeyValuePair<AnimationClip, AnimationClip>>();
            overrideController.GetOverrides(overrides);

            for (int i = 0; i < overrides.Count; i++)
            {
                overrides[i] = new KeyValuePair<AnimationClip, AnimationClip>(overrides[i].Key, chargePose);
            }

            overrideController.ApplyOverrides(overrides);
            animator.runtimeAnimatorController = overrideController;

            SetBoolIfExists(animator, "isWalking", false);
            SetBoolIfExists(animator, "isRunning", false);
            SetBoolIfExists(animator, "isGrounded", true);

            return true;
        }

        private static void SetBoolIfExists(Animator animator, string parameterName, bool value)
        {
            foreach (AnimatorControllerParameter parameter in animator.parameters)
            {
                if (parameter.name != parameterName || parameter.type != AnimatorControllerParameterType.Bool) continue;

                animator.SetBool(parameterName, value);
                return;
            }
        }

        // ------------------------------------------------------------------ điều kiện / trạng thái

        /// <summary>Đã đủ điều kiện phá cổng chưa, và còn thiếu những gì.</summary>
        private bool RequirementsMet(out int missingItems, out int missingStones)
        {
            missingItems = 0;
            missingStones = 0;

            ItemManager items = ItemManager.Instance;
            if (items == null)
            {
                // Không có bộ đếm vật phẩm (scene dựng thiếu) thì coi như đủ, đoạn kết không nên chết vì chuyện đó.
                return true;
            }

            if (requireQuestItems) missingItems = Mathf.Max(0, items.maxQuestItems - items.currentQuestItems);
            if (requireFreedomStones) missingStones = Mathf.Max(0, items.maxFreedomStones - items.currentFreedomStones);

            return missingItems == 0 && missingStones == 0;
        }

        private void ShowLockedHint(int missingItems, int missingStones)
        {
            DialogueManager dialogue = DialogueManager.Instance;
            if (dialogue == null || dialogue.IsPlaying) return;

            nextHintTime = Time.unscaledTime + Mathf.Max(1f, hintCooldown);
            dialogue.Play(GameplayDialogues.GateLocked(missingItems, missingStones));
        }

        private static bool IsBusy()
        {
            if (DialogueManager.Instance != null && DialogueManager.Instance.IsPlaying) return true;

            MinigameSystemManager minigames = MinigameSystemManager.Instance;
            return minigames != null && minigames.IsMinigameRunning;
        }

        private static void SetSavingSuspended(bool suspended)
        {
            if (GameplaySaveManager.Instance != null) GameplaySaveManager.Instance.suspendSaving = suspended;
        }

        /// <summary>Hủy cảnh phim khi chưa kịp dựng gì, để người chơi còn chơi tiếp được.</summary>
        private void Abort()
        {
            IsPlaying = false;
            inZone = true;

            if (pose != null)
            {
                pose.Release();
                pose = null;
            }

            RestoreFieldOfView();

            // Huỷ giữa chừng thì trả la bàn lại như lúc vào cảnh phim.
            if (compassWasVisible && CompassSystem.Instance != null) CompassSystem.Instance.SetCompassVisible(true);
            compassWasVisible = false;

            PlayerController player = PlayerController.Instance;
            if (player != null) player.enabled = true;

            MonsterAI.ResumeAll();
            SetSavingSuspended(false);
        }

        // ------------------------------------------------------------------ tiện ích

        /// <summary>Bỏ thành phần Y, chỉ giữ hướng trên mặt phẳng.</summary>
        private static Vector3 Flat(Vector3 direction)
        {
            direction.y = 0f;
            return direction;
        }

        /// <summary>Độ cao mặt đất dưới một chỗ, dò bằng tia từ trên xuống; không thấy gì thì trả về <paramref name="fallback"/>.</summary>
        private static float GroundHeight(Vector3 position, float fallback, Collider ignore)
        {
            RaycastHit[] hits = Physics.RaycastAll(new Vector3(position.x, position.y + 4f, position.z),
                                                   Vector3.down, 8f, ~0, QueryTriggerInteraction.Ignore);

            float best = float.NaN;
            foreach (RaycastHit hit in hits)
            {
                // Bỏ qua chính người chơi: họ đang đứng ngay đó nên tia sẽ trúng capsule của họ trước.
                if (ignore != null && hit.collider == ignore) continue;
                if (float.IsNaN(best) || hit.point.y > best) best = hit.point.y;
            }

            return float.IsNaN(best) ? fallback : best;
        }

        /// <summary>Dời người chơi: phải tắt CharacterController trong lúc dời, nếu không nó kéo ngược về chỗ cũ.</summary>
        private static void MovePlayer(Transform playerTransform, Vector3 position, Quaternion rotation)
        {
            CharacterController controller = playerTransform.GetComponent<CharacterController>();
            bool controllerWasEnabled = controller != null && controller.enabled;

            if (controllerWasEnabled) controller.enabled = false;
            playerTransform.SetPositionAndRotation(position, rotation);
            if (controllerWasEnabled) controller.enabled = true;
        }

        /// <summary>Tìm object con theo tên, kể cả object đang tắt.</summary>
        private static Transform FindDeep(Transform root, string name)
        {
            if (root == null || string.IsNullOrEmpty(name)) return null;

            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                if (child.name == name)
                    return child;

            return null;
        }

        private static IEnumerator Wait(float seconds)
        {
            float elapsed = 0f;
            while (elapsed < seconds)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
        }
    }
}