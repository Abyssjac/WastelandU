using UnityEngine;

public class TestManager : MonoBehaviour
{
    public CarriageAssembler assembler;
    public CarriageData carriageData;

    public ModuleData mudule;

    private void Start()
    {
        assembler.Build(carriageData);
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.T)) { 
            Debug.Log("Install module");
            assembler.Install(0, mudule);
        }
    }
}
