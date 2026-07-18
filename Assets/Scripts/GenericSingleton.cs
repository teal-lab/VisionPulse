// This code is from here: https://awesometuts.com/blog/singletons-unity?utm_source=reddit&utm_medium=r_unity3d&utm_campaign=singletons_unity

using UnityEngine;
using UnityEngine.SceneManagement;

public class GenericSingleton<T> : MonoBehaviour
    where T : Component
{
    private static T instance;

    public static T Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindFirstObjectByType<T>();

                // If it's null again create a new object
                // and attach the generic instance
                if (instance == null)
                {
                    GameObject obj = new()
                    {
                        name = typeof(T).Name,
                    };

                    instance = obj.AddComponent<T>();
                }
            }

            return instance;
        }
    }

    protected virtual void Awake()
    {
        if (instance == null)
        {
            instance = this as T;

            // The singleton will not be destroyed if we move between the scenes
            DontDestroyOnLoad(gameObject);

            SceneManager.sceneLoaded += HandleSceneLoaded;
        }
        else
        {
            // Ensures that we only have one singleton in a scene
            Destroy(gameObject);
        }
    }

    protected virtual void OnDestroy()
    {
        if (instance == this)
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
        }
    }

    protected virtual void OnSceneLoaded()
    {
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        OnSceneLoaded();
    }
}