using Microsoft.Azure.SpatialAnchors;
using Microsoft.Azure.SpatialAnchors.Unity;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// エントリーポイントを定義します。
/// </summary>
public class Main : MonoBehaviour
{
    [SerializeField]
    private AppModeManager m_AppModeManager = null;

    /// <summary>
    /// エントリーポイント
    /// </summary>
    private void Start()
    {
        Initialize();
    }

    /// <summary>
    /// 初期化処理
    /// </summary>
    private void Initialize()
    {
        m_AppModeManager.InitializeManager();
    }
}
