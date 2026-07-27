using UnityEngine;

public class ProgramTrigger : MonoBehaviour
{
    public ProgramRunner runner;
    public BlockNode startBlock;        //Bloque estático "Inicio"

    public void OnPlayPressed()
    {
        if (runner.IsRunning) return;
        StartCoroutine(runner.Run(startBlock));
    }
}