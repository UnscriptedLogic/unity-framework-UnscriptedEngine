using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnscriptedEngine;

public interface ICanvasController
{
    public GameObject gameObject { get; }
    public UTheme CanvasTheme { get; }
    public virtual void OnWidgetAttached(ILevelObject context) { }
    public virtual void OnWidgetDetached(ILevelObject context) { }
}
