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
        
        public List<DropZone> listDropZonesToInteractWith = new List<DropZone>();
        public DropZone currentDropZone;
        public DropZone previousDropZone;
        public Canvas canvas;
        
        public DraggableState currentState = DraggableState.Waiting;
        
        [SerializeField] private GameObject ghostRepresentation;
        
        private Vector3 initPosition = Vector3.zero;
        private Vector3 initLocalPosition = Vector3.zero;
        private Vector3 newInitPosition = Vector3.zero;
        private bool tryingDoubleTap = false;
        private bool singleTap = false;
        private GameObject objectCollide;
        private int draggableLayer;
        private int dropZoneLayer;
        private Vector3 pointerOffset;
        private float maxZDistance = 1f;

        private RectTransform rectTransform;
        
        private Coroutine doubleTapCoroutine;
        
        #region Accesors
        public bool IsCanvasTransform => refTransform is RectTransform;
        

        #endregion
        
        #region MonoBehaviour functions
        
        public virtual void Awake()
        {
            refTransform = gameObject.transform;
            if (IsCanvasTransform)
            {
                rectTransform = refTransform.GetComponent<RectTransform>();
                initPosition = rectTransform.anchoredPosition;
                initLocalPosition = rectTransform.localPosition;
                newInitPosition = initPosition;
            }
            else
            {
                rectTransform = null;
                initPosition = transform.position;
                initLocalPosition = transform.localPosition;
                newInitPosition = initPosition;
            }
            draggableLayer = LayerMask.NameToLayer("Draggable");
            dropZoneLayer = LayerMask.NameToLayer("DropZone");
            gameObject.layer = draggableLayer;
            
            
            if(!IsCanvasTransform && GetComponent<Collider>() == null)
                gameObject.AddComponent<PolygonCollider2D>();
            
            if(canvas == null)
            {
                canvas = GetComponentInParent<Canvas>();
                if (canvas == null)
                {
                    Debug.LogError("No Canvas found in the scene. Please assign a Canvas to the Draggable component.");
                }
            }
        }
        
        #endregion
        
        #region Interfaces functions
        
        public void OnPointerDown(PointerEventData eventData)
        {
            if (IsCanvasTransform)
            {
                RectTransformUtility.ScreenPointToWorldPointInRectangle(
                    (RectTransform)refTransform, eventData.position, 
                    canvas.worldCamera, out Vector3 localPointerPos);
                pointerOffset =  localPointerPos - refTransform.position;
            }
            else
            {
                // Offset para Mundo: calculamos punto de impacto con raycast
                if (Physics.Raycast(Camera.main.ScreenPointToRay(eventData.position), out RaycastHit hit))
                {
                    pointerOffset =  hit.point;
                }
            }
            if (tryingDoubleTap)
            {
                singleTap = false;
                DoubleTap();
            }
            if(doubleTapCoroutine == null)
                doubleTapCoroutine = StartCoroutine(CoCheckDoubleTap());
        }
        public void OnBeginDrag(PointerEventData eventData)
        {
            if (doubleTapCoroutine != null)
            {
                StopCoroutine(doubleTapCoroutine);
                doubleTapCoroutine = null;
            }
            singleTap = false;
            tryingDoubleTap = false;
            if (currentState == DraggableState.Waiting)
            {
                currentState = DraggableState.Dragging;
                StartDrag();
            }
        }

        public void OnDrag(PointerEventData eventData)
        {
            if(doubleTapCoroutine != null)
                StopCoroutine(doubleTapCoroutine);
            singleTap = false;
            tryingDoubleTap = false;
            if (IsCanvasTransform)
            {
                // Mover UI
                if (RectTransformUtility.ScreenPointToWorldPointInRectangle(
                        (RectTransform)canvas.transform, eventData.position,
                        canvas.worldCamera, out Vector3 localPoint))
                {
                    refTransform.position = localPoint - pointerOffset ;
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

        protected virtual void DropOnNothing()
        {
            Debug.Log("Dropped on nothing");
            if(previousDropZone != null)
                StartCoroutine(CoMoveToDropZone(previousDropZone));
            else
                StartCoroutine(CoMoveToPosition(newInitPosition));
        }

        public void MoveToPosition(Vector3 position)
        {
            StartCoroutine(CoMoveToPosition(position));
        }
        
        public void MoveToDropZone(DropZone dropZone)
        {
            if (dropZone == null)
            {
                Debug.LogError("DropZone is null");
                DropOnNothing();
                return;
            }
            
            if (!listDropZonesToInteractWith.Contains(dropZone))
            {
                Debug.LogWarning($"DropZone {dropZone.name} is not in the list of interactable DropZones.");
                DropOnNothing();
                return;
            }
            
            StartCoroutine(CoMoveToDropZone(dropZone));
        }

        public void MoveToDropZone(string dropZoneName)
        {
            foreach (var tmpDropZone in listDropZonesToInteractWith)
            {
                if (tmpDropZone.name == dropZoneName)
                {
                    MoveToDropZone(tmpDropZone);
                    return;
                }
            }
        }
        
        public void SetNewInitPosition(Vector3 position)
        {
            newInitPosition = position;
        }
        
        #endregion
        
        #region Private functions
        
        private void Drop(DropZone argDropZone)
        {
            objectCollide = null;
            singleTap = false;
            if (!argDropZone)
            {
                DropOnNothing();
                return;
            }
            if(currentDropZone != null)
                previousDropZone = currentDropZone;
            currentDropZone = argDropZone;
            if (currentDropZone != previousDropZone)
            {
                StartCoroutine(CoMoveToDropZone(currentDropZone));
                DropOnDropZone(argDropZone);
            }
        }
        
        #endregion

        #region Coroutines

        IEnumerator CoCheckSingleTap()
        {
            tryingDoubleTap = false;
            yield return new WaitForSeconds(doubleTapTime);
            if (!tryingDoubleTap)
            {
                Tap();
            }
        }
        
        IEnumerator CoCheckDoubleTap()
        {
            singleTap = false;
            tryingDoubleTap = true;
            yield return new WaitForSeconds(doubleTapTime);
            tryingDoubleTap = false;
            singleTap = true;
            Tap();
            if (doubleTapCoroutine != null)
            {
                StopCoroutine(doubleTapCoroutine);
                doubleTapCoroutine = null;
            }
        }

        IEnumerator CoMoveToDropZone(DropZone argDropZone)
        {
            var tmpTimeToMove = 0.1f;
            var tmpCurrentMovingTime = 0f;

            var tmpInitPosition = newInitPosition;

            if (IsCanvasTransform)
            {
                while (tmpCurrentMovingTime < tmpTimeToMove)
                {
                    rectTransform.anchoredPosition = Vector3.Lerp(tmpInitPosition, ((RectTransform)argDropZone.transform).anchoredPosition, tmpCurrentMovingTime / tmpTimeToMove);
                    tmpCurrentMovingTime += Time.deltaTime;
                    yield return null;
                }

                rectTransform.anchoredPosition = ((RectTransform)argDropZone.transform).anchoredPosition;
                newInitPosition = ((RectTransform)argDropZone.transform).anchoredPosition;
            }
            else
            {
                while (tmpCurrentMovingTime < tmpTimeToMove)
                {
                    transform.position = Vector3.Lerp(tmpInitPosition, argDropZone.transform.position, tmpCurrentMovingTime / tmpTimeToMove);
                    tmpCurrentMovingTime += Time.deltaTime;
                    yield return null;
                }
                transform.position = argDropZone.transform.position;
                newInitPosition = argDropZone.transform.position;
            }
            currentState = DraggableState.Waiting;
        }

        IEnumerator CoMoveToPosition(Vector3 argMoveToPosition)
        {
            var tmpTimeToMove = 0.1f;
            var tmpCurrentMovingTime = 0f;

            var tmpInitPosition = newInitPosition;

            if (IsCanvasTransform)
            {
                while (tmpCurrentMovingTime < tmpTimeToMove)
                {
                    rectTransform.anchoredPosition = Vector3.Lerp(tmpInitPosition, argMoveToPosition, tmpCurrentMovingTime / tmpTimeToMove);
                    tmpCurrentMovingTime += Time.deltaTime;
                    yield return null;
                }
                rectTransform.anchoredPosition = argMoveToPosition;
                newInitPosition = argMoveToPosition;
            }
            else
            {
                while (tmpCurrentMovingTime < tmpTimeToMove)
                {
                    transform.position = Vector3.Lerp(tmpInitPosition, argMoveToPosition, tmpCurrentMovingTime / tmpTimeToMove);
                    tmpCurrentMovingTime += Time.deltaTime;
                    yield return null;
                }
                transform.position = argMoveToPosition;
                newInitPosition = argMoveToPosition;
                
            }
            
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
