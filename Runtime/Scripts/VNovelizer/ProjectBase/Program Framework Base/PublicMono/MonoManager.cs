using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class MonoManager :BaseManager<MonoManager>
{
    public MonoController controller;
    public MonoManager()
    {
        EnsureController();
    }

    private MonoController EnsureController()
    {
        if (controller != null)
            return controller;

        GameObject obj = new GameObject("MonoController");
        controller = obj.AddComponent<MonoController>();
        return controller;
    }

    //给外部提供的添加帧更新事件的函数
    public void AddUpdateListener(UnityAction fun)
    {
        EnsureController().AddUpdateListener(fun);

    }
    //给外部提供的移除帧更新事件的函数
    public void RemoveUpdateListener(UnityAction fun)
    {
        EnsureController().RemoveUpdateListener(fun);
    }

    public Coroutine StartCoroutine(IEnumerator routine)
    {
        return EnsureController().StartCoroutine(routine);
    }

    public Coroutine StartCoroutine(string methodName, object value)
    {
        return EnsureController().StartCoroutine(methodName, value);
    }

    public Coroutine StartCoroutine(string methodName)
    {
        return EnsureController().StartCoroutine(methodName);
    }

    public Coroutine StartCoroutine_Auto(IEnumerator routine)
    {
        return EnsureController().StartCoroutine(routine);
    }

    public void StopCoroutine(Coroutine routine)
    {
        if (routine != null)
        {
            EnsureController().StopCoroutine(routine);
        }
    }

    public void StopAllCoroutines()
    {
        EnsureController().StopAllCoroutines();
    }
}
