using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections;

public class HousePanelController : MonoBehaviour
{
    [Header("Buttons")]
    public Button buyButton;
    public Button declineButton;

    private int _index = 0;
    private bool _active = false;
    private bool _buyVisible = true;
    private bool _blockInputThisFrame = false;

    void Start()
    {
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;
    }

    void OnEnable()
    {
        _active = false;
        StartCoroutine(InitAfterNarrator());
    }

    void OnDisable()
    {
        _active = false;
    }

    IEnumerator InitAfterNarrator()
    {
        yield return null;

        if (buyButton == null || declineButton == null)
        {
            Debug.LogError("HousePanelController: buyButton ou declineButton não estão atribuídos no Inspector.");
            yield break;
        }

        yield return new WaitUntil(() => !SoundManager.Instance.narratorSource.isPlaying);

        _buyVisible = buyButton.gameObject.activeSelf;
        _index = 0;
        _blockInputThisFrame = true;
        FocusCurrent();
        _active = true;
    }

    void Update()
    {
        if (!_active) return;
        if (_blockInputThisFrame) { _blockInputThisFrame = false; return; }

        if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.RightArrow))
        {
            if (_buyVisible)
            {
                _index = _index == 0 ? 1 : 0;
                FocusCurrent();
            }
        }

        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.Space))
        {
            _blockInputThisFrame = true;
            _active = false;
            if (_index == 0 && _buyVisible)
                buyButton.onClick.Invoke();
            else
                declineButton.onClick.Invoke();
        }
    }

    void FocusCurrent()
    {
        Button target = (_index == 0 && _buyVisible) ? buyButton : declineButton;
        if (_index == 1 || !_buyVisible) _index = 1;

        if (EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(target.gameObject);
            target.Select();
        }

        if (_index == 0 && _buyVisible)
            SoundManager.Instance.PlayNarrator(SoundManager.Instance.narr_Yes);
        else
            SoundManager.Instance.PlayNarrator(SoundManager.Instance.narr_No);
    }
}