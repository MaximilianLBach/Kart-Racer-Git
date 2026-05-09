using UnityEngine;
using Unity.Netcode;
using UnityEngine.InputSystem; // <-- Added this to read your controllers!

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

    [Header("Visibility & Scale Settings")]
    public Renderer[] meshesToHideLocally;
    public float remoteHandScale = 1.8f;

    [Header("Correction Offsets")]
    public Vector3 leftHandRotOffset = new Vector3(0, 0, 90);
    public Vector3 rightHandRotOffset = new Vector3(0, 0, -90);
    public Vector3 bodyPositionOffset = new Vector3(0, -0.2f, 0);

    [Header("Animation Syncing")]
    public Animator leftHandAnimator;
    public Animator rightHandAnimator;
    public string gripParamName = "Grip"; 
    public string triggerParamName = "Trigger";

    [Header("Animation Input Actions")]
    public InputActionReference leftGripInput;
    public InputActionReference leftTriggerInput;
    public InputActionReference rightGripInput;
    public InputActionReference rightTriggerInput;

    private NetworkVariable<float> netLeftGrip = new NetworkVariable<float>(0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
    private NetworkVariable<float> netLeftTrigger = new NetworkVariable<float>(0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
    private NetworkVariable<float> netRightGrip = new NetworkVariable<float>(0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
    private NetworkVariable<float> netRightTrigger = new NetworkVariable<float>(0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

    public override void OnNetworkSpawn()
    {
        if (IsOwner)
        {
            foreach (Renderer mesh in meshesToHideLocally)
            {
                if (mesh != null) mesh.enabled = false;
            }
        }
        else
        {
            if (miiLeftHand != null) miiLeftHand.localScale = Vector3.one * remoteHandScale;
            if (miiRightHand != null) miiRightHand.localScale = Vector3.one * remoteHandScale;
        }
    }

    void Update()
    {
        if (IsOwner)
        {
            // 1. Move the Avatar
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

            // 2. Read hardware inputs directly from the controllers
            float lGrip = leftGripInput != null ? leftGripInput.action.ReadValue<float>() : 0f;
            float lTrig = leftTriggerInput != null ? leftTriggerInput.action.ReadValue<float>() : 0f;
            float rGrip = rightGripInput != null ? rightGripInput.action.ReadValue<float>() : 0f;
            float rTrig = rightTriggerInput != null ? rightTriggerInput.action.ReadValue<float>() : 0f;

            // 3. Apply to LOCAL Animators
            if (leftHandAnimator != null)
            {
                leftHandAnimator.SetFloat(gripParamName, lGrip);
                leftHandAnimator.SetFloat(triggerParamName, lTrig);
            }
            if (rightHandAnimator != null)
            {
                rightHandAnimator.SetFloat(gripParamName, rGrip);
                rightHandAnimator.SetFloat(triggerParamName, rTrig);
            }

            // 4. Send to Network
            netLeftGrip.Value = lGrip;
            netLeftTrigger.Value = lTrig;
            netRightGrip.Value = rGrip;
            netRightTrigger.Value = rTrig;
        }
        else
        {
            // REMOTE PLAYERS ONLY: Read the network variables and apply to their Animators!
            if (leftHandAnimator != null)
            {
                leftHandAnimator.SetFloat(gripParamName, netLeftGrip.Value);
                leftHandAnimator.SetFloat(triggerParamName, netLeftTrigger.Value);
            }
            if (rightHandAnimator != null)
            {
                rightHandAnimator.SetFloat(gripParamName, netRightGrip.Value);
                rightHandAnimator.SetFloat(triggerParamName, netRightTrigger.Value);
            }
        }

        // Torso alignment
        if (miiHead != null && miiTorso != null)
        {
            miiTorso.position = miiHead.position - new Vector3(0, 0.8f, 0);
            Vector3 flatForward = miiHead.forward;
            flatForward.y = 0; 
            miiTorso.rotation = Quaternion.LookRotation(flatForward);
        }
    }
}