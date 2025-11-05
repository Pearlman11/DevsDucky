#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using TMPro;
using System.IO;
using System;

public static class MazeHintSignPrefabCreator
{
    [MenuItem("Tools/Maze/Create Hint Sign Prefab")]
    public static void CreatePrefab()
    {
        // Build the GameObject hierarchy in memory
        var root = new GameObject("MazeHintSign");
        try
        {
            // Core components
            var rb = root.AddComponent<Rigidbody>();
            rb.mass = 1.5f;
            rb.angularDamping = 0.5f;

            // Add XRGrabbable if it exists in the project (no compile dep)
            var grabbableType = Type.GetType("XRGrabbable");
            if (grabbableType == null)
            {
                // Try finding by assembly-qualified name if needed
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    grabbableType = asm.GetType("XRGrabbable");
                    if (grabbableType != null) break;
                }
            }
            if (grabbableType != null) root.AddComponent(grabbableType);

            // Add our runtime controller
            var sign = root.AddComponent<MazeHintSign>();

            // === Geometry ===
            // Board
            var board = GameObject.CreatePrimitive(PrimitiveType.Cube);
            board.name = "Board";
            board.transform.SetParent(root.transform, false);
            board.transform.localScale = new Vector3(0.6f, 0.03f, 0.4f);
            board.transform.localPosition = new Vector3(0f, 0.3f, 0f);
            var boardRenderer = board.GetComponent<MeshRenderer>();
            var boardMat = new Material(Shader.Find("Standard"));
            boardRenderer.sharedMaterial = boardMat;

            // Handle
            var handle = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            handle.name = "Handle";
            handle.transform.SetParent(root.transform, false);
            float handleRadius = 0.02f;
            float handleLength = 0.5f;
            handle.transform.localScale = new Vector3(handleRadius * 2f, handleLength * 0.5f, handleRadius * 2f);

            float boardBottomY = board.transform.localPosition.y - (0.03f * 0.5f);
            float handleTopY = boardBottomY - 0.02f;
            float handleCenterY = handleTopY - (handleLength * 0.5f);
            handle.transform.localPosition = new Vector3(0f, handleCenterY, 0f);

            // Remove any accidental RBs on children (shouldn't exist, but safe)
            var childRB = handle.GetComponent<Rigidbody>(); if (childRB) UnityEngine.Object.DestroyImmediate(childRB);
            childRB = board.GetComponent<Rigidbody>(); if (childRB) UnityEngine.Object.DestroyImmediate(childRB);

            // === Text (Front) ===
            var front = new GameObject("Text_Front", typeof(TextMeshPro));
            front.transform.SetParent(board.transform, false);
            var frontTMP = front.GetComponent<TextMeshPro>();
            frontTMP.text = "Follow the wall that isn't quite straight.";
            frontTMP.alignment = TextAlignmentOptions.Center;
            frontTMP.enableAutoSizing = true;
            frontTMP.textWrappingMode = TextWrappingModes.Normal;
            frontTMP.fontSize = 48;

            float textMargin = 0.02f;
            float w = 0.6f * 0.90f;
            float h = 0.4f * 0.85f;
            frontTMP.rectTransform.sizeDelta = new Vector2(w, h);
            front.transform.localRotation = Quaternion.identity;
            front.transform.localPosition = new Vector3(0f, 0f, (0.4f * 0.5f) + textMargin);
            front.transform.localEulerAngles = new Vector3(90f, 0f, 0f);

            // === Text (Back) ===
            var back = new GameObject("Text_Back", typeof(TextMeshPro));
            back.transform.SetParent(board.transform, false);
            var backTMP = back.GetComponent<TextMeshPro>();
            backTMP.text = frontTMP.text;
            backTMP.alignment = TextAlignmentOptions.Center;
            backTMP.enableAutoSizing = true;
            backTMP.textWrappingMode = TextWrappingModes.Normal;
            backTMP.fontSize = 48;
            backTMP.rectTransform.sizeDelta = new Vector2(w, h);
            back.transform.localRotation = Quaternion.identity;
            back.transform.localPosition = new Vector3(0f, 0f, -(0.4f * 0.5f) - textMargin);
            back.transform.localEulerAngles = new Vector3(-90f, 180f, 0f);

            // Wire references on the runtime script so you can tweak at runtime
            // (It also auto-finds if missing.)
            var signSO = new SerializedObject(sign);
            signSO.FindProperty("boardRenderer").objectReferenceValue = boardRenderer;
            signSO.FindProperty("frontTMP").objectReferenceValue = frontTMP;
            signSO.FindProperty("backTMP").objectReferenceValue = backTMP;
            signSO.ApplyModifiedPropertiesWithoutUndo();

            // === Save Prefab ===
            var dir = "Assets/Prefabs";
            if (!AssetDatabase.IsValidFolder(dir))
            {
                AssetDatabase.CreateFolder("Assets", "Prefabs");
            }
            var path = Path.Combine(dir, "MazeHintSign.prefab").Replace("\\", "/");
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, path, out bool success);
            if (!success || prefab == null) throw new System.Exception("Failed to save prefab.");

            Debug.Log($"Created prefab at: {path}");

            // Optionally drop an instance in the scene
            var instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
            if (SceneView.lastActiveSceneView != null && SceneView.lastActiveSceneView.camera != null)
            {
                var cam = SceneView.lastActiveSceneView.camera.transform;
                instance.transform.position = cam.position + cam.forward * 2.0f + Vector3.down * 0.5f;
                instance.transform.rotation = Quaternion.LookRotation(new Vector3(cam.forward.x, 0f, cam.forward.z), Vector3.up);
            }
            Selection.activeObject = instance;
        }
        catch
        {
            UnityEngine.Object.DestroyImmediate(root);
            throw;
        }
        finally
        {
            // If we successfully created a prefab asset above, the temp GO is no longer needed
            if (root != null) UnityEngine.Object.DestroyImmediate(root);
        }
    }
}
#endif