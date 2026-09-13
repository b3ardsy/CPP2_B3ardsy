using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class UIAudioManager : MonoBehaviour
{
    private readonly HashSet<Button> registeredButtons =
        new HashSet<Button>();

    private void Start()
    {
        RegisterAllButtons();
    }

    public void RegisterAllButtons()
    {
        Button[] buttons =
            FindObjectsByType<Button>(
                FindObjectsInactive.Include
            );

        foreach (Button button in buttons)
        {
            RegisterButton(button);
        }
    }

    private void RegisterButton(
        Button button
    )
    {
        if (
            button == null ||
            registeredButtons.Contains(button)
        )
        {
            return;
        }

        registeredButtons.Add(button);

        button.onClick.AddListener(
            () => PlayClick(button)
        );

        EventTrigger trigger =
            button.GetComponent<EventTrigger>();

        if (trigger == null)
        {
            trigger =
                button.gameObject.AddComponent<EventTrigger>();
        }

        if (trigger.triggers == null)
        {
            trigger.triggers =
                new List<EventTrigger.Entry>();
        }

        EventTrigger.Entry hoverEntry =
            new EventTrigger.Entry
            {
                eventID =
                    EventTriggerType.PointerEnter
            };

        hoverEntry.callback.AddListener(
            _ => PlayHover(button)
        );

        trigger.triggers.Add(
            hoverEntry
        );
    }

    private void PlayHover(
        Button button
    )
    {
        if (
            button == null ||
            !button.interactable
        )
        {
            return;
        }

        AudioManager.Instance?.PlayUIHover();
    }

    private void PlayClick(
        Button button
    )
    {
        if (
            button == null ||
            !button.interactable
        )
        {
            return;
        }

        AudioManager.Instance?.PlayUIClick();
    }
}
