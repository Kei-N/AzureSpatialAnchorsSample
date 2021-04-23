using Microsoft.Azure.SpatialAnchors;
using Microsoft.Azure.SpatialAnchors.Unity;
using Microsoft.MixedReality.Toolkit.UI;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

public class MultipleAnchorsController : MonoBehaviour
{
    [SerializeField] private SpatialAnchorManager m_SpatialAnchorManager = null;
    [SerializeField] private GameObject m_AnchorPrefab = null;
    [SerializeField] private GameObject m_TopMenu = null;
    [SerializeField] private Interactable m_CreateAnchorModeButton = null;
    [SerializeField] private Interactable m_ReproduceAnchorModeButton = null;
    [SerializeField] private Interactable m_CreateAnchorButton = null;
    [SerializeField] private Interactable m_DeleteAnchorButton = null;
    [SerializeField] private Interactable m_TopMenuButton = null;

    private AppProcess _currentProcess = AppProcess.CreateAnchor;
    private GameObject _layoutAnchorGhost = null;
    private Dictionary<string, GameObject> _createdAnchorObjects = new Dictionary<string, GameObject>();
    private CloudSpatialAnchorWatcher _currentWatcher;
    private List<CloudSpatialAnchor> _existingCloudAnchors = new List<CloudSpatialAnchor>();

    private enum AppProcess
    {
        CreateAnchor = 0,
        ReproduceAnchor,
    }

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
        if (FileUtility.ReadFile() == null) m_ReproduceAnchorModeButton.gameObject.SetActive(false);
        m_CreateAnchorModeButton.OnClick.AddListener(async () => await SelectProcessAsync(AppProcess.CreateAnchor));
        m_ReproduceAnchorModeButton.OnClick.AddListener(async () => await SelectProcessAsync(AppProcess.ReproduceAnchor));
        m_CreateAnchorButton.OnClick.AddListener(async () => await CreateAnchorAsync());
        m_DeleteAnchorButton.OnClick.AddListener(async () => await DeleteAnchorAsync());
        m_TopMenuButton.OnClick.AddListener(async () => await ResetSessionAsync());

        m_SpatialAnchorManager.AnchorLocated += SpatialAnchorManager_AnchorLocated;
        m_SpatialAnchorManager.LocateAnchorsCompleted += SpatialAnchorManager_LocateAnchorsCompleted;
    }

    /// <summary>
    /// �A�v���P�[�V�����̏�����I������
    /// </summary>
    /// <param name="appProcess"></param>
    private async Task SelectProcessAsync(AppProcess appProcess)
    {
        _currentProcess = appProcess;
        m_TopMenu.SetActive(false);
        await StartSessionAsync();
        switch (_currentProcess)
        {
            case AppProcess.CreateAnchor:
                // �A���J�[�z�u�p�̃A���J�[�S�[�X�g�𐶐�
                CreateLayoutAnchorGhost();
                // �{�^����\��
                m_CreateAnchorButton.gameObject.SetActive(true);
                m_TopMenuButton.gameObject.SetActive(true);
                break;
            case AppProcess.ReproduceAnchor:
                // �A���J�[��T�����߂ɃE�H�b�`���[�𐶐�����
                if (_currentWatcher != null)
                {
                    _currentWatcher.Stop();
                    _currentWatcher = null;
                }
                _currentWatcher = CreateWatcher();
                // �{�^����\��
                m_TopMenuButton.gameObject.SetActive(true);
                Debug.Log("�A���J�[��{����...");
                break;
        }
    }

    /// <summary>
    /// �Z�b�V�����̍쐬�E�J�n
    /// </summary>
    private async Task StartSessionAsync()
    {
        // �Z�b�V�����̍쐬
        if (m_SpatialAnchorManager.Session == null)
        {
            Debug.Log("�Z�b�V�������쐬���܂��B");
            await m_SpatialAnchorManager.CreateSessionAsync();
        }
        // �Z�b�V�����̊J�n
        if (!m_SpatialAnchorManager.IsSessionStarted)
        {
            Debug.Log("�Z�b�V�������J�n���܂��B");
            await m_SpatialAnchorManager.StartSessionAsync();
        }
    }

    /// <summary>
    /// �Z�b�V�������~�A���[�J���A���J�[��j���A�E�H�b�`���[���~
    /// </summary>
    private async Task ResetSessionAsync()
    {
        // �{�^�����\��
        m_CreateAnchorButton.gameObject.SetActive(false);
        m_DeleteAnchorButton.gameObject.SetActive(false);
        m_TopMenuButton.gameObject.SetActive(false);

        // �z�u�������[�J���A���J�[���폜
        DeleteLocalAnchor();

        // �Z�b�V���������Z�b�g
        Debug.Log("�Z�b�V���������Z�b�g���܂��B");
        await m_SpatialAnchorManager.ResetSessionAsync();

        // �E�H�b�`���[���~
        if (_currentWatcher != null)
        {
            Debug.Log("�E�H�b�`���[���~���܂��B");
            _currentWatcher.Stop();
            _currentWatcher = null;
        }

        // �{�^����\��
        m_TopMenu.SetActive(true);
        if (FileUtility.ReadFile() == null)
        {
            m_ReproduceAnchorModeButton.gameObject.SetActive(false);
        }
        else
        {
            m_ReproduceAnchorModeButton.gameObject.SetActive(true);
        }
            
    }

    /// <summary>
    /// ���C�A�E�g�p�̃A���J�[�S�[�X�g�𐶐�
    /// </summary>
    private void CreateLayoutAnchorGhost()
    {
        Debug.Log("���C�A�E�g�p�̃A���J�[�S�[�X�g�𐶐����܂��B");
        if(_layoutAnchorGhost == null) _layoutAnchorGhost = Instantiate(m_AnchorPrefab.gameObject, Camera.main.transform.position + Camera.main.transform.forward, m_AnchorPrefab.transform.rotation);
    }

    /// <summary>
    /// �A���J�[��z�u���A�A���J�[���N���E�h�ɕۑ��A�A���J�[Indentifier�����[�J���ɕۑ�
    /// </summary>
    /// <returns></returns>
    private async Task CreateAnchorAsync()
    {
        // �A���J�[�𐶐�
        Debug.Log("�A���J�[�𐶐����܂��B");
        var anchorObject = CreateAnchorObject(_layoutAnchorGhost.transform.position, _layoutAnchorGhost.transform.rotation);
        Destroy(_layoutAnchorGhost);
        // �A���J�[����ۑ�
        Debug.Log("�A���J�[����ۑ����܂��B");
        await SaveAnchorAsync(anchorObject);
        // ���C�A�E�g�p�̃A���J�[�S�[�X�g�𐶐�
        CreateLayoutAnchorGhost();
    }

    /// <summary>
    /// �A���J�[�I�u�W�F�N�g�𐶐�����
    /// </summary>
    private GameObject CreateAnchorObject(Vector3 worldPos, Quaternion worldRot)
    {
        // �A���J�[�p��GameObject�𐶐�
        GameObject anchorObject = Instantiate(m_AnchorPrefab.gameObject, worldPos, worldRot);

        // CloudNativeAnchor�R���|�[�l���g���A�^�b�`
        anchorObject.AddComponent<CloudNativeAnchor>();

        // �F��ݒ�
        anchorObject.GetComponent<MeshRenderer>().material.color = Color.yellow;

        // �R���C�_�[���A�N�e�B�u��
        anchorObject.GetComponent<BoxCollider>().enabled = false;

        return anchorObject;
    }

    /// <summary>
    /// �A���J�[���N���E�h�ɕۑ��E�A���J�[Identifier�����[�J���ɕۑ�
    /// </summary>
    private async Task SaveAnchorAsync(GameObject anchorObject)
    {
        // CloudNativeAnchor�R���|�[�l���g���擾
        CloudNativeAnchor cna = anchorObject.GetComponent<CloudNativeAnchor>();

        // CloudAnchor��Cloud Position���������̏ꍇ�A��������
        if (cna.CloudAnchor == null) await cna.NativeToCloud();
        CloudSpatialAnchor cloudAnchor = cna.CloudAnchor;

        // �A���J�[�̗L��������ݒ�
        cloudAnchor.Expiration = DateTimeOffset.Now.AddDays(7);

        // ������Ԃ̓����_�̎��W���\���ł��邩�̔���
        while (!m_SpatialAnchorManager.IsReadyForCreate)
        {
            await Task.Delay(330);
            float createProgress = m_SpatialAnchorManager.SessionStatus.RecommendedForCreateProgress;
            Debug.Log($"Move your device to capture more environment data: {createProgress:0%}");
        }

        Debug.Log("Saving...");

        try
        {
            // �N���E�h�ɃA���J�[��ۑ�
            await m_SpatialAnchorManager.CreateAnchorAsync(cloudAnchor);

            // ���[�J���ɃA���J�[Identifier��ۑ�
            FileUtility.SaveFile(cloudAnchor.Identifier);

            // �A���J�[Identifier��GameObject���L���b�V������
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
    /// �E�H�b�`���[�𐶐�����
    /// </summary>
    private CloudSpatialAnchorWatcher CreateWatcher()
    {
        Debug.Log("�E�H�b�`���[�𐶐����܂��B");
        if ((m_SpatialAnchorManager != null) && (m_SpatialAnchorManager.Session != null))
        {
            var anchorLocateCriteria = ConfigureAnchorLocateCriteria();
            return m_SpatialAnchorManager.Session.CreateWatcher(anchorLocateCriteria);
        }
        else
        {
            Debug.Log("�E�H�b�`���[�������ł��܂���B");
            return null;
        }
    }

    /// <summary>
    /// �A���J�[����������ݒ�
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
            Debug.Log("�ݒ�\�ȃA���J�[Identifier������܂���B");
        }
        anchorLocateCriteria.Identifiers = anchorsToFind.ToArray();

        return anchorLocateCriteria;
    }

    /// <summary>
    /// �N���E�h�̃A���J�[��S�폜
    /// </summary>
    private async Task DeleteAnchorAsync()
    {
        if (_existingCloudAnchors.Count == 0) return;
        // �N���E�h�A���J�[��S�폜
        Debug.Log("�N���E�h�A���J�[��S�폜���܂��B");
        foreach(var cloudAnchor in _existingCloudAnchors) await m_SpatialAnchorManager.DeleteAnchorAsync(cloudAnchor);
        _existingCloudAnchors.Clear();
        // ���[�J���A���J�[��S�폜
        DeleteLocalAnchor();
        // ���[�J���̃A���J�[Identifier��S�폜
        FileUtility.ResetFile();
        // �{�^�����\��
        m_DeleteAnchorButton.gameObject.SetActive(false);
    }

    /// <summary>
    /// ���[�J���̃A���J�[��S�폜
    /// </summary>
    private void DeleteLocalAnchor()
    {
        Debug.Log("���[�J���A���J�[��S�폜���܂��B");
        if (_layoutAnchorGhost != null) Destroy(_layoutAnchorGhost);
        foreach (var anchorObject in _createdAnchorObjects.Values) Destroy(anchorObject);
        _createdAnchorObjects.Clear();
    }

    /// <summary>
    /// �A���J�[�����m���ꂽ�Ƃ��ɌĂяo�����
    /// </summary>
    private void SpatialAnchorManager_AnchorLocated(object sender, AnchorLocatedEventArgs args)
    {
        Debug.LogFormat("Anchor recognized as a possible anchor {0} {1}", args.Identifier, args.Status);
        if (args.Status == LocateAnchorStatus.Located)
        {
            // ��������CloudSpatialAnchor���擾
            var cloudAnchor = args.Anchor;

            UnityDispatcher.InvokeOnAppThread(() =>
            {
                Pose anchorPose = Pose.identity;
                // �A���J�[�̈ʒu���擾
                anchorPose = cloudAnchor.GetPose();
                // �A���J�[�𐶐�
                var anchorObject = CreateAnchorObject(anchorPose.position, anchorPose.rotation);
                // �A���J�[�����L���b�V��
                _createdAnchorObjects.Add(cloudAnchor.Identifier, anchorObject);
                _existingCloudAnchors.Add(cloudAnchor);

                Debug.Log($"Reproduce anchor. Idendifier is {cloudAnchor.Identifier}");
            });
        }
    }

    /// <summary>
    /// Watcher�̂��ׂẴA���J�[�ɑ΂��錟�����삪�����������Ƃ�ʒm���܂��B
    /// (�T�m���ꂽ���ǂ����͖���܂���)
    /// </summary>
    private void SpatialAnchorManager_LocateAnchorsCompleted(object sender, LocateAnchorsCompletedEventArgs args)
    {
        Debug.Log($"�A���J�[�̌������������A{_existingCloudAnchors.Count}�̃A���J�[��������܂����B");
        UnityDispatcher.InvokeOnAppThread(() =>
        {
            m_DeleteAnchorButton.gameObject.SetActive(true);
        });
    }
}