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
        // instant events
        if (step.instantEvents != null)
        {
            //foreach (var e in step.instantEvents)
            //    e?.Invoke();
            step.instantEvents.Invoke();
        }

        int remaining = 0;
        bool finished = false;

        if (step.waitActions != null)
        {
            foreach (var action in step.waitActions)
            {
                var a = action.GetComponent<IStepAction>();

                if (a == null) continue;

                remaining++;
                yield return StartCoroutine(RunAction(a));
            }
        }

        if (remaining == 0)
            yield break;

        if (step.completionType == StepCompletionType.All)
        {
            yield return new WaitUntil(() => remaining == 0);
        }
        else // ANY
        {
            yield return new WaitUntil(() => finished);
        }

        IEnumerator RunAction(IStepAction action)
        {
            yield return action.Execute();

            if (step.completionType == StepCompletionType.All)
            {
                remaining--;
            }
            else // ANY
            {
                if (!finished)
                    finished = true;
            }
        }
    }


}

[Serializable]
public enum StepCompletionType
{
    All, // AND
    Any  // OR
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