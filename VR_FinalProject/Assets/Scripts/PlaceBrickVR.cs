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
    
    [Header("Configuración VR")]
    [SerializeField] private UnityEngine.XR.Interaction.Toolkit.Interactors.XRRayInteractor rayInteractor; // Ray Interactor del XR Interaction Toolkit
    [SerializeField] private LayerMask legoLayerMask = -1;
    [SerializeField] private float previewSmoothing = 0.1f; // Suavizado del preview
    
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
    private bool hasShownVRWarning = false;
    
    // Suavizado del preview
    private Vector3 targetPreviewPosition;
    private bool hasTargetPosition = false;
    
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
    
    private void SetupInputActionsDirectly()
    {
        Debug.Log("🔧 Configurando Input Actions SOLO para VR...");
        
        // Crear InputActions si no existe
        if (inputActions == null)
        {
            inputActions = new InputSystem_Actions();
            Debug.Log("✅ InputSystem_Actions creado");
        }
        
        // Configurar binding mask para VR ANTES de habilitar
        inputActions.asset.bindingMask = new UnityEngine.InputSystem.InputBinding { groups = "XR" };
        Debug.Log("✅ Binding mask configurado para grupo XR (SOLO VR)");
        
        // Deshabilitar dispositivos no VR
        DisableNonVRDevices();
        
        // Habilitar las acciones
        inputActions.Player.Attack.Enable();
        inputActions.Player.Jump.Enable();
        inputActions.Player.Crouch.Enable();
        inputActions.Player.Next.Enable();
        inputActions.Player.Interact.Enable();
        inputActions.Player.Previous.Enable();
        inputActions.Player.Sprint.Enable();
        
        Debug.Log("✅ Input Actions habilitados SOLO para VR");
        Debug.Log("- Colocar: Attack (Trigger DERECHO)");
        Debug.Log("- Confirmar: Jump (Trigger IZQUIERDO)");
        Debug.Log("- Rotar: Crouch (Botón A)");
        Debug.Log("- Eliminar: Next (Botón X)");
        Debug.Log("- Cambiar tipo: Interact (Botón B)");
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
    
    private void SetupInputActions()
    {
        // Configurar las referencias de Input Actions automáticamente
        if (inputActions != null)
        {
            Debug.Log("PlaceBrickVR: Input Actions configuradas automáticamente");
            Debug.Log("- Colocar: Attack (Trigger)");
            Debug.Log("- Rotar: Jump (Botón A)");
            Debug.Log("- Eliminar: Crouch (Botón X)");
            Debug.Log("- Cambiar tipo: Next (Joystick)");
        }
    }
    
    private void Start()
    {
        InitializeSystem();
        CreatePreviewBrick();
        SetupRaycastSystem();
        
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
                Debug.Log($"🎯 XRRayInteractor HIT: {hit.point}, Objeto: {hit.collider.name}, Layer: {hit.collider.gameObject.layer}");
            }
            else
            {
                // Debug cada 2 segundos para no spamear
                if (Time.time % 2f < 0.1f)
                {
                    Debug.Log($"🎯 XRRayInteractor MISS - No detecta hits en Layer 7+8");
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
            
            hasHit = Physics.Raycast(ray, out hit, 10f, (1 << 7) | (1 << 8)); // Layer 7 (Lego) + Layer 8 (Grid)
            if (hasHit)
            {
                Debug.Log($"🎯 Raycast Manual HIT: {hit.point}, Objeto: {hit.collider.name}, Layer: {hit.collider.gameObject.layer}");
            }
        }
        
        if (hasHit)
        {
            // Si hay hit, usar esa posición con snap
            Vector3 snappedPosition = GridSystem.SnapToGrid(hit.point);
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
        if (currentBrick != null && hasTargetPosition)
        {
            // Suavizar el movimiento del preview
            Vector3 currentPosition = currentBrick.transform.position;
            Vector3 smoothedPosition = Vector3.Lerp(currentPosition, targetPreviewPosition, previewSmoothing);
            currentBrick.transform.position = smoothedPosition;
            
            // Si está muy cerca de la posición objetivo, establecerla directamente
            if (Vector3.Distance(currentPosition, targetPreviewPosition) < 0.01f)
            {
                currentBrick.transform.position = targetPreviewPosition;
                hasTargetPosition = false;
            }
        }
    }
    
    private void HandleBrickPlacement()
    {
        // Usar sistema nativo de XR como RaycastSystem
        bool rightTriggerPressed = false;
        bool leftTriggerPressed = false;
        
        // Verificar controlador derecho
        List<UnityEngine.XR.InputDevice> rightHandDevices = new List<UnityEngine.XR.InputDevice>();
        UnityEngine.XR.InputDevices.GetDevicesAtXRNode(UnityEngine.XR.XRNode.RightHand, rightHandDevices);
        
        if (rightHandDevices.Count > 0)
        {
            var rightDevice = rightHandDevices[0];
            if (rightDevice.TryGetFeatureValue(UnityEngine.XR.CommonUsages.triggerButton, out bool rightTrigger))
            {
                rightTriggerPressed = rightTrigger;
            }
        }
        
        // Verificar controlador izquierdo
        List<UnityEngine.XR.InputDevice> leftHandDevices = new List<UnityEngine.XR.InputDevice>();
        UnityEngine.XR.InputDevices.GetDevicesAtXRNode(UnityEngine.XR.XRNode.LeftHand, leftHandDevices);
        
        if (leftHandDevices.Count > 0)
        {
            var leftDevice = leftHandDevices[0];
            if (leftDevice.TryGetFeatureValue(UnityEngine.XR.CommonUsages.triggerButton, out bool leftTrigger))
            {
                leftTriggerPressed = leftTrigger;
            }
        }
        
        // Debug cada 2 segundos
        if (Time.time % 2f < 0.1f)
        {
            Debug.Log($"🎮 XR Input Debug:");
            Debug.Log($"   - Right Trigger: {rightTriggerPressed}");
            Debug.Log($"   - Left Trigger: {leftTriggerPressed}");
        }
        
        // Trigger DERECHO = Colocar bloque
        if (rightTriggerPressed)
        {
            Debug.Log("🎮 TRIGGER DERECHO presionado - Colocando bloque");
            PlaceCurrentBrick();
        }
        
        // Trigger IZQUIERDO = Confirmar (si está en modo preview colocado)
        if (leftTriggerPressed)
        {
            Debug.Log("🎮 TRIGGER IZQUIERDO presionado - Confirmando bloque");
            if (!isPreviewMode)
            {
                ConfirmBrickPlacement();
            }
        }
    }
    
    private void UpdateBrickPosition(RaycastHit hitInfo)
    {
        Debug.Log($"🔧 UpdateBrickPosition: Hit en {hitInfo.point}, Collider: {hitInfo.collider.name}");
        
        if (currentBrick == null) 
        {
            Debug.LogWarning("❌ CurrentBrick es null en UpdateBrickPosition");
            return;
        }
        
        // Activar el bloque preview
        currentBrick.gameObject.SetActive(true);
        Debug.Log($"✅ Bloque activado: {currentBrick.gameObject.activeInHierarchy}");
        
        // Snap a la cuadrícula
        Vector3 snappedPosition = GridSystem.SnapToGrid(hitInfo.point);
        Debug.Log($"📐 Posición snappeada: {snappedPosition}");
        
        // Buscar posición libre
        Vector3 finalPosition = FindValidPosition(snappedPosition);
        
        // Si no se encuentra posición válida, usar la posición snappeada
        if (finalPosition == Vector3.zero)
        {
            finalPosition = snappedPosition;
            Debug.Log("⚠️ Usando posición snappeada como final");
        }
        
        // Actualizar posición del bloque
        currentBrick.transform.position = finalPosition;
        Debug.Log($"📍 Bloque posicionado en: {finalPosition}");
        
        // Verificar si la posición es válida
        isPositionValid = true; // Siempre permitir colocación
        
        // Actualizar transparencia según validez
        if (isPositionValid)
        {
            currentBrick.SetTransparency(true, transparentMaterial);
            Debug.Log("✅ Transparencia aplicada (verde)");
        }
        else
        {
            // Posición no válida - mostrar en rojo
            Material invalidMaterial = CreateInvalidMaterial();
            currentBrick.SetTransparency(true, invalidMaterial);
            Debug.Log("❌ Transparencia aplicada (rojo)");
        }
        
        Debug.Log($"🎯 Bloque preview en posición: {finalPosition}, Válida: {isPositionValid}");
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
    
    private void PlaceCurrentBrick()
    {
        Debug.Log($"🔧 PlaceCurrentBrick: CurrentBrick={currentBrick != null}, IsPositionValid={isPositionValid}");
        
        if (currentBrick == null) 
        {
            Debug.LogWarning("❌ No hay bloque para colocar");
            return;
        }
        
        if (!isPositionValid) 
        {
            Debug.LogWarning("❌ Posición no válida para colocar");
            return;
        }
        
        // Colocar el bloque definitivamente
        currentBrick.PlaceBrick();
        Debug.Log($"✅ Bloque colocado en: {currentBrick.transform.position}");
        
        // Crear nuevo bloque preview
        CreatePreviewBrick();
        
        Debug.Log("🎉 Bloque colocado exitosamente");
    }
    
    private Material CreateInvalidMaterial()
    {
        Material invalidMat = new Material(transparentMaterial);
        invalidMat.color = new Color(1f, 0f, 0f, 0.8f); // Rojo más opaco
        return invalidMat;
    }
    
    private bool IsPositionOccupied(Vector3 position)
    {
        if (currentBrick == null) 
        {
            Debug.Log("🔍 IsPositionOccupied: currentBrick es null");
            return false;
        }
        
        Vector3 brickSize = currentBrick.GetBrickSize();
        Quaternion brickRotation = currentBrick.transform.rotation;
        
        // Buscar todos los bloques colocados en la escena
        BrickVR[] allBricks = FindObjectsByType<BrickVR>(FindObjectsSortMode.None);
        
        foreach (BrickVR brick in allBricks)
        {
            // Saltar el bloque actual (preview) y bloques no colocados
            if (brick == currentBrick || !brick.IsPlaced) 
            {
                continue;
            }
            
            // Verificar si hay colisión con este bloque
            if (IsBrickColliding(position, brickSize, brickRotation, brick.transform.position, brick.GetBrickSize(), brick.transform.rotation))
            {
                Debug.Log($"🚫 Colisión detectada con bloque {brick.name} en posición {brick.transform.position}");
                return true;
            }
        }
        return false;
    }
    
    private bool IsBrickColliding(Vector3 pos1, Vector3 size1, Quaternion rot1, Vector3 pos2, Vector3 size2, Quaternion rot2)
    {
        // Crear bounds para ambos bloques
        Bounds bounds1 = new Bounds(pos1, size1);
        Bounds bounds2 = new Bounds(pos2, size2);
        
        // Verificar si los bounds se intersectan
        bool isColliding = bounds1.Intersects(bounds2);
        
        return isColliding;
    }
    
    /// <summary>
    /// Método de prueba para verificar colisiones de forma más simple
    /// </summary>
    [ContextMenu("Probar Detección de Colisiones")]
    public void TestCollisionDetection()
    {
        if (currentBrick == null)
        {
            Debug.LogError("No hay bloque actual para probar");
            return;
        }
        
        Vector3 testPosition = currentBrick.transform.position;
        Vector3 brickSize = currentBrick.GetBrickSize();
        
        Debug.Log($"🧪 Probando detección de colisiones en posición: {testPosition}");
        
        // Buscar todos los bloques colocados
        BrickVR[] allBricks = FindObjectsByType<BrickVR>(FindObjectsSortMode.None);
        Debug.Log($"🧪 Bloques encontrados: {allBricks.Length}");
        
        foreach (BrickVR brick in allBricks)
        {
            if (brick == currentBrick || !brick.IsPlaced) continue;
            
            float distance = Vector3.Distance(testPosition, brick.transform.position);
            Debug.Log($"🧪 Distancia a {brick.name}: {distance}");
            
            if (distance < 1.0f) // Si está cerca
            {
                Debug.Log($"🧪 Bloque cercano detectado: {brick.name}");
            }
        }
        
        // Probar la función de colisión
        bool isOccupied = IsPositionOccupied(testPosition);
        Debug.Log($"🧪 Resultado de IsPositionOccupied: {isOccupied}");
    }
    
    [ContextMenu("Forzar Material Rojo")]
    public void ForceRedMaterial()
    {
        if (currentBrick == null)
        {
            Debug.LogError("No hay bloque actual para probar");
            return;
        }
        
        Material invalidMaterial = CreateInvalidMaterial();
        currentBrick.SetTransparency(true, invalidMaterial);
        Debug.Log("🔴 Material rojo forzado para testing");
    }
    
    [ContextMenu("Toggle Raycast Visualization")]
    public void ToggleRaycastVisualization()
    {
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
        
        if (!isPositionValid)
        {
            Debug.LogWarning("⚠️ Posición no válida para colocar preview");
            return;
        }
        
        // Cambiar a modo de confirmación
        isPreviewMode = false;
        
        // Cambiar nombre del bloque para indicar que es un preview colocado
        currentBrick.gameObject.name = "PreviewPlaced_" + currentBrick.gameObject.name;
        
        // Cambiar material para indicar que está listo para confirmar
        Material confirmMaterial = CreateConfirmMaterial();
        currentBrick.SetTransparency(true, confirmMaterial);
        
        Debug.Log($"✅ Preview colocado en posición: {currentBrick.transform.position}");
        Debug.Log("🎯 Usa el Botón B para confirmar o Botón A para cancelar");
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
        
        // Colocar el bloque definitivamente
        currentBrick.PlaceBrick();
        
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
    
    private void HandleBrickRotation()
    {
        // Rotación con botón A del controlador derecho
        List<UnityEngine.XR.InputDevice> rightHandDevices = new List<UnityEngine.XR.InputDevice>();
        UnityEngine.XR.InputDevices.GetDevicesAtXRNode(UnityEngine.XR.XRNode.RightHand, rightHandDevices);
        
        if (rightHandDevices.Count > 0)
        {
            var rightDevice = rightHandDevices[0];
            if (rightDevice.TryGetFeatureValue(UnityEngine.XR.CommonUsages.primaryButton, out bool buttonA))
            {
                if (buttonA && currentBrick != null && isPreviewMode)
                {
                    currentBrick.RotateBrick();
                    Debug.Log("🔄 Bloque rotado (Botón A)");
                }
            }
        }
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
        
        // Posicionar en el centro de la escena inicialmente
        brickObject.transform.position = Vector3.zero;
        
        Debug.Log($"🧱 Bloque preview creado: {brickObject.name}, Activo: {brickObject.activeInHierarchy}");
    }
    
    
    private Material CreateWarningMaterial()
    {
        Material warningMat = new Material(transparentMaterial);
        warningMat.color = new Color(1f, 1f, 0f, 0.5f); // Amarillo transparente
        return warningMat;
    }
    
    private Material CreateConfirmMaterial()
    {
        Material confirmMat = new Material(transparentMaterial);
        confirmMat.color = new Color(0f, 1f, 1f, 0.7f); // Cian transparente para confirmar
        return confirmMat;
    }
    
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
    
    /// <summary>
    /// Probar eventos de raycast
    /// </summary>
    [ContextMenu("Probar Eventos Raycast")]
    public void TestRaycastEvents()
    {
        Debug.Log("🧪 Probando eventos de raycast...");
        
        // Suscribirse temporalmente a los eventos
        SubscribeToRaycastEvents(
            (hit) => Debug.Log($"🎯 EVENTO HIT: {hit.point}, Objeto: {hit.collider.name}"),
            () => Debug.Log("🎯 EVENTO MISS: No hay hit detectado")
        );
        
        Debug.Log("✅ Eventos configurados. Mueve el controlador para probar.");
    }
    
    /// <summary>
    /// Debug de todos los XRRayInteractor en la escena
    /// </summary>
    [ContextMenu("Debug All XRRayInteractors")]
    public void DebugAllXRRayInteractors()
    {
        Debug.Log("=== DEBUG TODOS LOS XR RAY INTERACTORS ===");
        
        XRRayInteractor[] allRayInteractors = FindObjectsByType<XRRayInteractor>(FindObjectsSortMode.None);
        Debug.Log($"📡 Total de XRRayInteractor encontrados: {allRayInteractors.Length}");
        
        for (int i = 0; i < allRayInteractors.Length; i++)
        {
            var interactor = allRayInteractors[i];
            Debug.Log($"\n--- XRRayInteractor {i + 1} ---");
            Debug.Log($"   Nombre: {interactor.name}");
            Debug.Log($"   Activo: {interactor.gameObject.activeInHierarchy}");
            Debug.Log($"   Habilitado: {interactor.enabled}");
            Debug.Log($"   Padre: {interactor.transform.parent?.name ?? "NULL"}");
            
            // Buscar controlador padre
            XRBaseController controller = interactor.GetComponentInParent<XRBaseController>();
            if (controller != null)
            {
                Debug.Log($"   Controlador: {controller.name}");
                Debug.Log($"   Controlador Activo: {controller.gameObject.activeInHierarchy}");
            }
            else
            {
                Debug.Log("   Controlador: NO ENCONTRADO");
            }
            
            // Mostrar jerarquía completa
            Transform current = interactor.transform;
            string hierarchy = interactor.name;
            while (current.parent != null)
            {
                current = current.parent;
                hierarchy = current.name + " -> " + hierarchy;
            }
            Debug.Log($"   Jerarquía: {hierarchy}");
        }
    }
    
    /// <summary>
    /// Forzar habilitación de Input Actions
    /// </summary>
    [ContextMenu("Forzar Habilitar Input Actions")]
    public void ForceEnableInputActions()
    {
        Debug.Log("🔧 Forzando habilitación de Input Actions...");
        
        if (inputActions == null)
        {
            Debug.LogError("❌ InputActions es NULL - no se puede habilitar");
            return;
        }
        
        try
        {
            // Forzar grupo de control XR
            inputActions.asset.bindingMask = new UnityEngine.InputSystem.InputBinding { groups = "XR" };
            Debug.Log("✅ Grupo de control cambiado a XR");
            
            inputActions.Player.Attack.Enable();
            inputActions.Player.Jump.Enable();
            inputActions.Player.Crouch.Enable();
            inputActions.Player.Next.Enable();
            Debug.Log("✅ Input Actions habilitados forzadamente");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"❌ Error al habilitar Input Actions: {e.Message}");
        }
    }
    
    
    /// <summary>
    /// Verificar Input Action Manager en detalle
    /// </summary>
    [ContextMenu("Verificar Input Action Manager")]
    public void CheckInputActionManager()
    {
        Debug.Log("=== VERIFICACIÓN INPUT ACTION MANAGER ===");
        
        // Buscar Input Action Manager
        var inputActionManager = FindFirstObjectByType<UnityEngine.XR.Interaction.Toolkit.Inputs.InputActionManager>();
        
        if (inputActionManager == null)
        {
            Debug.LogError("❌ No se encontró InputActionManager en la escena");
            return;
        }
        
        Debug.Log($"✅ InputActionManager encontrado: {inputActionManager.name}");
        Debug.Log($"   - Enabled: {inputActionManager.enabled}");
        Debug.Log($"   - GameObject Active: {inputActionManager.gameObject.activeInHierarchy}");
        
        // Verificar actionAssets usando reflexión
        var actionAssetsField = typeof(UnityEngine.XR.Interaction.Toolkit.Inputs.InputActionManager)
            .GetField("m_ActionAssets", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        
        if (actionAssetsField != null)
        {
            var actionAssets = actionAssetsField.GetValue(inputActionManager) as System.Collections.Generic.List<UnityEngine.InputSystem.InputActionAsset>;
            
            if (actionAssets != null)
            {
                Debug.Log($"   - ActionAssets Count: {actionAssets.Count}");
                for (int i = 0; i < actionAssets.Count; i++)
                {
                    if (actionAssets[i] != null)
                    {
                        Debug.Log($"     [{i}] {actionAssets[i].name}");
                        
                        // Verificar si es nuestro InputActions
                        if (inputActions != null && actionAssets[i] == inputActions.asset)
                        {
                            Debug.Log($"       ✅ Es nuestro InputSystem_Actions");
                        }
                    }
                }
            }
            else
            {
                Debug.LogWarning("   - ActionAssets es NULL");
            }
        }
        else
        {
            Debug.LogWarning("   - No se pudo acceder al campo ActionAssets");
        }
        
        // Verificar si está en un GameObject con nombre XR
        var parent = inputActionManager.transform.parent;
        bool isInXRHierarchy = false;
        string xrHierarchy = "";
        
        while (parent != null)
        {
            if (parent.name.ToLower().Contains("xr") || parent.name.ToLower().Contains("origin"))
            {
                isInXRHierarchy = true;
                xrHierarchy = parent.name;
                break;
            }
            parent = parent.parent;
        }
        
        if (isInXRHierarchy)
        {
            Debug.Log($"   - En jerarquía XR: {xrHierarchy}");
        }
        else
        {
            Debug.LogWarning("   - No está en una jerarquía XR");
        }
    }
    
    /// <summary>
    /// Debug del sistema de Input Actions
    /// </summary>
    [ContextMenu("Debug Input Actions")]
    public void DebugInputActions()
    {
        Debug.Log("=== DEBUG INPUT ACTIONS ===");
        
        if (inputActions == null)
        {
            Debug.LogError("❌ InputActions es NULL");
            return;
        }
        
        Debug.Log("✅ InputActions disponible");
        Debug.Log($"   - Player Actions: OK");
        Debug.Log($"   - UI Actions: OK");
        
        // PlayerActions y UIActions son structs, siempre están disponibles
        Debug.Log("   - Attack (Trigger) Enabled: " + inputActions.Player.Attack.enabled);
        Debug.Log("   - Jump (Botón A) Enabled: " + inputActions.Player.Jump.enabled);
        Debug.Log("   - Crouch (Botón X) Enabled: " + inputActions.Player.Crouch.enabled);
        Debug.Log("   - Next (Joystick) Enabled: " + inputActions.Player.Next.enabled);
        
        // Verificar si hay Input Action Manager
        var inputActionManager = FindFirstObjectByType<UnityEngine.XR.Interaction.Toolkit.Inputs.InputActionManager>();
        if (inputActionManager != null)
        {
            Debug.Log($"✅ InputActionManager encontrado: {inputActionManager.name}");
            Debug.Log($"   - Enabled: {inputActionManager.enabled}");
        }
        else
        {
            Debug.LogWarning("⚠️ No se encontró InputActionManager");
        }
    }
    
    /// <summary>
    /// Debug de objetos en Layer 7 y 8
    /// </summary>
    [ContextMenu("Debug Layers 7 y 8")]
    public void DebugLayers7y8()
    {
        Debug.Log("=== DEBUG LAYERS 7 Y 8 ===");
        
        // Buscar objetos en Layer 7 (Lego)
        GameObject[] legoObjects = FindObjectsByType<GameObject>(FindObjectsSortMode.None)
            .Where(obj => obj.layer == 7).ToArray();
        
        Debug.Log($"📦 Objetos en Layer 7 (Lego): {legoObjects.Length}");
        foreach (var obj in legoObjects)
        {
            Debug.Log($"   - {obj.name} (Activo: {obj.activeInHierarchy})");
        }
        
        // Buscar objetos en Layer 8 (Grid)
        GameObject[] gridObjects = FindObjectsByType<GameObject>(FindObjectsSortMode.None)
            .Where(obj => obj.layer == 8).ToArray();
        
        Debug.Log($"📐 Objetos en Layer 8 (Grid): {gridObjects.Length}");
        foreach (var obj in gridObjects)
        {
            Debug.Log($"   - {obj.name} (Activo: {obj.activeInHierarchy})");
        }
        
        // Verificar colliders
        Collider[] allColliders = FindObjectsByType<Collider>(FindObjectsSortMode.None);
        int legoColliders = allColliders.Count(c => c.gameObject.layer == 7);
        int gridColliders = allColliders.Count(c => c.gameObject.layer == 8);
        
        Debug.Log($"🔲 Colliders en Layer 7: {legoColliders}");
        Debug.Log($"🔲 Colliders en Layer 8: {gridColliders}");
    }
    
    /// <summary>
    /// Debug del estado completo del sistema de raycast
    /// </summary>
    [ContextMenu("Debug Sistema Completo")]
    public void DebugSistemaCompleto()
    {
        Debug.Log("=== DEBUG SISTEMA COMPLETO ===");
        
        // Debug XRRayInteractor
        if (rayInteractor != null)
        {
            Debug.Log("✅ XRRayInteractor: DISPONIBLE");
            Debug.Log($"   - Nombre: {rayInteractor.name}");
            Debug.Log($"   - Habilitado: {rayInteractor.enabled}");
            Debug.Log($"   - Max Distance: {rayInteractor.maxRaycastDistance}m");
            Debug.Log($"   - Raycast Mask: {rayInteractor.raycastMask.value}");
        }
        else
        {
            Debug.Log("❌ XRRayInteractor: NO DISPONIBLE");
        }
        
        // Debug sistema manual
        if (useManualRaycast)
        {
            Debug.Log("✅ Sistema Manual: ACTIVO");
            if (manualRayOrigin != null)
            {
                Debug.Log($"   - Origen: {manualRayOrigin.name}");
                Debug.Log($"   - Posición: {manualRayOrigin.position}");
                Debug.Log($"   - Forward: {manualRayOrigin.forward}");
            }
            else
            {
                Debug.Log("❌ Origen manual: NULL");
            }
        }
        else
        {
            Debug.Log("❌ Sistema Manual: INACTIVO");
        }
        
        // Debug preview
        if (currentBrick != null)
        {
            Debug.Log("✅ Preview Brick: DISPONIBLE");
            Debug.Log($"   - Activo: {currentBrick.gameObject.activeInHierarchy}");
            Debug.Log($"   - Posición: {currentBrick.transform.position}");
            Debug.Log($"   - Válido: {isPositionValid}");
        }
        else
        {
            Debug.Log("❌ Preview Brick: NO DISPONIBLE");
        }
    }
    
    /// <summary>
    /// Debug de la configuración del XRRayInteractor
    /// </summary>
    [ContextMenu("Debug XRRayInteractor")]
    public void DebugXRRayInteractor()
    {
        Debug.Log("=== DEBUG XR RAY INTERACTOR ===");
        
        if (rayInteractor == null)
        {
            Debug.LogError("❌ XRRayInteractor es NULL");
            return;
        }
        
        Debug.Log($"✅ XRRayInteractor encontrado: {rayInteractor.name}");
        Debug.Log($"   - Enabled: {rayInteractor.enabled}");
        Debug.Log($"   - Max Distance: {rayInteractor.maxRaycastDistance}m");
        Debug.Log($"   - Raycast Mask: {rayInteractor.raycastMask.value} (0x{rayInteractor.raycastMask.value:X8})");
        Debug.Log($"   - Ray Origin: {(rayInteractor.rayOriginTransform != null ? rayInteractor.rayOriginTransform.name : "NULL")}");
        
        if (rayInteractor.rayOriginTransform != null)
        {
            Debug.Log($"   - Ray Origin Position: {rayInteractor.rayOriginTransform.position}");
            Debug.Log($"   - Ray Origin Forward: {rayInteractor.rayOriginTransform.forward}");
        }
        
        // Probar si detecta hits
        if (rayInteractor.TryGetCurrent3DRaycastHit(out RaycastHit hit))
        {
            Debug.Log($"✅ XRRayInteractor HIT detectado:");
            Debug.Log($"   - Point: {hit.point}");
            Debug.Log($"   - Distance: {hit.distance:F2}m");
            Debug.Log($"   - Object: {hit.collider.name}");
            Debug.Log($"   - Layer: {hit.collider.gameObject.layer}");
        }
        else
        {
            Debug.Log("❌ XRRayInteractor NO detecta hits");
        }
    }
    
    /// <summary>
    /// Debug del estado del bloque preview
    /// </summary>
    [ContextMenu("Debug Preview Brick")]
    public void DebugPreviewBrick()
    {
        Debug.Log("=== DEBUG PREVIEW BRICK ===");
        Debug.Log($"CurrentBrick: {(currentBrick != null ? "OK" : "NULL")}");
        Debug.Log($"IsBuilding: {IsBuilding}");
        Debug.Log($"IsPositionValid: {isPositionValid}");
        Debug.Log($"BrickPrefabs Length: {brickPrefabs.Length}");
        Debug.Log($"CurrentBrickIndex: {currentBrickIndex}");
        
        if (currentBrick != null)
        {
            Debug.Log($"Brick GameObject Active: {currentBrick.gameObject.activeInHierarchy}");
            Debug.Log($"Brick Position: {currentBrick.transform.position}");
            Debug.Log($"Brick IsTransparent: {currentBrick.IsTransparent}");
            Debug.Log($"Brick IsPlaced: {currentBrick.IsPlaced}");
            
            Renderer[] renderers = currentBrick.GetComponentsInChildren<Renderer>();
            Debug.Log($"Brick Renderers: {renderers.Length}");
            foreach (Renderer renderer in renderers)
            {
                if (renderer != null)
                {
                    Debug.Log($"  - Renderer: {renderer.name}, Enabled: {renderer.enabled}, Material: {(renderer.material != null ? "OK" : "NULL")}");
                }
            }
        }
    }

    /// <summary>
    /// Debug de la línea de raycast
    /// </summary>
    [ContextMenu("Debug Raycast Line")]
    public void DebugRaycastLine()
    {
        if (rayInteractor != null)
        {
            Debug.Log("=== XR RAY INTERACTOR DEBUG ===");
            Debug.Log($"Ray Interactor: {rayInteractor.name}");
            Debug.Log($"Max Distance: {rayInteractor.maxRaycastDistance}m");
            Debug.Log($"Raycast Mask: {rayInteractor.raycastMask}");
            Debug.Log($"Enabled: {rayInteractor.enabled}");
            
            if (rayInteractor.TryGetCurrent3DRaycastHit(out RaycastHit hit))
            {
                Debug.Log($"Hit Point: {hit.point}");
                Debug.Log($"Hit Distance: {hit.distance:F2}m");
                Debug.Log($"Hit Object: {hit.collider.name}");
            }
            else
            {
                Debug.Log("No hit detected");
            }
        }
        else
        {
            Debug.Log("❌ XRRayInteractor no está asignado");
        }
    }

    // Método DebugSistemaCompleto duplicado eliminado - usando el método actualizado

    /// <summary>
    /// Forzar actualización del raycast para verificar posición
    /// </summary>
    [ContextMenu("Forzar Actualización Raycast")]
    public void ForceRaycastUpdate()
    {
        if (rayInteractor != null)
        {
            // El XRRayInteractor se actualiza automáticamente
            Debug.Log("✅ XRRayInteractor está activo y se actualiza automáticamente");
            
            if (rayInteractor.TryGetCurrent3DRaycastHit(out RaycastHit hit))
            {
                Debug.Log($"Raycast hit detectado: {hit.point}");
                UpdateBrickPosition(hit);
            }
            else
            {
                Debug.Log("No hay hit de raycast");
            }
        }
        else
        {
            Debug.Log("❌ XRRayInteractor no está asignado");
        }
    }
    
    /// <summary>
    /// Forzar la visibilidad del bloque preview
    /// </summary>
    [ContextMenu("Forzar Visibilidad Preview")]
    public void ForcePreviewVisibility()
    {
        if (currentBrick != null)
        {
            currentBrick.gameObject.SetActive(true);
            currentBrick.transform.position = Vector3.zero; // Posición central
            isPositionValid = true;
            Debug.Log("Bloque preview forzado a ser visible en posición central");
        }
        else
        {
            CreatePreviewBrick();
            Debug.Log("Bloque preview creado y forzado a ser visible");
        }
    }
    
    /// <summary>
    /// Probar colocación manualmente
    /// </summary>
    [ContextMenu("Probar Colocación")]
    public void TestPlacement()
    {
        Debug.Log("🧪 PROBANDO COLOCACIÓN MANUAL");
        if (isPreviewMode)
        {
            PlacePreviewBrick();
        }
        else
        {
            ConfirmBrickPlacement();
        }
    }
    
    /// <summary>
    /// Verificar estado del Input System
    /// </summary>
    [ContextMenu("Verificar Input System")]
    public void CheckInputSystem()
    {
        Debug.Log("=== VERIFICACIÓN INPUT SYSTEM ===");
        Debug.Log($"InputActions: {(inputActions != null ? "OK" : "NULL")}");
        
        if (inputActions != null)
        {
            Debug.Log($"Attack Enabled: {inputActions.Player.Attack.enabled}");
            Debug.Log($"Jump Enabled: {inputActions.Player.Jump.enabled}");
            Debug.Log($"Crouch Enabled: {inputActions.Player.Crouch.enabled}");
            Debug.Log($"Next Enabled: {inputActions.Player.Next.enabled}");
            
            // Verificar estado actual de los inputs
            Debug.Log($"Attack Pressed: {inputActions.Player.Attack.WasPressedThisFrame()}");
            Debug.Log($"Attack Held: {inputActions.Player.Attack.IsPressed()}");
            Debug.Log($"Jump Pressed: {inputActions.Player.Jump.WasPressedThisFrame()}");
        }
    }
    
    /// <summary>
    /// Probar detección de input en tiempo real
    /// </summary>
    [ContextMenu("Probar Input en Tiempo Real")]
    public void TestInputDetection()
    {
        StartCoroutine(TestInputCoroutine());
    }
    
    /// <summary>
    /// Verificar configuración VR
    /// </summary>
    [ContextMenu("Verificar Configuración VR")]
    public void CheckVRConfiguration()
    {
        Debug.Log("=== VERIFICACIÓN CONFIGURACIÓN VR ===");
        
        // Verificar si XR está habilitado
        bool xrEnabled = UnityEngine.XR.XRSettings.enabled;
        Debug.Log($"XR Habilitado: {xrEnabled}");
        
        if (xrEnabled)
        {
            Debug.Log($"Proveedor XR: {UnityEngine.XR.XRSettings.loadedDeviceName}");
            Debug.Log($"Modo Stereo: {UnityEngine.XR.XRSettings.stereoRenderingMode}");
        }
        else
        {
            Debug.LogWarning("⚠️ XR no está habilitado. Para usar VR:");
            Debug.Log("1. Ve a Edit > Project Settings > XR Plug-in Management");
            Debug.Log("2. Marca 'Initialize XR on Startup'");
            Debug.Log("3. Configura Oculus/OpenXR");
        }
        
        // Verificar dispositivos VR
        List<UnityEngine.XR.InputDevice> allDevices = new List<UnityEngine.XR.InputDevice>();
        UnityEngine.XR.InputDevices.GetDevices(allDevices);
        
        Debug.Log($"Dispositivos VR detectados: {allDevices.Count}");
        foreach (var device in allDevices)
        {
            Debug.Log($"  - {device.name} ({device.characteristics})");
        }
        
        // Verificar controladores específicos
        List<UnityEngine.XR.InputDevice> leftHand = new List<UnityEngine.XR.InputDevice>();
        List<UnityEngine.XR.InputDevice> rightHand = new List<UnityEngine.XR.InputDevice>();
        
        UnityEngine.XR.InputDevices.GetDevicesAtXRNode(XRNode.LeftHand, leftHand);
        UnityEngine.XR.InputDevices.GetDevicesAtXRNode(XRNode.RightHand, rightHand);
        
        Debug.Log($"Controlador izquierdo: {leftHand.Count}");
        Debug.Log($"Controlador derecho: {rightHand.Count}");
        
        if (leftHand.Count > 0)
        {
            Debug.Log($"  - Izquierdo: {leftHand[0].name}");
        }
        if (rightHand.Count > 0)
        {
            Debug.Log($"  - Derecho: {rightHand[0].name}");
        }
        
    }
    
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
        Debug.Log("PlaceBrickVR: OnDestroy - Limpiando recursos");
        
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