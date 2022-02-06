using UnityEngine;
using UnityEngine.Rendering;

public class PlayerCamera : MonoBehaviour {

	Gate[] gates;

	void Awake() {
		gates = FindObjectsOfType<Gate>();
		RenderPipelineManager.beginCameraRendering += OnBeginFrameRendering;
	}

	void OnBeginFrameRendering(ScriptableRenderContext context, Camera cameras) {
		for (int i = 0; i < gates.Length; i++) {
			gates[i].Render(context);
		}
	}

	// Remove your callback from the delegate's invocation list
	void OnDestroy() {
		RenderPipelineManager.beginCameraRendering -= OnBeginFrameRendering;
	}
}
