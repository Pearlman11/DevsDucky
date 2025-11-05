using UnityEngine;
using System.Collections.Generic;

public class InvertedHullOutline : MonoBehaviour
{
	[Tooltip("The material to use for the outline. It must use a shader with 'Cull Front'.")]
	public Material outlineMaterial;

	[Tooltip("How thick the outline is.")]
	[SerializeField] private float outlineScaleFactor = 1.05f;

	private Renderer outlineRenderer;

	void Start()
	{
		// 1. Get all MeshFilters from this object and its children.
		MeshFilter[] meshFilters = GetComponentsInChildren<MeshFilter>();

		// 2. Prepare a list to hold the CombineInstance data.
		List<CombineInstance> combineInstances = new List<CombineInstance>();

		for (int i = 0; i < meshFilters.Length; i++)
		{
			MeshFilter currentFilter = meshFilters[i];

			// Skip this object if it's the outline we've already created.
			if (currentFilter.gameObject == this.gameObject) continue;

			CombineInstance combine = new CombineInstance();
			combine.mesh = currentFilter.sharedMesh;
			// This matrix converts the mesh's local space to the root object's local space,
			// ensuring all parts are positioned correctly.
			combine.transform = transform.worldToLocalMatrix * currentFilter.transform.localToWorldMatrix;

			combineInstances.Add(combine);
		}

		// 3. Create a new mesh and combine all the child meshes into it.
		Mesh combinedMesh = new Mesh();
		combinedMesh.CombineMeshes(combineInstances.ToArray(), true, true);

		// 4. Create the outline object using the new combined mesh.
		GameObject outlineObject = new GameObject("Combined Outline");
		outlineObject.transform.SetParent(this.transform);
		outlineObject.transform.localPosition = Vector3.zero;
		outlineObject.transform.localRotation = Quaternion.identity;
		outlineObject.transform.localScale = new Vector3(outlineScaleFactor, outlineScaleFactor, outlineScaleFactor);

		// Add components and set them up.
		MeshFilter meshFilter = outlineObject.AddComponent<MeshFilter>();
		meshFilter.mesh = combinedMesh;

		outlineRenderer = outlineObject.AddComponent<MeshRenderer>();
		outlineRenderer.material = outlineMaterial;

		// Start with the outline hidden.
		HideOutline();
	}

	public void ShowOutline()
	{
		if (outlineRenderer != null)
		{
			outlineRenderer.enabled = true;
		}
	}

	public void HideOutline()
	{
		if (outlineRenderer != null)
		{
			outlineRenderer.enabled = false;
		}
	}

}