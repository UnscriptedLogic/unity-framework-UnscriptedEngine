using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TriggerHandler : MonoBehaviour
{
    public new Collider collider => GetComponent<Collider>();

    public event EventHandler<Collider> TriggerEnter;
    public event EventHandler<Collider> TriggerExit;
    public event EventHandler<Collider> TriggerStay;

    public event EventHandler<Collision> CollisionEnter;
    public event EventHandler<Collision> CollisionExit;
    public event EventHandler<Collision> CollisionStay;

    private void OnTriggerEnter(Collider other) => TriggerEnter?.Invoke(this, other);
    private void OnTriggerExit(Collider other) => TriggerExit?.Invoke(this, other);
    private void OnTriggerStay(Collider other) => TriggerStay?.Invoke(this, other);

    private void OnCollisionEnter(Collision collision) => CollisionEnter?.Invoke(this, collision);
    private void OnCollisionExit(Collision collision) => CollisionExit?.Invoke(this, collision);
    private void OnCollisionStay(Collision collision) => CollisionStay?.Invoke(this, collision);
}
