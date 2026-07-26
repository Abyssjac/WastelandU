using UnityEngine;

/// <summary>Base visual component for one active notification toast.</summary>
public abstract class NotificationToastUI : MonoBehaviour
{
    public NotificationRequest Request { get; private set; }

    public virtual void Bind(NotificationRequest request)
    {
        Request = request;
    }
}
