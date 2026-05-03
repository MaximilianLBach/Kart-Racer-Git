using UnityEngine;
using Unity.Netcode;

public class VRAvatarSync : NetworkBehaviour
{
    [Header("Invisible Local XR Rig (The Master)")]
    public Transform xrHead;
    public Transform xrLeftHand;
    public Transform xrRightHand;

    [Header("Visible Networked Avatar (The Puppet)")]
    public Transform miiHead;
    public Transform miiLeftHand;
    public Transform miiRightHand;
    public Transform miiTorso;

    [Header("Visibility Settings")]
    [Tooltip("Drag the MeshRenderers of the Head and Torso here so they hide locally")]
    public Renderer[] meshesToHideLocally;

    [Header("Correction Offsets")]
    [Tooltip("Tweak these values in Play Mode to fix the character offsets")]
    public Vector3 leftHandRotOffset = new Vector3(0, 0, 90);  // A guess based on standard VR models!
    public Vector3 rightHandRotOffset = new Vector3(0, 0, -90);
    public Vector3 bodyPositionOffset = new Vector3(0, -0.2f, 0);

    public override void OnNetworkSpawn()
    {
        // When this avatar spawns, check if it belongs to the local player
        if (IsOwner)
        {
            // Turn off the visual meshes so they don't block your VR camera!
            foreach (Renderer mesh in meshesToHideLocally)
            {
                if (mesh != null) mesh.enabled = false;
            }
        }
    }

    void Update()
    {
        // ONLY the person wearing the headset runs this logic
        if (IsOwner)
        {
            // 1. Copy the invisible XR rig's position to the visible Mii parts
            if (xrHead != null)
            {
                miiHead.position = xrHead.position + bodyPositionOffset;
                miiHead.rotation = xrHead.rotation;
            }
            
            if (xrLeftHand != null) 
            {
                miiLeftHand.position = xrLeftHand.position;
                miiLeftHand.rotation = xrLeftHand.rotation * Quaternion.Euler(leftHandRotOffset);
            }

            if (xrRightHand != null) 
            {
                miiRightHand.position = xrRightHand.position;
                miiRightHand.rotation = xrRightHand.rotation * Quaternion.Euler(rightHandRotOffset);
            }
        }

        // EVERYONE (Local and Remote) runs this logic to keep the torso looking normal
        if (miiHead != null && miiTorso != null)
        {
            // Keep the torso slightly below the head
            miiTorso.position = miiHead.position - new Vector3(0, 0.8f, 0);
            
            // Make the torso face the same way as the head, but don't let it tilt up/down
            Vector3 flatForward = miiHead.forward;
            flatForward.y = 0; 
            miiTorso.rotation = Quaternion.LookRotation(flatForward);
        }
    }
}