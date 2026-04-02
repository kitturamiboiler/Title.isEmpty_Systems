using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 프리팹 인스턴스를 미리 만들어 두고 Get/Release로 재사용.
/// 산나비 모작 ObjectPooler 패턴을 KnifeSystem용으로 단순화 (단일 프리팹, Transform 부모 고정).
/// </summary>
public class SimpleGameObjectPool : MonoBehaviour
{
    [SerializeField] private GameObject prefab;
    [SerializeField] private int prewarmCount = 8;
    [SerializeField] private int maxCapacity = 64;

    private readonly Stack<GameObject> _stack = new Stack<GameObject>();

    private void Awake()
    {
        if (prefab == null)
            return;

        for (int i = 0; i < prewarmCount; i++)
            _stack.Push(CreateInstance());
    }

    private GameObject CreateInstance()
    {
        var go = Instantiate(prefab, transform);
        go.SetActive(false);
        return go;
    }

    public GameObject Get(Vector3 position, Quaternion rotation)
    {
        GameObject go = _stack.Count > 0 ? _stack.Pop() : CreateInstance();
        var t = go.transform;
        t.SetPositionAndRotation(position, rotation);
        go.SetActive(true);
        return go;
    }

    public void Release(GameObject instance)
    {
        if (instance == null)
            return;

        instance.SetActive(false);
        instance.transform.SetParent(transform);

        if (_stack.Count >= maxCapacity)
        {
            Destroy(instance);
            return;
        }

        _stack.Push(instance);
    }
}
