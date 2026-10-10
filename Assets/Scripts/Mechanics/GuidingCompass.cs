using UnityEngine;

namespace SilentSprint.Mechanics
{
    /// <summary>
    /// Chiếc la bàn mà người bí ẩn thả xuống trước mặt người chơi. Component nằm trên chính prefab Compass.
    ///
    /// La bàn là CON CỦA PLAYER: vị trí, góc nghiêng và tỉ lệ đều lấy đúng theo transform đã đặt trong
    /// prefab, nên chỉnh trong Editor là thấy ngay trong game, và người chơi xoay người thì la bàn theo.
    /// Component này chỉ làm ba việc:
    /// - Rơi từ trên trời xuống đúng chỗ đã đặt (<see cref="BeginDrop"/>).
    /// - Lấy ra / cất đi bằng cách trượt lên xuống (<see cref="SetVisible"/>).
    /// - Xoay kim (Needle) về phía mục tiêu (<see cref="SetTarget"/>).
    ///
    /// Hướng trục của kim suy ra từ chính mesh của kim nên không phải căn chỉnh tay: mesh lệch về phía
    /// nào (tâm mesh so với gốc xoay) thì phía đó là mũi kim, còn trục mỏng nhất của mesh chính là trục
    /// kim quay quanh (pháp tuyến mặt số). Asset đổi làm cách suy luận này sai thì ghi đè bằng
    /// needlePointAxis / needleSpinAxis.
    /// </summary>
    public class GuidingCompass : MonoBehaviour
    {
        [Header("Kim la bàn")]
        [Tooltip("Kim của la bàn. Để trống sẽ tự lấy con tên 'Needle'.")]
        public Transform needle;

        [Tooltip("Trục chỉ hướng của kim, trong không gian local của kim. (0,0,0) = tự suy ra từ mesh.")]
        public Vector3 needlePointAxis = Vector3.zero;

        [Tooltip("Trục kim quay quanh, trong không gian local của kim. (0,0,0) = tự suy ra từ mesh.")]
        public Vector3 needleSpinAxis = Vector3.zero;

        [Tooltip("Bật nếu kim chỉ ngược hướng cần chỉ.")]
        public bool invertNeedle;

        [Tooltip("Tốc độ kim xoay, độ mỗi giây.")]
        public float needleTurnSpeed = 540f;

        [Header("Rơi từ trên trời")]
        [Tooltip("Rơi từ bao nhiêu mét phía trên vị trí đã đặt, tính theo trục lên của object cha.")]
        public float dropHeight = 5f;
        public float dropDuration = 1.1f;

        [Header("Bật/tắt")]
        [Tooltip("Thời gian đưa la bàn lên trước mặt, giây.")]
        public float showDuration = 0.28f;

        [Tooltip("Thời gian cất la bàn đi, giây.")]
        public float hideDuration = 0.18f;

        [Tooltip("Lúc lấy ra, la bàn bắt đầu ở dưới vị trí đã đặt bao nhiêu mét rồi đi lên. Cất đi thì đi ngược lại.")]
        public float showRiseHeight = 0.6f;

        [Header("Tư thế")]
        [Tooltip("Xoay mặt số về phía camera mỗi frame, thay vì giữ đúng góc đã đặt trong prefab.")]
        public bool faceCamera;

        private Transform target;

        // Trục của kim, trong không gian local của kim.
        private Vector3 pointAxis = Vector3.forward;
        private Vector3 spinAxis = Vector3.up;
        // Hướng pháp tuyến của mặt đọc (mặt có kim nằm trên), trong không gian local của la bàn.
        private Vector3 frontNormalLocal = Vector3.up;
        // Tư thế nghỉ của kim so với thân la bàn; mọi phép xoay đều tính từ đây.
        private Quaternion restLocalRotation = Quaternion.identity;

        private bool dropping;
        private float dropTimer;
        private Vector3 dropFromLocal;
        private Vector3 dropToLocal;

        // Vị trí đã đặt trong prefab. Hiệu ứng lấy ra / cất đi chỉ chạy quanh vị trí này,
        // không đụng tới tỉ lệ — phóng tỉ lệ sẽ kéo lệch cả đĩa vì gốc prefab nằm ở vành đĩa.
        private Vector3 restLocalPosition;
        private bool showing;
        private bool hiding;
        private float toggleTimer;

        /// <summary>Đang rơi từ trên trời xuống.</summary>
        public bool IsDropping => dropping;

        /// <summary>Đã rơi xong và đang nằm đúng chỗ đã đặt.</summary>
        public bool HasLanded { get; private set; }

        /// <summary>La bàn đang được bật hay đang cất đi.</summary>
        public bool IsVisible => gameObject.activeSelf && !hiding;

        /// <summary>Kim đang có mục tiêu để chỉ tới.</summary>
        public bool HasTarget => target != null;

        private void Awake()
        {
            if (needle == null)
            {
                needle = transform.Find("Needle");
            }

            restLocalPosition = transform.localPosition;
            CacheNeedleAxes();
        }

        /// <summary>Bắt đầu thả la bàn xuống đúng chỗ đã đặt trong prefab.</summary>
        public void BeginDrop()
        {
            dropToLocal = transform.localPosition;
            dropFromLocal = dropToLocal + UpInParentSpace() * dropHeight;
            transform.localPosition = dropFromLocal;

            dropping = true;
            HasLanded = false;
            dropTimer = 0f;
        }

        /// <summary>Kim sẽ chỉ về phía này. Truyền null để kim quay về tư thế nghỉ.</summary>
        public void SetTarget(Transform newTarget)
        {
            target = newTarget;
        }

        /// <summary>Bật/tắt la bàn. Đang hiện thì cất đi, đang cất thì lấy ra.</summary>
        public void SetVisible(bool visible)
        {
            if (visible)
            {
                // Đang hiện rồi thì thôi; đang cất dở thì lấy ra lại.
                if (gameObject.activeSelf && !hiding) return;

                gameObject.SetActive(true);

                hiding = false;
                showing = true;
                toggleTimer = 0f;
                transform.localPosition = RestPositionBelow();
                return;
            }

            if (!gameObject.activeSelf || hiding) return;

            showing = false;
            hiding = true;
            toggleTimer = 0f;
        }

        public void ToggleVisible()
        {
            SetVisible(!gameObject.activeSelf || hiding);
        }

        /// <summary>Vị trí đã đặt hạ xuống dưới — chỗ la bàn bắt đầu khi được lấy ra.</summary>
        private Vector3 RestPositionBelow()
        {
            return restLocalPosition - UpInParentSpace() * showRiseHeight;
        }

        private void Update()
        {
            if (dropping) UpdateDrop();
            UpdateToggle();
            if (faceCamera) FaceView();

            UpdateNeedle();
        }

        /// <summary>
        /// Hiệu ứng lấy la bàn ra / cất đi: trượt từ dưới đi lên như nhấc một món đồ ra khỏi túi,
        /// cất đi thì hạ xuống nhanh dần rồi mới tắt. Không đụng tới tỉ lệ.
        /// Dùng thời gian không theo timeScale để không bị đứng hình khi game đang tạm dừng.
        /// </summary>
        private void UpdateToggle()
        {
            if (!showing && !hiding) return;

            toggleTimer += Time.unscaledDeltaTime;

            if (showing)
            {
                float t = Mathf.Clamp01(toggleTimer / Mathf.Max(0.01f, showDuration));
                // Nhanh lúc nhấc lên, chậm dần khi tới trước mặt.
                float eased = 1f - Mathf.Pow(1f - t, 3f);

                transform.localPosition = Vector3.LerpUnclamped(RestPositionBelow(), restLocalPosition, eased);

                if (t >= 1f)
                {
                    showing = false;
                    transform.localPosition = restLocalPosition;
                }

                return;
            }

            float hideT = Mathf.Clamp01(toggleTimer / Mathf.Max(0.01f, hideDuration));
            // Hạ xuống nhanh dần: lúc tắt object thì la bàn đang đi nhanh nên không thấy nó "biến mất".
            float hideEased = hideT * hideT;

            transform.localPosition = Vector3.LerpUnclamped(restLocalPosition, RestPositionBelow(), hideEased);

            if (hideT < 1f) return;

            hiding = false;
            transform.localPosition = restLocalPosition;
            gameObject.SetActive(false);
        }

        private void UpdateDrop()
        {
            dropTimer += Time.deltaTime;

            float t = Mathf.Clamp01(dropTimer / Mathf.Max(0.01f, dropDuration));

            // Rơi nhanh dần như vật nặng, rồi khựng lại đúng vị trí đã đặt.
            transform.localPosition = Vector3.LerpUnclamped(dropFromLocal, dropToLocal, t * t);

            if (t >= 1f)
            {
                dropping = false;
                HasLanded = true;
                transform.localPosition = dropToLocal;
            }
        }

        /// <summary>Trục lên của object cha, để la bàn rơi thẳng đứng dù người chơi đang quay hướng nào.</summary>
        private Vector3 UpInParentSpace()
        {
            return transform.parent != null ? transform.parent.InverseTransformDirection(Vector3.up) : Vector3.up;
        }

        /// <summary>Xoay ít nhất có thể để mặt số hướng về camera (chỉ dùng khi bật faceCamera).</summary>
        private void FaceView()
        {
            Camera main = Camera.main;
            if (main == null) return;

            Vector3 toCamera = main.transform.position - transform.position;
            if (toCamera.sqrMagnitude < 0.0001f) return;

            transform.rotation =
                Quaternion.FromToRotation(transform.rotation * frontNormalLocal, toCamera.normalized) * transform.rotation;
        }

        private void UpdateNeedle()
        {
            if (needle == null) return;

            // Tư thế nghỉ dựng lại mỗi frame theo thân la bàn, để la bàn có xoay thì kim vẫn đúng.
            Quaternion restRotation = needle.parent != null
                ? needle.parent.rotation * restLocalRotation
                : restLocalRotation;

            Quaternion desired = restRotation;

            if (target != null)
            {
                Vector3 toTarget = target.position - transform.position;
                Vector3 spinWorld = restRotation * spinAxis;

                if (toTarget.sqrMagnitude > 0.0001f && spinWorld.sqrMagnitude > 0.0001f)
                {
                    // Kim chỉ được xoay trong mặt số, nên hướng đẹp nhất là hướng mà hình chiếu
                    // của kim lên đường tới mục tiêu là lớn nhất: giải cực đại của a*cos(x) + b*sin(x).
                    Vector3 restPoint = restRotation * pointAxis;
                    Vector3 across = Vector3.Cross(spinWorld, restPoint);

                    Vector3 dir = toTarget.normalized;
                    float angle = Mathf.Atan2(Vector3.Dot(across, dir), Vector3.Dot(restPoint, dir)) * Mathf.Rad2Deg;

                    desired = Quaternion.AngleAxis(angle, spinWorld) * restRotation;
                }
            }

            needle.rotation = Quaternion.RotateTowards(needle.rotation, desired, needleTurnSpeed * Time.deltaTime);
        }

        private void CacheNeedleAxes()
        {
            if (needle == null)
            {
                Debug.LogWarning("[Compass] Không tìm thấy kim (Needle) nên la bàn không chỉ được hướng.", this);
                return;
            }

            restLocalRotation = needle.parent != null
                ? Quaternion.Inverse(needle.parent.rotation) * needle.rotation
                : needle.rotation;

            Bounds bounds = GetNeedleBounds();

            pointAxis = needlePointAxis.sqrMagnitude > 0.0001f
                ? needlePointAxis.normalized
                : GuessPointAxis(bounds);

            spinAxis = needleSpinAxis.sqrMagnitude > 0.0001f
                ? needleSpinAxis.normalized
                : GuessSpinAxis(bounds, pointAxis);

            if (invertNeedle) pointAxis = -pointAxis;

            // Mặt đọc là mặt có kim nằm trên, nên lấy dấu từ độ lệch của kim so với tâm la bàn.
            Transform body = needle.parent != null ? needle.parent : transform;
            Vector3 planeNormal = (restLocalRotation * spinAxis).normalized;
            if (planeNormal.sqrMagnitude < 0.0001f) planeNormal = Vector3.up;

            Vector3 needleOffset = body.InverseTransformPoint(needle.position);
            frontNormalLocal = Vector3.Dot(needleOffset, planeNormal) < 0f ? -planeNormal : planeNormal;
        }

        private Bounds GetNeedleBounds()
        {
            MeshFilter filter = needle != null ? needle.GetComponent<MeshFilter>() : null;
            if (filter != null && filter.sharedMesh != null)
            {
                return filter.sharedMesh.bounds;
            }

            return new Bounds(Vector3.zero, Vector3.one);
        }

        /// <summary>
        /// Mũi kim: mesh được dựng lệch hẳn về một phía so với gốc xoay của nó, phía đó là mũi.
        /// Mesh cân đối thì lấy trục dài nhất.
        /// </summary>
        private static Vector3 GuessPointAxis(Bounds bounds)
        {
            int axis = DominantAxis(bounds.center, 0.01f);
            if (axis < 0) axis = DominantAxis(bounds.size, 0f);
            if (axis < 0) return Vector3.forward;

            Vector3 direction = Vector3.zero;
            direction[axis] = OffsetSign(bounds.center[axis]);
            return direction;
        }

        /// <summary>Trục kim quay quanh: chiều mỏng nhất của mesh.</summary>
        private static Vector3 GuessSpinAxis(Bounds bounds, Vector3 pointAxis)
        {
            int axis = SmallestAxis(bounds.size);

            Vector3 direction = Vector3.zero;
            if (axis >= 0) direction[axis] = 1f;

            // Trục quay không được trùng trục kim, kẻo kim xoay quanh chính nó.
            if (direction.sqrMagnitude < 0.0001f || Mathf.Abs(Vector3.Dot(direction, pointAxis)) > 0.9f)
            {
                direction = Mathf.Abs(Vector3.Dot(Vector3.up, pointAxis)) > 0.9f ? Vector3.forward : Vector3.up;
            }

            return direction;
        }

        private static int DominantAxis(Vector3 v, float minimum)
        {
            int axis = -1;
            float best = minimum;

            for (int i = 0; i < 3; i++)
            {
                float magnitude = Mathf.Abs(v[i]);
                if (magnitude > best)
                {
                    best = magnitude;
                    axis = i;
                }
            }

            return axis;
        }

        private static int SmallestAxis(Vector3 v)
        {
            int axis = -1;
            float best = float.MaxValue;

            for (int i = 0; i < 3; i++)
            {
                if (v[i] < best)
                {
                    best = v[i];
                    axis = i;
                }
            }

            return axis;
        }

        private static float OffsetSign(float value)
        {
            if (value > 0.0001f) return 1f;
            if (value < -0.0001f) return -1f;
            return 1f;
        }
    }
}
