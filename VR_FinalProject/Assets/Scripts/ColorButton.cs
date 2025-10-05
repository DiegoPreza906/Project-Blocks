using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Script simple para los botones de color en el menú VR
/// Solo necesita que le asignes el color que quieres que tenga
/// </summary>
public class ColorButton : MonoBehaviour
{
    [Header("Color del Botón")]
    [SerializeField] private Color buttonColor = Color.white;
    
    private Image colorImage;
    
    private void Start()
    {
        // Obtener el componente Image
        colorImage = GetComponent<Image>();
        
        // Aplicar el color
        SetColor(buttonColor);
    }
    
    /// <summary>
    /// Establece el color del botón
    /// </summary>
    public void SetColor(Color color)
    {
        buttonColor = color;
        
        if (colorImage != null)
        {
            colorImage.color = color;
        }
    }
    
    /// <summary>
    /// Obtiene el color del botón
    /// </summary>
    public Color GetColor()
    {
        return buttonColor;
    }
}
