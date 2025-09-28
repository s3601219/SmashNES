using UnityEngine;

[CreateAssetMenu(menuName = "SSB/Attack Set")]
public class AttackSet : ScriptableObject
{
    [Header("Ground")]
    public AttackClip tilt;
    public AttackClip upTilt;
    public AttackClip downTilt;

    [Header("Air")]
    public AttackClip nair;
    public AttackClip fair;
    public AttackClip bair;
    public AttackClip upAir;
    public AttackClip dair;

    [Header("Specials")]
    public AttackClip special;
    public AttackClip upSpecial;
    public AttackClip downSpecial;

    [Header("Smash Attacks")]
    public AttackClip fsmash;
    public AttackClip upSmash;
    public AttackClip dsmash;

    [Header("Grab")]
    public AttackClip grab;
}
