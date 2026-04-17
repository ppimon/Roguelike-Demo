using UnityEngine;
using System.Collections;

public class SlidingDoor : MonoBehaviour
{
    public Vector3 openOffset = new Vector3(0, 2, 0); // 开启时向上移动的距离
    public float speed = 7f;

    private Vector3 closedPos;
    private Vector3 targetPos;

    void Awake()
    {
        closedPos = transform.localPosition;
        transform.localPosition = closedPos + openOffset;
        targetPos = closedPos;
    }

    public void SetLock(bool isLocked)
    {
        targetPos = isLocked ? closedPos : closedPos + openOffset;
        StopAllCoroutines();
        StartCoroutine(MoveRoutine());
    }

    IEnumerator MoveRoutine()
    {
        while (Vector3.Distance(transform.localPosition, targetPos) > 0.01f)
        {
            transform.localPosition = Vector3.Lerp(transform.localPosition, targetPos, Time.deltaTime * speed);
            yield return null;
        }
        transform.localPosition = targetPos;
    }
}