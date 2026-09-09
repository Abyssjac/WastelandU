using JackyUtility;
using UnityEngine;

public enum Key_TestPP
{
    None = 0,
}

[CreateAssetMenu(fileName = "TestPropertyPP_", menuName = "AllProperties/TestProperty")]
public class TestProperty : EnumStringKeyedProperty<Key_TestPP>
{
}