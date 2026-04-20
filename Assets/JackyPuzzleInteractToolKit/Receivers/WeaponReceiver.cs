using System;
using UnityEngine;
using JackyPuzzleInteract;

/// <summary>
/// Puzzle receiver that controls a WeaponBehaviour's container.
/// OnActivated  ¡ú copies maxContainer into the weapon's container (restores ammo).
/// OnDeactivated ¡ú clears the weapon's container entirely.
/// </summary>
public class WeaponReceiver : TwoSignalReceiver
{
    [Header("Weapon Receiver")]
    [SerializeField] private Container<Key_BuildablePP> maxContainer;
    [SerializeField] private WeaponBehaviour weaponBehaviour;

    public Action OnWeaponContainerRestored;
    public Action OnWeaponContainerCleared;

    protected override void OnActivated(GameObject sender)
    {
        if (!ValidateRefs()) return;

        weaponBehaviour.container.CopyFrom(maxContainer);
        OnWeaponContainerRestored?.Invoke();
    }

    protected override void OnDeactivated(GameObject sender)
    {
        if (!ValidateRefs()) return;

        weaponBehaviour.container.ClearAll();
        OnWeaponContainerCleared?.Invoke();
    }

    private bool ValidateRefs()
    {
        if (weaponBehaviour == null)
        {
            Debug.LogError($"[{name}] WeaponReceiver: weaponBehaviour Î´ÉèÖÃ£¡", this);
            return false;
        }
        if (weaponBehaviour.container == null)
        {
            Debug.LogError($"[{name}] WeaponReceiver: weaponBehaviour.container Îª¿Õ£¡", this);
            return false;
        }
        return true;
    }
}
