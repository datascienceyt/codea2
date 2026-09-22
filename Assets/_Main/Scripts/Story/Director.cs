using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class Director : MonoBehaviour
{
    [Header("Control")]
    public bool playOnStart = true;

    public List<Scenario> scenarios;


    private int currentScenarioIndex = 0;
    private int currentStepIndex = 0;

    /// <summary>
    /// Petición de dar por terminado el paso en curso. Se limpia al entrar en cada paso, para
    /// que una pulsación a destiempo no se cuele en el siguiente y encadene saltos.
    /// </summary>
    private bool skipRequested;

    void Start()
    {
        if (playOnStart)
            Play();
    }

    [ContextMenu("Play")]
    public void Play()
    {
        currentScenarioIndex = 0;
        currentStepIndex = 0;

        StartCoroutine(Run());
    }

    /// <summary>
    /// Da por terminado el paso en curso sin esperar a sus acciones.
    ///
    /// Para probar sin jugar la nave entera: llegar al escenario 4 en el visor cuesta varios
    /// minutos de narración cada vez que quieres comprobar un cambio.
    ///
    /// **No corta las acciones que ya estaban corriendo**, por la misma razón por la que el
    /// modo Any tampoco lo hace: un fundido a medias o un audio cortado dejan estados sin forma
    /// limpia de revertirse. Lo que hace es dejar de esperarlas, así que puede oírse el final
    /// de una narración por encima del paso siguiente. Es una herramienta de prueba, no una
    /// transición.
    /// </summary>
    [ContextMenu("Skip Step")]
    private void SkipStep()
    {
        if (scenarios == null || currentScenarioIndex >= scenarios.Count)
        {
            Debug.LogWarning("[Director] No hay ningún paso corriendo que saltar.", this);
            return;
        }

        skipRequested = true;

        Debug.Log($"[Director] Salto pedido · escenario {currentScenarioIndex} · " +
                  $"paso {currentStepIndex}", this);
    }

    IEnumerator Run()
    {
        while (currentScenarioIndex < scenarios.Count)
        {
            Scenario scenario = scenarios[currentScenarioIndex];

            //Debug.Log($"[Director] Scenario {currentScenarioIndex}");

            yield return RunScenario(scenario);

            currentScenarioIndex++;
            currentStepIndex = 0;
        }

        Debug.Log("[Director] Todo terminado");
    }

    IEnumerator RunScenario(Scenario scenario)
    {
        while (currentStepIndex < scenario.steps.Count)
        {
            Step step = scenario.steps[currentStepIndex];

            Debug.Log($"[Step {currentStepIndex}]: {step.message}");

            yield return RunStep(step);

            currentStepIndex++;
        }
    }

    IEnumerator RunStep(Step step)
    {
        // Una petición que no llegó a consumirse no debe saltarse este paso.
        skipRequested = false;

        step.instantEvents?.Invoke();

        if (step.waitActions == null) yield break;

        // Se resuelven los componentes ANTES de lanzar nada: así el contador de pendientes
        // es correcto desde el principio y no puede llegar a cero a mitad del recuento.
        List<IStepAction> actions = new List<IStepAction>();

        foreach (GameObject actionObject in step.waitActions)
        {
            if (actionObject == null) continue;

            IStepAction action = actionObject.GetComponent<IStepAction>();
            if (action != null) actions.Add(action);
        }

        if (actions.Count == 0) yield break;

        // En serie: cada acción espera a que termine la anterior. Es lo que permite encadenar
        // "espera a que resuelva → abre la puerta → funde a negro" dentro de un solo paso.
        if (step.completionType == StepCompletionType.Sequence)
        {
            foreach (IStepAction action in actions)
            {
                // Se bombea a mano en vez de 'yield return action.Execute()'. Es la única forma
                // de abandonar la acción entre dos yields: con el yield return directo, la
                // corrutina anidada no se puede dejar a medias desde fuera, y el salto no
                // funcionaría en los pasos en serie.
                IEnumerator routine = action.Execute();

                while (routine.MoveNext())
                {
                    if (skipRequested) break;

                    yield return routine.Current;
                }

                if (skipRequested) break;
            }

            if (skipRequested) LogSkipped(step);

            yield break;
        }

        int remaining = actions.Count;
        bool anyFinished = false;

        // En PARALELO, sin 'yield return'. Con 'yield return StartCoroutine(...)' las acciones
        // corrían en serie y el paso ya había terminado al llegar a los WaitUntil de abajo,
        // lo que dejaba muertos tanto el contador como el modo Any.
        foreach (IStepAction action in actions)
            StartCoroutine(RunAction(action));

        if (step.completionType == StepCompletionType.All)
            yield return new WaitUntil(() => remaining == 0 || skipRequested);
        else
            yield return new WaitUntil(() => anyFinished || skipRequested);

        if (skipRequested) LogSkipped(step);

        IEnumerator RunAction(IStepAction action)
        {
            yield return action.Execute();

            remaining--;
            anyFinished = true;
        }

        // Nota: en modo Any, las acciones que no ganaron siguen corriendo hasta terminar.
        // Es deliberado: cortarlas a media corrutina dejaría estados a medias (un fundido
        // sin acabar, un audio cortado) sin forma limpia de revertirlos. Saltar un paso se
        // comporta igual, y por el mismo motivo.
    }

    private void LogSkipped(Step step)
    {
        Debug.LogWarning($"[Director] SALTADO el paso {currentStepIndex} '{step.message}'. " +
                         "Las acciones que ya estaban corriendo siguen hasta terminar.", this);
    }


}

// Los valores nuevos van SIEMPRE al final: el índice se serializa en los pasos ya
// configurados en la escena, e insertar en medio cambiaría el modo de todos ellos.
[Serializable]
public enum StepCompletionType
{
    /// <summary>En paralelo. El paso termina cuando han terminado TODAS las acciones.</summary>
    All,

    /// <summary>En paralelo. El paso termina con la PRIMERA que acabe; las demás siguen.</summary>
    Any,

    /// <summary>En serie, una tras otra, en el orden de la lista.</summary>
    Sequence
}

[Serializable]
public class Scenario
{
    public string name;

    public List<Step> steps;
}

[Serializable]
public class Step
{
    public string message;
    public StepCompletionType completionType;

    public UnityEvent instantEvents;
    public List<GameObject> waitActions;
}
