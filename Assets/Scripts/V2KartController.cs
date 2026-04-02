using UnityEngine;

public class V2KartController : MonoBehaviour
{
    private float moveInput;
    private float turnInput;
    private bool isCarGrounded;

    

    public float alignSpeed = 10f;

    public float airDrag;
    public float groundDrag;
    public float fwdSpeed;
    public float revSpeed;
    public float turnSpeed;
    public LayerMask GroundLayer;

    public Rigidbody sphereRB;

    void Start()
    {
        //detach rigidbody from car
        sphereRB.transform.parent = null;
    }

    void Update()
    {
        moveInput = Input.GetAxisRaw("Vertical");
        turnInput = Input.GetAxisRaw("Horizontal");

        //adjust speed for car
        //adjust speed for car

        moveInput *= moveInput > 0 ? fwdSpeed : revSpeed;

        //set cars position to sphere
        transform.position = sphereRB.transform.position;

        //set cars rotation
        float newRotation = turnInput * turnSpeed * Time.deltaTime * Input.GetAxisRaw("Vertical");
        transform.Rotate(0, newRotation, 0, Space.World);

        //Raycast GroundCheck
        RaycastHit hit;
        isCarGrounded = Physics.Raycast(transform.position, -transform.up, out hit, 1f, GroundLayer);


        if(isCarGrounded)
        {
            sphereRB.linearDamping = groundDrag;

            // rotate car to be parallel to ground
            Quaternion targetGroundRotation = Quaternion.FromToRotation(transform.up, hit.normal) * transform.rotation;
            transform.rotation = Quaternion.Slerp(transform.rotation, targetGroundRotation, Time.deltaTime * alignSpeed);
        }
        else
        {
            sphereRB.linearDamping = airDrag;

            // level out car when in the air
            Quaternion levelRotation = Quaternion.FromToRotation(transform.up, Vector3.up) * transform.rotation;
            transform.rotation = Quaternion.Slerp(transform.rotation, levelRotation, Time.deltaTime * alignSpeed * 0.5f);
        }
    }

    private void FixedUpdate()
    {
        if(isCarGrounded)
        {
            // move car
            sphereRB.AddForce(transform.forward * moveInput, ForceMode.Acceleration);
        } else
        {
            sphereRB.AddForce(-transform.up * 9.8f);
        }

        
    }
    
}
