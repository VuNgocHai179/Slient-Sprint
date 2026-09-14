using UnityEngine;

namespace SilentSprint.Mechanics
{
    public class FollowTargetPosition : MonoBehaviour
    {
        public Transform target;
        public Vector3 offset = new Vector3(0f, 7f, 0f);

        void LateUpdate()
        {
            if (target == null) return;
            transform.position = target.position + offset;
        }
    }
}