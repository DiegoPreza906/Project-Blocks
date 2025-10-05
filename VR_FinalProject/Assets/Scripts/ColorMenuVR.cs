using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

/// <summary>
/// Sistema de menú de colores para VR
/// Se activa al mantener presionado el grip izquierdo
/// Se hace click en botones con el trigger izquierdo
/// </summary>
public class ColorMenuVR : MonoBehaviour
{
    [Header("Configuración del Menú")]
    [SerializeField] private Canvas colorMenuCanvas;
    [SerializeField] private GameObject colorMenuPanel; // Panel que contiene los botones
    
    [Header("Botones de Colores (Asignar Manualmente)")]
    [SerializeField] private ColorButton[] colorButtons;
    
    
    private bool isMenuActive = false;
    private Color currentSelectedColor = Color.white;
    private PlaceBrickVR placeBrickSystem;
    
    // Eventos
    public System.Action<Color> OnColorSelected;
    
    private void Start()
    {
        // Buscar el sistema de colocación de bloques
        placeBrickSystem = FindFirstObjectByType<PlaceBrickVR>();
        
        // Configurar el Canvas para VR
        SetupVRCanvas();
        
        // Configurar los botones manuales
        SetupManualButtons();
        
        // Inicialmente el panel oculto (Canvas permanece activo)
        SetMenuActive(false);
        
        Debug.Log("🎨 ColorMenuVR inicializado correctamente");
    }
    
    private void Update()
    {
        HandleMenuActivation();
    }
    
    /// <summary>
    /// Maneja la activación del menú con el grip izquierdo
    /// </summary>
    private void HandleMenuActivation()
    {
        bool leftGripPressed = false;
        bool leftTriggerPressed = false;
        
        // Verificar controlador izquierdo
        List<UnityEngine.XR.InputDevice> leftHandDevices = new List<UnityEngine.XR.InputDevice>();
        UnityEngine.XR.InputDevices.GetDevicesAtXRNode(UnityEngine.XR.XRNode.LeftHand, leftHandDevices);
        
        if (leftHandDevices.Count > 0)
        {
            var leftDevice = leftHandDevices[0];
            
            // Grip izquierdo para activar/desactivar menú
            if (leftDevice.TryGetFeatureValue(UnityEngine.XR.CommonUsages.gripButton, out bool leftGrip))
            {
                leftGripPressed = leftGrip;
            }
            
            // Trigger izquierdo para hacer click en botones
            if (leftDevice.TryGetFeatureValue(UnityEngine.XR.CommonUsages.triggerButton, out bool leftTrigger))
            {
                leftTriggerPressed = leftTrigger;
            }
        }
        
        // Activar/desactivar menú según el estado del grip
        if (leftGripPressed && !isMenuActive)
        {
            ActivateMenu();
        }
        else if (!leftGripPressed && isMenuActive)
        {
            DeactivateMenu();
        }
        
        // Si el menú está activo, manejar clicks con trigger
        if (isMenuActive && leftTriggerPressed)
        {
            HandleButtonClick();
        }
    }
    
    /// <summary>
    /// Activa el menú de colores
    /// </summary>
    private void ActivateMenu()
    {
        isMenuActive = true;
        SetMenuActive(true);
        UpdateMenuPosition();
        
    }
    
    /// <summary>
    /// Desactiva el menú de colores
    /// </summary>
    private void DeactivateMenu()
    {
        isMenuActive = false;
        SetMenuActive(false);
        
    }
    
    /// <summary>
    /// Maneja el click en botones con el trigger izquierdo
    /// </summary>
    private void HandleButtonClick()
    {
        // Obtener la posición del controlador izquierdo para raycast
        List<UnityEngine.XR.InputDevice> leftHandDevices = new List<UnityEngine.XR.InputDevice>();
        UnityEngine.XR.InputDevices.GetDevicesAtXRNode(UnityEngine.XR.XRNode.LeftHand, leftHandDevices);
        
        if (leftHandDevices.Count > 0)
        {
            var leftDevice = leftHandDevices[0];
            if (leftDevice.TryGetFeatureValue(UnityEngine.XR.CommonUsages.devicePosition, out Vector3 position) &&
                leftDevice.TryGetFeatureValue(UnityEngine.XR.CommonUsages.deviceRotation, out Quaternion rotation))
            {
                // Crear un ray desde el controlador hacia adelante
                Vector3 forward = rotation * Vector3.forward;
                Ray ray = new Ray(position, forward);
                
                // Hacer raycast para detectar botones
                RaycastHit hit;
                if (Physics.Raycast(ray, out hit, 10f))
                {
                    // Verificar si el objeto golpeado es un botón de color
                    ColorButton colorButton = hit.collider.GetComponent<ColorButton>();
                    if (colorButton != null)
                    {
                        // Simular click en el botón
                        Button button = colorButton.GetComponent<Button>();
                        if (button != null && button.interactable)
                        {
                            button.onClick.Invoke();
                            Debug.Log($"🎨 Botón clickeado: {colorButton.name}");
                        }
                    }
                }
            }
        }
    }
    
    /// <summary>
    /// Actualiza la posición del menú para que siga el controlador izquierdo
    /// </summary>
    private void UpdateMenuPosition()
    {
        // El menú ya está posicionado manualmente, solo verificar que esté visible
        // Aquí puedes agregar lógica adicional si necesitas que el menú siga al controlador
    }
    
    /// <summary>
    /// Configura el Canvas para VR
    /// </summary>
    private void SetupVRCanvas()
    {
        if (colorMenuCanvas != null)
        {
            // Configurar para VR
            colorMenuCanvas.renderMode = RenderMode.WorldSpace;
            colorMenuCanvas.worldCamera = Camera.main;
            
            // Configurar el Canvas Scaler
            CanvasScaler scaler = colorMenuCanvas.GetComponent<CanvasScaler>();
            if (scaler != null)
            {
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
                scaler.scaleFactor = 0.01f; // Escala pequeña para VR
            }
            
            // Configurar el GraphicRaycaster para interacción
            GraphicRaycaster raycaster = colorMenuCanvas.GetComponent<GraphicRaycaster>();
            if (raycaster == null)
            {
                raycaster = colorMenuCanvas.gameObject.AddComponent<GraphicRaycaster>();
            }
        }
    }
    
    /// <summary>
    /// Configura los botones de colores asignados manualmente
    /// </summary>
    private void SetupManualButtons()
    {
        if (colorButtons == null || colorButtons.Length == 0)
        {
            Debug.LogWarning("⚠️ ColorMenuVR: No hay botones asignados manualmente");
            return;
        }
        
        // Configurar cada botón asignado
        for (int i = 0; i < colorButtons.Length; i++)
        {
            ColorButton colorButton = colorButtons[i];
            if (colorButton != null)
            {
                // Debug del color del botón
                Color buttonColor = colorButton.GetColor();
                Debug.Log($"🎨 SetupManualButtons: Botón {i} ({colorButton.name}) tiene color: {buttonColor}");
                Debug.Log($"🎨 SetupManualButtons: Color RGB: R={buttonColor.r:F3}, G={buttonColor.g:F3}, B={buttonColor.b:F3}");
                
                // Configurar el evento de click
                Button button = colorButton.GetComponent<Button>();
                if (button != null)
                {
                    // Limpiar listeners anteriores
                    button.onClick.RemoveAllListeners();
                    
                    // Agregar nuevo listener
                    button.onClick.AddListener(() => {
                        Debug.Log($"🎨 Botón {i} clickeado - Color: {colorButton.GetColor()}");
                        SelectColor(colorButton.GetColor());
                    });
                    
                    Debug.Log($"🎨 SetupManualButtons: Evento de click configurado para botón {i}");
                }
                else
                {
                    Debug.LogError($"❌ SetupManualButtons: No se encontró componente Button en {colorButton.name}");
                }
                
                Debug.Log($"🎨 Configurado botón {i}: {colorButton.name} con color {colorButton.GetColor()}");
            }
            else
            {
                Debug.LogError($"❌ SetupManualButtons: ColorButton {i} es null");
            }
        }
        
        Debug.Log($"🎨 Configurados {colorButtons.Length} botones de colores manuales");
    }
    
    
    /// <summary>
    /// Selecciona un color
    /// </summary>
    public void SelectColor(Color color)
    {
        currentSelectedColor = color;
        
        Debug.Log($"🎨 ColorMenuVR SelectColor: Color seleccionado {color}");
        Debug.Log($"🎨 ColorMenuVR SelectColor: PlaceBrickSystem existe: {placeBrickSystem != null}");
        Debug.Log($"🎨 ColorMenuVR SelectColor: Color RGB: R={color.r:F3}, G={color.g:F3}, B={color.b:F3}");
        
        // Notificar al sistema de colocación de bloques
        if (placeBrickSystem != null)
        {
            Debug.Log($"🎨 ColorMenuVR SelectColor: Llamando SetBrickColor en PlaceBrickVR");
            placeBrickSystem.SetBrickColor(color);
            Debug.Log($"🎨 ColorMenuVR SelectColor: SetBrickColor llamado exitosamente");
        }
        else
        {
            Debug.LogError("❌ ColorMenuVR: PlaceBrickSystem no encontrado!");
        }
        
        // Disparar evento
        OnColorSelected?.Invoke(color);
        
        Debug.Log($"🎨 Color seleccionado: {color}");
    }
    
    /// <summary>
    /// Activa o desactiva la visibilidad del panel del menú
    /// </summary>
    private void SetMenuActive(bool active)
    {
        if (colorMenuPanel != null)
        {
            colorMenuPanel.SetActive(active);
        }
        else
        {
            Debug.LogError("❌ ColorMenuPanel no asignado en ColorMenuVR!");
        }
    }
    
    /// <summary>
    /// Obtiene el color actualmente seleccionado
    /// </summary>
    public Color GetCurrentColor()
    {
        return currentSelectedColor;
    }
    
    /// <summary>
    /// Verifica si el menú está activo
    /// </summary>
    public bool IsMenuActive()
    {
        return isMenuActive;
    }
    
}
