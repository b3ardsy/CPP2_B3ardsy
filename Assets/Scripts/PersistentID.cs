using System;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

[DisallowMultipleComponent]
public class PersistentID : MonoBehaviour
{
    [Header("Persistent Identity")]
    [Tooltip(
        "Stable identifier used by checkpoint/save data. " +
        "Do not change this after the object has been shipped in a save."
    )]
    [SerializeField]
    private string persistentId;

    public string ID =>
        persistentId;

    public bool HasValidID =>
        !string.IsNullOrWhiteSpace(
            persistentId
        );

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (
            Application.isPlaying ||
            EditorUtility.IsPersistent(this)
        )
        {
            return;
        }

        EnsureIDExists();
        EnsureIDIsUnique();
    }

    private void EnsureIDExists()
    {
        if (HasValidID)
        {
            return;
        }

        GenerateNewID();
    }

    private void EnsureIDIsUnique()
    {
        if (!HasValidID)
        {
            return;
        }

        PersistentID[] allPersistentIds =
            Resources.FindObjectsOfTypeAll<PersistentID>();

        foreach (
            PersistentID other
            in allPersistentIds
        )
        {
            if (
                other == null ||
                other == this ||
                EditorUtility.IsPersistent(other) ||
                !other.gameObject.scene.IsValid() ||
                other.persistentId != persistentId
            )
            {
                continue;
            }

            GlobalObjectId thisGlobalId =
                GlobalObjectId.GetGlobalObjectIdSlow(
                    this
                );

            GlobalObjectId otherGlobalId =
                GlobalObjectId.GetGlobalObjectIdSlow(
                    other
                );

            bool thisLooksNewer =
                thisGlobalId.targetObjectId >
                otherGlobalId.targetObjectId;

            if (
                thisLooksNewer ||
                thisGlobalId.targetObjectId ==
                    otherGlobalId.targetObjectId
            )
            {
                GenerateNewID();
                EnsureIDIsUnique();
            }

            return;
        }
    }

    [ContextMenu("Regenerate Persistent ID")]
    private void RegeneratePersistentID()
    {
        if (
            Application.isPlaying ||
            EditorUtility.IsPersistent(this)
        )
        {
            return;
        }

        GenerateNewID();
        EnsureIDIsUnique();
    }

    private void GenerateNewID()
    {
        persistentId =
            Guid.NewGuid()
                .ToString("N");

        EditorUtility.SetDirty(
            this
        );
    }
#endif

    private void Awake()
    {
        if (HasValidID)
        {
            return;
        }

        Debug.LogError(
            $"{name}: PersistentID is missing an ID. " +
            "Open/save this scene in the Unity Editor so an ID " +
            "can be generated before using save/load.",
            this
        );
    }
}
