using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.SceneManagement;

public class CanvasManager : MonoBehaviour
{
    public static CanvasManager Instance;

    private Dictionary<string, GameObject> canvasDictionary = new Dictionary<string, GameObject>();
    private GameObject currentActiveCanvas = null;
    public GameObject CurrentActiveCanvas => currentActiveCanvas;

    [Header("Scene Settings")]
    [Tooltip("Bu sahnedeyken oyuncu kontrolleri aranmaz ve menü mantığı çalışır.")]
    public string mainMenuSceneName = "MainMenu";

    [Header("Canvas Exceptions")]
    [Tooltip("Envanter gibi menüler açıldığında kapanmamasını istediğiniz canvas isimleri (Örn: DiaryCanvas, HUD)")]
    public List<string> persistentCanvases = new List<string>();
    
    [Tooltip("Sürekli çalışması gereken (Update'i durmamalı) ama görünmez olabilen Canvas'lar (Örn: WeaponWheel). Obje kapanmaz, CanvasGroup sıfırlanır.")]
    public List<string> canvasGroupOnlyCanvases = new List<string>();

    [Header("Events")]
    [Tooltip("UI açıldığında/kapandığında tetiklenir. True = Oyuncu hareket edebilir, False = UI açık, oyuncu durmalı.")]
    public UnityEvent<bool> onPlayerControlToggled;

    [Header("HDRP Blur Kontrolü")]
    private Volume globalVolume;
    private DepthOfField depthOfField;
    private bool isBlurActive = false;

    #region Singleton ve Sahne Yönetimi

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void Start()
    {
        InitializeSceneReferences();
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name == mainMenuSceneName)
        {
            SetPlayerState(false);
            InitializeSceneReferences();
            return;
        }
        InitializeSceneReferences();
    }

    private void InitializeSceneReferences()
    {
        canvasDictionary.Clear();
        currentActiveCanvas = null;

        globalVolume = FindObjectOfType<Volume>();
        depthOfField = null;

        if (globalVolume != null && globalVolume.profile.TryGet<DepthOfField>(out DepthOfField dof))
        {
            depthOfField = dof;
            depthOfField.active = false;
            isBlurActive = false;
        }

        if (SceneManager.GetActiveScene().name != mainMenuSceneName)
        {
            SetPlayerState(true);
        }
    }

    #endregion

    #region Canvas Kontrol Metotları

    public void RegisterCanvas(string canvasName, GameObject canvasObject)
    {
        if (canvasObject == null) return;

        if (!canvasDictionary.ContainsKey(canvasName))
        {
            canvasDictionary.Add(canvasName, canvasObject);
            SetCanvasVisibility(canvasObject, false);
        }
    }

    public void OpenCanvas(string canvasName)
    {
        // Zaman durmuşsa (Pause menüsü vb.) işlemi iptal et
        if (Time.timeScale == 0f) return;

        // İstisna listesinde DEĞİLSE mevcut canvas'ı kapat
        if (currentActiveCanvas != null && currentActiveCanvas.activeInHierarchy && !persistentCanvases.Contains(currentActiveCanvas.name))
        {
            CloseCanvas(currentActiveCanvas.name);
        }

        SetPlayerState(false);
        SetScreenBlur(true);

        if (canvasDictionary.TryGetValue(canvasName, out GameObject canvasToOpen))
        {
            if (canvasToOpen == null)
            {
                canvasDictionary.Remove(canvasName);
                SetPlayerState(true);
                SetScreenBlur(false);
                return;
            }

            SetCanvasVisibility(canvasToOpen, true);
            currentActiveCanvas = canvasToOpen;
            Canvas.ForceUpdateCanvases();
        }
        else
        {
            Debug.LogWarning($"CanvasManager: '{canvasName}' bulunamadı!");
            SetPlayerState(true);
            SetScreenBlur(false);
        }
    }

    public bool IsAnyCanvasOpen()
    {
        if (currentActiveCanvas != null)
        {
            if (canvasGroupOnlyCanvases.Contains(currentActiveCanvas.name))
            {
                CanvasGroup group = currentActiveCanvas.GetComponent<CanvasGroup>();
                return group != null && group.alpha > 0;
            }
            return currentActiveCanvas.activeInHierarchy;
        }
        return false;
    }

    public void CloseAllCanvases()
    {
        List<GameObject> canvasesToClose = new List<GameObject>();
        foreach (var canvas in canvasDictionary.Values)
        {
            if (canvas == null) continue;

            if (canvas.activeInHierarchy && !persistentCanvases.Contains(canvas.name))
            {
                canvasesToClose.Add(canvas);
            }
        }

        foreach (var canvas in canvasesToClose)
        {
            SetCanvasVisibility(canvas, false);
        }

        currentActiveCanvas = null;
        SetScreenBlur(false);
        SetPlayerState(true);
    }

    public void CloseCanvas(string canvasName)
    {
        if (canvasDictionary.TryGetValue(canvasName, out GameObject canvas))
        {
            if (canvas == null)
            {
                canvasDictionary.Remove(canvasName);
                return;
            }

            SetCanvasVisibility(canvas, false);

            if (currentActiveCanvas == canvas)
            {
                currentActiveCanvas = null;
                SetScreenBlur(false);
                
                if (Time.timeScale > 0f) // Oyun duraklatılmamışsa
                {
                    SetPlayerState(true);
                }
            }
        }
    }

    private void SetCanvasVisibility(GameObject canvasObj, bool isVisible)
    {
        bool useCanvasGroup = canvasGroupOnlyCanvases.Contains(canvasObj.name);

        if (useCanvasGroup)
        {
            if (!canvasObj.activeSelf) canvasObj.SetActive(true);

            CanvasGroup group = canvasObj.GetComponent<CanvasGroup>();
            if (group == null) group = canvasObj.AddComponent<CanvasGroup>();

            group.alpha = isVisible ? 1f : 0f;
            group.interactable = isVisible;
            group.blocksRaycasts = isVisible;
        }
        else
        {
            canvasObj.SetActive(isVisible);
        }
    }

    #endregion

    #region Yardımcı Metotlar

    public void SetPlayerState(bool isActive)
    {
        // Fareyi kilitle / serbest bırak
        Cursor.lockState = isActive ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !isActive;

        // Diğer scriptlere (Hareket, Kamera vb.) haber ver
        onPlayerControlToggled?.Invoke(isActive);
    }

    private void SetScreenBlur(bool shouldBeActive)
    {
        if (isBlurActive == shouldBeActive) return;
        if (depthOfField == null) return;

        depthOfField.active = shouldBeActive;
        isBlurActive = shouldBeActive;
    }

    #endregion
}
