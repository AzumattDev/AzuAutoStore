using UnityEngine;

namespace AzuAutoStore.Interfaces;

public interface IContainer
{
    public int TryStore();
    
    public int TryStoreThisItem(ItemDrop.ItemData item, Inventory playerInventory);
    public bool IsOwner();
    public GameObject gameObject {get;}
    public ZNetView m_nview {get;}
}