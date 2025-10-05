using UnityEngine;

/// <summary>
/// Manager principal del sistema LEGO VR
/// Coloca este script en un GameObject vacío en tu escena
/// </summary>
public class LegoVRManager : MonoBehaviour
{
    [Header("Sistema LEGO VR")]
    [SerializeField] private PlaceBrickVR placeBrickSystem;
    [SerializeField] private GameObject[] brickPrefabs;
    [SerializeField] private Material[] brickMaterials;
    [SerializeField] private Material transparentMaterial;
    
    [Header("Configuración")]
    [SerializeField] private bool autoSetup = true;
    
    private void Awake()
    {
        if (autoSetup)
        {
            SetupSystem();
        }
    }
    
    [ContextMenu("Configurar Sistema")]
    public void SetupSystem()
    {
        Debug.Log("=== CONFIGURANDO SISTEMA LEGO VR ===");
        
        // Crear o encontrar PlaceBrickVR
        if (placeBrickSystem == null)
        {
            placeBrickSystem = FindObjectOfType<PlaceBrickVR>();
            if (placeBrickSystem == null)
            {
                GameObject placeBrickObject = new GameObject("PlaceBrickVR");
                placeBrickObject.transform.SetParent(transform);
                placeBrickSystem = placeBrickObject.AddComponent<PlaceBrickVR>();
            }
        }
        
        // Configurar PlaceBrickVR
        ConfigurePlaceBrickSystem();
        
        Debug.Log("Sistema configurado correctamente!");
    }
    
    private void ConfigurePlaceBrickSystem()
    {
        // Usar reflexión para configurar campos privados
        var placeBrickType = typeof(PlaceBrickVR);
        
        // Configurar prefabs
        if (brickPrefabs.Length > 0)
        {
            var prefabsField = placeBrickType.GetField("brickPrefabs", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            prefabsField?.SetValue(placeBrickSystem, brickPrefabs);
        }
        
        // Configurar materiales
        if (brickMaterials.Length > 0)
        {
            var materialsField = placeBrickType.GetField("brickMaterials", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            materialsField?.SetValue(placeBrickSystem, brickMaterials);
        }
        
        // Configurar material transparente
        if (transparentMaterial != null)
        {
            var transparentField = placeBrickType.GetField("transparentMaterial", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            transparentField?.SetValue(placeBrickSystem, transparentMaterial);
        }
        
        Debug.Log("PlaceBrickVR configurado con prefabs y materiales");
    }
    
    /// <summary>
    /// Método para crear prefabs de ejemplo
    /// </summary>
    [ContextMenu("Crear Prefabs de Ejemplo")]
    public void CreateExamplePrefabs()
    {
        Debug.Log("Creando prefabs de ejemplo...");
        
        // Crear prefab básico 2x2
        CreateBasicBrickPrefab("Lego2x2", new Vector3(0.4f, 0.2f, 0.4f));
        
        // Crear prefab básico 2x4
        CreateBasicBrickPrefab("Lego2x4", new Vector3(0.8f, 0.2f, 0.4f));
        
        Debug.Log("Prefabs de ejemplo creados en Assets/Prefabs/");
    }
    
    private void CreateBasicBrickPrefab(string name, Vector3 size)
    {
        // Crear GameObject
        GameObject brick = GameObject.CreatePrimitive(PrimitiveType.Cube);
        brick.name = name;
        
        // Configurar tamaño
        brick.transform.localScale = size;
        
        // Agregar componente BrickVR
        BrickVR brickVR = brick.AddComponent<BrickVR>();
        
        // Configurar material
        Renderer renderer = brick.GetComponent<Renderer>();
        if (brickMaterials.Length > 0)
        {
            renderer.material = brickMaterials[0];
        }
        
        // Configurar capa
        brick.layer = LayerMask.NameToLayer("Lego");
        
        Debug.Log($"Prefab {name} creado con tamaño {size}");
    }
}
