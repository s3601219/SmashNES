using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class Hurtbox : MonoBehaviour
{
    public PlatformFighterActor owner; // set in prefab root
    void Reset()
    {
        var c = GetComponent<Collider2D>();
        c.isTrigger = true;
        gameObject.layer = LayerMask.NameToLayer("Hurtbox");
    }
}
