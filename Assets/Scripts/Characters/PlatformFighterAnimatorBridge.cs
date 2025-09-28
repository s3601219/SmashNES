using UnityEngine;

public class PlatformFighterAnimatorBridge : MonoBehaviour
{
    Rigidbody2D rb;
    PlatformFighterController ctrl;
    Animator anim;

    void Awake()
    {
        rb   = GetComponent<Rigidbody2D>();
        ctrl = GetComponent<PlatformFighterController>();
        anim = GetComponentInChildren<Animator>(); // Animator on GFX
    }

    void Update()
    {
        anim.SetFloat("speed", Mathf.Abs(rb.linearVelocity.x));
        anim.SetBool("grounded", ctrl.Grounded);
        anim.SetFloat("vert_velocity", rb.linearVelocity.y);
    }
}
