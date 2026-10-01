using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(TMP_Text))]
public sealed class LocalizedTMPText : MonoBehaviour
{
    [Tooltip("策划在 TextTable 中维护的唯一 key，例如 mainmenu.title")]
    [SerializeField] private string key;

    private TMP_Text target;

    private void Awake()
    {
        target = GetComponent<TMP_Text>();
        if (!string.IsNullOrWhiteSpace(target.text))
            key = target.text;
    }

    private void OnEnable()
    {
        Refresh();
    }

    private void OnDisable()
    {
        if (target != null)
            target.text = key;
    }

    public void Refresh()
    {
        if (target == null)
            target = GetComponent<TMP_Text>();

        if (string.IsNullOrWhiteSpace(key))
        {
            Debug.LogWarning($"[TextLocalization] Empty key on {name}.", this);
            return;
        }

        if (TextLocalizationService.TryGetText(key, out string localizedText))
        {
            target.text = localizedText;
            return;
        }

        Debug.LogError($"[TextLocalization] Missing key: {key}", this);
    }
}
