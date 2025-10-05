using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Inputs;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

public class PlaceBrickVR : MonoBehaviour
{
    [Header("Configuración de Bloques")]
    [SerializeField] private GameObject[] brickPrefabs;
    [SerializeField] private Material[] brickMaterials;
    [SerializeField] private Material transparentMaterial;
    [SerializeField] private Color currentBrickColor = Color.white;
    
    [Header("Configuración VR")]
    [SerializeField] private UnityEngine.XR.Interaction.Toolkit.Interactors.XRRayInteractor rayInteractor; // Ray Interactor del XR Interaction Toolkit
    [SerializeField] private LayerMask legoLayerMask = -1;
    [SerializeField] private float previewSmoothing = 0.1f; // Suavizado del preview
    
    [Header("Sistema de Validación")]
    [SerializeField] private BrickValidationSystem validationSystem;
    
    // Removido RaycastSystem - usando solo XRRayInteractor
    
    [Header("Input Actions")]
    [SerializeField] private InputSystem_Actions inputActions;
    
    [Header("Estados")]
    [SerializeField] private int currentBrickIndex = 0;
    [SerializeField] private int currentMaterialIndex = 0;
    [SerializeField] private bool isPreviewMode = true; // true = preview, false = confirmar
    
    // Componentes
    private BrickVR currentBrick;
    private bool isPositionValid = false;
    private bool isInitialized = false;
    
    // Propiedades
    public bool IsBuilding { get; private set; } = true;
    public BrickVR CurrentBrick => currentBrick;
    public bool IsPositionValid => isPositionValid;
    
    private void Awake()
    {
        // Buscar Input Action Manager en el XR Origin
        InputActionManager inputActionManager = GetComponentInParent<InputActionManager>();
        if (inputActionManager != null)
        {
            Debug.Log("PlaceBrickVR: Input Action Manager encontrado en XR Origin");
        }
        
        // NO crear InputActions aquí - se hará en Start() después de configurar el manager
        Debug.Log("PlaceBrickVR: Awake completado - InputActions se crearán en Start()");
    }
    
    
    private void DisableNonVRDevices()
    {
        Debug.Log("🚫 Deshabilitando dispositivos no VR...");
        
        var devices = UnityEngine.InputSystem.InputSystem.devices;
        int disabledCount = 0;
        
        foreach (var device in devices)
        {
            // Deshabilitar TODO excepto controladores VR
            if (device.name.Contains("Keyboard") || 
                device.name.Contains("Mouse") || 
                device.name.Contains("Gamepad") ||
                device.name.Contains("Hand") ||
                (!device.name.Contains("XR") && !device.name.Contains("OpenXR") && !device.name.Contains("Controller")))
            {
                UnityEngine.InputSystem.InputSystem.DisableDevice(device);
                disabledCount++;
                Debug.Log($"🚫 Dispositivo deshabilitado: {device.name}");
            }
        }
        
        Debug.Log($"✅ {disabledCount} dispositivos no VR deshabilitados");
    }
    
    
    private void Start()
    {
        InitializeSystem();
        CreatePreviewBrick();
        SetupRaycastSystem();
        SetupValidationSystem();
        
        // Verificar dispositivos VR (sistema nativo)
        CheckVRDevices();
        
        Debug.Log("✅ PlaceBrickVR iniciado con sistema nativo de XR");
    }
    
    private void InitializeSystem()
    {
        if (brickPrefabs.Length == 0)
        {
            Debug.LogError("PlaceBrickVR: No hay prefabs de bloques configurados");
            return;
        }
        
        // Inicializar el sistema de cuadrícula con el primer prefab
        GridSystem.InitializeGrid(brickPrefabs[currentBrickIndex]);
        
        // Configurar layer mask si no está configurado
        if (legoLayerMask == -1)
        {
            legoLayerMask = GridSystem.LayerMaskLego;
        }
        
        isInitialized = true;
        Debug.Log("PlaceBrickVR: Sistema inicializado correctamente");
    }
    
    private void SetupValidationSystem()
    {
        // Buscar o crear el sistema de validación
        if (validationSystem == null)
        {
            validationSystem = FindFirstObjectByType<BrickValidationSystem>();
            
            if (validationSystem == null)
            {
                GameObject validationObj = new GameObject("BrickValidationSystem");
                validationSystem = validationObj.AddComponent<BrickValidationSystem>();
                Debug.Log("✅ BrickValidationSystem creado automáticamente");
            }
        }
        
        // Configurar materiales de validación
        if (validationSystem != null)
        {
            validationSystem.SetupMaterials(transparentMaterial, null, null);
            Debug.Log("✅ Sistema de validación configurado");
        }
    }
    
    private void SetupRaycastSystem()
    {
        // Buscar Ray Interactor en el controlador derecho
        if (rayInteractor == null)
        {
            Debug.Log("🔍 Buscando XRRayInteractor...");
            
            // Buscar todos los XRRayInteractor en la escena
            XRRayInteractor[] allRayInteractors = FindObjectsByType<XRRayInteractor>(FindObjectsSortMode.None);
            Debug.Log($"📡 Encontrados {allRayInteractors.Length} XRRayInteractor(s) en la escena");
            
            foreach (var interactor in allRayInteractors)
            {
                Debug.Log($"   - {interactor.name} (Padre: {interactor.transform.parent?.name ?? "NULL"})");
                
                // Buscar en controladores
                XRBaseController controller = interactor.GetComponentInParent<XRBaseController>();
                if (controller != null)
                {
                    Debug.Log($"     └─ Controlador: {controller.name}");
                    
                    // Verificar si es el controlador derecho
                    if (controller.name.ToLower().Contains("right") || 
                        controller.name.ToLower().Contains("derecho") ||
                        controller.name.ToLower().Contains("r_"))
                    {
                        rayInteractor = interactor;
                        Debug.Log($"✅ XRRayInteractor encontrado en controlador derecho: {interactor.name}");
                        break;
                    }
                }
            }
            
            // Si no se encuentra específicamente en el derecho, usar el primero disponible
            if (rayInteractor == null && allRayInteractors.Length > 0)
            {
                rayInteractor = allRayInteractors[0];
                Debug.Log($"⚠️ Usando primer XRRayInteractor disponible: {rayInteractor.name}");
            }
        }
        
        if (rayInteractor != null)
        {
            // Configurar el Ray Interactor para detectar la cuadrícula y bloques LEGO
            rayInteractor.maxRaycastDistance = 10f;
            rayInteractor.raycastMask = (1 << 7) | (1 << 8); // Layer 7 (Lego) + Layer 8 (Grid)
            rayInteractor.enabled = true;
            
            // Debug de configuración
            Debug.Log($"🎯 XRRayInteractor configurado:");
            Debug.Log($"   - Max Distance: {rayInteractor.maxRaycastDistance}m");
            Debug.Log($"   - Raycast Mask: Layer 7 (Lego) + Layer 8 (Grid) = {rayInteractor.raycastMask.value}");
            Debug.Log($"   - Enabled: {rayInteractor.enabled}");
            Debug.Log($"   - Ray Origin: {rayInteractor.rayOriginTransform?.name ?? "NULL"}");
            
            Debug.Log("✅ Ray Interactor configurado para detectar Layer 7 (Lego) + Layer 8 (Grid)");
        }
        else
        {
            Debug.LogError("❌ No se encontró Ray Interactor. Creando sistema de raycast manual...");
            
            // Crear un sistema de raycast manual como fallback
            CreateManualRaycastSystem();
        }
    }
    
    // Variables para sistema manual de raycast
    private Transform manualRayOrigin;
    private bool useManualRaycast = false;
    
    // Eventos de raycast (como en RaycastSystem)
    public System.Action<RaycastHit> OnRaycastHit;
    public System.Action OnRaycastMiss;
    
    private void CreateManualRaycastSystem()
    {
        Debug.Log("🔧 Creando sistema de raycast manual...");
        
        // Buscar el controlador derecho manualmente
        XRBaseController rightController = FindFirstObjectByType<XRBaseController>();
        if (rightController != null)
        {
            manualRayOrigin = rightController.transform;
            useManualRaycast = true;
            Debug.Log($"✅ Sistema manual creado usando controlador: {rightController.name}");
        }
        else
        {
            // Si no hay controlador, usar la cámara como fallback
            if (Camera.main != null)
            {
                manualRayOrigin = Camera.main.transform;
                useManualRaycast = true;
                Debug.Log("⚠️ Usando cámara principal como origen del raycast manual");
            }
            else
            {
                Debug.LogError("❌ No se encontró controlador ni cámara para raycast manual");
            }
        }
    }
    
    // Métodos simplificados - solo usando XRRayInteractor
    
    private void Update()
    {
        if (!isInitialized || !IsBuilding) return;
        
        // Actualizar preview del bloque usando XRRayInteractor
        UpdatePreviewWithXRRayInteractor();
        
        HandleBrickPlacement();
        HandleBrickRotation();
        HandleBrickDeletion();
        HandleBrickSelection();
    }
    
    private void UpdatePreviewWithXRRayInteractor()
    {
        if (currentBrick == null) return;
        
        // Activar el bloque preview
        currentBrick.gameObject.SetActive(true);
        
        bool hasHit = false;
        RaycastHit hit = new RaycastHit();
        
        // Usar XRRayInteractor si está disponible
        if (rayInteractor != null)
        {
            hasHit = rayInteractor.TryGetCurrent3DRaycastHit(out hit);
            if (hasHit)
            {
                // Debug log removido para evitar spam
            }
            else
            {
                // Debug cada 2 segundos para no spamear
                if (Time.time % 2f < 0.1f)
                {
                    // Debug log removido para evitar spam
                }
            }
        }
        // Usar sistema manual si no hay XRRayInteractor
        else if (useManualRaycast && manualRayOrigin != null)
        {
            Vector3 rayOrigin = manualRayOrigin.position;
            Vector3 rayDirection = manualRayOrigin.forward;
            Ray ray = new Ray(rayOrigin, rayDirection);
            
            // Debug visual del raycast manual
            Debug.DrawRay(rayOrigin, rayDirection * 10f, Color.blue, 0.1f);
            
            hasHit = Physics.Raycast(ray, out hit, 10f, (1 << 7) | (1 << 8)); // Layer 7 (Grid) + Layer 8 (Lego)
            if (hasHit)
            {
                Debug.Log($"🎯 Raycast Manual HIT: {hit.point}, Objeto: {hit.collider.name}, Layer: {hit.collider.gameObject.layer}");
            }
        }
        
        if (hasHit)
        {
            // Determinar el tipo de snap según el objeto hit
            Vector3 snappedPosition;
            
            // Verificar si es realmente un bloque LEGO (Layer 8 = Lego)
            bool isLegoBlock = hit.collider.gameObject.layer == 8 && 
                              (hit.collider.gameObject.name.Contains("Brick") || 
                               hit.collider.gameObject.name.Contains("Lego") ||
                               hit.collider.gameObject.name.Contains("PlacedBrick"));
            
            if (isLegoBlock)
            {
                // SNAP A BLOQUE: Colocar encima del collider del bloque existente
                snappedPosition = SnapToBlock(hit);
                // Debug log removido para evitar spam
        }
        else
        {
                // SNAP A GRID: Colocar en espacio de cuadrícula
                snappedPosition = SnapToGrid(hit.point);
                // Debug log removido para evitar spam
            }
            
            currentBrick.transform.position = Vector3.Lerp(currentBrick.transform.position, snappedPosition, previewSmoothing);
            isPositionValid = true;
            
            // Disparar evento de hit
            OnRaycastHit?.Invoke(hit);
        }
        else
        {
            // Si no hay hit, posicionar a distancia fija
            Vector3 rayOrigin, rayDirection;
            
            if (rayInteractor != null)
            {
                rayOrigin = rayInteractor.rayOriginTransform.position;
                rayDirection = rayInteractor.rayOriginTransform.forward;
            }
            else if (manualRayOrigin != null)
            {
                rayOrigin = manualRayOrigin.position;
                rayDirection = manualRayOrigin.forward;
            }
            else
            {
                return; // No hay origen de raycast disponible
            }
            
            Vector3 defaultPosition = rayOrigin + rayDirection * 2f;
            currentBrick.transform.position = Vector3.Lerp(currentBrick.transform.position, defaultPosition, previewSmoothing);
            isPositionValid = true;
            
            // Disparar evento de miss
            OnRaycastMiss?.Invoke();
        }
        
        // Aplicar transparencia verde
        if (transparentMaterial != null)
        {
            currentBrick.SetTransparency(true, transparentMaterial);
        }
    }
    
    private void UpdatePreviewSmoothing()
    {
        // El preview ahora se actualiza directamente en UpdatePreviewWithXRRayInteractor
        // No necesita suavizado adicional ya que el XRRayInteractor maneja esto internamente
    }
    
    private void HandleBrickPlacement()
    {
        // Usar sistema nativo de XR como RaycastSystem
        bool rightTriggerPressed = false;
        bool rightGripPressed = false;
        
        // Verificar controlador derecho
        List<UnityEngine.XR.InputDevice> rightHandDevices = new List<UnityEngine.XR.InputDevice>();
        UnityEngine.XR.InputDevices.GetDevicesAtXRNode(UnityEngine.XR.XRNode.RightHand, rightHandDevices);
        
        if (rightHandDevices.Count > 0)
        {
            var rightDevice = rightHandDevices[0];
            
            // Trigger derecho
            if (rightDevice.TryGetFeatureValue(UnityEngine.XR.CommonUsages.triggerButton, out bool rightTrigger))
            {
                rightTriggerPressed = rightTrigger;
            }
            
            // Grip derecho
            if (rightDevice.TryGetFeatureValue(UnityEngine.XR.CommonUsages.gripButton, out bool rightGrip))
            {
                rightGripPressed = rightGrip;
            }
        }
        
        // Debug cada 2 segundos
        if (Time.time % 2f < 0.1f)
        {
            // Debug logs removidos para evitar spam
        }
        
        // Trigger DERECHO = Colocar preview (bloque transparente que no se mueve)
        if (rightTriggerPressed)
        {
            PlacePreviewBrick();
        }
        
        // Grip DERECHO = Confirmar/Validar (si está en modo preview colocado)
        if (rightGripPressed)
        {
            if (!isPreviewMode)
            {
                ConfirmBrickPlacement();
            }
            else if (currentBrick != null)
            {
                CancelPreviewPlacement();
            }
        }
        
        // Cancelar preview si se mueve el controlador (botón A)
        HandlePreviewCancellation();
    }
    
    private void UpdateBrickPosition(RaycastHit hitInfo)
    {
        if (currentBrick == null) 
        {
            Debug.LogWarning("❌ CurrentBrick es null en UpdateBrickPosition");
            return;
        }
        
        // Solo actualizar posición si estamos en modo preview libre
        if (!isPreviewMode) 
        {
            Debug.Log("🎯 Preview colocado - no se mueve");
            return;
        }
        
        // Activar el bloque preview
        currentBrick.gameObject.SetActive(true);
        
        // Snap a la cuadrícula
        Vector3 snappedPosition = GridSystem.SnapToGrid(hitInfo.point);
        
        // Buscar posición libre
        Vector3 finalPosition = FindValidPosition(snappedPosition);
        
        // Si no se encuentra posición válida, usar la posición snappeada
        if (finalPosition == Vector3.zero)
        {
            finalPosition = snappedPosition;
        }
        
        // Actualizar posición del bloque
        currentBrick.transform.position = finalPosition;
        
        // Usar el sistema de validación para colores
        if (validationSystem != null)
        {
            isPositionValid = validationSystem.ValidatePosition(finalPosition, currentBrick.GetBrickSize(), currentBrick.transform.rotation, currentBrick);
            validationSystem.ApplyPreviewColor(currentBrick, isPositionValid);
        }
        else
        {
            Debug.LogError("❌ BrickValidationSystem no está disponible");
            isPositionValid = false;
        }
        
        Debug.Log($"🎯 Preview siguiendo en {finalPosition} - Válida: {isPositionValid}");
    }
    
    private Vector3 FindValidPosition(Vector3 startPosition)
    {
        Vector3 brickSize = currentBrick.GetBrickSize();
        Quaternion brickRotation = currentBrick.transform.rotation;
        
        // Verificar posición inicial
        if (GridSystem.IsPositionFree(startPosition, brickSize, brickRotation))
        {
            return startPosition;
        }
        
        // Buscar hacia arriba
        Vector3 freePosition = GridSystem.FindNextFreePositionUp(startPosition, brickSize, brickRotation);
        return freePosition;
    }
    
    
    // CreateInvalidMaterial movido a BrickValidationSystem.cs
    
    // Validaciones movidas a BrickValidationSystem.cs
    
    // Método de prueba de colisiones movido a BrickValidationSystem.cs
    
    
    
    /// <summary>
    /// SNAP A BLOQUE: Coloca el bloque encima del collider de un bloque existente
    /// </summary>
    /// <param name="hit">Información del raycast hit en un bloque LEGO</param>
    /// <returns>Posición centrada encima del bloque existente</returns>
    private Vector3 SnapToBlock(RaycastHit hit)
    {
        // Obtener el collider del bloque existente
        Collider blockCollider = hit.collider;
        
        // Calcular el centro del bloque en X y Z
        Vector3 blockCenter = blockCollider.bounds.center;
        float centerX = blockCenter.x;
        float centerZ = blockCenter.z;
        
        // Obtener la parte superior del collider del bloque
        float blockTop = blockCollider.bounds.max.y;
        
        // Calcular la altura del nuevo bloque
        Vector3 newBrickSize = currentBrick.GetBrickSize();
        float newBrickHeight = newBrickSize.y;
        
        // Pequeño espacio entre bloques (casi pegados)
        float smallGap = 0.01f;
        
        // Posición final: centro del bloque existente + altura del nuevo bloque + espacio
        Vector3 finalPosition = new Vector3(
            centerX,
            blockTop + (newBrickHeight * 0.5f) + smallGap,
            centerZ
        );
        
        return finalPosition;
    }
    
    /// <summary>
    /// SNAP A GRID: Coloca el bloque en un espacio de la cuadrícula
    /// </summary>
    /// <param name="hitPoint">Punto donde se hizo el hit en la grid</param>
    /// <returns>Posición snappeada a la cuadrícula</returns>
    private Vector3 SnapToGrid(Vector3 hitPoint)
    {
        // Usar el sistema de grid existente
        return GridSystem.SnapToGrid(hitPoint);
    }
    
    
    
    
    
    
    
    /// <summary>
    /// Hacer snap a un bloque LEGO existente
    /// </summary>
    /// <param name="hitPoint">Punto donde se hizo el hit</param>
    /// <param name="legoCollider">Collider del bloque LEGO</param>
    /// <returns>Posición snappeada al bloque</returns>
    private Vector3 SnapToLegoBlock(Vector3 hitPoint, Collider legoCollider)
    {
        // Obtener el centro del bloque LEGO
        Vector3 blockCenter = legoCollider.bounds.center;
        
        // Usar el centro X,Z del bloque existente (no recalcular con grid)
        float snappedX = blockCenter.x;
        float snappedZ = blockCenter.z;
        
        // Agregar un pequeño espacio entre bloques (0.01 unidades)
        float smallGap = 0.01f;
        
        // Colocar el nuevo bloque encima del bloque existente con un pequeño espacio
        float newHeight = legoCollider.bounds.max.y + (currentBrick.GetBrickSize().y * 0.5f) + smallGap;
        
        return new Vector3(snappedX, newHeight, snappedZ);
    }
    
    private void PlacePreviewBrick()
    {
        Debug.Log("🎯 Colocando preview de bloque");
        
        if (currentBrick == null)
        {
            Debug.LogError("❌ No hay bloque preview para colocar");
            return;
        }
        
        // Verificar validez de la posición actual usando el sistema de validación
        Vector3 currentPosition = currentBrick.transform.position;
        bool positionValid = false;
        
        if (validationSystem != null)
        {
            positionValid = validationSystem.ValidatePosition(currentPosition, currentBrick.GetBrickSize(), currentBrick.transform.rotation, currentBrick);
        }
        else
        {
            Debug.LogError("❌ BrickValidationSystem no está disponible");
            return;
        }
        
        if (!positionValid)
        {
            Debug.LogWarning("⚠️ Posición no válida para colocar preview");
            validationSystem.ApplyPreviewColor(currentBrick, false);
            return;
        }
        
        // Cambiar a modo de confirmación (preview fijo - NO se mueve)
        isPreviewMode = false;
        
        // Cambiar nombre del bloque para indicar que es un preview colocado
        currentBrick.gameObject.name = "PreviewPlaced_" + currentBrick.gameObject.name;
        
        // Aplicar color cian para indicar que está listo para confirmar
        if (validationSystem != null)
        {
            validationSystem.ApplyConfirmColor(currentBrick);
        }
        else
        {
            Debug.LogError("❌ BrickValidationSystem no está disponible");
        }
        
        Debug.Log($"✅ Preview colocado en posición: {currentBrick.transform.position}");
        Debug.Log("🎯 Preview FIJO - No se mueve aunque muevas el controlador");
        Debug.Log("🎯 Usa el TRIGGER IZQUIERDO para confirmar o Botón A para cancelar");
    }
    
    private void ConfirmBrickPlacement()
    {
        Debug.Log($"🔧 ConfirmBrickPlacement - CurrentBrick: {(currentBrick != null ? "OK" : "NULL")}, IsPositionValid: {isPositionValid}");
        
        if (currentBrick == null)
        {
            Debug.LogError("❌ No hay preview para confirmar");
            return;
        }
        
        if (!isPositionValid)
        {
            Debug.LogWarning("⚠️ Posición no válida para confirmar");
            return;
        }
        
        Debug.Log($"📍 Posición del bloque: {currentBrick.transform.position}");
        Debug.Log($"👁️ Bloque activo antes de colocar: {currentBrick.gameObject.activeInHierarchy}");
        
        // Cambiar nombre del bloque para distinguirlo del preview
        currentBrick.gameObject.name = "PlacedBrick_" + currentBrick.gameObject.name;
        
        // Colocar el bloque definitivamente PRIMERO
        currentBrick.PlaceBrick();
        
        // Aplicar el color seleccionado al bloque final DESPUÉS de colocarlo
        ApplyColorToBrick(currentBrick, currentBrickColor);
        Debug.Log($"🎨 Color aplicado al bloque final DESPUÉS de colocar: {currentBrickColor}");
        
        // Registrar la colocación en el sistema de validación (inicia cooldown)
        if (validationSystem != null)
        {
            validationSystem.OnBrickPlaced();
        }
        
        Debug.Log($"👁️ Bloque activo después de colocar: {currentBrick.gameObject.activeInHierarchy}");
        Debug.Log($"🎨 Bloque es transparente: {currentBrick.IsTransparent}");
        Debug.Log($"🏗️ Bloque está colocado: {currentBrick.IsPlaced}");
        
        // Verificar renderers
        Renderer[] renderers = currentBrick.GetComponentsInChildren<Renderer>();
        Debug.Log($"🎨 Renderers del bloque colocado: {renderers.Length}");
        foreach (Renderer renderer in renderers)
        {
            if (renderer != null)
            {
                Debug.Log($"  - {renderer.name}: Enabled={renderer.enabled}, Material={renderer.material.name}");
            }
        }
        
        // Volver a modo preview
        isPreviewMode = true;
        currentBrick = null;
        
        // Crear nuevo bloque preview
        CreatePreviewBrick();
        
        // Verificar bloques en la escena
        BrickVR[] allBricks = FindObjectsByType<BrickVR>(FindObjectsSortMode.None);
        Debug.Log($"🏗️ Total de bloques en la escena: {allBricks.Length}");
        for (int i = 0; i < allBricks.Length; i++)
        {
            if (allBricks[i] != null)
            {
                Debug.Log($"  - Bloque {i}: {allBricks[i].name} en posición {allBricks[i].transform.position}, Activo: {allBricks[i].gameObject.activeInHierarchy}");
            }
        }
        
        Debug.Log("✅ Bloque confirmado y colocado exitosamente");
    }
    
    /// <summary>
    /// Obtiene información sobre el cooldown de colocación
    /// </summary>
    public string GetCooldownInfo()
    {
        if (validationSystem != null)
        {
            float remainingTime = validationSystem.GetRemainingCooldownTime();
            if (remainingTime > 0)
            {
                return $"⏰ Cooldown activo: {remainingTime:F1}s restantes";
            }
            else
            {
                return "✅ Listo para colocar";
            }
        }
        return "❌ Sistema de validación no disponible";
    }
    
    /// <summary>
    /// Establece el color del bloque actual
    /// </summary>
    public void SetBrickColor(Color color)
    {
        currentBrickColor = color;
        
        Debug.Log($"🎨 SetBrickColor llamado con color: {color}");
        Debug.Log($"🎨 SetBrickColor: Color RGB: R={color.r:F3}, G={color.g:F3}, B={color.b:F3}");
        Debug.Log($"🎨 SetBrickColor: currentBrickColor actualizado a: {currentBrickColor}");
        Debug.Log($"🎨 CurrentBrick existe: {currentBrick != null}");
        
        // Aplicar el color al preview actual si existe
        if (currentBrick != null)
        {
            Debug.Log($"🎨 Aplicando color al bloque: {currentBrick.name}");
            ApplyColorToBrick(currentBrick, color);
        }
        else
        {
            Debug.LogWarning("⚠️ No hay bloque preview para aplicar el color");
        }
        
        Debug.Log($"🎨 Color del bloque cambiado a: {color}");
    }
    
    /// <summary>
    /// Método de prueba para cambiar el color manualmente
    /// </summary>
    [ContextMenu("Test Red Color")]
    public void TestRedColor()
    {
        SetBrickColor(Color.red);
        Debug.Log("🔴 Color rojo aplicado manualmente");
    }
    
    [ContextMenu("Test Blue Color")]
    public void TestBlueColor()
    {
        SetBrickColor(Color.blue);
        Debug.Log("🔵 Color azul aplicado manualmente");
    }
    
    [ContextMenu("Test Green Color")]
    public void TestGreenColor()
    {
        SetBrickColor(Color.green);
        Debug.Log("🟢 Color verde aplicado manualmente");
    }
    
    /// <summary>
    /// Aplica un color a un bloque
    /// </summary>
    private void ApplyColorToBrick(BrickVR brick, Color color)
    {
        if (brick == null) 
        {
            Debug.LogWarning("⚠️ ApplyColorToBrick: Brick es null");
            return;
        }
        
        Debug.Log($"🎨 ApplyColorToBrick: Aplicando color {color} a {brick.name}");
        
        // Obtener todos los renderers del bloque
        Renderer[] renderers = brick.GetComponentsInChildren<Renderer>();
        Debug.Log($"🎨 ApplyColorToBrick: Encontrados {renderers.Length} renderers");
        
        foreach (Renderer renderer in renderers)
        {
            if (renderer != null && renderer.material != null)
            {
                Debug.Log($"🎨 ApplyColorToBrick: Aplicando color a renderer {renderer.name}");
                Debug.Log($"🎨 ApplyColorToBrick: Material original: {renderer.material.name}");
                Debug.Log($"🎨 ApplyColorToBrick: Shader original: {renderer.material.shader.name}");
                
                // Crear un material completamente nuevo con shader Unlit/Color (más simple)
                Shader unlitShader = Shader.Find("Unlit/Color");
                if (unlitShader == null)
                {
                    // Fallback a shader estándar
                    unlitShader = Shader.Find("Standard");
                }
                
                if (unlitShader == null)
                {
                    // Último fallback al shader original
                    unlitShader = renderer.material.shader;
                }
                
                Material newMaterial = new Material(unlitShader);
                newMaterial.color = color;
                
                Debug.Log($"🎨 ApplyColorToBrick: Usando shader: {newMaterial.shader.name}");
                Debug.Log($"🎨 ApplyColorToBrick: Color aplicado: {newMaterial.color}");
                
                // Aplicar el nuevo material
                renderer.material = newMaterial;
                
                Debug.Log($"🎨 ApplyColorToBrick: Material aplicado exitosamente");
            }
            else
            {
                Debug.LogWarning($"⚠️ ApplyColorToBrick: Renderer o material es null en {renderer?.name}");
            }
        }
    }
    
    private void CancelPreviewPlacement()
    {
        Debug.Log("❌ Cancelando preview de bloque");
        
            if (currentBrick != null)
            {
            // Destruir el preview colocado
            DestroyImmediate(currentBrick.gameObject);
            currentBrick = null;
        }
        
        // Volver a modo preview
        isPreviewMode = true;
        
        // Crear nuevo bloque preview
        CreatePreviewBrick();
        
        Debug.Log("✅ Preview cancelado, volviendo a modo preview");
    }
    
    private void HandlePreviewCancellation()
    {
        // Cancelar preview con botón A del controlador derecho
        List<UnityEngine.XR.InputDevice> rightHandDevices = new List<UnityEngine.XR.InputDevice>();
        UnityEngine.XR.InputDevices.GetDevicesAtXRNode(UnityEngine.XR.XRNode.RightHand, rightHandDevices);
        
        if (rightHandDevices.Count > 0)
        {
            var rightDevice = rightHandDevices[0];
            if (rightDevice.TryGetFeatureValue(UnityEngine.XR.CommonUsages.primaryButton, out bool buttonA))
            {
                if (buttonA)
                {
                    if (!isPreviewMode)
                    {
                        // Cancelar preview colocado
                        CancelPreviewPlacement();
                    }
                    else if (currentBrick != null)
                    {
                        // Rotar bloque en modo preview libre
                currentBrick.RotateBrick();
                        // Debug log removido para evitar spam
                    }
                }
            }
        }
    }
    
    private void HandleBrickRotation()
    {
        // La rotación ahora se maneja en HandlePreviewCancellation()
        // Este método se mantiene por compatibilidad pero no se usa
    }
    
    private void HandleBrickDeletion()
    {
        // Eliminación con botón X del controlador derecho
        List<UnityEngine.XR.InputDevice> rightHandDevices = new List<UnityEngine.XR.InputDevice>();
        UnityEngine.XR.InputDevices.GetDevicesAtXRNode(UnityEngine.XR.XRNode.RightHand, rightHandDevices);
        
        if (rightHandDevices.Count > 0)
        {
            var rightDevice = rightHandDevices[0];
            if (rightDevice.TryGetFeatureValue(UnityEngine.XR.CommonUsages.secondaryButton, out bool buttonX))
            {
                if (buttonX && isPreviewMode)
        {
            DeleteBrickAtController();
                    Debug.Log("🗑️ Eliminando bloque (Botón X)");
                }
            }
        }
    }
    
    private void DeleteBrickAtController()
    {
        if (rayInteractor == null || !rayInteractor.TryGetCurrent3DRaycastHit(out RaycastHit hit)) return;
        
        // Usar el hit del ray interactor
        BrickVR brickToDelete = hit.collider.GetComponent<BrickVR>();
            if (brickToDelete != null)
            {
                Destroy(brickToDelete.gameObject);
            Debug.Log("🗑️ Bloque eliminado");
        }
    }
    
    private void HandleBrickSelection()
    {
        // Cambiar tipo de bloque con grip del controlador derecho
        List<UnityEngine.XR.InputDevice> rightHandDevices = new List<UnityEngine.XR.InputDevice>();
        UnityEngine.XR.InputDevices.GetDevicesAtXRNode(UnityEngine.XR.XRNode.RightHand, rightHandDevices);
        
        if (rightHandDevices.Count > 0)
        {
            var rightDevice = rightHandDevices[0];
            if (rightDevice.TryGetFeatureValue(UnityEngine.XR.CommonUsages.gripButton, out bool gripButton))
            {
                if (gripButton && isPreviewMode)
        {
            SelectNextBrick();
                    Debug.Log("🔄 Cambiando tipo de bloque (Grip)");
                }
            }
        }
    }
    
    private void SelectNextBrick()
    {
        currentBrickIndex = (currentBrickIndex + 1) % brickPrefabs.Length;
        
        // Destruir bloque actual
        if (currentBrick != null)
        {
            Destroy(currentBrick.gameObject);
        }
        
        // Crear nuevo bloque
        CreatePreviewBrick();
        
        // Re-inicializar grid con nuevo prefab
        GridSystem.InitializeGrid(brickPrefabs[currentBrickIndex]);
        
        Debug.Log($"🔄 Bloque seleccionado: {brickPrefabs[currentBrickIndex].name}");
    }
    
    private void CreatePreviewBrick()
    {
        if (brickPrefabs.Length == 0)
        {
            Debug.LogError("PlaceBrickVR: No hay prefabs de bloques configurados");
            return;
        }
        
        // NO destruir el bloque anterior si ya está colocado
        if (currentBrick != null && !currentBrick.IsPlaced)
        {
            DestroyImmediate(currentBrick.gameObject);
        }
        
        // Instanciar nuevo bloque
        GameObject brickObject = Instantiate(brickPrefabs[currentBrickIndex]);
        brickObject.name = "PreviewBrick_" + brickPrefabs[currentBrickIndex].name;
        
        currentBrick = brickObject.GetComponent<BrickVR>();
        if (currentBrick == null)
        {
            currentBrick = brickObject.AddComponent<BrickVR>();
        }
        
        // Asegurar que esté activo
        brickObject.SetActive(true);
        
        // Configurar como preview
        currentBrick.PrepareForPlacement(transparentMaterial);
        
        // Aplicar material
        if (brickMaterials.Length > 0)
        {
            currentBrick.SetMaterial(brickMaterials[currentMaterialIndex]);
        }
        
        // Aplicar el color seleccionado
        ApplyColorToBrick(currentBrick, currentBrickColor);
        
        // Posicionar en el centro de la escena inicialmente
        brickObject.transform.position = Vector3.zero;
        
        Debug.Log($"🧱 Bloque preview creado: {brickObject.name}, Activo: {brickObject.activeInHierarchy}");
    }
    
    
    // Métodos de materiales movidos a BrickValidationSystem.cs
    
    /// <summary>
    /// Cambia el modo de construcción
    /// </summary>
    /// <param name="building">True para modo construcción, false para modo exploración</param>
    public void SetBuildingMode(bool building)
    {
        IsBuilding = building;
        
        if (!building && currentBrick != null)
        {
            currentBrick.gameObject.SetActive(false);
        }
        else if (building && currentBrick != null)
        {
            currentBrick.gameObject.SetActive(true);
        }
    }
    
    /// <summary>
    /// Cambia el material del bloque actual
    /// </summary>
    /// <param name="materialIndex">Índice del material en el array</param>
    public void SetBrickMaterial(int materialIndex)
    {
        if (materialIndex >= 0 && materialIndex < brickMaterials.Length)
        {
            currentMaterialIndex = materialIndex;
            if (currentBrick != null)
            {
                currentBrick.SetMaterial(brickMaterials[materialIndex]);
            }
        }
    }
    
    /// <summary>
    /// Suscribirse a los eventos de raycast
    /// </summary>
    /// <param name="onHit">Callback cuando hay hit</param>
    /// <param name="onMiss">Callback cuando no hay hit</param>
    public void SubscribeToRaycastEvents(System.Action<RaycastHit> onHit, System.Action onMiss)
    {
        OnRaycastHit += onHit;
        OnRaycastMiss += onMiss;
        Debug.Log("✅ Suscrito a eventos de raycast");
    }
    
    /// <summary>
    /// Desuscribirse de los eventos de raycast
    /// </summary>
    /// <param name="onHit">Callback cuando hay hit</param>
    /// <param name="onMiss">Callback cuando no hay hit</param>
    public void UnsubscribeFromRaycastEvents(System.Action<RaycastHit> onHit, System.Action onMiss)
    {
        OnRaycastHit -= onHit;
        OnRaycastMiss -= onMiss;
        Debug.Log("✅ Desuscrito de eventos de raycast");
    }
    
    
    
    
    
    
    
    
    
    


    // Método DebugSistemaCompleto duplicado eliminado - usando el método actualizado
    
    
    
    
    
    /// <summary>
    /// Verificar mapeo del Input System
    /// </summary>
    [ContextMenu("Verificar Mapeo Input")]
    public void CheckInputMapping()
    {
        Debug.Log("=== VERIFICACIÓN MAPEO INPUT ===");
        
        if (inputActions != null)
        {
            Debug.Log($"InputActions: OK");
            Debug.Log($"Attack Enabled: {inputActions.Player.Attack.enabled}");
            Debug.Log($"Jump Enabled: {inputActions.Player.Jump.enabled}");
            Debug.Log($"Crouch Enabled: {inputActions.Player.Crouch.enabled}");
            Debug.Log($"Next Enabled: {inputActions.Player.Next.enabled}");
            Debug.Log($"Interact Enabled: {inputActions.Player.Interact.enabled}");
            
            // Verificar valores actuales
            float attackValue = inputActions.Player.Attack.ReadValue<float>();
            bool attackPressed = inputActions.Player.Attack.WasPressedThisFrame();
            bool attackHeld = inputActions.Player.Attack.IsPressed();
            
            Debug.Log($"Attack Value: {attackValue}");
            Debug.Log($"Attack Pressed: {attackPressed}");
            Debug.Log($"Attack Held: {attackHeld}");
            
            // Verificar otros botones
            bool jumpPressed = inputActions.Player.Jump.WasPressedThisFrame();
            bool crouchPressed = inputActions.Player.Crouch.WasPressedThisFrame();
            bool nextPressed = inputActions.Player.Next.WasPressedThisFrame();
            bool interactPressed = inputActions.Player.Interact.WasPressedThisFrame();
            
            Debug.Log($"Jump Pressed: {jumpPressed}");
            Debug.Log($"Crouch Pressed: {crouchPressed}");
            Debug.Log($"Next Pressed: {nextPressed}");
            Debug.Log($"Interact Pressed: {interactPressed}");
        }
        else
        {
            Debug.LogError("InputActions es null");
        }
    }
    
    /// <summary>
    /// Verificar dispositivos VR y mapeos
    /// </summary>
    [ContextMenu("Verificar Dispositivos VR")]
    public void CheckVRDevices()
    {
        Debug.Log("=== VERIFICACIÓN DISPOSITIVOS VR (SOLO VR) ===");
        
        // Verificar dispositivos de entrada
        var devices = UnityEngine.InputSystem.InputSystem.devices;
        Debug.Log($"Total dispositivos detectados: {devices.Count}");
        
        // Solo mostrar dispositivos VR
        var vrDevices = devices.Where(d => 
            d.name.Contains("XR") || 
            d.name.Contains("OpenXR") || 
            d.name.Contains("Controller") ||
            d.name.Contains("Hand")).ToArray();
        
        Debug.Log($"Dispositivos VR activos: {vrDevices.Length}");
        
        foreach (var device in vrDevices)
        {
            Debug.Log($"  ✅ {device.name} ({device.GetType().Name})");
        }
        
        // Mostrar dispositivos deshabilitados
        var disabledDevices = devices.Where(d => 
            d.name.Contains("Keyboard") || 
            d.name.Contains("Mouse") || 
            d.name.Contains("Gamepad")).ToArray();
        
        Debug.Log($"Dispositivos no VR deshabilitados: {disabledDevices.Length}");
        
        // Verificar grupos de control
        if (inputActions != null)
        {
            Debug.Log($"Grupo de control: XR (SOLO VR)");
            Debug.Log($"Binding mask: {inputActions.asset.bindingMask}");
        }
    }
    
    /// <summary>
    /// Verificar todos los botones del controlador VR
    /// </summary>
    [ContextMenu("Verificar Botones VR")]
    public void CheckVRButtons()
    {
        Debug.Log("=== VERIFICACIÓN BOTONES VR ===");
        
        // Verificar todos los dispositivos XR disponibles
        List<UnityEngine.XR.InputDevice> allDevices = new List<UnityEngine.XR.InputDevice>();
        UnityEngine.XR.InputDevices.GetDevices(allDevices);
        
        Debug.Log($"Total dispositivos XR encontrados: {allDevices.Count}");
        
        foreach (var device in allDevices)
        {
            Debug.Log($"- Dispositivo: {device.name}, Características: {device.characteristics}");
        }
        
        // Verificar controladores específicos
        List<UnityEngine.XR.InputDevice> rightHandDevices = new List<UnityEngine.XR.InputDevice>();
        List<UnityEngine.XR.InputDevice> leftHandDevices = new List<UnityEngine.XR.InputDevice>();
        
        UnityEngine.XR.InputDevices.GetDevicesAtXRNode(UnityEngine.XR.XRNode.RightHand, rightHandDevices);
        UnityEngine.XR.InputDevices.GetDevicesAtXRNode(UnityEngine.XR.XRNode.LeftHand, leftHandDevices);
        
        Debug.Log($"Controladores mano derecha: {rightHandDevices.Count}");
        Debug.Log($"Controladores mano izquierda: {leftHandDevices.Count}");
        
        // Probar con el controlador disponible
        bool hasTargetDevice = false;
        UnityEngine.XR.InputDevice targetDevice = new UnityEngine.XR.InputDevice();
        
        if (rightHandDevices.Count > 0)
        {
            targetDevice = rightHandDevices[0];
            hasTargetDevice = true;
            Debug.Log($"Usando controlador derecho: {targetDevice.name}");
        }
        else if (leftHandDevices.Count > 0)
        {
            targetDevice = leftHandDevices[0];
            hasTargetDevice = true;
            Debug.Log($"Usando controlador izquierdo: {targetDevice.name}");
        }
        
        if (hasTargetDevice)
        {
            // Verificar todos los botones disponibles
            if (targetDevice.TryGetFeatureValue(UnityEngine.XR.CommonUsages.triggerButton, out bool triggerButton))
                Debug.Log($"Trigger Button: {triggerButton}");
            
            if (targetDevice.TryGetFeatureValue(UnityEngine.XR.CommonUsages.trigger, out float triggerValue))
                Debug.Log($"Trigger Value: {triggerValue}");
            
            if (targetDevice.TryGetFeatureValue(UnityEngine.XR.CommonUsages.gripButton, out bool gripButton))
                Debug.Log($"Grip Button: {gripButton}");
            
            if (targetDevice.TryGetFeatureValue(UnityEngine.XR.CommonUsages.grip, out float gripValue))
                Debug.Log($"Grip Value: {gripValue}");
            
            if (targetDevice.TryGetFeatureValue(UnityEngine.XR.CommonUsages.primaryButton, out bool primaryButton))
                Debug.Log($"Primary Button: {primaryButton}");
            
            if (targetDevice.TryGetFeatureValue(UnityEngine.XR.CommonUsages.secondaryButton, out bool secondaryButton))
                Debug.Log($"Secondary Button: {secondaryButton}");
            
            if (targetDevice.TryGetFeatureValue(UnityEngine.XR.CommonUsages.menuButton, out bool menuButton))
                Debug.Log($"Menu Button: {menuButton}");
        }
        else
        {
            Debug.LogWarning("⚠️ No se encontraron controladores VR. Verifica que el Quest esté conectado y configurado.");
            Debug.Log("💡 Sugerencias:");
            Debug.Log("1. Asegúrate de que el Quest esté conectado por USB");
            Debug.Log("2. Habilita el modo desarrollador en el Quest");
            Debug.Log("3. Verifica que XR Plugin Management esté configurado");
        }
    }
    
    /// <summary>
    /// Configurar Input Action Manager automáticamente
    /// </summary>
    [ContextMenu("Configurar Input Action Manager")]
    public void SetupInputActionManager()
    {
        Debug.Log("=== CONFIGURANDO INPUT ACTION MANAGER ===");
        
        // Buscar Input Action Manager
        InputActionManager inputActionManager = GetComponentInParent<InputActionManager>();
        if (inputActionManager == null)
        {
            Debug.LogError("❌ No se encontró Input Action Manager en el XR Origin");
            return;
        }
        
        Debug.Log("✅ Input Action Manager encontrado");
        
        // InputActions ya deberían estar creados por SetupInputActionsDirectly()
        if (inputActions == null)
        {
            Debug.LogWarning("⚠️ InputActions no está creado - esto no debería pasar");
            return;
        }
        
        // Configurar el Input Action Manager usando reflexión
        var inputActionManagerType = typeof(InputActionManager);
        var actionsField = inputActionManagerType.GetField("m_ActionAssets", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        
        if (actionsField != null)
        {
            var currentActions = actionsField.GetValue(inputActionManager) as List<InputActionAsset>;
            if (currentActions == null || currentActions.Count == 0)
            {
                // Crear List con nuestro InputActions
                var newActions = new List<InputActionAsset> { inputActions.asset };
                actionsField.SetValue(inputActionManager, newActions);
                Debug.Log("✅ InputActions asignado al Input Action Manager");
            }
            else
            {
                Debug.Log($"✅ Input Action Manager ya tiene {currentActions.Count} acciones configuradas");
                // Agregar nuestro InputActions si no está ya presente
                if (!currentActions.Contains(inputActions.asset))
                {
                    currentActions.Add(inputActions.asset);
                    Debug.Log("✅ InputActions agregado a la lista existente");
                }
            }
        }
        else
        {
            Debug.LogWarning("⚠️ No se pudo acceder al campo de acciones del Input Action Manager");
        }
        
        // Habilitar las acciones
        if (inputActions != null)
        {
            inputActions.Player.Attack.Enable();
            inputActions.Player.Jump.Enable();
            inputActions.Player.Crouch.Enable();
            inputActions.Player.Next.Enable();
            Debug.Log("✅ Acciones habilitadas");
        }
    }
    
    private System.Collections.IEnumerator TestInputCoroutine()
    {
        Debug.Log("🧪 INICIANDO PRUEBA DE INPUT - Presiona el trigger ahora");
        
        for (int i = 0; i < 100; i++) // 10 segundos de prueba
        {
            if (inputActions != null)
            {
                bool attackPressed = inputActions.Player.Attack.WasPressedThisFrame();
                bool attackHeld = inputActions.Player.Attack.IsPressed();
                bool jumpPressed = inputActions.Player.Jump.WasPressedThisFrame();
                
                if (attackPressed)
                {
                    Debug.Log("✅ TRIGGER DETECTADO!");
                    yield break;
                }
                else if (attackHeld)
                {
                    Debug.Log("🔄 TRIGGER MANTENIDO");
                }
                else if (jumpPressed)
                {
                    Debug.Log("🔄 BOTÓN A DETECTADO");
                }
            }
            
            yield return new WaitForSeconds(0.1f);
        }
        
        Debug.Log("⏰ Prueba de input terminada - No se detectó trigger");
    }
    
    private void OnDestroy()
    {
        // Debug log removido para evitar spam
        
        if (currentBrick != null)
        {
            Destroy(currentBrick.gameObject);
        }
        
        // RaycastSystem removido - usando solo XRRayInteractor
        
        // Limpiar ray interactor
        if (rayInteractor != null)
        {
            rayInteractor = null;
        }
        
        // Limpiar Input Actions para evitar memory leaks
        if (inputActions != null)
        {
            try
            {
                // Deshabilitar todas las acciones individualmente
                inputActions.Player.Attack.Disable();
                inputActions.Player.Jump.Disable();
                inputActions.Player.Crouch.Disable();
                inputActions.Player.Next.Disable();
                inputActions.Player.Interact.Disable();
                inputActions.Player.Previous.Disable();
                inputActions.Player.Sprint.Disable();
                
                // Deshabilitar mapas completos
                inputActions.Player.Disable();
                inputActions.UI.Disable();
                
                // Dispose del asset completo
                inputActions.Dispose();
                
                Debug.Log("PlaceBrickVR: InputActions limpiado correctamente");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Error al limpiar InputActions: {e.Message}");
            }
            finally
            {
                inputActions = null;
            }
        }
    }
    
    private void OnEnable()
    {
        Debug.Log("🔧 OnEnable - Verificando Input Actions");
        
        // Si inputActions es null, se configurarán en Start()
        if (inputActions == null)
        {
            Debug.Log("⚠️ InputActions es NULL en OnEnable - se configurarán en Start()");
            return;
        }
        
        try
        {
            inputActions.Player.Attack.Enable();
            inputActions.Player.Jump.Enable();
            inputActions.Player.Crouch.Enable();
            inputActions.Player.Next.Enable();
            inputActions.Player.Interact.Enable();
            inputActions.Player.Previous.Enable();
            inputActions.Player.Sprint.Enable();
            Debug.Log("✅ Input Actions habilitados en OnEnable");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"❌ Error al habilitar Input Actions: {e.Message}");
        }
    }
    
    private void OnDisable()
    {
        if (inputActions != null)
        {
            try
            {
                // Deshabilitar todas las acciones individualmente
            inputActions.Player.Attack.Disable();
            inputActions.Player.Jump.Disable();
            inputActions.Player.Crouch.Disable();
            inputActions.Player.Next.Disable();
                inputActions.Player.Interact.Disable();
                inputActions.Player.Previous.Disable();
                inputActions.Player.Sprint.Disable();
                
                // Deshabilitar mapas completos
                inputActions.Player.Disable();
                inputActions.UI.Disable();
                
                Debug.Log("🎮 Input Actions deshabilitadas");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Error al deshabilitar InputActions: {e.Message}");
            }
        }
    }
}