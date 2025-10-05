using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Sistema dedicado para validar posiciones de bloques LEGO
/// Incluye validación de colisiones, límites, soporte y colores
/// </summary>
public class BrickValidationSystem : MonoBehaviour
{
    [Header("Configuración de Validación")]
    [SerializeField] private float minY = 0f;
    [SerializeField] private float maxY = 10f;
    [SerializeField] private float maxDistance = 20f;
    [SerializeField] private float supportRayDistance = 1f;
    [SerializeField] private float placementCooldown = 3f; // Tiempo en segundos entre colocaciones
    
    [Header("Materiales de Validación")]
    [SerializeField] private Material validMaterial; // Verde
    [SerializeField] private Material invalidMaterial; // Rojo
    [SerializeField] private Material confirmMaterial; // Cian
    
    [Header("Configuración de Colores")]
    [SerializeField] private Color validColor = new Color(0f, 1f, 0f, 0.8f); // Verde
    [SerializeField] private Color invalidColor = new Color(1f, 0f, 0f, 0.8f); // Rojo
    [SerializeField] private Color confirmColor = new Color(0f, 1f, 1f, 0.7f); // Cian
    
    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = true;
    
    // Singleton para acceso fácil
    public static BrickValidationSystem Instance { get; private set; }
    
    // Control de tiempo entre colocaciones
    private float lastPlacementTime = 0f;
    
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
    
    /// <summary>
    /// Valida si una posición es válida para colocar un bloque
    /// </summary>
    /// <param name="position">Posición a validar</param>
    /// <param name="brickSize">Tamaño del bloque</param>
    /// <param name="brickRotation">Rotación del bloque</param>
    /// <param name="currentBrick">Bloque actual (para excluir de colisiones)</param>
    /// <returns>True si la posición es válida</returns>
    public bool ValidatePosition(Vector3 position, Vector3 brickSize, Quaternion brickRotation, BrickVR currentBrick = null)
    {
        if (currentBrick == null)
        {
            LogDebug("❌ CurrentBrick es null en validación");
            return false;
        }
        
        // Verificar cooldown entre colocaciones
        bool cooldownValid = IsPlacementCooldownValid();
        
        // Verificar si la posición está ocupada
        bool isOccupied = IsPositionOccupied(position, brickSize, brickRotation, currentBrick);
        
        // Verificar si está dentro de límites razonables
        bool withinBounds = IsWithinBounds(position);
        
        // Verificar si hay soporte debajo (no puede flotar)
        bool hasSupport = HasSupportBelow(position, brickSize);
        
        bool isValid = cooldownValid && !isOccupied && withinBounds && hasSupport;
        
        LogDebug($"🔍 Validación: Cooldown={cooldownValid}, Ocupada={isOccupied}, Bounds={withinBounds}, Soporte={hasSupport} → Válida={isValid}");
        
        return isValid;
    }
    
    /// <summary>
    /// Verifica si el cooldown entre colocaciones es válido
    /// </summary>
    private bool IsPlacementCooldownValid()
    {
        float timeSinceLastPlacement = Time.time - lastPlacementTime;
        bool isValid = timeSinceLastPlacement >= placementCooldown;
        
        if (!isValid)
        {
            float remainingTime = placementCooldown - timeSinceLastPlacement;
            LogDebug($"⏰ Cooldown activo: {remainingTime:F1}s restantes");
        }
        
        return isValid;
    }
    
    /// <summary>
    /// Registra que se colocó un bloque (actualiza el cooldown)
    /// </summary>
    public void OnBrickPlaced()
    {
        lastPlacementTime = Time.time;
        LogDebug($"✅ Bloque colocado - Cooldown iniciado ({placementCooldown}s)");
    }
    
    /// <summary>
    /// Obtiene el tiempo restante del cooldown
    /// </summary>
    public float GetRemainingCooldownTime()
    {
        float timeSinceLastPlacement = Time.time - lastPlacementTime;
        return Mathf.Max(0f, placementCooldown - timeSinceLastPlacement);
    }
    
    /// <summary>
    /// Verifica si la posición está dentro de límites razonables
    /// </summary>
    private bool IsWithinBounds(Vector3 position)
    {
        bool withinY = position.y >= minY && position.y <= maxY;
        bool withinDistance = position.magnitude <= maxDistance;
        
        bool withinBounds = withinY && withinDistance;
        
        if (!withinBounds)
        {
            LogDebug($"🚫 Fuera de límites - Y: {position.y} (min: {minY}, max: {maxY}), Distancia: {position.magnitude:F2} (max: {maxDistance})");
        }
        
        return withinBounds;
    }
    
    /// <summary>
    /// Verifica si hay soporte debajo del bloque
    /// </summary>
    private bool HasSupportBelow(Vector3 position, Vector3 brickSize)
    {
        float brickHeight = brickSize.y * 0.5f;
        
        // Raycast hacia abajo desde la base del bloque
        Vector3 rayStart = position + Vector3.down * brickHeight;
        Ray ray = new Ray(rayStart, Vector3.down);
        
        bool hasSupport = false;
        
        if (Physics.Raycast(ray, out RaycastHit hit, supportRayDistance))
        {
            hasSupport = true;
            LogDebug($"✅ Soporte encontrado debajo: {hit.collider.name} a distancia {hit.distance:F2}");
        }
        else if (position.y <= 0.1f)
        {
            hasSupport = true;
            LogDebug("✅ Soporte en el suelo");
        }
        else
        {
            LogDebug($"❌ Sin soporte - Bloque flotando a altura {position.y:F2}");
        }
        
        return hasSupport;
    }
    
    /// <summary>
    /// Verifica si la posición está ocupada por otros bloques
    /// </summary>
    private bool IsPositionOccupied(Vector3 position, Vector3 brickSize, Quaternion brickRotation, BrickVR currentBrick)
    {
        // Buscar todos los bloques colocados en la escena
        BrickVR[] allBricks = FindObjectsByType<BrickVR>(FindObjectsSortMode.None);
        int checkedBricks = 0;
        int collidingBricks = 0;
        
        foreach (BrickVR brick in allBricks)
        {
            // Saltar el bloque actual (preview) y bloques no colocados
            if (brick == currentBrick || !brick.IsPlaced) 
            {
                continue;
            }
            
            checkedBricks++;
            
            // Verificar si hay colisión con este bloque
            if (IsBrickColliding(position, brickSize, brickRotation, brick.transform.position, brick.GetBrickSize(), brick.transform.rotation))
            {
                collidingBricks++;
                float distance = Vector3.Distance(position, brick.transform.position);
                LogDebug($"🚫 Colisión con {brick.name} en {brick.transform.position} (distancia: {distance:F2})");
            }
        }
        
        bool isOccupied = collidingBricks > 0;
        LogDebug($"🔍 Verificadas {checkedBricks} posiciones, {collidingBricks} colisiones → Ocupada: {isOccupied}");
        
        return isOccupied;
    }
    
    /// <summary>
    /// Verifica si dos bloques están colisionando
    /// </summary>
    private bool IsBrickColliding(Vector3 pos1, Vector3 size1, Quaternion rot1, Vector3 pos2, Vector3 size2, Quaternion rot2)
    {
        // Crear bounds para ambos bloques
        Bounds bounds1 = new Bounds(pos1, size1);
        Bounds bounds2 = new Bounds(pos2, size2);
        
        // Verificar si los bounds se intersectan
        bool isColliding = bounds1.Intersects(bounds2);
        
        // Debug adicional para colisiones
        if (isColliding)
        {
            float distance = Vector3.Distance(pos1, pos2);
            LogDebug($"🚫 Colisión detectada - Distancia: {distance:F2}, Bounds1: {bounds1}, Bounds2: {bounds2}");
        }
        
        return isColliding;
    }
    
    /// <summary>
    /// Aplica el color apropiado al preview según la validez
    /// </summary>
    public void ApplyPreviewColor(BrickVR brick, bool isValid)
    {
        if (brick == null) return;
        
        if (isValid)
        {
            // Verde para posición válida
            Material greenMaterial = CreateValidMaterial();
            brick.SetTransparency(true, greenMaterial);
            LogDebug("✅ Color verde aplicado (posición válida)");
        }
        else
        {
            // Rojo para posición inválida
            Material redMaterial = CreateInvalidMaterial();
            brick.SetTransparency(true, redMaterial);
            LogDebug("❌ Color rojo aplicado (posición inválida)");
        }
    }
    
    /// <summary>
    /// Aplica color de confirmación (cian) al preview colocado
    /// </summary>
    public void ApplyConfirmColor(BrickVR brick)
    {
        if (brick == null) return;
        
        Material cyanMaterial = CreateConfirmMaterial();
        brick.SetTransparency(true, cyanMaterial);
        LogDebug("🔵 Color cian aplicado (preview colocado)");
    }
    
    /// <summary>
    /// Crea un material válido (verde) dinámicamente
    /// </summary>
    public Material CreateValidMaterial()
    {
        if (validMaterial != null)
        {
            // Crear copia del material válido
            Material validMat = new Material(validMaterial);
            validMat.color = validColor;
            return validMat;
        }
        
        // Crear material básico
        Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        mat.color = validColor;
        return mat;
    }
    
    /// <summary>
    /// Crea un material inválido (rojo) dinámicamente
    /// </summary>
    public Material CreateInvalidMaterial()
    {
        if (invalidMaterial != null)
        {
            // Crear copia del material inválido
            Material invalidMat = new Material(invalidMaterial);
            invalidMat.color = invalidColor;
            return invalidMat;
        }
        
        // Crear material básico
        Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        mat.color = invalidColor;
        return mat;
    }
    
    /// <summary>
    /// Crea un material de confirmación (cian) dinámicamente
    /// </summary>
    public Material CreateConfirmMaterial()
    {
        if (confirmMaterial != null)
        {
            // Crear copia del material de confirmación
            Material confirmMat = new Material(confirmMaterial);
            confirmMat.color = confirmColor;
            return confirmMat;
        }
        
        // Crear material básico
        Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        mat.color = confirmColor;
        return mat;
    }
    
    /// <summary>
    /// Método de prueba para verificar colisiones
    /// </summary>
    [ContextMenu("Probar Detección de Colisiones")]
    public void TestCollisionDetection()
    {
        // Buscar un bloque en la escena para probar
        BrickVR testBrick = FindFirstObjectByType<BrickVR>();
        
        if (testBrick == null)
        {
            Debug.LogError("No hay bloques en la escena para probar");
            return;
        }
        
        Vector3 testPosition = testBrick.transform.position;
        Vector3 brickSize = testBrick.GetBrickSize();
        
        Debug.Log($"🧪 Probando detección de colisiones en posición: {testPosition}");
        Debug.Log($"🧪 Tamaño del bloque: {brickSize}");
        
        // Buscar todos los bloques colocados
        BrickVR[] allBricks = FindObjectsByType<BrickVR>(FindObjectsSortMode.None);
        Debug.Log($"🧪 Bloques encontrados: {allBricks.Length}");
        
        int placedBricks = 0;
        foreach (BrickVR brick in allBricks)
        {
            if (brick.IsPlaced)
            {
                placedBricks++;
                float distance = Vector3.Distance(testPosition, brick.transform.position);
                Debug.Log($"🧪 Bloque colocado {brick.name} - Distancia: {distance:F2}");
                
                if (distance < 2.0f)
                {
                    Debug.Log($"🧪 Bloque cercano detectado: {brick.name}");
                }
            }
        }
        
        Debug.Log($"🧪 Bloques colocados en la escena: {placedBricks}");
        
        // Probar la función de colisión
        bool isOccupied = IsPositionOccupied(testPosition, brickSize, testBrick.transform.rotation, testBrick);
        Debug.Log($"🧪 Resultado de IsPositionOccupied: {isOccupied}");
        
        // Probar validación completa
        bool isValid = ValidatePosition(testPosition, brickSize, testBrick.transform.rotation, testBrick);
        Debug.Log($"🧪 Resultado de ValidatePosition: {isValid}");
    }
    
    /// <summary>
    /// Configura los materiales de validación
    /// </summary>
    public void SetupMaterials(Material validMat, Material invalidMat, Material confirmMat)
    {
        validMaterial = validMat;
        invalidMaterial = invalidMat;
        confirmMaterial = confirmMat;
        
        LogDebug("✅ Materiales de validación configurados");
    }
    
    private void LogDebug(string message)
    {
        if (enableDebugLogs)
        {
            Debug.Log($"[BrickValidation] {message}");
        }
    }
}
