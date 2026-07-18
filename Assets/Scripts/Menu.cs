using System.Collections;
using UnityEngine;

public class Menu : MonoBehaviour
{
    private void Start()
    {
        StartCoroutine(PreloadMenu());
    }

    private IEnumerator PreloadMenu()
    {
        // Activate the GameObject so Unity initializes all components (e.g., UI elements, layout, etc.)
        gameObject.SetActive(true);

        // Wait one frame to allow Unity to complete initialization processes
        yield return null;

        // Deactivate the GameObject again — now it's preloaded and ready to show instantly later
        gameObject.SetActive(false);
    }
}
