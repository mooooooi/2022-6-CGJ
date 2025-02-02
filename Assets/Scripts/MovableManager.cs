using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using MyBox;
using DG.Tweening;
using DG.Tweening.Core;
using DG.Tweening.Plugins.Core.PathCore;
using DG.Tweening.Plugins.Options;
using DG.Tweening.Plugins;

[Serializable]
public class RelativePoint
{
    [SerializeField]
    private Vector3 value;

    public void Set(Vector3 point, Vector3 start, Vector3 end)
    {
        var ray = end - start;
        var dir = ray.normalized;
        var root = Matrix4x4.TRS(start, Quaternion.LookRotation(dir), Vector3.one);
        value = root.inverse.MultiplyPoint(point);
    }

    public Vector3 Get(Vector3 start, Vector3 end, float scalar = 1f)
    {
        var ray = end - start;
        var dir = ray.normalized;
        var root = Matrix4x4.TRS(start, Quaternion.LookRotation(dir), Vector3.one);
        return root.MultiplyPoint(value * scalar);
    }
}

public class MovableManager : MonoBehaviour
{
    static System.Random Random = new System.Random();
    public bool AutoRun = true;
    [Range(0.2f, 5)]
    public float Duration = 2f;
    public Transform Container;
    public RelativePoint APoint;
    public RelativePoint BPoint;
    [SerializeField]
    public List<Transform> Ball = new List<Transform>();
    public AnimationCurve SpeedCurve;
    public GameObject HatPrefab;
    public int HatCount = 7;
    public int ParallelCount = 2;
    public int Rounds = 10;
    [Header("Ԥ����")]
    [ReadOnly]
    public List<Transform> Movables;
    public int ballIdx = 0;

    private Queue<(int, int)> swapTuples;
    private int currentRound = -1;
    private int[] indexs;
    private int startIdx;

    private Action<int> end;

    // Start is called before the first frame update
    public void init(Action<int> endFunc)
    {
        end = endFunc;
        swapTuples = new Queue<(int, int)>();
        Run();
    }

    void Run()
    {
        currentRound = -1;
        swapTuples.Clear();
        Debug.Log("HERE");
        CollectHats();
        SettleupHats();
        indexs = new int[Movables.Count];
        indexs.FillBy(i => i);
        startIdx = Random.Next(0, Movables.Count);
        for (var i = 0; i < Rounds; i++)
        {
            //Append(GetTwoRandomInt(Movables.Count));
            Append(GetRandomInts(Movables.Count, ParallelCount));
        }
        CreatePutBallAnimation(startIdx).AppendCallback(() => Next());
    }

    void Append(params (int, int)[] tuples)
    {
        foreach (var tuple in tuples)
        {
            swapTuples.Enqueue(tuple);
        }
    }

    public void CollectHats()
    {
        Movables.Clear();
        foreach (Transform trs in Container)
        {
            Destroy(trs.gameObject);
        }
        Container.DetachChildren();
        for (var i = 0; i < HatCount; i++)
        {
            var obj = Instantiate(HatPrefab, Container);
            obj.GetComponent<Hat>().Index = i;
            Movables.Add(obj.transform);
        }
    }

    public void SettleupHats()
    {
        var center = (Movables.Count - 1) / 2f;
        for (var i = 0; i < Movables.Count; i++)
        {
            Movables[i].position = new Vector3((i - center) * 4, 0, 0);
        }
    }

    public (Tween a, Tween b) CreateSwapAnimation(int aIdx, int bIdx, float scalar = 1f)
    {
        var a = Movables[aIdx];
        var b = Movables[bIdx];
        var aT = a.DOPath(
            new Vector3[] { b.transform.position, APoint.Get(a.position, b.position, scalar), BPoint.Get(b.position, a.position, scalar) }
            , Duration, PathType.CubicBezier, PathMode.Sidescroller2D);
        var bT = b.DOPath(
            new Vector3[] { a.transform.position, APoint.Get(b.position, a.position, scalar), BPoint.Get(a.position, b.position, scalar) }
            , Duration, PathType.CubicBezier, PathMode.Sidescroller2D);
        return (aT, bT);
    }

    public Sequence CreatePutBallAnimation(int idx, bool hasBall = true, int delay = 0)
    {
        var movable = Movables[idx];
        var seq = DOTween.Sequence();

        var upSide = new Vector3(0, 3, 1);
        var rotSide = new Vector3(45, 0, 0);

        if (hasBall)
        {
            var ballPos = movable.position;
            ballPos.y = 0.5f;
            Ball[ballIdx].position = ballPos;
            Ball[ballIdx].gameObject.SetActive(true);
        }
        if (delay > 0)
        {
            seq.AppendInterval(delay);
        }
        seq.Append(movable.DOBlendableLocalMoveBy(upSide, 0.7f).SetEase(Ease.InOutBack).SetDelay(0.2f));
        seq.Join(movable.DOBlendableLocalRotateBy(rotSide, 0.7f));
        seq.AppendInterval(2f);
        seq.Append(movable.DOBlendableLocalMoveBy(-upSide, 1f));
        seq.Join(movable.DOBlendableLocalRotateBy(-rotSide, 1f));
        if (hasBall)
        {
            seq.AppendCallback(() =>
            {
                for(int i = 0; i < 3; i++)
                {
                    Ball[ballIdx].gameObject.SetActive(false);
                }
            }
            );
        }
        return seq;
    }

    public Tween Combine(Tween ta, Tween tb)
    {
        var seq = DOTween.Sequence();
        seq.Join(ta).Join(tb);
        return seq;
    }

    public void Next()
    {
        var factor = SpeedCurve.Evaluate((float)++currentRound / Rounds);
        Duration = Mathf.Lerp(0.2f, 1.5f, factor) / 2f;
        if (swapTuples.Count < ParallelCount)
        {
            end.Invoke(startIdx);
            return;
        }

        var seq = DOTween.Sequence();
        for (int i = 0; i < ParallelCount; i++)
        {
            var tuple = swapTuples.Dequeue();
            seq.Join(Swap(tuple.Item1, tuple.Item2, 1 + (ParallelCount - i - 1) * 0.5f));
        }
        seq.AppendCallback(() => Next());
    }

    public Sequence Swap(int aIdx, int bIdx, float animationScalr = 1f)
    {
        var (ta, tb) = CreateSwapAnimation(aIdx, bIdx, animationScalr);
        var t = Combine(ta, tb);
        return DOTween.Sequence().Append(t).AppendCallback(() =>
        {
            var temp = Movables[aIdx];
            Movables[aIdx] = Movables[bIdx];
            Movables[bIdx] = temp;

            var temp1 = indexs[aIdx];
            indexs[aIdx] = indexs[bIdx];
            indexs[bIdx] = temp1;
        });
    }

    public int GetEndIndexFromStartIndex(int startIndex)
    {
        return indexs.FirstIndex(val => val == startIndex);
    }

    static (int, int)[] GetRandomInts(int count, int parallelCount)
    {
        if (count < parallelCount * 2) throw new Exception("!!!!!!!!!!");
        var arr = new int[count];
        for (var i = 0; i < count; i++)
        {
            arr[i] = i;
        }
        ShuffleCopy(arr);
        var subArr = arr[..(parallelCount * 2)];
        Array.Sort(subArr);

        var outs = new (int, int)[parallelCount];
        for (var i = 0; i < parallelCount; i++)
        {
            outs[i] = (subArr[i], subArr[subArr.Length - 1 - i]);
        }
        return outs;
    }

    static T[] ShuffleCopy<T>(T[] arr)
    {

        for (var i = arr.Length - 1; i > 0; --i)
        {
            int randomIndex = Random.Next(i + 1);

            T temp = arr[i];
            arr[i] = arr[randomIndex];
            arr[randomIndex] = temp;
        }

        return arr;
    }
}