using System.Collections;
using UnityEngine;

#nullable enable

public class CoroutineHandler
{
    private readonly MonoBehaviour owner = null!;

    private Coroutine? coroutine = null;

    public CoroutineHandler(MonoBehaviour owner)
    {
        if (owner == null)
        {
            Debug.LogError($"{nameof(owner)} is null");
            return;
        }

        this.owner = owner;
    }

    public bool IsRunning => coroutine != null;

    public void Start(IEnumerator routine)
    {
        if (routine == null)
        {
            Debug.LogError($"{nameof(routine)} is null.");
            return;
        }

        if (IsRunning)
        {
            return;
        }

        coroutine = owner.StartCoroutine(Run(routine));
    }

    public void RestartWith(IEnumerator routine)
    {
        Stop();
        Start(routine);
    }

    public void Stop()
    {
        if (!IsRunning)
        {
            return;
        }

        owner.StopCoroutine(coroutine);
        coroutine = null;
    }

    private IEnumerator Run(IEnumerator routine)
    {
        yield return routine;
        coroutine = null;
    }
}
