using UnityEngine;

[CreateAssetMenu(fileName = "PlayerMovementDataProperty", menuName = "Scriptable Objects/PlayerMovementDataProperty")]
public class PlayerMovementDataProperty : ScriptableObject
{
    public float moveSpeed;
    public float rotationSpeed;
    public bool useCameraRelative;

    public float walkSpeed;
    public float extraSpeed;

    public float GetRunSpeed()
    { 
        return walkSpeed + extraSpeed;
    }
}
