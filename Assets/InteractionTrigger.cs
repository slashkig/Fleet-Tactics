using System;
using UnityEngine;

public class InteractionTrigger : MonoBehaviour
{
    public Action OnClick;

    void OnMouseUpAsButton() => OnClick();
}