using Microsoft.MixedReality.Toolkit.UI;
using System;
using UniRx;
using UnityEngine;
using static Common;

public class UIManager : MonoBehaviour
{
    [SerializeField] private Interactable m_CreateAnchorModeButton = null;
    [SerializeField] private Interactable m_ReproduceAnchorModeButton = null;
    [SerializeField] private Interactable m_CreateAnchorButton = null;
    [SerializeField] private Interactable m_DeleteAnchorButton = null;
    [SerializeField] private Interactable m_TopMenuButton = null;

    /// <summary>
    /// �{�^���̃N���b�N�C�x���g
    /// </summary>
    private readonly Subject<AppProcess> onClickedButton = new Subject<AppProcess>();
    public IObservable<AppProcess> OnClickedButton => onClickedButton;

    /// <summary>
    /// ����������
    /// </summary>
    public void InitializeManager()
    {
        if (FileUtility.ReadFile() == null) m_ReproduceAnchorModeButton.gameObject.SetActive(false);
        m_CreateAnchorModeButton.OnClick.AddListener(() => onClickedButton.OnNext(AppProcess.CreateAnchorMode));
        m_ReproduceAnchorModeButton.OnClick.AddListener(() => onClickedButton.OnNext(AppProcess.ReproduceAnchorMode));
        m_CreateAnchorButton.OnClick.AddListener(() => onClickedButton.OnNext(AppProcess.CreateAnchor));
        m_DeleteAnchorButton.OnClick.AddListener(() => onClickedButton.OnNext(AppProcess.DeleteAnchor));
        m_TopMenuButton.OnClick.AddListener(() => onClickedButton.OnNext(AppProcess.TopMenu));
    }

    /// <summary>
    /// UI�̕\����Ԃ�ύX
    /// </summary>
    public void ChangeUI(bool isActive, AppProcess appProcess)
    {
        switch (appProcess)
        {
            case AppProcess.CreateAnchorMode:
                m_CreateAnchorModeButton.gameObject.SetActive(isActive);
                break;
            case AppProcess.ReproduceAnchorMode:
                m_ReproduceAnchorModeButton.gameObject.SetActive(isActive);
                break;
            case AppProcess.CreateAnchor:
                m_CreateAnchorButton.gameObject.SetActive(isActive);
                break;
            case AppProcess.DeleteAnchor:
                m_DeleteAnchorButton.gameObject.SetActive(isActive);
                break;
            case AppProcess.TopMenu:
                m_TopMenuButton.gameObject.SetActive(isActive);
                break;
        }
    }
}
