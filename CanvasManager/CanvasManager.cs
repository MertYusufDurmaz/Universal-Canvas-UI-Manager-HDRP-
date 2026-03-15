using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.SceneManagement;

public class CanvasManager : MonoBehaviour
{
    public static CanvasManager Instance;

    private Dictionary<string, GameObject> canvasDictionary = new Dictionary<string, GameObject>();
    private GameObject currentActiveCanvas = null;

    [Header("Player Kontrol Referanslarý")]
    private MovementController playerMovement;
    private MouseLook mouseLook;
    private InspectionHandler inspectorHandler;
    private GameObject crosshairUI;

    [Header("HDRP Blur Kontrolü")]
    private Volume globalVolume;
    private DepthOfField depthOfField;
    private bool isBlurActive = false;

    public GameObject CurrentActiveCanvas => currentActiveCanvas;

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
        if (scene.name == "MainMenu")
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

        playerMovement = FindObjectOfType<MovementController>();
        mouseLook = FindObjectOfType<MouseLook>();
        inspectorHandler = FindObjectOfType<InspectionHandler>();

        Transform crosshairTransform = transform.Find("Crosshair");
        if (crosshairTransform == null) crosshairUI = GameObject.Find("Crosshair");
        else crosshairUI = crosshairTransform.gameObject;

        globalVolume = FindObjectOfType<Volume>();
        depthOfField = null;

        if (globalVolume != null)
        {
            if (globalVolume.profile.TryGet<DepthOfField>(out DepthOfField dof))
            {
                depthOfField = dof;
                depthOfField.active = false;
                isBlurActive = false;
            }
        }

        if (SceneManager.GetActiveScene().name != "MainMenu")
        {
            SetPlayerState(true);
        }
    }

    #endregion

    #region Canvas Kontrol Metotlarý

    public void RegisterCanvas(string canvasName, GameObject canvasObject)
    {
        if (canvasObject == null) return;

        if (!canvasDictionary.ContainsKey(canvasName))
        {
            canvasDictionary.Add(canvasName, canvasObject);

            // ÖNEMLÝ: Elle SetActive(false) yapma!
            // Yeni yazdýðýmýz akýllý fonksiyonu kullan.
            // O, WeaponWheel ise görünmez yapar, deðilse kapatýr.
            SetCanvasVisibility(canvasObject, false);
        }
    }

    public void OpenCanvas(string canvasName)
    {
        if (PauseManager.GameIsPaused) return;

        // WeaponWheel hariç diðerlerini kapat (Envanter açýlýrken diðerleri kapansýn istiyorsan)
        if (currentActiveCanvas != null && currentActiveCanvas.activeInHierarchy && currentActiveCanvas.name != "DiaryCanvas")
        {
            CloseCanvas(currentActiveCanvas.name);
        }

        SetPlayerState(false);

        if (inspectorHandler != null && inspectorHandler.IsInspecting)
        {
            inspectorHandler.ExitInspectionModeForced();
        }

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

            // Görünür yap
            SetCanvasVisibility(canvasToOpen, true);

            currentActiveCanvas = canvasToOpen;
            Canvas.ForceUpdateCanvases();
        }
        else
        {
            Debug.LogWarning($"CanvasManager: '{canvasName}' bulunamadý!");
            SetPlayerState(true);
            SetScreenBlur(false);
        }
    }

    public bool IsAnyCanvasOpen()
    {
        // WeaponWheel özel kontrolü: Obje aktif olabilir ama Canvas disabled ise "kapalý" sayýlýr.
        if (currentActiveCanvas != null)
        {
            if (currentActiveCanvas.name == "WeaponWheelCanvas")
            {
                Canvas c = currentActiveCanvas.GetComponent<Canvas>();
                return c != null && c.enabled;
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

            // WeaponWheelCanvas veya aktif olan herhangi bir canvas
            if (canvas.activeInHierarchy)
            {
                // DiaryCanvas istisnasý (eðer kalacaksa)
                if (canvas.name != "DiaryCanvas")
                {
                    canvasesToClose.Add(canvas);
                }
            }
        }

        foreach (var canvas in canvasesToClose)
        {
            // Özel kapatma fonksiyonumuzu kullan
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

            // Özel kapatma fonksiyonu
            SetCanvasVisibility(canvas, false);

            if (currentActiveCanvas == canvas)
            {
                currentActiveCanvas = null;
                SetScreenBlur(false);

                if (!PauseManager.GameIsPaused)
                {
                    SetPlayerState(true);
                }
            }
        }
    }

    // --- SÝHÝRLÝ FONKSÝYON ---
    // Bu fonksiyon, WeaponWheelCanvas ise Objesini kapatmaz, sadece çizimini (Canvas) kapatýr.
    // Böylece script çalýþmaya devam eder.
    // --- KESÝN ÇÖZÜM FONKSÝYONU ---
    private void SetCanvasVisibility(GameObject canvasObj, bool isVisible)
    {
        // Kontrol: Bu obje Weapon Wheel mi? (Ýsmi veya içindeki scriptten anlarýz)
        // Senin hiyerarþinde ismi "WeaponWheel" olduðu için bu kontrolü ekledim.
        bool isWeaponWheel = canvasObj.name == "WeaponWheelCanvas" || canvasObj.name == "WeaponWheel";

        if (isWeaponWheel)
        {
            // 1. Objeyi ASLA kapatma, hep açýk kalsýn ki script çalýþsýn
            if (!canvasObj.activeSelf) canvasObj.SetActive(true);

            // 2. Görünürlüðü CanvasGroup ile yönet
            CanvasGroup group = canvasObj.GetComponent<CanvasGroup>();

            // Eðer CanvasGroup eklemeyi unuttuysan kod otomatik eklesin:
            if (group == null) group = canvasObj.AddComponent<CanvasGroup>();

            if (isVisible)
            {
                group.alpha = 1f; // Tam görünür
                group.interactable = true; // Týklanabilir
                group.blocksRaycasts = true; // Iþýnlarý tutar
            }
            else
            {
                group.alpha = 0f; // Tamamen þeffaf (Görünmez)
                group.interactable = false; // Týklanamaz
                group.blocksRaycasts = false; // Arkasýndaki her þeye týklanabilir
            }
        }
        else
        {
            // Diðer tüm normal menüler (Pause, Notlar vb.) eskisi gibi açýlýp kapansýn
            canvasObj.SetActive(isVisible);
        }
    }

    #endregion

    #region Yardýmcý Metotlar

    public void SetPlayerState(bool isActive)
    {
        if (PauseManager.GameIsPaused && isActive) return;

        if (playerMovement != null)
            playerMovement.enabled = isActive;

        if (mouseLook != null)
            mouseLook.enabled = isActive;

        if (inspectorHandler != null)
            inspectorHandler.enabled = isActive;

        if (crosshairUI != null)
            crosshairUI.SetActive(isActive);

        Cursor.lockState = isActive ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !isActive;
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