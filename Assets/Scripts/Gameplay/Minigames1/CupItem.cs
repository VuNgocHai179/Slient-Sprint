using UnityEngine;
using DG.Tweening;

namespace SilentSprint.Gameplay.Minigames1
{
    public class CupItem : MonoBehaviour
    {
        [HideInInspector] public int cupIndex;
        private Mini1gameManager manager;
        private Vector3 initialLocalPos;

        public void Setup(int index, Mini1gameManager minigameManager)
        {
            cupIndex = index;
            manager = minigameManager;
            initialLocalPos = transform.localPosition;
        }

        public Tween LiftUp(float height = 0.35f, float duration = 0.3f)
        {
            return transform.DOLocalMoveY(initialLocalPos.y + height, duration).SetEase(Ease.OutQuad);
        }

        public Tween PutDown(float duration = 0.25f)
        {
            return transform.DOLocalMoveY(initialLocalPos.y, duration).SetEase(Ease.InQuad);
        }

        public void ResetPosition()
        {
            transform.localPosition = initialLocalPos;
        }

        private void OnMouseDown()
        {
            if (manager != null)
            {
                manager.OnCupSelected(this);
            }
        }
    }
}