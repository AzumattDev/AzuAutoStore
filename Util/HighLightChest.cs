using UnityEngine;

namespace AzuAutoStore.Util;

public class HighLightChest : MonoBehaviour
{
    private WearNTear? wnt;

    private void Awake()
    {
        wnt = TryGetComponent<WearNTear>(out WearNTear? wearNTear) ? wearNTear : null;
        if (wnt == null)
        {
            DestroyNow();
        }
        else
        {
            Trigger();
        }
    }

    public void Trigger() => InvokeRepeating(nameof(DestroyNow), 10f, 1f); // 10 seconds after the awake, it will start to destroy the object

    public void DestroyNow()
    {
        DestroyImmediate(this);
    }

    void Update()
    {
        wnt.Highlight();
    }
}