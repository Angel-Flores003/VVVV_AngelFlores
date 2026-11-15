using System.Collections.Generic;
using UnityEngine;

public class ObjectPool : MonoBehaviour
{
    [SerializeField] private GameObject prefab;
    [SerializeField] private int initialSize = 10;

    private Stack<GameObject> freeObjects = new Stack<GameObject>();

    private void Awake()
    {
        for (int i = 0; i < initialSize; i++)
        {
            freeObjects.Push(CreateNewObject());
        }
    }

    private GameObject CreateNewObject()
    {
        GameObject newObject = Instantiate(prefab, transform);
        newObject.SetActive(false);
        return newObject;
    }

    public GameObject GetObject(Vector3 position)
    {
        GameObject objectToUse;

        if (freeObjects.Count > 0)
        {
            objectToUse = freeObjects.Pop();
        }
        else
        {
            objectToUse = CreateNewObject();
        }

        //objectToUse.transform.localPosition = position;
        objectToUse.transform.position = position;
        objectToUse.SetActive(true);
        return objectToUse;
    }

    public void ReturnObject(GameObject objectToReturn)
    {
        objectToReturn.SetActive(false);
        freeObjects.Push(objectToReturn);
    }
}