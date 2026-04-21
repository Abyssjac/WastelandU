using UnityEngine;
using JackyPuzzleInteract;
using DG.Tweening;
public class GateReceiver : TwoSignalReceiver
{
    //[SerializeField] private Animator animator;
    [Header("Move Settings")]
    [SerializeField] private Transform targetTransform; // 可选移动目标，不指定时默认自身
    [SerializeField] private Vector3 moveOffset = new Vector3(0f, 2f, 0f);   // 移动偏移（可多方向）
    [SerializeField] private float moveDuration = 0.5f; // 移动耗时
    [SerializeField] private Ease ease = Ease.OutQuad;  // 缓动方式
    [SerializeField] private bool useLocalPosition = false; // 是否使用 localPosition

    private Tween currentTween;
    private Transform moveTransform;
    private Vector3 startPos;


    //public virtual void Awake()
    //{
    //    base.Awake();
    //    startPos = useLocalPosition ? transform.localPosition : transform.position;
    //}
    private void Start()
    {
        moveTransform = targetTransform != null ? targetTransform : transform;
        startPos = useLocalPosition ? moveTransform.localPosition : moveTransform.position;
    }
    
    protected override void OnActivated(GameObject sender)
    {
        //throw new System.NotImplementedException();
        StartMoveUp();
    }

    protected override void OnDeactivated(GameObject sender)
    {
        //throw new System.NotImplementedException();
        ResetPosition();
    }

    /// <summary>
    /// 开始移动
    /// </summary>
    public void StartMoveUp()
    {
        StopMove(); // 先终止旧动画，避免叠加
        if (moveTransform == null)
            moveTransform = targetTransform != null ? targetTransform : transform;

        Vector3 targetPos = startPos + moveOffset;

        if (useLocalPosition)
        {
            currentTween = moveTransform.DOLocalMove(targetPos, moveDuration).SetEase(ease);
        }
        else
        {
            currentTween = moveTransform.DOMove(targetPos, moveDuration).SetEase(ease);
        }
    }

    /// <summary>
    /// 随时终止当前移动
    /// </summary>
    public void StopMove()
    {
        if (currentTween != null && currentTween.IsActive())
        {
            currentTween.Kill();
            currentTween = null;
        }
    }

    /// <summary>
    /// 停止并回到初始位置
    /// </summary>
    public void ResetPosition()
    {
        StopMove();

        if (moveTransform == null)
            moveTransform = targetTransform != null ? targetTransform : transform;

        if (useLocalPosition)
        {
            currentTween = moveTransform.DOLocalMove(startPos, moveDuration).SetEase(ease);
        }
        else
        {
            currentTween = moveTransform.DOMove(startPos, moveDuration).SetEase(ease);
        }
    }
}
