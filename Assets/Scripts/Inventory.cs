using System.Collections.Generic;
using UnityEngine;

public class Inventory : MonoBehaviour
{
    [Header("Inventory Items")]
    [SerializeField] private List<string> items = new List<string>();

    public void AddItem(string itemName)
    {
        if (!items.Contains(itemName))
        {
            items.Add(itemName);
            Debug.Log($"Added {itemName} to inventory.");
        }
    }

    public bool HasItem(string itemName)
    {
        return items.Contains(itemName);
    }

    public void RemoveItem(string itemName)
    {
        if (items.Contains(itemName))
        {
            items.Remove(itemName);
            Debug.Log($"Removed {itemName} from inventory.");
        }
    }

    public List<string> GetAllItems()
    {
        return items;
    }

    public void ClearInventory()
    {
        items.Clear();
    }
}
