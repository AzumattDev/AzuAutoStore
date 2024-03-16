using UnityEngine;

namespace AzuAutoStore.Util;

public interface IContainer
{
    public int TryStore();
    public bool IsOwner();
    public GameObject gameObject {get;}
}