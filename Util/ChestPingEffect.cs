using UnityEngine;

namespace AzuAutoStore.Util;

public class ChestPingEffect : MonoBehaviour
{
    GameObject pingObject;

    private void Awake()
    {
        if (!string.IsNullOrWhiteSpace(AzuAutoStorePlugin.PingVfxString.Value))
        {
            pingObject = Object.Instantiate(ZNetScene.instance.GetPrefab(AzuAutoStorePlugin.PingVfxString.Value), transform.position, Quaternion.identity);
            Trigger();
        }
    }

    public void Trigger() => InvokeRepeating(nameof(DestroyNow), 10f, 1f); // 10 seconds after the awake, it will start to destroy the object

    public void DestroyNow()
    {
        if (pingObject != null)
            ZNetScene.instance.Destroy(pingObject);
        DestroyImmediate(this);
    }
}