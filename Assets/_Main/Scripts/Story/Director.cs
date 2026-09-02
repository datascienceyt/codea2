using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class Director : MonoBehaviour
{
    public List<Scenario> scenarios;

    [Header("Control")]
    public bool playOnStart = true;

    private int currentScenarioIndex = 0;
    private int currentStepIndex = 0;

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
                yield return action.Execute();

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
            yield return new WaitUntil(() => remaining == 0);
        else
            yield return new WaitUntil(() => anyFinished);

        IEnumerator RunAction(IStepAction action)
        {
            yield return action.Execute();

            remaining--;
            anyFinished = true;
        }

        // Nota: en modo Any, las acciones que no ganaron siguen corriendo hasta terminar.
        // Es deliberado: cortarlas a media corrutina dejaría estados a medias (un fundido
        // sin acabar, un audio cortado) sin forma limpia de revertirlos.
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