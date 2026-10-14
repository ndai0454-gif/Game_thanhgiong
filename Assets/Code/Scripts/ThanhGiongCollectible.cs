using UnityEngine;

public class ThanhGiongCollectible : MonoBehaviour
{
    public enum Kind { Rice, Eggplant, Firewood, Meat, Bamboo, Stone }

    public Kind kind;
    public string displayName = "Lương Thực";
    public float foodValue = 25f;
    public float spinSpeed = 35f;
    public Transform visualRoot;
    public TextMesh labelText;

    private Vector3 baseVisualLocalPos;
    private float bobOffset;
    private bool collected;
    public bool IsCollected => collected;

    private void Awake()
    {
        if (visualRoot == null)
        {
            Transform child = transform.Find("Visual");
            visualRoot = child != null ? child : transform;
        }
        baseVisualLocalPos = visualRoot != transform ? visualRoot.localPosition : Vector3.zero;
        bobOffset = Random.Range(0f, Mathf.PI * 2f);
    }

    private void Update()
    {
        if (collected) return;
        if (visualRoot != null && visualRoot != transform)
        {
            visualRoot.Rotate(0f, spinSpeed * Time.deltaTime, 0f, Space.World);
            visualRoot.localPosition = baseVisualLocalPos + Vector3.up * (Mathf.Sin((Time.time + bobOffset) * 2.5f) * 0.08f);
        }
        else
        {
            transform.Rotate(0f, spinSpeed * Time.deltaTime, 0f, Space.World);
        }
    }

    private void LateUpdate()
    {
        if (labelText != null && Camera.main != null)
        {
            labelText.transform.rotation = Camera.main.transform.rotation;
        }
    }

    public void MarkCollected()
    {
        collected = true;
        Collider[] colliders = GetComponentsInChildren<Collider>();
        foreach (Collider c in colliders) c.enabled = false;
        enabled = false;
    }

    public Color GetAuraColor() => kind switch
    {
        Kind.Rice => new Color(1.0f, 0.88f, 0.25f, 0.95f),      // Gold
        Kind.Eggplant => new Color(0.35f, 0.95f, 0.55f, 0.95f),  // Jade green
        Kind.Firewood => new Color(1.0f, 0.62f, 0.15f, 0.95f),  // Amber fire
        Kind.Meat => new Color(1.0f, 0.32f, 0.25f, 0.95f),      // Crimson feast
        Kind.Bamboo => new Color(0.3f, 1.0f, 0.4f, 0.95f),      // Bamboo green
        _ => Color.white
    };

    private void OnTriggerEnter(Collider other)
    {
        // Bamboo requires three deliberate E presses; proximity must not consume a pull.
        if (kind == Kind.Bamboo || collected) return;
        ThanhGiongCampaignController campaign = other.GetComponentInParent<ThanhGiongCampaignController>();
        if (campaign == null || !campaign.TryCollect(this)) return;
        MarkCollected();
        gameObject.SetActive(false);
    }
}


