using Microsoft.Azure.SpatialAnchors;
using Microsoft.Azure.SpatialAnchors.Unity;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// �G���g���[�|�C���g���`���܂��B
/// </summary>
public class Main : MonoBehaviour
{
    [SerializeField]
    private AppModeManager m_AppModeManager = null;

    /// <summary>
    /// �G���g���[�|�C���g
    /// </summary>
    private void Start()
    {
        Initialize();
    }

    /// <summary>
    /// ����������
    /// </summary>
    private void Initialize()
    {
        m_AppModeManager.InitializeManager();
    }
}
