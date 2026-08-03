using UnityEngine;

public class ProgramTrigger : MonoBehaviour
{
    public ProgramRunner runner;
    public BlockNode startBlock;        //Bloque estático "Inicio"

    [ContextMenu("Press")]
    public void OnPlayPressed()
    {
        if (runner.IsRunning) return;
        StartCoroutine(runner.Run(startBlock));
    }
}