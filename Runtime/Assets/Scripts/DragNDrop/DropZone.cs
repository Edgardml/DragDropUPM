using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace DragNDrop
{
    public class DropZone : MonoBehaviour, IDropHandler
    {
        private int dropZoneLayer;
        
        private void Awake()
        {
            dropZoneLayer = LayerMask.NameToLayer("DropZone");
            gameObject.layer = dropZoneLayer;
        }

        public void OnDrop(PointerEventData eventData)
        {
            if (eventData.pointerDrag != null)
            {
                Debug.Log("Objeto soltado: " + eventData.pointerDrag.name);
                
            }
        }
    }
}
