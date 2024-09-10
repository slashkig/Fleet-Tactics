using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class InteractionElement : MonoBehaviour
{
    public string Text { get => TMPText?.text ?? textBox.text; set {
        if (TMPText == null) textBox.text = value;
        else TMPText.text = value;
    } }
    public Toggle toggle;
    public RectTransform secondaryPanel;
    public Slider slider;
    [SerializeField] TextMeshProUGUI TMPText = null;
    [SerializeField] Text textBox;

    public void SetActive(bool active) => gameObject.SetActive(active);
}
