using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

public class RaycastSystem : MonoBehaviour
{
    [Header("Configuración del Raycast")]
    [SerializeField] private XRNode controllerNode = XRNode.RightHand;
    [SerializeField] private float raycastDistance = 5f;
    [SerializeField] private LayerMask raycastLayers = ~0;
    
    [Header("XRRayInteractor Integration")]
    [SerializeField] private XRRayInteractor rayInteractor;
    
    [Header("Visualización")]
    [SerializeField] private bool showRaycastLine = true;
    [SerializeField] private float lineThickness = 0.1f;
    [SerializeField] private Color noHitColor = Color.cyan;
    [SerializeField] private Color hitColor = Color.green;
    [SerializeField] private bool enableBlinking = true;
    
    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = false; // Deshabilitado por defecto para evitar spam
    [SerializeField] private float logInterval = 2f; // Reducido el intervalo
    
    // Componentes del sistema
    private GameObject raycastLineObject;
    private Material raycastMaterial;
    private LineRenderer lineRenderer;
    private bool isInitialized = false;
    
    // Estado del raycast
    public Vector3 ControllerPosition { get; private set; }
    public Quaternion ControllerRotation { get; private set; }
    public Vector3 RaycastOrigin { get; private set; }
    public Vector3 RaycastDirection { get; private set; }
    public Vector3 RaycastEndPoint { get; private set; }
    public bool HasHit { get; private set; }
    public RaycastHit LastHit { get; private set; }
    
    // Eventos
    public System.Action<RaycastHit> OnRaycastHit;
    public System.Action OnRaycastMiss;
    
    private void Awake()
    {
        SetupRaycastVisualization();
        isInitialized = true;
    }
    
    private void Update()
    {
        if (!isInitialized) return;
        
        UpdateRaycast();
    }
    
    private void UpdateRaycast()
    {
        // Si tenemos XRRayInteractor, usarlo como fuente principal
        if (rayInteractor != null)
        {
            UpdateRaycastWithXRRayInteractor();
        }
        else
        {
            // Fallback al método original
            UpdateRaycastWithController();
        }
    }
    
    private void UpdateRaycastWithXRRayInteractor()
    {
        // Obtener posición y rotación del XRRayInteractor
        if (rayInteractor.rayOriginTransform != null)
        {
            ControllerPosition = rayInteractor.rayOriginTransform.position;
            ControllerRotation = rayInteractor.rayOriginTransform.rotation;
            RaycastOrigin = ControllerPosition;
            RaycastDirection = rayInteractor.rayOriginTransform.forward;
        }
        else
        {
            Debug.LogWarning("❌ RaycastSystem: XRRayInteractor no tiene rayOriginTransform");
            return;
        }
        
        // Usar el hit del XRRayInteractor si está disponible
        if (rayInteractor.TryGetCurrent3DRaycastHit(out RaycastHit hit))
        {
            HasHit = true;
            LastHit = hit;
            RaycastEndPoint = hit.point;
            
            // Log de hit cada intervalo
            if (enableDebugLogs && Time.time % logInterval < 0.1f)
            {
                Debug.Log($"🎯 RaycastSystem (XRRayInteractor) HIT: Punto: {hit.point}, Distancia: {hit.distance:F2}m, Objeto: {hit.collider.name}");
            }
            
            // Actualizar visualización
            UpdateRaycastVisualization(true, hit.point);
            
            // Disparar evento
            OnRaycastHit?.Invoke(hit);
        }
        else
        {
            HasHit = false;
            RaycastEndPoint = RaycastOrigin + RaycastDirection * raycastDistance;
            
            // Log de miss cada intervalo
            if (enableDebugLogs && Time.time % logInterval < 0.1f)
            {
                Debug.Log($"🎯 RaycastSystem (XRRayInteractor) MISS: Origen: {RaycastOrigin}, Dirección: {RaycastDirection}, Distancia: {raycastDistance}m");
            }
            
            // Actualizar visualización
            UpdateRaycastVisualization(false, Vector3.zero);
            
            // Disparar evento
            OnRaycastMiss?.Invoke();
        }
    }
    
    private void UpdateRaycastWithController()
    {
        // Obtener posición del controlador
        Vector3 tempPosition;
        Quaternion tempRotation;
        if (!GetControllerPose(out tempPosition, out tempRotation))
        {
            if (enableDebugLogs && Time.time % logInterval < 0.1f)
            {
                Debug.Log("❌ RaycastSystem: No se pudo obtener posición del controlador VR");
            }
            return;
        }
        
        // Asignar valores a las propiedades
        ControllerPosition = tempPosition;
        ControllerRotation = tempRotation;
        
        // Calcular dirección del raycast
        RaycastDirection = GetControllerAimDirection(ControllerPosition, ControllerRotation);
        RaycastOrigin = ControllerPosition;
        
        // Realizar raycast
        Ray ray = new Ray(RaycastOrigin, RaycastDirection);
        RaycastHit hit;
        
        if (Physics.Raycast(ray, out hit, raycastDistance, raycastLayers))
        {
            HasHit = true;
            LastHit = hit;
            RaycastEndPoint = hit.point;
            
            // Log de hit cada intervalo
            if (enableDebugLogs && Time.time % logInterval < 0.1f)
            {
                Debug.Log($"🎯 RaycastSystem (Controller) HIT: Punto: {hit.point}, Distancia: {hit.distance:F2}m, Objeto: {hit.collider.name}");
            }
            
            // Actualizar visualización
            UpdateRaycastVisualization(true, hit.point);
            
            // Disparar evento
            OnRaycastHit?.Invoke(hit);
        }
        else
        {
            HasHit = false;
            RaycastEndPoint = RaycastOrigin + RaycastDirection * raycastDistance;
            
            // Log de miss cada intervalo
            if (enableDebugLogs && Time.time % logInterval < 0.1f)
            {
                Debug.Log($"🎯 RaycastSystem (Controller) MISS: Origen: {RaycastOrigin}, Dirección: {RaycastDirection}, Distancia: {raycastDistance}m");
            }
            
            // Actualizar visualización
            UpdateRaycastVisualization(false, Vector3.zero);
            
            // Disparar evento
            OnRaycastMiss?.Invoke();
        }
    }
    
    private void SetupRaycastVisualization()
    {
        if (!showRaycastLine) return;
        
        // Crear objeto para la línea
        raycastLineObject = new GameObject("RaycastLine");
        raycastLineObject.transform.SetParent(transform);
        
        // Configurar LineRenderer
        lineRenderer = raycastLineObject.AddComponent<LineRenderer>();
        lineRenderer.material = CreateRaycastMaterial();
        lineRenderer.startColor = noHitColor;
        lineRenderer.endColor = noHitColor;
        lineRenderer.startWidth = lineThickness;
        lineRenderer.endWidth = lineThickness;
        lineRenderer.positionCount = 2;
        lineRenderer.useWorldSpace = true;
        lineRenderer.enabled = false; // Inicialmente deshabilitado
        
        // Configurar shader para mejor visibilidad
        lineRenderer.material.shader = Shader.Find("Sprites/Default");
        
        Debug.Log("🎯 RaycastSystem: Visualización configurada");
    }
    
    private Material CreateRaycastMaterial()
    {
        Material mat = new Material(Shader.Find("Sprites/Default"));
        mat.color = noHitColor;
        mat.SetFloat("_Mode", 3); // Transparente
        mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        mat.SetInt("_ZWrite", 0);
        mat.DisableKeyword("_ALPHATEST_ON");
        mat.EnableKeyword("_ALPHABLEND_ON");
        mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        mat.renderQueue = 3000;
        
        raycastMaterial = mat;
        return mat;
    }
    
    private void UpdateRaycastVisualization(bool hasHit, Vector3 hitPoint)
    {
        if (!showRaycastLine || lineRenderer == null) return;
        
        // Activar la línea
        lineRenderer.enabled = true;
        
        // Configurar puntos de la línea
        Vector3 startPoint = RaycastOrigin;
        Vector3 endPoint = hasHit ? hitPoint : RaycastEndPoint;
        
        lineRenderer.SetPosition(0, startPoint);
        lineRenderer.SetPosition(1, endPoint);
        
        // Configurar color
        if (hasHit)
        {
            lineRenderer.startColor = hitColor;
            lineRenderer.endColor = hitColor;
        }
        else
        {
            if (enableBlinking)
            {
                // Efecto de parpadeo
                float blink = Mathf.Sin(Time.time * 8f) * 0.3f + 0.7f;
                Color blinkingColor = new Color(noHitColor.r, noHitColor.g, noHitColor.b, blink);
                lineRenderer.startColor = blinkingColor;
                lineRenderer.endColor = blinkingColor;
            }
            else
            {
                lineRenderer.startColor = noHitColor;
                lineRenderer.endColor = noHitColor;
            }
        }
        
        // Actualizar material
        if (raycastMaterial != null)
        {
            raycastMaterial.color = lineRenderer.startColor;
        }
    }
    
    private bool GetControllerPose(out Vector3 position, out Quaternion rotation)
    {
        var devices = new System.Collections.Generic.List<InputDevice>();
        InputDevices.GetDevicesAtXRNode(controllerNode, devices);
        
        if (devices.Count > 0)
        {
            var device = devices[0];
            if (device.TryGetFeatureValue(CommonUsages.devicePosition, out position) &&
                device.TryGetFeatureValue(CommonUsages.deviceRotation, out rotation))
            {
                return true;
            }
        }
        
        // Fallback a la cámara si no hay controlador
        if (Camera.main != null)
        {
            position = Camera.main.transform.position;
            rotation = Camera.main.transform.rotation;
            return true;
        }
        
        position = Vector3.zero;
        rotation = Quaternion.identity;
        return false;
    }
    
    private Vector3 GetControllerAimDirection(Vector3 position, Quaternion rotation)
    {
        // Dirección hacia adelante con ligero ajuste hacia abajo para apuntar naturalmente
        Vector3 forward = rotation * Vector3.forward;
        Vector3 down = rotation * Vector3.down;
        
        // Combinar hacia adelante con un poco hacia abajo para apuntar más naturalmente
        Vector3 aimDirection = (forward + down * 0.1f).normalized;
        
        return aimDirection;
    }
    
    // Métodos públicos para configuración
    public void SetRaycastDistance(float distance)
    {
        raycastDistance = distance;
    }
    
    public void SetControllerNode(XRNode node)
    {
        controllerNode = node;
    }
    
    public void SetRaycastLayers(LayerMask layers)
    {
        raycastLayers = layers;
    }
    
    public void SetVisualizationEnabled(bool enabled)
    {
        showRaycastLine = enabled;
        if (lineRenderer != null)
        {
            lineRenderer.enabled = enabled;
        }
    }
    
    public void SetLineThickness(float thickness)
    {
        lineThickness = thickness;
        if (lineRenderer != null)
        {
            lineRenderer.startWidth = thickness;
            lineRenderer.endWidth = thickness;
        }
    }
    
    // Métodos para XRRayInteractor
    public void SetXRRayInteractor(XRRayInteractor interactor)
    {
        rayInteractor = interactor;
        if (rayInteractor != null)
        {
            Debug.Log("✅ RaycastSystem: XRRayInteractor asignado");
        }
    }
    
    public XRRayInteractor GetXRRayInteractor()
    {
        return rayInteractor;
    }
    
    public void AutoFindXRRayInteractor()
    {
        if (rayInteractor == null)
        {
            // Buscar en el XR Origin
            XRBaseController rightController = FindFirstObjectByType<XRBaseController>();
            if (rightController != null)
            {
                rayInteractor = rightController.GetComponent<XRRayInteractor>();
            }
            
            // Si no se encuentra, buscar en la escena
            if (rayInteractor == null)
            {
                rayInteractor = FindFirstObjectByType<XRRayInteractor>();
            }
            
            if (rayInteractor != null)
            {
                Debug.Log("✅ RaycastSystem: XRRayInteractor encontrado automáticamente");
            }
            else
            {
                Debug.LogWarning("❌ RaycastSystem: No se encontró XRRayInteractor");
            }
        }
    }
    
    // Métodos de debug
    [ContextMenu("Debug Raycast Info")]
    public void DebugRaycastInfo()
    {
        Debug.Log("=== RAYCAST SYSTEM DEBUG ===");
        Debug.Log($"Controller Node: {controllerNode}");
        Debug.Log($"Raycast Distance: {raycastDistance}m");
        Debug.Log($"Show Line: {showRaycastLine}");
        Debug.Log($"Line Thickness: {lineThickness}");
        Debug.Log($"Controller Position: {ControllerPosition}");
        Debug.Log($"Controller Rotation: {ControllerRotation.eulerAngles}");
        Debug.Log($"Raycast Origin: {RaycastOrigin}");
        Debug.Log($"Raycast Direction: {RaycastDirection}");
        Debug.Log($"Raycast End Point: {RaycastEndPoint}");
        Debug.Log($"Has Hit: {HasHit}");
        if (HasHit)
        {
            Debug.Log($"Hit Point: {LastHit.point}");
            Debug.Log($"Hit Distance: {LastHit.distance:F2}m");
            Debug.Log($"Hit Object: {LastHit.collider.name}");
        }
    }
    
    [ContextMenu("Force Raycast Update")]
    public void ForceRaycastUpdate()
    {
        Debug.Log("🔧 Forzando actualización del raycast...");
        UpdateRaycast();
    }
    
    private void OnDestroy()
    {
        if (raycastMaterial != null)
        {
            DestroyImmediate(raycastMaterial);
        }
    }
}
