using UnityEngine;

public static class GridSystem
{
    private static Vector3 _gridSize;
    private static int _layerMaskLego;
    
    public static Vector3 GridSize => _gridSize;
    public static int LayerMaskLego => _layerMaskLego;
    
    /// <summary>
    /// Inicializa el sistema de cuadrícula basado en las dimensiones del prefab
    /// </summary>
    /// <param name="prefabBrick">El prefab del bloque que define las dimensiones</param>
    public static void InitializeGrid(GameObject prefabBrick)
    {
        if (prefabBrick == null)
        {
            Debug.LogError("GridSystem: PrefabBrick es null");
            return;
        }
        
        // Obtener el collider del prefab para calcular las dimensiones
        BoxCollider prefabCollider = prefabBrick.GetComponent<BoxCollider>();
        
        if (prefabCollider == null)
        {
            Debug.LogError("GridSystem: No se encontró BoxCollider en el prefab");
            return;
        }
        
        // Usar escala fija de 10x1x10 para la grid
        _gridSize = new Vector3(1.0f, 0.1f, 1.0f);
        
        Debug.Log($"GridSystem: Usando escala fija 10x1x10 (1.0x0.1x1.0) en lugar de tamaño del prefab");
        
        // Crear la máscara de capa para LEGO
        _layerMaskLego = LayerMask.GetMask("Lego");
        
        Debug.Log($"GridSystem inicializado con tamaño: {_gridSize}");
    }
    
    /// <summary>
    /// Convierte una posición 3D a la cuadrícula más cercana
    /// </summary>
    /// <param name="worldPosition">Posición en el mundo</param>
    /// <returns>Posición ajustada a la cuadrícula</returns>
    public static Vector3 SnapToGrid(Vector3 worldPosition)
    {
        if (_gridSize == Vector3.zero)
        {
            Debug.LogWarning("GridSystem: Grid no inicializado, usando valores por defecto");
            return new Vector3(
                Mathf.Round(worldPosition.x),
                Mathf.Round(worldPosition.y),
                Mathf.Round(worldPosition.z)
            );
        }
        
        // Calcular posición X y Z en el centro de los cuadros de la grid
        float snappedX = Mathf.Round(worldPosition.x / _gridSize.x) * _gridSize.x + _gridSize.x * 0.5f;
        float snappedZ = Mathf.Round(worldPosition.z / _gridSize.z) * _gridSize.z + _gridSize.z * 0.5f;
        
        // Para la altura, buscar bloques existentes en esa posición
        float correctHeight = FindCorrectHeight(snappedX, snappedZ, _gridSize);
        
        return new Vector3(snappedX, correctHeight, snappedZ);
    }
    
    /// <summary>
    /// Snap solo a la grid (suelo) sin considerar bloques existentes
    /// </summary>
    /// <param name="worldPosition">Posición en el mundo</param>
    /// <returns>Posición ajustada solo a la grid</returns>
    public static Vector3 SnapToGridOnly(Vector3 worldPosition)
    {
        if (_gridSize == Vector3.zero)
        {
            Debug.LogWarning("GridSystem: Grid no inicializado, usando valores por defecto");
            return new Vector3(
                Mathf.Round(worldPosition.x),
                Mathf.Round(worldPosition.y),
                Mathf.Round(worldPosition.z)
            );
        }
        
        // Snap a la grid en X y Z, luego calcular altura correcta
        float snappedX = Mathf.Round(worldPosition.x / _gridSize.x) * _gridSize.x + _gridSize.x * 0.5f;
        float snappedZ = Mathf.Round(worldPosition.z / _gridSize.z) * _gridSize.z + _gridSize.z * 0.5f;
        
        // Calcular altura correcta (suelo o encima de bloques existentes)
        float correctHeight = FindCorrectHeight(snappedX, snappedZ, new Vector3(1f, 1f, 1f)); // Tamaño por defecto
        
        Vector3 snappedPosition = new Vector3(snappedX, correctHeight, snappedZ);
        
        // Debug logs removidos para evitar spam
        
        return snappedPosition;
    }
    
    /// <summary>
    /// Verifica si una posición está libre en la cuadrícula
    /// </summary>
    /// <param name="position">Posición a verificar</param>
    /// <param name="brickSize">Tamaño del bloque a colocar</param>
    /// <param name="rotation">Rotación del bloque</param>
    /// <returns>True si la posición está libre</returns>
    public static bool IsPositionFree(Vector3 position, Vector3 brickSize, Quaternion rotation)
    {
        if (_layerMaskLego == 0)
        {
            Debug.LogWarning("GridSystem: LayerMask no inicializado");
            return false;
        }
        
        // Verificar colisiones en la posición
        Collider[] colliders = Physics.OverlapBox(
            position,
            brickSize / 2f,
            rotation,
            _layerMaskLego
        );
        
        return colliders.Length == 0;
    }
    
    /// <summary>
    /// Busca la siguiente posición libre hacia arriba
    /// </summary>
    /// <param name="startPosition">Posición inicial</param>
    /// <param name="brickSize">Tamaño del bloque</param>
    /// <param name="rotation">Rotación del bloque</param>
    /// <param name="maxHeight">Altura máxima de búsqueda</param>
    /// <returns>Posición libre encontrada o Vector3.zero si no hay espacio</returns>
    public static Vector3 FindNextFreePositionUp(Vector3 startPosition, Vector3 brickSize, Quaternion rotation, int maxHeight = 10)
    {
        Vector3 currentPosition = startPosition;
        
        for (int i = 0; i < maxHeight; i++)
        {
            if (IsPositionFree(currentPosition, brickSize, rotation))
            {
                return currentPosition;
            }
            
            currentPosition.y += _gridSize.y;
        }
        
        return Vector3.zero; // No se encontró posición libre
    }
    
    /// <summary>
    /// Encuentra la altura correcta para colocar un bloque encima de otros
    /// </summary>
    /// <param name="x">Posición X en la grid</param>
    /// <param name="z">Posición Z en la grid</param>
    /// <param name="brickSize">Tamaño del bloque a colocar</param>
    /// <returns>Altura correcta para el bloque</returns>
    public static float FindCorrectHeight(float x, float z, Vector3 brickSize)
    {
        if (_layerMaskLego == 0)
        {
            return 0f; // Si no hay layer mask, colocar en el suelo
        }
        
        // Buscar el bloque más alto en esta posición X,Z
        float highestY = 0f;
        int blockCount = 0;
        
        // Crear un raycast hacia abajo desde una altura alta para encontrar bloques
        Vector3 rayStart = new Vector3(x, 100f, z);
        RaycastHit[] hits = Physics.RaycastAll(rayStart, Vector3.down, 200f, _layerMaskLego);
        
        // Debug log removido para evitar spam
        
        foreach (RaycastHit hit in hits)
        {
            // Verificar si el hit es de un bloque LEGO
            if (hit.collider.gameObject.layer == LayerMask.NameToLayer("Lego"))
            {
                blockCount++;
                // Calcular la parte superior del bloque
                float blockTop = hit.collider.bounds.max.y;
                if (blockTop > highestY)
                {
                    highestY = blockTop;
                    // Debug log removido para evitar spam
                }
            }
        }
        
        // Si no hay bloques debajo, colocar en el suelo
        if (blockCount == 0)
        {
            // Debug log removido para evitar spam
            return _gridSize.y * 0.5f; // Centro del primer cuadro
        }
        
        // Colocar directamente encima del bloque más alto con un pequeño espacio
        float smallGap = 0.01f; // Pequeño espacio entre bloques
        float finalHeight = highestY + (brickSize.y * 0.5f) + smallGap;
        
        // Debug logs removidos para evitar spam
        
        return finalHeight;
    }
    
    /// <summary>
    /// Obtiene las dimensiones de la cuadrícula
    /// </summary>
    /// <returns>Vector3 con las dimensiones X, Y, Z de la cuadrícula</returns>
    public static Vector3 GetGridDimensions()
    {
        return _gridSize;
    }
}
