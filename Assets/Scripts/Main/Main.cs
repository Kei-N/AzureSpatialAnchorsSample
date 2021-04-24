using UniRx;
using UnityEngine;

/// <summary>
/// エントリーポイントを定義します。
/// </summary>
public class Main : MonoBehaviour
{
    [SerializeField] private SpatialAnchorController m_SpatialAnchorController = null;
    [SerializeField] private UIManager m_UIManager = null;

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
        m_SpatialAnchorController.Initialize();
        m_UIManager.InitializeManager();

        m_UIManager.OnClickedButton
            .Subscribe(async appProcess =>
            {
                await m_SpatialAnchorController.ExecuteProcessAsync(appProcess);
            })
            .AddTo(this);

        m_SpatialAnchorController.OnChangedUI
            .Subscribe(appProcess =>
            {
                m_UIManager.ChangeUI(appProcess.Item1, appProcess.Item2);
            })
            .AddTo(this);
    }
}
