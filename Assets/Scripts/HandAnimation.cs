using UnityEngine;
using UnityEngine.InputSystem;

public class HandAnimation : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    [SerializeField] private InputActionReference gripActionReference;
    [SerializeField] private InputActionReference TriggerActionReference;

    private Animator animator;

    private string gripActionName = "Grip";
    private string triggerActionName = "Pinch";

    private void Awake()
    {
        animator = GetComponent<Animator>();
        if(animator == null)
        {
            Debug.LogError("Animator Component not found on the GameObject");
        }
    }

    private void OnEnable()
    {
        gripActionReference.action.Enable();
        TriggerActionReference.action.Enable();
    }

    private void OnDisable()
    {
        gripActionReference.action.Disable();
        TriggerActionReference.action.Disable();
    }
    // Update is called once per frame
    void Update()
    {
        if(animator == null)
        {
            return;
        }
        float gripValue = gripActionReference.action.ReadValue<float>();
        float triggerValue = TriggerActionReference.action.ReadValue<float>();
    
        animator.SetFloat(gripActionName, gripValue);
        animator.SetFloat(triggerActionName, triggerValue);
        
    }
}
