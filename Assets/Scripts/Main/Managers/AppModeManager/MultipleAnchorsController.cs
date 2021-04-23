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
    [SerializeField] private Interactable m_TopMenuButton = null;

    private AppProcess _currentProcess = AppProcess.CreateAnchor;
    private GameObject _layoutAnchorGhost = null;
    private Dictionary<string, GameObject> _createdAnchorObjects = new Dictionary<string, GameObject>();
    private CloudSpatialAnchorWatcher _currentWatcher;

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
        m_CreateAnchorModeButton.OnClick.AddListener(async () => await SelectProcess(AppProcess.CreateAnchor));
        m_ReproduceAnchorModeButton.OnClick.AddListener(async () => await SelectProcess(AppProcess.ReproduceAnchor));
        m_CreateAnchorButton.OnClick.AddListener(async () => await CreateAnchor());
        m_TopMenuButton.OnClick.AddListener(() => StopSession());

        m_SpatialAnchorManager.AnchorLocated += SpatialAnchorManager_AnchorLocated;
        m_SpatialAnchorManager.LocateAnchorsCompleted += SpatialAnchorManager_LocateAnchorsCompleted;
    }

    private void OnDestroy()
    {
        StopSession();
    }

    /// <summary>
    /// �A�v���P�[�V�����̏�����I������
    /// </summary>
    /// <param name="appProcess"></param>
    private async Task SelectProcess(AppProcess appProcess)
    {
        _currentProcess = appProcess;
        m_TopMenu.SetActive(false);
        await StartSession();
        switch (_currentProcess)
        {
            case AppProcess.CreateAnchor:
                // �{�^����\��
                m_CreateAnchorButton.gameObject.SetActive(true);
                m_TopMenuButton.gameObject.SetActive(true);
                // �A���J�[�z�u�p�̃A���J�[�S�[�X�g�𐶐�
                Debug.Log("���C�A�E�g�p�̃A���J�[�S�[�X�g�𐶐����܂��B");
                _layoutAnchorGhost = Instantiate(m_AnchorPrefab, Camera.main.transform.position + Vector3.forward, m_AnchorPrefab.transform.rotation);
                break;
            case AppProcess.ReproduceAnchor:
                // �{�^����\��
                m_TopMenuButton.gameObject.SetActive(true);
                // �A���J�[��T�����߂ɃE�H�b�`���[�𐶐�����
                if (_currentWatcher != null)
                {
                    _currentWatcher.Stop();
                    _currentWatcher = null;
                }
                _currentWatcher = CreateWatcher();
                Debug.Log("�A���J�[��{����...");
                break;
        }
    }

    /// <summary>
    /// �Z�b�V�����̍쐬�E�J�n
    /// </summary>
    private async Task StartSession()
    {
        // �Z�b�V�����̍쐬
        if (m_SpatialAnchorManager.Session == null)
        {
            Debug.Log("�Z�b�V�������쐬���܂��B");
            await m_SpatialAnchorManager.CreateSessionAsync();
        }
        // �Z�b�V�����̊J�n
        Debug.Log("�Z�b�V�������J�n���܂��B");
        await m_SpatialAnchorManager.StartSessionAsync();
    }

    /// <summary>
    /// �Z�b�V�������~�A���[�J���A���J�[��j���A�E�H�b�`���[���~
    /// </summary>
    private void StopSession()
    {
        // �{�^�����\��
        m_CreateAnchorButton.gameObject.SetActive(false);
        m_TopMenuButton.gameObject.SetActive(false);

        // �z�u�������[�J���A���J�[���폜
        if (_layoutAnchorGhost != null) Destroy(_layoutAnchorGhost);
        foreach (var anchorObject in _createdAnchorObjects.Values) Destroy(anchorObject);
        _createdAnchorObjects.Clear();

        // �Z�b�V�������~
        Debug.Log("�Z�b�V�������~���܂��B");
        m_SpatialAnchorManager.StopSession();

        // �E�H�b�`���[���~
        if (_currentWatcher != null)
        {
            Debug.Log("�E�H�b�`���[���~���܂��B");
            _currentWatcher.Stop();
            _currentWatcher = null;
        }

        // �{�^����\��
        m_TopMenu.SetActive(true);
    }

    /// <summary>
    /// �A���J�[��z�u���A�A���J�[���N���E�h�ɕۑ��A�A���J�[Indentifier�����[�J���ɕۑ�
    /// </summary>
    /// <returns></returns>
    private async Task CreateAnchor()
    {
        if (_layoutAnchorGhost == null)
        {
            Debug.Log("���C�A�E�g�p�̃A���J�[�S�[�X�g������܂���B");
            return;
        }
        else
        {
            // ���C�A�E�g�p�̃A���J�[�S�[�X�g��j��
            Destroy(_layoutAnchorGhost);
        }
        // �A���J�[�𐶐�
        Debug.Log("�A���J�[�𐶐����܂��B");
        var anchorObject = CreateAnchorObject(_layoutAnchorGhost.transform.position, _layoutAnchorGhost.transform.rotation);
        // �A���J�[����ۑ�
        Debug.Log("�A���J�[����ۑ����܂��B");
        await SaveAnchorAsync(anchorObject);
        // ���C�A�E�g�p�̃A���J�[�S�[�X�g�𐶐�
        Debug.Log("���C�A�E�g�p�̃A���J�[�S�[�X�g�𐶐����܂��B");
        _layoutAnchorGhost = Instantiate(m_AnchorPrefab, Camera.main.transform.position + Vector3.forward, m_AnchorPrefab.transform.rotation);
    }

    /// <summary>
    /// �A���J�[�I�u�W�F�N�g�𐶐�����
    /// </summary>
    private GameObject CreateAnchorObject(Vector3 worldPos, Quaternion worldRot)
    {
        // �A���J�[�p��GameObject�𐶐�
        GameObject anchorObject = Instantiate(m_AnchorPrefab, worldPos, worldRot);

        // CloudNativeAnchor�R���|�[�l���g���A�^�b�`
        anchorObject.AddComponent<CloudNativeAnchor>();

        // �F��ݒ�
        anchorObject.GetComponent<MeshRenderer>().material.color = Color.yellow;

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
        Debug.Log("�A���J�[�̌������������܂����B");
    }
}