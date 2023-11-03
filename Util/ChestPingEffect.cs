using UnityEngine;

namespace AzuAutoStore.Util;

public class ChestPingEffect : MonoBehaviour
{
    private void Awake()
    {
        if (!string.IsNullOrWhiteSpace(AzuAutoStorePlugin.PingVfxString.Value))
        {
            Object.Instantiate(ZNetScene.instance.GetPrefab(AzuAutoStorePlugin.PingVfxString.Value), transform.position, Quaternion.identity);
            Trigger();
        }
    }

    public void Trigger() => InvokeRepeating(nameof(DestroyNow), 10f, 1f); // 10 seconds after the awake, it will start to destroy the object

    public void DestroyNow()
    {
        DestroyImmediate(this);
    }
}