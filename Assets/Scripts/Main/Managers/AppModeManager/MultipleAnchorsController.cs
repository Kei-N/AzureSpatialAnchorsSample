using Microsoft.Azure.SpatialAnchors;
using Microsoft.Azure.SpatialAnchors.Unity;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

public class MultipleAnchorsController : MonoBehaviour
{
    [SerializeField]
    private SpatialAnchorManager m_SpatialAnchorManager = null;
    [SerializeField]
    private GameObject m_AnchorPrefab = null;

    private AnchorLocateCriteria _anchorLocateCriteria = null;
    private CloudSpatialAnchor _currentCloudAnchor;
    private List<string> _anchorIds = new List<string>();
    private AppState _currentAppState = AppState.CreateSession;
    private GameObject _layoutAnchorObj = null;
    private GameObject _spawnedObject = null;
    private Material _spawnedObjectMat = null;
    private List<GameObject> _allSpawnedObjects = new List<GameObject>();
    private List<Material> _allSpawnedMaterials = new List<Material>();
    private int numToMake = 3;
    private CloudSpatialAnchorWatcher _currentWatcher;

    /// <summary>
    /// �����̗���
    /// </summary>
    private enum AppState
    {
        CreateSession = 0,
        ConfigureSession,
        StartSession,
        Placing,
        Saving,
        ReadyToSearch,
        Searching,
        ReadyToNeighborQuery,
        Neighboring,
        Done,
        ModeCount,
    }

    /// <summary>
    /// ����������
    /// </summary>
    public void Initialize()
    {
        //m_SpatialAnchorManager.AnchorLocated += SpatialAnchorManager_AnchorLocated;
        _anchorLocateCriteria = new AnchorLocateCriteria();
    }

    /// <summary>
    /// �A�v���P�[�V�����̊e�폈�������s����
    /// </summary>
    public async void ExecuteProcess()
    {
        switch (_currentAppState)
        {
            case AppState.Placing:
                if (_spawnedObject != null)
                {
                    _currentAppState = AppState.Saving;
                    if (!m_SpatialAnchorManager.IsSessionStarted)
                    {
                        await m_SpatialAnchorManager.StartSessionAsync();
                    }
                    await SaveCurrentObjectAnchorToCloudAsync();
                }
                break;
            case AppState.ReadyToSearch:
                //await DoSearchingPassAsync();
                break;
            case AppState.ReadyToNeighborQuery:
                //DoNeighboringPassAsync();
                break;
            case AppState.Done:
                await m_SpatialAnchorManager.ResetSessionAsync();
                //CleanupObjectsBetweenPasses();
                _currentAppState = AppState.Placing;
                Debug.Log($"Place an object. {_allSpawnedObjects.Count}/{numToMake} ");
                break;
        }
    }

    /// <summary>
    /// �A���J�[���N���E�h�ɕۑ�����
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
                await Task.CompletedTask;
                _anchorIds.Add(_currentCloudAnchor.Identifier);
                Pose anchorPose = Pose.identity;
#if UNITY_ANDROID || UNITY_IOS
                anchorPose = _currentCloudAnchor.GetPose();
#endif
                // HoloLens: The position will be set based on the unityARUserAnchor that was located.

                _spawnedObject = null;
                _currentCloudAnchor = null;
                if (_allSpawnedObjects.Count < numToMake)
                {
                    Debug.Log($"Saved...Make another {_allSpawnedObjects.Count}/{numToMake} ");
                    _currentAppState = AppState.Placing;
                }
                else
                {
                    Debug.Log("Saved... ready to start finding them.");
                    _currentAppState = AppState.ReadyToSearch;
                }
            }
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            Debug.Log("Failed to save anchor " + exception.ToString());
        }
    }
}