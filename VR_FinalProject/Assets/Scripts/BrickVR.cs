using UnityEngine;

[RequireComponent(typeof(BoxCollider))]
public class BrickVR : MonoBehaviour
{
    [Header("Configuración del Bloque")]
    [SerializeField] private string brickType = "Basic";
    [SerializeField] private int brickID = 0;
    [SerializeField] private bool isPlaced = false;
    
    [Header("Componentes")]
    private BoxCollider _collider;
    private Renderer[] _renderers;
    private Rigidbody _rigidbody;
    
    [Header("Estados")]
    [SerializeField] private bool isTransparent = false;
    [SerializeField] private bool isGrabbable = true;
    
    // Propiedades públicas
    public BoxCollider Collider => _collider;
    public bool IsPlaced => isPlaced;
    public bool IsTransparent => isTransparent;
    public bool IsGrabbable => isGrabbable;
    public string BrickType => brickType;
    public int BrickID => brickID;
    
    // Eventos
    public System.Action<BrickVR> OnBrickPlaced;
    public System.Action<BrickVR> OnBrickDestroyed;
    
    private void Awake()
    {
        InitializeComponents();
        SetupLayer();
    }
    
    private void InitializeComponents()
    {
        // Obtener o crear componentes necesarios
        _collider = GetComponent<BoxCollider>();
        _renderers = GetComponentsInChildren<Renderer>();
        
        // Remover LOD Group si existe (causa problemas de visibilidad)
        LODGroup lodGroup = GetComponent<LODGroup>();
        if (lodGroup != null)
        {
            DestroyImmediate(lodGroup);
            Debug.Log("LOD Group removido del bloque para mejorar visibilidad");
        }
        
        // Crear Rigidbody si no existe
        _rigidbody = GetComponent<Rigidbody>();
        if (_rigidbody == null)
        {
            _rigidbody = gameObject.AddComponent<Rigidbody>();
        }
        
        // Configurar Rigidbody
        _rigidbody.mass = 0.1f; // Masa ligera para LEGOs
        _rigidbody.linearDamping = 0.5f;
        _rigidbody.angularDamping = 0.5f;
        
        // Asegurar que el renderer esté habilitado
        if (_renderers != null)
        {
            foreach (Renderer renderer in _renderers)
            {
                if (renderer != null)
                {
                    renderer.enabled = true;
                }
            }
        }
    }
    
    private void SetupLayer()
    {
        // Asegurar que el bloque esté en la capa LEGO
        gameObject.layer = LayerMask.NameToLayer("Lego");
        
        // Aplicar la capa a todos los hijos
        SetLayerRecursively(transform, LayerMask.NameToLayer("Lego"));
    }
    
    private void SetLayerRecursively(Transform parent, int layer)
    {
        parent.gameObject.layer = layer;
        foreach (Transform child in parent)
        {
            SetLayerRecursively(child, layer);
        }
    }
    
    /// <summary>
    /// Establece el material del bloque
    /// </summary>
    /// <param name="material">Material a aplicar</param>
    public void SetMaterial(Material material)
    {
        if (_renderers == null) return;
        
        foreach (Renderer renderer in _renderers)
        {
            if (renderer != null)
            {
                renderer.material = material;
            }
        }
    }
    
    /// <summary>
    /// Establece la transparencia del bloque
    /// </summary>
    /// <param name="transparent">Si debe ser transparente</param>
    /// <param name="transparentMaterial">Material transparente a usar</param>
    public void SetTransparency(bool transparent, Material transparentMaterial = null)
    {
        isTransparent = transparent;
        
        // Cambiar el modo de renderizado para transparencia
        foreach (Renderer renderer in _renderers)
        {
            if (renderer != null)
            {
                if (transparent && transparentMaterial != null)
                {
                    // Usar el material transparente proporcionado
                    renderer.material = transparentMaterial;
                }
                else if (transparent)
                {
                    // Crear material transparente por defecto si no se proporciona uno
                    Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                    mat.SetFloat("_Surface", 1); // Transparent
                    mat.SetFloat("_Blend", 0); // Alpha
                    mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                    mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                    mat.SetInt("_ZWrite", 0);
                    mat.SetInt("_Cull", 0); // No culling
                    mat.DisableKeyword("_ALPHATEST_ON");
                    mat.EnableKeyword("_ALPHABLEND_ON");
                    mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                    mat.renderQueue = 3000;
                    
                    // Color más visible para preview
                    Color previewColor = Color.green; // Verde más visible
                    previewColor.a = 0.8f; // 80% de opacidad para mejor visibilidad
                    mat.color = previewColor;
                    
                    renderer.material = mat;
                }
                else
                {
                    // Restaurar material opaco
                    Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                    mat.SetFloat("_Surface", 0); // Opaque
                    mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
                    mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.Zero);
                    mat.SetInt("_ZWrite", 1);
                    mat.DisableKeyword("_ALPHATEST_ON");
                    mat.DisableKeyword("_ALPHABLEND_ON");
                    mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                    mat.renderQueue = -1;
                    
                    renderer.material = mat;
                }
            }
        }
    }
    
    /// <summary>
    /// Coloca el bloque definitivamente
    /// </summary>
    public void PlaceBrick()
    {
        isPlaced = true;
        isGrabbable = false;
        
        // Deshabilitar la física para bloques colocados
        if (_rigidbody != null)
        {
            _rigidbody.isKinematic = true;
        }
        
        // Habilitar el collider
        if (_collider != null)
        {
            _collider.enabled = true;
        }
        
        // Hacer el bloque opaco
        SetTransparency(false);
        
        OnBrickPlaced?.Invoke(this);
        
        Debug.Log($"Bloque {brickType} colocado en posición: {transform.position}");
    }
    
    /// <summary>
    /// Prepara el bloque para ser colocado (modo preview)
    /// </summary>
    /// <param name="transparentMaterial">Material transparente para preview</param>
    public void PrepareForPlacement(Material transparentMaterial)
    {
        isPlaced = false;
        isGrabbable = true;
        
        // Asegurar que el GameObject esté activo
        gameObject.SetActive(true);
        
        // Deshabilitar física temporalmente
        if (_rigidbody != null)
        {
            _rigidbody.isKinematic = true;
        }
        
        // Deshabilitar collider temporalmente
        if (_collider != null)
        {
            _collider.enabled = false;
        }
        
        // Asegurar que los renderers estén habilitados
        if (_renderers != null)
        {
            foreach (Renderer renderer in _renderers)
            {
                if (renderer != null)
                {
                    renderer.enabled = true;
                }
            }
        }
        
        // Hacer transparente
        SetTransparency(true, transparentMaterial);
        
        Debug.Log($"Bloque preparado para colocación - Activo: {gameObject.activeInHierarchy}, Renderers: {_renderers?.Length}");
    }
    
    /// <summary>
    /// Rota el bloque 90 grados en el eje Y
    /// </summary>
    public void RotateBrick()
    {
        transform.Rotate(0, 90, 0);
    }
    
    /// <summary>
    /// Obtiene las dimensiones del bloque
    /// </summary>
    /// <returns>Vector3 con las dimensiones del collider</returns>
    public Vector3 GetBrickSize()
    {
        return _collider != null ? _collider.size : Vector3.one;
    }
    
    /// <summary>
    /// Configura el tipo y ID del bloque
    /// </summary>
    /// <param name="type">Tipo de bloque</param>
    /// <param name="id">ID único del bloque</param>
    public void SetBrickInfo(string type, int id)
    {
        brickType = type;
        brickID = id;
        gameObject.name = $"Brick_{type}_{id}";
    }
    
    private void OnDestroy()
    {
        OnBrickDestroyed?.Invoke(this);
    }
    
    /// <summary>
    /// Método para debugging - muestra información del bloque
    /// </summary>
    [ContextMenu("Debug Brick Info")]
    public void DebugBrickInfo()
    {
        Debug.Log($"Brick Info - Type: {brickType}, ID: {brickID}, Placed: {isPlaced}, " +
                  $"Position: {transform.position}, Size: {GetBrickSize()}");
    }
}
