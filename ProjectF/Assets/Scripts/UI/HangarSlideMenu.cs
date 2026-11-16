using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class HangarSlideMenu : MonoBehaviour
{
    [System.Serializable]
    public class MenuTab
    {
        public Button button;
        public GameObject cardsGroup; // Група карток для цієї вкладки (можна залишити пустим)

        [HideInInspector] public Image buttonBg;
        [HideInInspector] public Outline buttonOutline;
        [HideInInspector] public TMP_Text buttonText;
        [HideInInspector] public RectTransform buttonRect;
    }

    [Header("Вкладки меню (справа)")]
    public List<MenuTab> tabs = new List<MenuTab>();

    [Header("Кольори кіберпанк-інтерфейсу")]
    public Color bgColor = new Color(0.039f, 0.047f, 0.078f, 0.72f);       // #0A0C14 напівпрозорий
    public Color normalNeonColor = new Color(0f, 0.94f, 1f, 0.65f);        // #00F0FF спокійний ціан
    public Color activeNeonColor = new Color(1f, 0.165f, 0.52f, 1f);       // #FF2A85 активний неон
    public float activeButtonSlideLeft = 14f;                              // Зсув активної кнопки вліво (px)

    [Header("Анімація виїзду карток вліво")]
    public RectTransform cardsRoot;
    public CanvasGroup cardsCanvasGroup;
    public float closedOffsetX = -200f;
    public float openOffsetX = -290f;
    public float animSpeed = 12f;

    [Header("Опціональний зсув камери")]
    public Transform cameraTransform;
    public Vector3 cameraOpenLocalOffset = new Vector3(1.8f, 0f, -1.2f);

    private int _activeIndex = -1;
    private Vector3 _camBaseLocalPos;

    private void Start()
    {
        if (cameraTransform) _camBaseLocalPos = cameraTransform.localPosition;

        for (int i = 0; i < tabs.Count; i++)
        {
            MenuTab tab = tabs[i];
            if (!tab.button) continue;

            // Автоматично дістаємо компоненти кнопки
            tab.buttonRect = tab.button.GetComponent<RectTransform>();
            tab.buttonBg = tab.button.GetComponent<Image>();
            tab.buttonOutline = tab.button.GetComponent<Outline>();
            tab.buttonText = tab.button.GetComponentInChildren<TMP_Text>();

            if (tab.buttonBg) tab.buttonBg.color = bgColor;

            int index = i;
            tab.button.onClick.AddListener(() => ToggleTab(index));
        }

        RefreshVisualState(instant: true);
    }

    public void ToggleTab(int index)
    {
        _activeIndex = (_activeIndex == index) ? -1 : index;
        RefreshVisualState(instant: false);
    }

    private void RefreshVisualState(bool instant)
    {
        for (int i = 0; i < tabs.Count; i++)
        {
            MenuTab tab = tabs[i];
            bool isSelected = (i == _activeIndex);
            Color targetNeon = isSelected ? activeNeonColor : normalNeonColor;

            if (tab.buttonOutline) tab.buttonOutline.effectColor = targetNeon;
            if (tab.buttonText) tab.buttonText.color = targetNeon;
            if (tab.cardsGroup) tab.cardsGroup.SetActive(isSelected);
        }

        if (instant && cardsRoot && cardsCanvasGroup)
        {
            bool isOpen = _activeIndex >= 0;
            cardsRoot.anchoredPosition = new Vector2(isOpen ? openOffsetX : closedOffsetX, 0f);
            cardsCanvasGroup.alpha = isOpen ? 1f : 0f;
            cardsCanvasGroup.blocksRaycasts = isOpen;
        }
    }

    private void Update()
    {
        bool isOpen = _activeIndex >= 0;
        float dt = Time.deltaTime * animSpeed;

        // 1. Плавний виїзд карток справа наліво
        if (cardsRoot && cardsCanvasGroup)
        {
            float targetX = isOpen ? openOffsetX : closedOffsetX;
            Vector2 pos = cardsRoot.anchoredPosition;
            pos.x = Mathf.Lerp(pos.x, targetX, dt);
            cardsRoot.anchoredPosition = pos;

            cardsCanvasGroup.alpha = Mathf.Lerp(cardsCanvasGroup.alpha, isOpen ? 1f : 0f, dt);
            cardsCanvasGroup.blocksRaycasts = isOpen;
        }

        // 2. Плавний зсув активної кнопки вліво (працює навіть усередині Vertical Layout Group)
        for (int i = 0; i < tabs.Count; i++)
        {
            if (!tabs[i].buttonText) continue;

            // Зсуваємо текст усередині кнопки на 10 px вліво при виборі, щоб не конфліктувати з Layout Group
            RectTransform textRect = tabs[i].buttonText.rectTransform;
            float targetTextX = (i == _activeIndex) ? -activeButtonSlideLeft : 0f;
            Vector2 textPos = textRect.anchoredPosition;
            textPos.x = Mathf.Lerp(textPos.x, targetTextX, dt);
            textRect.anchoredPosition = textPos;
        }

        // 3. Плавний зсув камери (якщо призначена)
        if (cameraTransform)
        {
            Vector3 targetCamPos = isOpen ? _camBaseLocalPos + cameraOpenLocalOffset : _camBaseLocalPos;
            cameraTransform.localPosition = Vector3.Lerp(cameraTransform.localPosition, targetCamPos, dt * 0.6f);
        }
    }
}