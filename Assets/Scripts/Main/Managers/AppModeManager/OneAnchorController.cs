using Microsoft.Azure.SpatialAnchors;
using Microsoft.Azure.SpatialAnchors.Unity;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

public class OneAnchorController : MonoBehaviour
{
    [SerializeField]
    private SpatialAnchorManager m_SpatialAnchorManager = null;
    [SerializeField]
    private GameObject m_AnchorPrefab = null;

    private AnchorLocateCriteria _anchorLocateCriteria = null;
    private CloudSpatialAnchor _currentCloudAnchor;
    private string _currentAnchorId;
    private AppState _currentAppState = AppState.CreateSession;
    private GameObject _layoutAnchorObj = null;
    private GameObject _spawnedObject = null;
    private Material _spawnedObjectMat = null;
    private CloudSpatialAnchorWatcher _currentWatcher;

    /// <summary>
    /// 処理の流れ
    /// </summary>
    private enum AppState
    {
        CreateSession = 0,
        ConfigureSession,
        StartSession,
        LayoutLocalAnchor,
        CreateLocalAnchor,
        SaveCloudAnchor,
        StopSession,
        DestroySession,
        CreateSessionForQuery,
        StartSessionForQuery,
        LookForAnchor,
        LookingForAnchor,
        DeleteFoundAnchor,
        StopSessionForQuery,
        Complete,
        Processing,
    }

    /// <summary>
    /// 初期化処理
    /// </summary>
    public void Initialize()
    {
        m_SpatialAnchorManager.AnchorLocated += SpatialAnchorManager_AnchorLocated;
        _anchorLocateCriteria = new AnchorLocateCriteria();
    }

    private void OnDestroy()
    {
        if (m_SpatialAnchorManager != null)
        {
            m_SpatialAnchorManager.StopSession();
        }

        if (_currentWatcher != null)
        {
            _currentWatcher.Stop();
            _currentWatcher = null;
        }

        CleanupSpawnedObjects();
    }

    /// <summary>
    /// アプリケーションの各種処理を実行する
    /// </summary>
    public async void ExecuteProcess()
    {
        switch (_currentAppState)
        {
            case AppState.CreateSession:
                Debug.Log("AppState.CreateSession Start");
                _currentAppState = AppState.Processing;
                if (m_SpatialAnchorManager.Session == null)
                {
                    await m_SpatialAnchorManager.CreateSessionAsync();
                }
                _currentAnchorId = "";
                _currentCloudAnchor = null;
                _currentAppState = AppState.ConfigureSession;
                Debug.Log("AppState.CreateSession End");
                break;
            case AppState.ConfigureSession:
                Debug.Log("AppState.ConfigureSession Start");
                _currentAppState = AppState.Processing;
                ConfigureSession();
                _currentAppState = AppState.StartSession;
                Debug.Log("AppState.ConfigureSession End");
                break;
            case AppState.StartSession:
                Debug.Log("AppState.StartSession Start");
                _currentAppState = AppState.Processing;
                await m_SpatialAnchorManager.StartSessionAsync();
                var identifiers = FileUtility.ReadFile();
                if (identifiers != null)
                {
                    _currentAppState = AppState.LookForAnchor;
                }
                else
                {
                    _currentAppState = AppState.LayoutLocalAnchor;
                }
                Debug.Log("AppState.StartSession End");
                break;
            case AppState.LayoutLocalAnchor:
                Debug.Log("AppState.LayoutLocalAnchor Start");
                _currentAppState = AppState.Processing;
                _layoutAnchorObj = Instantiate(m_AnchorPrefab, Camera.main.transform.position + Vector3.forward, m_AnchorPrefab.transform.rotation);
                _currentAppState = AppState.CreateLocalAnchor;
                Debug.Log("AppState.LayoutLocalAnchor End");
                break;
            case AppState.CreateLocalAnchor:
                Debug.Log("AppState.CreateLocalAnchor Start");
                _currentAppState = AppState.Processing;
                SpawnOrMoveCurrentAnchoredObject(_layoutAnchorObj.transform.position, _layoutAnchorObj.transform.rotation);
                Destroy(_layoutAnchorObj);
                if (_spawnedObject != null)
                {
                    _currentAppState = AppState.SaveCloudAnchor;
                }
                else
                {
                    _currentAppState = AppState.CreateLocalAnchor;
                }
                Debug.Log("AppState.CreateLocalAnchor End");
                break;
            case AppState.SaveCloudAnchor:
                Debug.Log("AppState.SaveCloudAnchor Start");
                _currentAppState = AppState.Processing;
                await SaveCurrentObjectAnchorToCloudAsync();
                _currentAppState = AppState.StopSession;
                Debug.Log("AppState.SaveCloudAnchor End");
                break;
            case AppState.StopSession:
                Debug.Log("AppState.StopSession Start");
                _currentAppState = AppState.Processing;
                m_SpatialAnchorManager.StopSession();
                CleanupSpawnedObjects();
                await m_SpatialAnchorManager.ResetSessionAsync();
                _currentAppState = AppState.CreateSessionForQuery;
                Debug.Log("AppState.StopSession End");
                break;
            case AppState.CreateSessionForQuery:
                Debug.Log("AppState.CreateSessionForQuery Start");
                ConfigureSession();
                _currentAppState = AppState.StartSessionForQuery;
                Debug.Log("AppState.CreateSessionForQuery End");
                break;
            case AppState.StartSessionForQuery:
                Debug.Log("AppState.StartSessionForQuery Start");
                _currentAppState = AppState.Processing;
                await m_SpatialAnchorManager.StartSessionAsync();
                _currentAppState = AppState.LookForAnchor;
                Debug.Log("AppState.StartSessionForQuery End");
                break;
            case AppState.LookForAnchor:
                Debug.Log("AppState.LookForAnchor Start");
                _currentAppState = AppState.LookingForAnchor;
                if (_currentWatcher != null)
                {
                    _currentWatcher.Stop();
                    _currentWatcher = null;
                }
                _currentWatcher = CreateWatcher();
                if (_currentWatcher == null)
                {
                    Debug.Log("Either cloudmanager or session is null, should not be here!");
                    _currentAppState = AppState.LookForAnchor;
                }
                break;
            case AppState.LookingForAnchor:
                Debug.Log("AppState.LookingForAnchor");
                break;
            case AppState.DeleteFoundAnchor:
                Debug.Log("AppState.DeleteFoundAnchor Start");
                _currentAppState = AppState.Processing;
                await m_SpatialAnchorManager.DeleteAnchorAsync(_currentCloudAnchor);
                CleanupSpawnedObjects();
                _currentAppState = AppState.StopSessionForQuery;
                Debug.Log("AppState.DeleteFoundAnchor End");
                break;
            case AppState.StopSessionForQuery:
                Debug.Log("AppState.StopSessionForQuery Start");
                _currentAppState = AppState.Processing;
                m_SpatialAnchorManager.StopSession();
                _currentWatcher.Stop();
                _currentWatcher = null;
                _currentAppState = AppState.Complete;
                Debug.Log("AppState.StopSessionForQuery End");
                break;
            case AppState.Complete:
                Debug.Log("AppState.Complete Start");
                _currentAppState = AppState.Processing;
                _currentCloudAnchor = null;
                CleanupSpawnedObjects();
                _currentAppState = AppState.CreateSession;
                Debug.Log("AppState.Complete End");
                break;
            case AppState.Processing:
                Debug.Log("AppState.Processing");
                break;
            default:
                Debug.Log("Shouldn't get here for app state " + _currentAppState.ToString());
                break;
        }
    }

    private void SpawnOrMoveCurrentAnchoredObject(Vector3 worldPos, Quaternion worldRot)
    {
        // Create the object if we need to, and attach the platform appropriate
        // Anchor behavior to the spawned object
        if (_spawnedObject == null)
        {
            // Use factory method to create
            _spawnedObject = SpawnNewAnchoredObject(worldPos, worldRot, _currentCloudAnchor);

            // Update color
            _spawnedObjectMat = _spawnedObject.GetComponent<MeshRenderer>().material;
        }
        else
        {
            // Use factory method to move
            MoveAnchoredObject(_spawnedObject, worldPos, worldRot, _currentCloudAnchor);
        }
    }

    /// <summary>
    /// アンカー用のGameObjectを生成する
    /// </summary>
    protected virtual GameObject SpawnNewAnchoredObject(Vector3 worldPos, Quaternion worldRot, CloudSpatialAnchor cloudSpatialAnchor)
    {
        // Create the object like usual
        GameObject newGameObject = SpawnNewAnchoredObject(worldPos, worldRot);

        // If a cloud anchor is passed, apply it to the native anchor
        if (cloudSpatialAnchor != null)
        {
            CloudNativeAnchor cloudNativeAnchor = newGameObject.GetComponent<CloudNativeAnchor>();
            cloudNativeAnchor.CloudToNative(cloudSpatialAnchor);
        }

        // Return newly created object
        return newGameObject;
    }

    /// <summary>
    /// アンカー用のGameObjectを生成する
    /// </summary>
    private GameObject SpawnNewAnchoredObject(Vector3 worldPos, Quaternion worldRot)
    {
        // Create the prefab
        GameObject newGameObject = Instantiate(m_AnchorPrefab, worldPos, worldRot);

        // Attach a cloud-native anchor behavior to help keep cloud
        // and native anchors in sync.
        newGameObject.AddComponent<CloudNativeAnchor>();

        // Set the color
        newGameObject.GetComponent<MeshRenderer>().material.color = Color.yellow;

        // Return created object
        return newGameObject;
    }

    /// <summary>
    /// アンカーを移動する
    /// </summary>
    protected virtual void MoveAnchoredObject(GameObject objectToMove, Vector3 worldPos, Quaternion worldRot, CloudSpatialAnchor cloudSpatialAnchor = null)
    {
        // Get the cloud-native anchor behavior
        CloudNativeAnchor cna = objectToMove.GetComponent<CloudNativeAnchor>();

        // Warn and exit if the behavior is missing
        if (cna == null)
        {
            Debug.LogWarning($"The object {objectToMove.name} is missing the {nameof(CloudNativeAnchor)} behavior.");
            return;
        }

        // Is there a cloud anchor to apply
        if (cloudSpatialAnchor != null)
        {
            // Yes. Apply the cloud anchor, which also sets the pose.
            cna.CloudToNative(cloudSpatialAnchor);
        }
        else
        {
            // No. Just set the pose.
            cna.SetPose(worldPos, worldRot);
        }
    }

    /// <summary>
    /// アンカーをクラウドに保存する
    /// </summary>
    private async Task SaveCurrentObjectAnchorToCloudAsync()
    {
        // Get the cloud-native anchor behavior
        CloudNativeAnchor cna = _spawnedObject.GetComponent<CloudNativeAnchor>();

        // If the cloud portion of the anchor hasn't been created yet, create it
        if (cna.CloudAnchor == null)
        {
            await cna.NativeToCloud();
        }

        // Get the cloud portion of the anchor
        CloudSpatialAnchor cloudAnchor = cna.CloudAnchor;

        // In this sample app we delete the cloud anchor explicitly, but here we show how to set an anchor to expire automatically
        cloudAnchor.Expiration = DateTimeOffset.Now.AddDays(7);

        while (!m_SpatialAnchorManager.IsReadyForCreate)
        {
            await Task.Delay(330);
            float createProgress = m_SpatialAnchorManager.SessionStatus.RecommendedForCreateProgress;
            Debug.Log($"Move your device to capture more environment data: {createProgress:0%}");
        }

        Debug.Log("Saving...");

        try
        {
            // Actually save
            await m_SpatialAnchorManager.CreateAnchorAsync(cloudAnchor);

            // Store
            _currentCloudAnchor = cloudAnchor;

            if (_currentCloudAnchor != null)
            {
                // Await override, which may perform additional tasks
                // such as storing the key in the AnchorExchanger
                await Task.CompletedTask;
                _currentAnchorId = _currentCloudAnchor.Identifier;
                FileUtility.SaveFile(_currentAnchorId);
                Pose anchorPose = _currentCloudAnchor.GetPose();
                SpawnOrMoveCurrentAnchoredObject(anchorPose.position, anchorPose.rotation);
            }
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
        if ((m_SpatialAnchorManager != null) && (m_SpatialAnchorManager.Session != null))
        {
            return m_SpatialAnchorManager.Session.CreateWatcher(_anchorLocateCriteria);
        }
        else
        {
            return null;
        }
    }

    /// <summary>
    /// セッションの設定
    /// </summary>
    private void ConfigureSession()
    {
        List<string> anchorsToFind = new List<string>();
        if (_currentAppState == AppState.CreateSessionForQuery)
        {
            anchorsToFind.Add(_currentAnchorId);
        }

        var identifiers = FileUtility.ReadFile();
        if(identifiers != null)
        {
            anchorsToFind.AddRange(identifiers);
        }

        _anchorLocateCriteria.NearAnchor = new NearAnchorCriteria();
        _anchorLocateCriteria.Identifiers = anchorsToFind.ToArray();
    }

    /// <summary>
    /// 生成したアンカーをクリーンアップする
    /// </summary>
    private void CleanupSpawnedObjects()
    {
        if (_spawnedObject != null)
        {
            Destroy(_spawnedObject);
            _spawnedObject = null;
        }

        if (_spawnedObjectMat != null)
        {
            Destroy(_spawnedObjectMat);
            _spawnedObjectMat = null;
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
            _currentCloudAnchor = args.Anchor;

            UnityDispatcher.InvokeOnAppThread(() =>
            {
                Pose anchorPose = Pose.identity;

                anchorPose = _currentCloudAnchor.GetPose();

                SpawnOrMoveCurrentAnchoredObject(anchorPose.position, anchorPose.rotation);
                _currentAppState = AppState.DeleteFoundAnchor;
                Debug.Log("AppState.LookForAnchor End");
            });
        }
    }
}