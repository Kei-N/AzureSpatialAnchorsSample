using Microsoft.Azure.SpatialAnchors;
using Microsoft.Azure.SpatialAnchors.Unity;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UniRx;
using UnityEngine;
using static Common;

public class SpatialAnchorController : MonoBehaviour
{
    [SerializeField] private SpatialAnchorManager m_SpatialAnchorManager = null;
    [SerializeField] private GameObject m_AnchorPrefab = null;

    private AppProcess _currentProcess = AppProcess.CreateAnchorMode;
    private GameObject _layoutAnchorGhost = null;
    private Dictionary<string, GameObject> _createdAnchorObjects = new Dictionary<string, GameObject>();
    private CloudSpatialAnchorWatcher _currentWatcher;
    private List<CloudSpatialAnchor> _existingCloudAnchors = new List<CloudSpatialAnchor>();

    /// <summary>
    /// UIの表示・非表示状態の変更
    /// </summary>
    private readonly Subject<(bool, AppProcess)> onChangedUI = new Subject<(bool, AppProcess)>();
    public IObservable<(bool, AppProcess)> OnChangedUI => onChangedUI;

    /// <summary>
    /// 初期化処理
    /// </summary>
    public void Initialize()
    {
        m_SpatialAnchorManager.AnchorLocated += SpatialAnchorManager_AnchorLocated;
        m_SpatialAnchorManager.LocateAnchorsCompleted += SpatialAnchorManager_LocateAnchorsCompleted;
    }

    /// <summary>
    /// アプリケーションの処理を実行する
    /// </summary>
    public async Task ExecuteProcessAsync(AppProcess appProcess)
    {
        _currentProcess = appProcess;
        switch (_currentProcess)
        {
            case AppProcess.CreateAnchorMode:
                await StartCreateAnchorModeAsync();
                break;
            case AppProcess.ReproduceAnchorMode:
                await StartReproduceAnchorModeAsync();
                break;
            case AppProcess.CreateAnchor:
                await CreateAnchorAsync();
                break;
            case AppProcess.DeleteAnchor:
                await DeleteAnchorAsync();
                break;
            case AppProcess.TopMenu:
                await ResetSessionAsync();
                break;
        }
    }

    /// <summary>
    /// 新規アンカー配置モードを開始
    /// </summary>
    private async Task StartCreateAnchorModeAsync()
    {
        // ボタンを非表示
        onChangedUI.OnNext((false, AppProcess.CreateAnchorMode));
        onChangedUI.OnNext((false, AppProcess.ReproduceAnchorMode));
        // セッションを開始
        await StartSessionAsync();
        // レイアウト用のアンカーゴーストを生成
        CreateLayoutAnchorGhost();
        // ボタンを表示
        onChangedUI.OnNext((true, AppProcess.CreateAnchor));
        onChangedUI.OnNext((true, AppProcess.TopMenu));
    }

    /// <summary>
    /// 既存アンカー再現モードを開始
    /// </summary>
    private async Task StartReproduceAnchorModeAsync()
    {
        // ボタンを非表示
        onChangedUI.OnNext((false, AppProcess.CreateAnchorMode));
        onChangedUI.OnNext((false, AppProcess.ReproduceAnchorMode));
        // セッションを開始
        await StartSessionAsync();
        // ウォッチャーを生成
        if (_currentWatcher != null)
        {
            _currentWatcher.Stop();
            _currentWatcher = null;
        }
        _currentWatcher = CreateWatcher();
        // ボタンを表示
        onChangedUI.OnNext((true, AppProcess.TopMenu));
    }

    /// <summary>
    /// セッションの作成・開始
    /// </summary>
    private async Task StartSessionAsync()
    {
        // セッションの作成
        if (m_SpatialAnchorManager.Session == null)
        {
            Debug.Log("セッションを作成します。");
            await m_SpatialAnchorManager.CreateSessionAsync();
        }
        // セッションの開始
        if (!m_SpatialAnchorManager.IsSessionStarted)
        {
            Debug.Log("セッションを開始します。");
            await m_SpatialAnchorManager.StartSessionAsync();
        }
    }

    /// <summary>
    /// レイアウト用のアンカーゴーストを生成
    /// </summary>
    private void CreateLayoutAnchorGhost()
    {
        Debug.Log("レイアウト用のアンカーゴーストを生成します。");
        if (_layoutAnchorGhost == null) _layoutAnchorGhost = Instantiate(m_AnchorPrefab.gameObject, Camera.main.transform.position + Camera.main.transform.forward, m_AnchorPrefab.transform.rotation);
    }

    /// <summary>
    /// アンカーを配置し、アンカーをクラウドに保存、アンカーIndentifierをローカルに保存
    /// </summary>
    /// <returns></returns>
    private async Task CreateAnchorAsync()
    {
        // ボタンを非表示
        onChangedUI.OnNext((false, AppProcess.CreateAnchor));
        onChangedUI.OnNext((false, AppProcess.TopMenu));
        // アンカーを生成
        Debug.Log("アンカーを生成します。");
        var anchorObject = CreateAnchorObject(_layoutAnchorGhost.transform.position, _layoutAnchorGhost.transform.rotation);
        Destroy(_layoutAnchorGhost);
        // アンカー情報を保存
        Debug.Log("アンカー情報を保存します。");
        await SaveAnchorAsync(anchorObject);
        // レイアウト用のアンカーゴーストを生成
        CreateLayoutAnchorGhost();
        // ボタンを表示
        onChangedUI.OnNext((true, AppProcess.CreateAnchor));
        onChangedUI.OnNext((true, AppProcess.TopMenu));
    }

    /// <summary>
    /// アンカーオブジェクトを生成する
    /// </summary>
    private GameObject CreateAnchorObject(Vector3 worldPos, Quaternion worldRot)
    {
        // アンカー用のGameObjectを生成
        GameObject anchorObject = Instantiate(m_AnchorPrefab.gameObject, worldPos, worldRot);

        // CloudNativeAnchorコンポーネントをアタッチ
        anchorObject.AddComponent<CloudNativeAnchor>();

        // 色を設定
        anchorObject.GetComponent<MeshRenderer>().material.color = Color.yellow;

        // コライダーを非アクティブ化
        anchorObject.GetComponent<BoxCollider>().enabled = false;

        return anchorObject;
    }

    /// <summary>
    /// アンカーをクラウドに保存・アンカーIdentifierをローカルに保存
    /// </summary>
    private async Task SaveAnchorAsync(GameObject anchorObject)
    {
        // CloudNativeAnchorコンポーネントを取得
        CloudNativeAnchor cna = anchorObject.GetComponent<CloudNativeAnchor>();

        // CloudAnchorのCloud Positionが未生成の場合、生成する
        if (cna.CloudAnchor == null) await cna.NativeToCloud();
        CloudSpatialAnchor cloudAnchor = cna.CloudAnchor;

        // アンカーの有効期限を設定
        cloudAnchor.Expiration = DateTimeOffset.Now.AddDays(7);

        // 現実空間の特徴点の収集が十分であるかの判定
        while (!m_SpatialAnchorManager.IsReadyForCreate)
        {
            await Task.Delay(330);
            float createProgress = m_SpatialAnchorManager.SessionStatus.RecommendedForCreateProgress;
            Debug.Log($"Move your device to capture more environment data: {createProgress:0%}");
        }

        Debug.Log("Saving...");

        try
        {
            // クラウドにアンカーを保存
            await m_SpatialAnchorManager.CreateAnchorAsync(cloudAnchor);

            // ローカルにアンカーIdentifierを保存
            FileUtility.SaveFile(cloudAnchor.Identifier);

            // アンカーIdentifierとGameObjectをキャッシュする
            _createdAnchorObjects.Add(cloudAnchor.Identifier, anchorObject);

            Debug.Log($"Saved anchor. Idendifier is {cloudAnchor.Identifier}");

        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            Debug.Log("Failed to save anchor " + exception.ToString());
        }
    }

    /// <summary>
    /// ウォッチャーを生成する
    /// </summary>
    private CloudSpatialAnchorWatcher CreateWatcher()
    {
        Debug.Log("ウォッチャーを生成します。");
        if ((m_SpatialAnchorManager != null) && (m_SpatialAnchorManager.Session != null))
        {
            Debug.Log("アンカーを捜索中...");
            var anchorLocateCriteria = ConfigureAnchorLocateCriteria();
            return m_SpatialAnchorManager.Session.CreateWatcher(anchorLocateCriteria);
        }
        else
        {
            Debug.Log("ウォッチャーが生成できません。");
            return null;
        }
    }

    /// <summary>
    /// アンカー検索条件を設定
    /// </summary>
    private AnchorLocateCriteria ConfigureAnchorLocateCriteria()
    {
        var anchorLocateCriteria = new AnchorLocateCriteria();
        var anchorsToFind = new List<string>();
        var identifiers = FileUtility.ReadFile();
        if (identifiers != null)
        {
            anchorsToFind.AddRange(identifiers);
        }
        else
        {
            Debug.Log("設定可能なアンカーIdentifierがありません。");
        }
        anchorLocateCriteria.Identifiers = anchorsToFind.ToArray();

        return anchorLocateCriteria;
    }

    /// <summary>
    /// クラウドのアンカーを全削除
    /// </summary>
    private async Task DeleteAnchorAsync()
    {
        if (_existingCloudAnchors.Count == 0) return;
        // ボタンを非表示
        onChangedUI.OnNext((false, AppProcess.DeleteAnchor));
        onChangedUI.OnNext((false, AppProcess.TopMenu));
        // クラウドアンカーを全削除
        Debug.Log("クラウドアンカーを全削除します。");
        foreach (var cloudAnchor in _existingCloudAnchors) await m_SpatialAnchorManager.DeleteAnchorAsync(cloudAnchor);
        _existingCloudAnchors.Clear();
        // ローカルアンカーを全削除
        DeleteLocalAnchor();
        // ローカルのアンカーIdentifierを全削除
        FileUtility.ResetFile();
        // ボタンを表示
        onChangedUI.OnNext((true, AppProcess.TopMenu));
    }

    /// <summary>
    /// ローカルのアンカーを全削除
    /// </summary>
    private void DeleteLocalAnchor()
    {
        Debug.Log("ローカルアンカーを全削除します。");
        if (_layoutAnchorGhost != null) Destroy(_layoutAnchorGhost);
        foreach (var anchorObject in _createdAnchorObjects.Values) Destroy(anchorObject);
        _createdAnchorObjects.Clear();
    }

    /// <summary>
    /// セッションを停止、ローカルアンカーを破棄、ウォッチャーを停止
    /// </summary>
    private async Task ResetSessionAsync()
    {
        // ボタンを非表示
        onChangedUI.OnNext((false, AppProcess.CreateAnchor));
        onChangedUI.OnNext((false, AppProcess.DeleteAnchor));
        onChangedUI.OnNext((false, AppProcess.TopMenu));

        // 配置したローカルアンカーを削除
        DeleteLocalAnchor();

        // セッションをリセット
        Debug.Log("セッションをリセットします。");
        await m_SpatialAnchorManager.ResetSessionAsync();

        // ウォッチャーを停止
        if (_currentWatcher != null)
        {
            Debug.Log("ウォッチャーを停止します。");
            _currentWatcher.Stop();
            _currentWatcher = null;
        }

        // ボタンを表示
        if (FileUtility.ReadFile() == null)
        {
            onChangedUI.OnNext((true, AppProcess.CreateAnchorMode));
        }
        else
        {
            onChangedUI.OnNext((true, AppProcess.CreateAnchorMode));
            onChangedUI.OnNext((true, AppProcess.ReproduceAnchorMode));
        }

    }

    /// <summary>
    /// アンカーが検知されたときに呼び出される
    /// </summary>
    private void SpatialAnchorManager_AnchorLocated(object sender, AnchorLocatedEventArgs args)
    {
        Debug.LogFormat("Anchor recognized as a possible anchor {0} {1}", args.Identifier, args.Status);
        if (args.Status == LocateAnchorStatus.Located)
        {
            // 引数からCloudSpatialAnchorを取得
            var cloudAnchor = args.Anchor;

            UnityDispatcher.InvokeOnAppThread(() =>
            {
                Pose anchorPose = Pose.identity;
                // アンカーの位置を取得
                anchorPose = cloudAnchor.GetPose();
                // アンカーを生成
                var anchorObject = CreateAnchorObject(anchorPose.position, anchorPose.rotation);
                // アンカー情報をキャッシュ
                _createdAnchorObjects.Add(cloudAnchor.Identifier, anchorObject);
                _existingCloudAnchors.Add(cloudAnchor);

                Debug.Log($"Reproduce anchor. Idendifier is {cloudAnchor.Identifier}");
            });
        }
    }

    /// <summary>
    /// Watcherのすべてのアンカーに対する検索操作が完了したことを通知します。
    /// (探知されたかどうかは問われません)
    /// </summary>
    private void SpatialAnchorManager_LocateAnchorsCompleted(object sender, LocateAnchorsCompletedEventArgs args)
    {
        Debug.Log($"アンカーの検索が完了し、{_existingCloudAnchors.Count}個のアンカーが見つかりました。");
        UnityDispatcher.InvokeOnAppThread(() =>
        {
            onChangedUI.OnNext((true, AppProcess.DeleteAnchor));
        });
    }
}
