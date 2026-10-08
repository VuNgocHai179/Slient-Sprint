using UnityEngine;
using UnityEngine.EventSystems;

namespace SilentSprint.UI
{
    public class Joystick : MonoBehaviour, IDragHandler, IPointerUpHandler, IPointerDownHandler
    {
        public RectTransform background;
        public RectTransform handle;
        private Vector2 inputVector;

        public float Horizontal { get { return inputVector.x; } }
        public float Vertical { get { return inputVector.y; } }

        public void OnDrag(PointerEventData eventData)
        {
            Vector2 position;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(background, eventData.position, eventData.pressEventCamera, out position))
            {
                float bgWidth = background.rect.width;
                float bgHeight = background.rect.height;

                inputVector = new Vector2(position.x / (bgWidth / 2), position.y / (bgHeight / 2));

                inputVector = (inputVector.magnitude > 1.0f) ? inputVector.normalized : inputVector;

                handle.anchoredPosition = new Vector2(inputVector.x * (bgWidth / 2), inputVector.y * (bgHeight / 2));
            }
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            OnDrag(eventData);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            ResetInput();
        }

        /// <summary>
        /// Trả cần gạt về vị trí giữa. Dùng khi UI bị ẩn đột ngột (hội thoại, mini game)
        /// vì lúc đó OnPointerUp không được gọi và cần gạt sẽ giữ nguyên hướng cũ.
        /// </summary>
        public void ResetInput()
        {
            inputVector = Vector2.zero;
            if (handle != null) handle.anchoredPosition = Vector2.zero;
        }
    }
}