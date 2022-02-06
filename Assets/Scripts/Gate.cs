using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class Gate : MonoBehaviour {

	[Header("Main Settings")]
	public Gate linkedGate;

	[HideInInspector]
	public MeshRenderer screen;

	private Transform playerTransform;
	private Transform playerShadowTransform;
	private MeshRenderer playerShadowRenderer;
	private Camera playerCamera;
	private Camera gateCamera;
	private RenderTexture viewTexture;
	private List<PortalTraveller> trackedTravellers = new List<PortalTraveller>();
	private ShadowCastingMode previousShadowCastingMode;
	private bool shadowMode = false;

	private void Awake() {
		screen = transform.Find("Screen").GetComponent<MeshRenderer>();
		playerCamera = Camera.main;
		gateCamera = GetComponentInChildren<Camera>();
		gateCamera.enabled = false;
		previousShadowCastingMode = screen.shadowCastingMode;
		playerShadowTransform = transform.Find("PlayerShadow").GetComponent<Transform>();
		playerShadowRenderer = transform.Find("PlayerShadow").GetComponentInChildren<MeshRenderer>();
	}

	void Start() {
		playerTransform = GameObject.FindGameObjectWithTag("Player").transform;
		if (playerTransform && playerTransform.GetComponent<MeshRenderer>() && playerTransform.GetComponent<MeshRenderer>().enabled) {
			shadowMode = true;
		}
	}

	void LateUpdate() {
		HandleTravellers();
	}

	public void Render(ScriptableRenderContext context) {
		if (LinkedGateIsVisible()) {
			playerShadowRenderer.enabled = shadowMode;
			PositionAndRotateGateCamera();
			AdjustFieldOfView();
			AdjustGateCameraNearClipPlane();
			HideGateScreen(true);
			RenderCameraOnLinkedGateScreen(context);
			HideGateScreen(false);
		} else {
			playerShadowRenderer.enabled = false;
		}
	}

	void HandleTravellers() {
		for (int i = 0; i < trackedTravellers.Count; i++) {
			PortalTraveller traveller = trackedTravellers[i];
			Transform travellerT = traveller.transform;
			Matrix4x4 m = linkedGate.transform.localToWorldMatrix * transform.worldToLocalMatrix * travellerT.localToWorldMatrix;

			Vector3 offsetFromPortal = travellerT.position - transform.position;
			int portalSide = System.Math.Sign(Vector3.Dot(offsetFromPortal, transform.forward));
			int portalSideOld = System.Math.Sign(Vector3.Dot(traveller.previousOffsetFromPortal, transform.forward));
			// Teleport the traveller if it has crossed from one side of the portal to the other
			if (portalSide != portalSideOld) {
				traveller.Teleport(transform, linkedGate.transform, m.GetColumn(3), m.rotation);
				// Can't rely on OnTriggerEnter/Exit to be called next frame since it depends on when FixedUpdate runs
				linkedGate.OnTravellerEnterPortal(traveller);
				trackedTravellers.RemoveAt(i);
				i--;
			} else {
				traveller.previousOffsetFromPortal = offsetFromPortal;
			}
		}
	}

	void OnTravellerEnterPortal(PortalTraveller traveller) {
		if (!trackedTravellers.Contains(traveller)) {
			traveller.previousOffsetFromPortal = traveller.transform.position - transform.position;
			trackedTravellers.Add(traveller);
		}
	}

	void OnTriggerEnter(Collider other) {
		var traveller = other.GetComponent<PortalTraveller>();
		if (traveller) {
			OnTravellerEnterPortal(traveller);
		}
	}

	void OnTriggerExit(Collider other) {
		var traveller = other.GetComponent<PortalTraveller>();
		if (traveller && trackedTravellers.Contains(traveller)) {
			trackedTravellers.Remove(traveller);
		}
	}

	private void PositionAndRotateGateCamera() {
		Matrix4x4 m = transform.localToWorldMatrix * linkedGate.transform.worldToLocalMatrix * playerCamera.transform.localToWorldMatrix;
		gateCamera.transform.SetPositionAndRotation(m.GetColumn(3), m.rotation);
		playerShadowTransform.SetPositionAndRotation(m.GetColumn(3), playerTransform.rotation);
	}

	private void HideGateScreen(bool value) {
		if (value) {    //Hide the screen
			previousShadowCastingMode = screen.shadowCastingMode;
			screen.shadowCastingMode = ShadowCastingMode.ShadowsOnly;
		} else {    //Show the screen
			screen.shadowCastingMode = previousShadowCastingMode;
		}

	}

	private void RenderCameraOnLinkedGateScreen(ScriptableRenderContext context) {
		linkedGate.SetViewTexture(gateCamera);
		UniversalRenderPipeline.RenderSingleCamera(context, gateCamera);
	}

	private bool LinkedGateIsVisible() {
		return CameraUtility.VisibleFromCamera(linkedGate.screen, playerCamera);
	}

	public void SetViewTexture(Camera sourceCamera) {
		if (viewTexture == null || viewTexture.width != Screen.width || viewTexture.height != Screen.height) {
			if (viewTexture != null) {
				viewTexture.Release();
			}
			viewTexture = new RenderTexture(Screen.width, Screen.height, 32);
			sourceCamera.targetTexture = viewTexture;   // Render the view from the gate camera to the view texture
			screen.material.mainTexture = viewTexture;
		}
	}

	//Make the gateCamera's "field of view" the same as the playerCamera's, just in case the later changes
	private void AdjustFieldOfView() {
		gateCamera.projectionMatrix = playerCamera.projectionMatrix;
	}

	void AdjustGateCameraNearClipPlane() {
		Plane p = new Plane(-transform.forward, transform.position);
		Vector4 clipPlaneWorldSpace = new Vector4(p.normal.x, p.normal.y, p.normal.z, p.distance);
		Vector4 clipPlaneCameraSpace = Matrix4x4.Transpose(Matrix4x4.Inverse(linkedGate.gateCamera.worldToCameraMatrix)) * clipPlaneWorldSpace;
		linkedGate.gateCamera.projectionMatrix = playerCamera.CalculateObliqueMatrix(clipPlaneCameraSpace);
	}
}
