using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;

/// <summary>
/// Agrega visualización de grid al TeleportationArea existente
/// Coloca este script en el mismo GameObject que tiene TeleportationArea
/// </summary>
[RequireComponent(typeof(TeleportationArea))]
public class GridOnTeleportationArea : MonoBehaviour
{
    [Header("Configuración de Grid")]
    [SerializeField] private Color gridColor = Color.cyan;
    [SerializeField] private Color highlightColor = Color.yellow;
    [SerializeField] private float gridAlpha = 0.7f; // Más visible
    [SerializeField] private bool showGrid = true;
    
    [Header("Líneas de Grid")]
    [SerializeField] private bool showGridLines = true;
    [SerializeField] private Color gridLineColor = Color.white;
    [SerializeField] private float gridLineWidth = 0.03f; // Más gruesas
    
    [Header("Integración")]
    [SerializeField] private PlaceBrickVR placeBrickSystem;
    
    // Componentes
    private TeleportationArea teleportationArea;
    private Renderer gridRenderer;
    private LineRenderer[] gridLines;
    private Vector3 currentGridDimensions;
    private Vector3 gridSize;
    
    private void Awake()
    {
        // Obtener TeleportationArea
        teleportationArea = GetComponent<TeleportationArea>();
        
        // Crear visualización de grid
        CreateGridVisualization();
        
        // Buscar PlaceBrickVR automáticamente
        if (placeBrickSystem == null)
        {
            placeBrickSystem = FindObjectOfType<PlaceBrickVR>();
        }
    }
    
    private void Start()
    {
        // Obtener tamaño del collider del TeleportationArea
        BoxCollider collider = GetComponent<BoxCollider>();
        if (collider != null)
        {
            gridSize = collider.size;
        }
        else
        {
            gridSize = Vector3.one * 10f; // Tamaño por defecto
        }
        
        // Obtener dimensiones de la grid
        UpdateGridDimensions();
        
        // Conectar con PlaceBrickVR
        ConnectWithPlaceBrickSystem();
    }
    
    private void CreateGridVisualization()
    {
        // Crear objeto visual para la grid
        GameObject gridVisual = GameObject.CreatePrimitive(PrimitiveType.Cube);
        gridVisual.name = "GridVisual";
        gridVisual.transform.SetParent(transform);
        gridVisual.transform.localPosition = Vector3.zero;
        gridVisual.transform.localScale = Vector3.one; // Se ajustará en Start
        
        // Configurar renderer
        gridRenderer = gridVisual.GetComponent<Renderer>();
        
        // Remover collider (usamos el del TeleportationArea)
        BoxCollider gridCollider = gridVisual.GetComponent<BoxCollider>();
        if (gridCollider != null)
        {
            DestroyImmediate(gridCollider);
        }
        
        // Configurar material
        SetupGridMaterial();
        
        Debug.Log("✅ Grid visual creada en TeleportationArea");
    }
    
    private void SetupGridMaterial()
    {
        if (gridRenderer == null)
        {
            Debug.LogError("GridOnTeleportationArea: gridRenderer es null, no se puede configurar material");
            return;
        }
        
        // Crear material transparente usando URP/Lit
        Material gridMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        
        // Configurar para transparencia
        gridMat.SetFloat("_Surface", 1); // Transparent
        gridMat.SetFloat("_Blend", 0); // Alpha
        gridMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        gridMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        gridMat.SetInt("_ZWrite", 0);
        gridMat.SetInt("_Cull", 0); // No culling
        gridMat.DisableKeyword("_ALPHATEST_ON");
        gridMat.EnableKeyword("_ALPHABLEND_ON");
        gridMat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        gridMat.renderQueue = 3000;
        
        // Aplicar color con mayor visibilidad
        Color finalColor = gridColor;
        finalColor.a = Mathf.Max(gridAlpha, 0.5f); // Mínimo 50% de opacidad
        gridMat.color = finalColor;
        
        // Configurar propiedades adicionales para mejor visibilidad
        gridMat.SetFloat("_Metallic", 0f);
        gridMat.SetFloat("_Smoothness", 0.1f);
        
        gridRenderer.material = gridMat;
        
        Debug.Log($"Material de grid configurado - Color: {finalColor}, Alpha: {finalColor.a}");
    }
    
    private void UpdateGridDimensions()
    {
        // Obtener dimensiones del GridSystem
        currentGridDimensions = GridSystem.GetGridDimensions();
        
        if (currentGridDimensions == Vector3.zero)
        {
            // Usar dimensiones por defecto para LEGO con escala 10x1x10
            currentGridDimensions = new Vector3(1.0f, 0.1f, 1.0f);
            Debug.LogWarning("GridOnTeleportationArea: GridSystem no inicializado, usando dimensiones por defecto (10x1x10)");
        }
        
        // Verificar que las dimensiones sean válidas
        if (currentGridDimensions.x <= 0 || currentGridDimensions.z <= 0)
        {
            Debug.LogError("GridOnTeleportationArea: Dimensiones de grid inválidas, no se puede crear la visualización");
            return;
        }
        
        // Actualizar tamaño del visual
        if (gridRenderer != null)
        {
            gridRenderer.transform.localScale = gridSize;
        }
        
        // Crear líneas de grid solo si las dimensiones son válidas
        if (showGridLines)
        {
            CreateGridLines();
        }
        
        Debug.Log($"Grid configurada - Tamaño: {gridSize}, Dimensiones: {currentGridDimensions}");
    }
    
    private void CreateGridLines()
    {
        // Verificar que las dimensiones de grid sean válidas
        if (currentGridDimensions.x <= 0 || currentGridDimensions.z <= 0)
        {
            Debug.LogError("GridOnTeleportationArea: Dimensiones de grid inválidas, no se pueden crear líneas");
            return;
        }
        
        // Limpiar líneas existentes
        if (gridLines != null)
        {
            foreach (var line in gridLines)
            {
                if (line != null)
                    DestroyImmediate(line.gameObject);
            }
        }
        
        // Calcular número de líneas
        int gridLinesX = Mathf.RoundToInt(gridSize.x / currentGridDimensions.x);
        int gridLinesZ = Mathf.RoundToInt(gridSize.z / currentGridDimensions.z);
        
        // Verificar que los cálculos sean válidos
        if (gridLinesX < 0 || gridLinesZ < 0)
        {
            Debug.LogError($"GridOnTeleportationArea: Cálculo de líneas inválido - X: {gridLinesX}, Z: {gridLinesZ}");
            return;
        }
        
        // El array debe incluir espacio para todas las líneas: (gridLinesX + 1) verticales + (gridLinesZ + 1) horizontales
        gridLines = new LineRenderer[(gridLinesX + 1) + (gridLinesZ + 1)];
        
        // Crear material para líneas con mejor visibilidad
        Material lineMat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        lineMat.color = new Color(gridLineColor.r, gridLineColor.g, gridLineColor.b, 1f); // Sin transparencia para líneas
        
        // Crear líneas verticales (X)
        for (int i = 0; i <= gridLinesX; i++)
        {
            GameObject lineObj = new GameObject($"GridLine_X_{i}");
            lineObj.transform.SetParent(transform);
            lineObj.layer = 8; // Asignar Layer 8 para detección del XRRayInteractor
            
            LineRenderer line = lineObj.AddComponent<LineRenderer>();
            line.material = lineMat;
            line.material.color = gridLineColor;
            line.startWidth = Mathf.Max(gridLineWidth, 0.02f); // Mínimo 2cm de grosor
            line.endWidth = Mathf.Max(gridLineWidth, 0.02f);
            line.positionCount = 2;
            line.useWorldSpace = false;
            line.sortingOrder = 1; // Asegurar que esté por encima
            
            float x = (i * currentGridDimensions.x) - (gridSize.x / 2f);
            line.SetPosition(0, new Vector3(x, 0.01f, -gridSize.z / 2f));
            line.SetPosition(1, new Vector3(x, 0.01f, gridSize.z / 2f));
            
            gridLines[i] = line;
        }
        
        // Crear líneas horizontales (Z)
        for (int i = 0; i <= gridLinesZ; i++)
        {
            GameObject lineObj = new GameObject($"GridLine_Z_{i}");
            lineObj.transform.SetParent(transform);
            lineObj.layer = 8; // Asignar Layer 8 para detección del XRRayInteractor
            
            LineRenderer line = lineObj.AddComponent<LineRenderer>();
            line.material = lineMat;
            line.material.color = gridLineColor;
            line.startWidth = Mathf.Max(gridLineWidth, 0.02f); // Mínimo 2cm de grosor
            line.endWidth = Mathf.Max(gridLineWidth, 0.02f);
            line.positionCount = 2;
            line.useWorldSpace = false;
            line.sortingOrder = 1; // Asegurar que esté por encima
            
            float z = (i * currentGridDimensions.z) - (gridSize.z / 2f);
            line.SetPosition(0, new Vector3(-gridSize.x / 2f, 0.01f, z));
            line.SetPosition(1, new Vector3(gridSize.x / 2f, 0.01f, z));
            
            // Usar el índice correcto: (gridLinesX + 1) + i
            gridLines[(gridLinesX + 1) + i] = line;
        }
        
        Debug.Log($"✅ Líneas de grid creadas: {gridLinesX + 1} verticales, {gridLinesZ + 1} horizontales (total: {gridLines.Length})");
    }
    
    private void ConnectWithPlaceBrickSystem()
    {
        if (placeBrickSystem != null)
        {
            // Crear wrapper para compatibilidad con PlaceBrickVR
            GridVisualizerWrapper wrapper = new GridVisualizerWrapper(this);
            
            // Conectar usando reflexión
            var placeBrickType = typeof(PlaceBrickVR);
            var gridField = placeBrickType.GetField("gridVisualizer", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            gridField?.SetValue(placeBrickSystem, wrapper);
            
            Debug.Log("✅ GridOnTeleportationArea conectada con PlaceBrickVR");
        }
    }
    
    /// <summary>
    /// Resalta una posición específica en la grid
    /// </summary>
    public void HighlightPosition(Vector3 worldPosition)
    {
        if (gridRenderer != null)
        {
            Color highlightColorWithAlpha = highlightColor;
            highlightColorWithAlpha.a = gridAlpha * 1.5f;
            gridRenderer.material.color = highlightColorWithAlpha;
        }
        
        // Resaltar líneas
        if (gridLines != null)
        {
            foreach (var line in gridLines)
            {
                if (line != null)
                {
                    line.material.color = highlightColor;
                }
            }
        }
    }
    
    /// <summary>
    /// Restaura el color normal de la grid
    /// </summary>
    public void RestoreNormalColor()
    {
        if (gridRenderer != null)
        {
            Color normalColor = gridColor;
            normalColor.a = gridAlpha;
            gridRenderer.material.color = normalColor;
        }
        
        // Restaurar líneas
        if (gridLines != null)
        {
            foreach (var line in gridLines)
            {
                if (line != null)
                {
                    line.material.color = gridLineColor;
                }
            }
        }
    }
    
    /// <summary>
    /// Actualiza las dimensiones de la grid
    /// </summary>
    public void UpdateGridDimensions(Vector3 newDimensions)
    {
        currentGridDimensions = newDimensions;
        
        if (showGridLines)
        {
            CreateGridLines();
        }
        
        Debug.Log($"Grid dimensions actualizadas: {newDimensions}");
    }
    
    [ContextMenu("Actualizar Grid")]
    public void RefreshGrid()
    {
        UpdateGridDimensions();
        SetupGridMaterial();
        
        // Forzar actualización de líneas
        if (showGridLines)
        {
            CreateGridLines();
        }
        
        Debug.Log("Grid actualizada y forzada a ser visible");
    }
    
    [ContextMenu("Mostrar/Ocultar Grid")]
    public void ToggleGrid()
    {
        showGrid = !showGrid;
        
        if (gridRenderer != null)
        {
            gridRenderer.enabled = showGrid;
        }
        
        if (gridLines != null)
        {
            foreach (var line in gridLines)
            {
                if (line != null)
                {
                    line.enabled = showGrid;
                }
            }
        }
    }
    
    [ContextMenu("Debug Grid Info")]
    public void DebugGridInfo()
    {
        Debug.Log("=== DEBUG GRID INFO ===");
        Debug.Log($"GridRenderer: {(gridRenderer != null ? "OK" : "NULL")}");
        Debug.Log($"GridLines: {(gridLines != null ? gridLines.Length + " líneas" : "NULL")}");
        Debug.Log($"GridSize: {gridSize}");
        Debug.Log($"CurrentGridDimensions: {currentGridDimensions}");
        Debug.Log($"ShowGrid: {showGrid}");
        Debug.Log($"ShowGridLines: {showGridLines}");
        Debug.Log($"GridAlpha: {gridAlpha}");
        Debug.Log($"GridLineWidth: {gridLineWidth}");
        
        if (gridRenderer != null)
        {
            Debug.Log($"GridRenderer enabled: {gridRenderer.enabled}");
            Debug.Log($"GridRenderer material: {(gridRenderer.material != null ? "OK" : "NULL")}");
        }
    }
    
    private void OnDestroy()
    {
        // Limpiar líneas de grid
        if (gridLines != null)
        {
            foreach (var line in gridLines)
            {
                if (line != null)
                    Destroy(line.gameObject);
            }
        }
    }
    
    // Wrapper para compatibilidad con PlaceBrickVR
    private class GridVisualizerWrapper
    {
        private GridOnTeleportationArea gridArea;
        
        public GridVisualizerWrapper(GridOnTeleportationArea area)
        {
            gridArea = area;
        }
        
        public void HighlightPosition(Vector3 position)
        {
            gridArea.HighlightPosition(position);
        }
        
        public void RestoreNormalColor()
        {
            gridArea.RestoreNormalColor();
        }
        
        public void UpdateGridDimensions(Vector3 dimensions)
        {
            gridArea.UpdateGridDimensions(dimensions);
        }
    }
}
