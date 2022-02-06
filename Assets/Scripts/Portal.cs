using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class Portal : MonoBehaviour {
	[Header("Main Settings")]
	public Portal linkedPortal;
	public MeshRenderer screen;

	[Header("Advanced Settings")]
	public float nearClipOffset = 0.05f;
	public float nearClipLimit = 0.2f;

	// Private variables
	RenderTexture viewTexture;
	Camera portalCamera;
	Camera playerCamera;
	List<PortalTraveller> trackedTravellers;
	MeshFilter screenMeshFilter;
	ShadowCastingMode previousShadowCastingMode;
	int MainTexture = Shader.PropertyToID("_BaseMap");

	void Awake() {
		playerCamera = Camera.main;
		portalCamera = GetComponentInChildren<Camera>();
		portalCamera.enabled = false;
		trackedTravellers = new List<PortalTraveller>();
		screenMeshFilter = screen.GetComponent<MeshFilter>();
		screen.material.SetInteger("displayMask", 1);
	}

	void LateUpdate() {
		HandleTravellers();
	}

	void HandleTravellers() {
		for (int i = 0; i < trackedTravellers.Count; i++) {
			PortalTraveller traveller = trackedTravellers[i];
			Transform travellerT = traveller.transform;
			var m = linkedPortal.transform.localToWorldMatrix * transform.worldToLocalMatrix * travellerT.localToWorldMatrix;

			Vector3 offsetFromPortal = travellerT.position - transform.position;
			int portalSide = System.Math.Sign(Vector3.Dot(offsetFromPortal, transform.forward));
			int portalSideOld = System.Math.Sign(Vector3.Dot(traveller.previousOffsetFromPortal, transform.forward));
			// Teleport the traveller if it has crossed from one side of the portal to the other
			if (portalSide != portalSideOld) {
				traveller.Teleport(transform, linkedPortal.transform, m.GetColumn(3), m.rotation);
				// Can't rely on OnTriggerEnter/Exit to be called next frame since it depends on when FixedUpdate runs
				linkedPortal.OnTravellerEnterPortal(traveller);
				trackedTravellers.RemoveAt(i);
				i--;
			} else {
				//UpdateSliceParams (traveller);
				traveller.previousOffsetFromPortal = offsetFromPortal;
			}
		}
	}

	// Called before any portal cameras are rendered for the current frame
	public void PrePortalRender() {
		//Do stuff here if required
	}

	// Manually render the camera attached to this portal
	// Called after PrePortalRender, and before PostPortalRender
	public void Render(ScriptableRenderContext context) {
		// Skip rendering the view from this portal if player is not looking at the linked portal
		if (!CameraUtility.VisibleFromCamera(linkedPortal.screen, playerCamera)) {
			return;
		}

		CreateViewTexture();

		portalCamera.projectionMatrix = playerCamera.projectionMatrix;

		// Hide screen so that camera can see through portal
		previousShadowCastingMode = screen.shadowCastingMode;
		screen.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly;

		// Position and rotate the portal's camera according to the player's position and rotation
		Vector3 playerOffsetFromPortal = playerCamera.transform.position - linkedPortal.transform.position;
		portalCamera.transform.position = transform.position + playerOffsetFromPortal;
		float angularDifferenceBetweenPortalRotation = Quaternion.Angle(transform.rotation, linkedPortal.transform.rotation);
		Quaternion portalRotationalDifference = Quaternion.AngleAxis(angularDifferenceBetweenPortalRotation, Vector3.up);
		Vector3 newCameraDirection = portalRotationalDifference * playerCamera.transform.forward;
		portalCamera.transform.rotation = Quaternion.LookRotation(newCameraDirection, Vector3.up);

		//SetNearClipPlane();
		UniversalRenderPipeline.RenderSingleCamera(context, portalCamera);

		// Unhide objects hidden at start of render
		screen.shadowCastingMode = previousShadowCastingMode;
	}

	// Called once all portals have been rendered, but before the player camera renders
	public void PostPortalRender() {
		//ProtectScreenFromClipping(playerCamera.transform.position);
	}

	void CreateViewTexture() {
		if (viewTexture == null || viewTexture.width != Screen.width || viewTexture.height != Screen.height) {
			if (viewTexture != null) {
				viewTexture.Release();
			}
			viewTexture = new RenderTexture(Screen.width, Screen.height, 24, RenderTextureFormat.ARGB32);
			// Render the view from the portal camera to the view texture
			portalCamera.targetTexture = viewTexture;
			// Display the view texture on the screen of the linked portal
			linkedPortal.screen.material.mainTexture = viewTexture;
		}
	}

	// Sets the thickness of the portal screen so as not to clip with camera near plane when player goes through
	float ProtectScreenFromClipping(Vector3 viewPoint) {
		float halfHeight = playerCamera.nearClipPlane * Mathf.Tan(playerCamera.fieldOfView * 0.5f * Mathf.Deg2Rad);
		float halfWidth = halfHeight * playerCamera.aspect;
		float dstToNearClipPlaneCorner = new Vector3(halfWidth, halfHeight, playerCamera.nearClipPlane).magnitude;
		float screenThickness = dstToNearClipPlaneCorner;

		bool camFacingSameDirAsPortal = Vector3.Dot(transform.forward, transform.position - viewPoint) > 0;
		//screen.transform.localScale = new Vector3(screen.transform.localScale.x, screen.transform.localScale.y, screenThickness);
		//screen.transform.localPosition = Vector3.forward * screenThickness * ((camFacingSameDirAsPortal) ? 0.5f : -0.5f);
		return screenThickness;
	}

	// Use custom projection matrix to align portal camera's near clip plane with the surface of the portal
	// Note that this affects precision of the depth buffer, which can cause issues with effects like screenspace AO
	void SetNearClipPlane() {
		// Learning resource:
		// http://www.terathon.com/lengyel/Lengyel-Oblique.pdf
		Transform clipPlane = transform;
		int dot = System.Math.Sign(Vector3.Dot(clipPlane.forward, transform.position - portalCamera.transform.position));

		Vector3 camSpacePos = portalCamera.worldToCameraMatrix.MultiplyPoint(clipPlane.position);
		Vector3 camSpaceNormal = portalCamera.worldToCameraMatrix.MultiplyVector(clipPlane.forward) * dot;
		float camSpaceDst = -Vector3.Dot(camSpacePos, camSpaceNormal) + nearClipOffset;

		// Don't use oblique clip plane if very close to portal as it seems this can cause some visual artifacts
		if (Mathf.Abs(camSpaceDst) > nearClipLimit) {
			Vector4 clipPlaneCameraSpace = new Vector4(camSpaceNormal.x, camSpaceNormal.y, camSpaceNormal.z, camSpaceDst);

			// Update projection based on new clip plane
			// Calculate matrix with player cam so that player camera settings (fov, etc) are used
			portalCamera.projectionMatrix = playerCamera.CalculateObliqueMatrix(clipPlaneCameraSpace);
		} else {
			portalCamera.projectionMatrix = playerCamera.projectionMatrix;
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

	/*
     ** Some helper/convenience stuff:
     */

	int SideOfPortal(Vector3 pos) {
		return System.Math.Sign(Vector3.Dot(pos - transform.position, transform.forward));
	}

	bool SameSideOfPortal(Vector3 posA, Vector3 posB) {
		return SideOfPortal(posA) == SideOfPortal(posB);
	}

	Vector3 portalCamPos {
		get {
			return portalCamera.transform.position;
		}
	}

	void OnValidate() {
		if (linkedPortal != null) {
			linkedPortal.linkedPortal = this;
		}
	}
}
