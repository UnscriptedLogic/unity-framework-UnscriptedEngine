using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class TelekenisisListener : MonoBehaviour
{
    public UnityEvent<GameObject> OnTelekenisisStartedEvent;
    public UnityEvent<GameObject> OnTelekenisisEndedEvent;

    public void OnTelekenisisStarted(GameObject context)
    {
        OnTelekenisisStartedEvent.Invoke(context);
    }

    public void OnTelekenisisEnded(GameObject context)
    {
        OnTelekenisisEndedEvent.Invoke(context);
    }
}
