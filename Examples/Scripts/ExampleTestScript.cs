using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;

public class ExampleTestScript : MonoBehaviour
{
    // MUST BE PUBLIC for AnimatorTrigger to access it!
    public List<GameObject> myList = new List<GameObject>();

    private InputAction addAction;
    private InputAction removeAction;

    private void Awake()
    {
        addAction = new InputAction(
            name: "AddItem",
            binding: "<Keyboard>/space"
        );

        removeAction = new InputAction(
            name: "RemoveItem",
            binding: "<Keyboard>/backspace"
        );

        addAction.performed += _ => AddItem();
        removeAction.performed += _ => RemoveItem();
    }

    private void OnEnable()
    {
        addAction.Enable();
        removeAction.Enable();
    }

    private void OnDisable()
    {
        addAction.Disable();
        removeAction.Disable();
    }

    private void Start()
    {
        Debug.Log($"[TestListScript] Starting with {myList.Count} items");
    }

    private void AddItem()
    {
        myList.Add(gameObject);
        Debug.Log($"[TestListScript] Added item! Count is now: {myList.Count}");
    }

    private void RemoveItem()
    {
        if (myList.Count <= 0) return;

        myList.RemoveAt(0);
        Debug.Log($"[TestListScript] Removed item! Count is now: {myList.Count}");
    }
}
