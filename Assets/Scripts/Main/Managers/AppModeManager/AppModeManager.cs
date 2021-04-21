using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AppModeManager : MonoBehaviour
{
    [SerializeField]
    private AppMode m_AppMode = AppMode.OneAnchor;
    [SerializeField]
    private OneAnchorController m_OneAnchor = null;
    [SerializeField]
    private MultipleAnchorsController m_MultipleAnchors = null;

    /// <summary>
    /// èâä˙âªèàóù
    /// </summary>
    public void InitializeManager()
    {
        switch (m_AppMode)
        {
            case AppMode.OneAnchor:
                m_OneAnchor.gameObject.SetActive(true);
                m_MultipleAnchors.gameObject.SetActive(false);
                m_OneAnchor.Initialize();
                break;
            case AppMode.MultipleAnchors:
                m_OneAnchor.gameObject.SetActive(false);
                m_MultipleAnchors.gameObject.SetActive(true);
                m_MultipleAnchors.Initialize();
                break;
            default:
                Debug.Log("AppModeManager cannot initialized.");
                break;
        }
    }
}

internal enum AppMode
{
    OneAnchor = 0,
    MultipleAnchors,
}