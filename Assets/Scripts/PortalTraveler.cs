using UnityEngine;

public class PortalTraveller : MonoBehaviour {

	public Vector3 previousOffsetFromPortal { get; set; }

	public virtual void Teleport(Transform fromPortal, Transform toPortal, Vector3 pos, Quaternion rot) {
		transform.position = pos;
		transform.rotation = rot;
	}
}
