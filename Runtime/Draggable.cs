using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace DragNDrop 
{
    public abstract class Draggable : MonoBehaviour, IPointerDownHandler, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerUpHandler
    {
        public Transform refTransform; // Transform: World; RectTransform: Canvas;
        
        public float doubleTapTime = 0.25f;
        
        public DropZone currentDropZone;
        public DropZone previousDropZone;
        public Canvas canvas;
        
        public DraggableState currentState = DraggableState.Waiting;
        
        [SerializeField] private GameObject ghostRepresentation;
        
        private Vector3 initPosition = Vector3.zero;
        private Vector3 initLocalPosition = Vector3.zero;
        private Vector3 newInitPosition = Vector3.zero;
        private bool tryingDoubleTap = false;
        private GameObject objectCollide;
        private int draggableLayer;
        private int dropZoneLayer;
        private Vector3 pointerOffset;
        private float maxZDistance = 1f;

        #region Accesors
        public bool IsCanvasTransform => refTransform is RectTransform;
        

        #endregion
        
        #region MonoBehaviour functions
        
        public virtual void Awake()
        {
            refTransform = gameObject.transform;
            initPosition = transform.position;
            initLocalPosition = transform.localPosition;
            newInitPosition = transform.position;
            draggableLayer = LayerMask.NameToLayer("Draggable");
            dropZoneLayer = LayerMask.NameToLayer("DropZone");
            gameObject.layer = draggableLayer;
            
            if(!IsCanvasTransform && GetComponent<Collider>() == null)
                gameObject.AddComponent<PolygonCollider2D>();
        }
        
        #endregion
        
        #region Interfaces functions
        
        public void OnPointerDown(PointerEventData eventData)
        {
            if (IsCanvasTransform)
            {
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    (RectTransform)refTransform, eventData.position, 
                    canvas.worldCamera, out Vector2 localPointerPos);
                pointerOffset =  (Vector3)localPointerPos;
            }
            else
            {
                // Offset para Mundo: calculamos punto de impacto con raycast
                if (Physics.Raycast(Camera.main.ScreenPointToRay(eventData.position), out RaycastHit hit))
                {
                    pointerOffset =  hit.point;
                }
            }
            if(tryingDoubleTap)
                DoubleTap();
            StartCoroutine(CoCheckDoubleTap());
        }
        public void OnBeginDrag(PointerEventData eventData)
        {
            if (currentState == DraggableState.Waiting)
            {
                currentState = DraggableState.Dragging;
                StartDrag();
            }
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (IsCanvasTransform)
            {
                // Mover UI
                if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                        (RectTransform)canvas.transform, eventData.position,
                        canvas.worldCamera, out Vector2 localPoint))
                {
                    refTransform.localPosition = localPoint + (Vector2)pointerOffset;
                }
            }
            else
            {
                // Mover objeto mundo
                Ray ray = Camera.main.ScreenPointToRay(eventData.position);
                // Asumimos un plano paralelo a la cámara; ajusta normal/posición según tu caso
                Plane plane = new Plane(-Camera.main.transform.forward, refTransform.position);
                if (plane.Raycast(ray, out float enter))
                {
                    Vector3 hitPoint = ray.GetPoint(enter);
                    var tmpPosition = hitPoint + pointerOffset;
                    tmpPosition.z = Mathf.Clamp(tmpPosition.z, -maxZDistance, maxZDistance);
                    refTransform.position = tmpPosition;
                }
            }
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (currentState == DraggableState.Dragging)
            {
                EndDrag();
            }
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (IsCanvasTransform)
            {
                List<RaycastResult> results = new List<RaycastResult>();
                EventSystem.current.RaycastAll(eventData, results);

                foreach (var result in results)
                {
                    if (result.gameObject.layer == dropZoneLayer)
                    {
                        objectCollide = result.gameObject;
                        Debug.Log("Canvas object hit: " + objectCollide.name);
                        break;
                    }
                }
            }
            else
            {
                int mask = 1 << dropZoneLayer;
                Vector3 worldPoint = Camera.main.ScreenToWorldPoint(eventData.position);
                RaycastHit2D hit2D = Physics2D.Raycast(worldPoint, Vector2.zero, Mathf.Infinity, mask);

                if (hit2D.collider != null)
                {
                    objectCollide = hit2D.collider.gameObject;
                    Debug.Log("2D object hit: " + objectCollide.name);
                }
                else
                {
                    Ray ray = Camera.main.ScreenPointToRay(eventData.position);
                    RaycastHit hit3D;
                    if (Physics.Raycast(ray, out hit3D, Mathf.Infinity, mask))
                    {
                        objectCollide = hit3D.collider.gameObject;
                        Debug.Log("3D object hit: " + objectCollide.name);
                    }
                }
            }
            Drop(objectCollide?.GetComponent<DropZone>());
            if (currentState == DraggableState.Dragging)
                currentState = DraggableState.Dropped;
        }
        
        #endregion

        #region Abstract functions
        protected abstract void Tap();
        protected abstract void DoubleTap();
        protected abstract void StartDrag();
        protected abstract void EndDrag();
        protected abstract void DropOnDropZone(DropZone argDropZone);
        
        #endregion
        
        #region Public functions
        
        protected virtual void Drop(DropZone argDropZone)
        {
            objectCollide = null;
            var tmpPreviousDropZone = previousDropZone;
            if (argDropZone == null)
            {
                DropOnNothing();
                return;
            }
            else
            {
                if(currentDropZone != null)
                    previousDropZone = currentDropZone;
                currentDropZone = argDropZone;
                if (currentDropZone != previousDropZone)
                {
                    StartCoroutine(CoMoveToDropZone(currentDropZone));
                    DropOnDropZone(argDropZone);
                }
            }
        }
        

        protected virtual void DropOnNothing()
        {
            if(previousDropZone != null)
                StartCoroutine(CoMoveToDropZone(previousDropZone));
            else
                StartCoroutine(CoMoveToPosition(initPosition));
        }
        
        #endregion
        
        #region Private functions
        
        #endregion

        #region Coroutines

        IEnumerator CoCheckDoubleTap()
        {
            tryingDoubleTap = true;
            yield return new WaitForSeconds(doubleTapTime);
            tryingDoubleTap = false;
        }

        IEnumerator CoMoveToDropZone(DropZone argDropZone)
        {
            var tmpTimeToMove = 0.1f;
            var tmpCurrentMovingTime = 0f;

            var tmpInitPosition = newInitPosition;
            while (tmpCurrentMovingTime < tmpTimeToMove)
            {
                transform.position = Vector3.Lerp(tmpInitPosition, argDropZone.transform.position, tmpCurrentMovingTime / tmpTimeToMove);
                tmpCurrentMovingTime += Time.deltaTime;
                yield return null;
            }
            transform.position = argDropZone.transform.position;
            newInitPosition = argDropZone.transform.position;
            currentState = DraggableState.Waiting;
            Drop(argDropZone);
        }

        IEnumerator CoMoveToPosition(Vector3 argMoveToPosition)
        {
            var tmpTimeToMove = 0.1f;
            var tmpCurrentMovingTime = 0f;

            var tmpInitPosition = newInitPosition;
            while (tmpCurrentMovingTime < tmpTimeToMove)
            {
                transform.position = Vector3.Lerp(tmpInitPosition, argMoveToPosition, tmpCurrentMovingTime / tmpTimeToMove);
                tmpCurrentMovingTime += Time.deltaTime;
                yield return null;
            }
            transform.position = argMoveToPosition;
            newInitPosition = argMoveToPosition;
            currentState = DraggableState.Waiting;
        }

        #endregion
    }

    public enum DraggableState
    {
        Waiting = 0,
        Dragging = 1,
        Dropped = 2,
        
    }
}
