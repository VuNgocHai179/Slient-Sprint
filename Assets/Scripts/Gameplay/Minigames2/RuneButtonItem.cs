using DG.Tweening;
using SilentSprint.Gameplay.Minigames1;
using UnityEngine;

namespace SilentSprint.Gameplay.Minigames2
{
    public class RuneButtonItem : MonoBehaviour
    {
        public int buttonId;

        private Mini2gameManager manager;
        private Vector3 initialPos;

        public void Setup(int id, Mini2gameManager mgr)
        {
            buttonId = id;
            manager = mgr;
            initialPos = transform.position;

            if (GetComponent<Collider>() == null)
            {
                gameObject.AddComponent<BoxCollider>();
            }
        }

        public void OnPressed()
        {
            transform.DOKill();
            transform.position = initialPos;
            transform.DOMoveY(initialPos.y - 0.08f, 0.1f)
                     .SetLoops(2, LoopType.Yoyo)
                     .SetEase(Ease.OutQuad);

            manager?.OnRuneButtonPressed(buttonId);
        }

        public void ResetPosition()
        {
            transform.DOKill();
            if (initialPos != Vector3.zero)
            {
                transform.position = initialPos;
            }
        }
    }
}