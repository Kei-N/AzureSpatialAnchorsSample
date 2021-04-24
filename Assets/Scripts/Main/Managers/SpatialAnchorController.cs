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
    /// UI�̕\���E��\����Ԃ̕ύX
    /// </summary>
    private readonly Subject<(bool, AppProcess)> onChangedUI = new Subject<(bool, AppProcess)>();
    public IObservable<(bool, AppProcess)> OnChangedUI => onChangedUI;

    /// <summary>
    /// ����������
    /// </summary>
    public void Initialize()
    {
        m_SpatialAnchorManager.AnchorLocated += SpatialAnchorManager_AnchorLocated;
        m_SpatialAnchorManager.LocateAnchorsCompleted += SpatialAnchorManager_LocateAnchorsCompleted;
    }

    /// <summary>
    /// �A�v���P�[�V�����̏��������s����
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
    /// �V�K�A���J�[�z�u���[�h���J�n
    /// </summary>
    private async Task StartCreateAnchorModeAsync()
    {
        // �{�^�����\��
        onChangedUI.OnNext((false, AppProcess.CreateAnchorMode));
        onChangedUI.OnNext((false, AppProcess.ReproduceAnchorMode));
        // �Z�b�V�������J�n
        await StartSessionAsync();
        // ���C�A�E�g�p�̃A���J�[�S�[�X�g�𐶐�
        CreateLayoutAnchorGhost();
        // �{�^����\��
        onChangedUI.OnNext((true, AppProcess.CreateAnchor));
        onChangedUI.OnNext((true, AppProcess.TopMenu));
    }

    /// <summary>
    /// �����A���J�[�Č����[�h���J�n
    /// </summary>
    private async Task StartReproduceAnchorModeAsync()
    {
        // �{�^�����\��
        onChangedUI.OnNext((false, AppProcess.CreateAnchorMode));
        onChangedUI.OnNext((false, AppProcess.ReproduceAnchorMode));
        // �Z�b�V�������J�n
        await StartSessionAsync();
        // �E�H�b�`���[�𐶐�
        if (_currentWatcher != null)
        {
            _currentWatcher.Stop();
            _currentWatcher = null;
        }
        _currentWatcher = CreateWatcher();
        // �{�^����\��
        onChangedUI.OnNext((true, AppProcess.TopMenu));
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
    /// ���C�A�E�g�p�̃A���J�[�S�[�X�g�𐶐�
    /// </summary>
    private void CreateLayoutAnchorGhost()
    {
        Debug.Log("���C�A�E�g�p�̃A���J�[�S�[�X�g�𐶐����܂��B");
        if (_layoutAnchorGhost == null) _layoutAnchorGhost = Instantiate(m_AnchorPrefab.gameObject, Camera.main.transform.position + Camera.main.transform.forward, m_AnchorPrefab.transform.rotation);
    }

    /// <summary>
    /// �A���J�[��z�u���A�A���J�[���N���E�h�ɕۑ��A�A���J�[Indentifier�����[�J���ɕۑ�
    /// </summary>
    /// <returns></returns>
    private async Task CreateAnchorAsync()
    {
        // �{�^�����\��
        onChangedUI.OnNext((false, AppProcess.CreateAnchor));
        onChangedUI.OnNext((false, AppProcess.TopMenu));
        // �A���J�[�𐶐�
        Debug.Log("�A���J�[�𐶐����܂��B");
        var anchorObject = CreateAnchorObject(_layoutAnchorGhost.transform.position, _layoutAnchorGhost.transform.rotation);
        Destroy(_layoutAnchorGhost);
        // �A���J�[����ۑ�
        Debug.Log("�A���J�[����ۑ����܂��B");
        await SaveAnchorAsync(anchorObject);
        // ���C�A�E�g�p�̃A���J�[�S�[�X�g�𐶐�
        CreateLayoutAnchorGhost();
        // �{�^����\��
        onChangedUI.OnNext((true, AppProcess.CreateAnchor));
        onChangedUI.OnNext((true, AppProcess.TopMenu));
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
            Debug.Log("�A���J�[��{����...");
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
        // �{�^�����\��
        onChangedUI.OnNext((false, AppProcess.DeleteAnchor));
        onChangedUI.OnNext((false, AppProcess.TopMenu));
        // �N���E�h�A���J�[��S�폜
        Debug.Log("�N���E�h�A���J�[��S�폜���܂��B");
        foreach (var cloudAnchor in _existingCloudAnchors) await m_SpatialAnchorManager.DeleteAnchorAsync(cloudAnchor);
        _existingCloudAnchors.Clear();
        // ���[�J���A���J�[��S�폜
        DeleteLocalAnchor();
        // ���[�J���̃A���J�[Identifier��S�폜
        FileUtility.ResetFile();
        // �{�^����\��
        onChangedUI.OnNext((true, AppProcess.TopMenu));
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
    /// �Z�b�V�������~�A���[�J���A���J�[��j���A�E�H�b�`���[���~
    /// </summary>
    private async Task ResetSessionAsync()
    {
        // �{�^�����\��
        onChangedUI.OnNext((false, AppProcess.CreateAnchor));
        onChangedUI.OnNext((false, AppProcess.DeleteAnchor));
        onChangedUI.OnNext((false, AppProcess.TopMenu));

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
            onChangedUI.OnNext((true, AppProcess.DeleteAnchor));
        });
    }
}
