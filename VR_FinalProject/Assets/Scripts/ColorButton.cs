using UnityEngine;
using UnityEngine.UI;

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
    
    public void SetColor(Color color)
    {
        buttonColor = color;
        
        if (colorImage != null)
        {
            colorImage.color = color;
        }
    }
    
    public Color GetColor()
    {
        return buttonColor;
    }
}
