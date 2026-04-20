using UnityEngine;
using JackyPuzzleInteract;
using DG.Tweening;
public class GateReceiver : TwoSignalReceiver
{
    //[SerializeField] private Animator animator;
    [Header("Move Settings")]
    [SerializeField] private float moveDistance = 2f;   // 向上移动距离
    [SerializeField] private float moveDuration = 0.5f; // 移动耗时
    [SerializeField] private Ease ease = Ease.OutQuad;  // 缓动方式
    [SerializeField] private bool useLocalPosition = false; // 是否使用 localPosition

    private Tween currentTween;
    private Vector3 startPos;


    //public virtual void Awake()
    //{
    //    base.Awake();
    //    startPos = useLocalPosition ? transform.localPosition : transform.position;
    //}
    private void Start()
    {
        startPos = useLocalPosition ? transform.localPosition : transform.position;
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
    /// 开始向上移动
    /// </summary>
    public void StartMoveUp()
    {
        StopMove(); // 先终止旧动画，避免叠加

        //Vector3 currentPos = useLocalPosition ? transform.localPosition : transform.position;
        Vector3 targetPos = startPos + Vector3.up * moveDistance;

        if (useLocalPosition)
        {
            currentTween = transform.DOLocalMove(targetPos, moveDuration).SetEase(ease);
        }
        else
        {
            currentTween = transform.DOMove(targetPos, moveDuration).SetEase(ease);
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

        if (useLocalPosition)
        {
            currentTween = transform.DOLocalMove(startPos, moveDuration).SetEase(ease);
        }
        else
        {
            currentTween = transform.DOMove(startPos, moveDuration).SetEase(ease);
        }
    }
}
