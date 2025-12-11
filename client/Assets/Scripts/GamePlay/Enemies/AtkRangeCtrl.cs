using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public class AtkRangeCtrl : MonoBehaviour
{
    public IVictim Victim;
    private BoxCollider boxCollider;

    public void Init(Vector3 size, IVictim victim)
    {
        Victim = victim;
        gameObject.layer = LayerMask.NameToLayer("EnemyAtkRangeTrigger");
        var trans = transform;
        trans.SetParent(victim.GetTransform());
        trans.localPosition = Vector3.zero;

        boxCollider = this.AddComponent<BoxCollider>();
        boxCollider.isTrigger = true;
        boxCollider.size = size;
    }

    public void Remove()
    {
        Victim = null;
        Destroy(this.gameObject);
    }
    private void OnTriggerEnter(Collider other)
    {
        Victim?.OnEnter(other);
    }

    private void OnTriggerStay(Collider other)
    {
    }

    private void OnTriggerExit(Collider other)
    {
        Victim?.OnExit(other);
    }
}