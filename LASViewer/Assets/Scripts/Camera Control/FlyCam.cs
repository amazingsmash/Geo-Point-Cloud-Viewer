using UnityEngine;
using System.Collections;

public class FlyCam : MonoBehaviour {

	/**
     * Writen by Windexglow 11-13-10.  Use it, edit it, steal it I don't care.
     * Converted to C# 27-02-13 - no credit wanted.
     * Added resetRotation, RF control, improved initial mouse position, 2015-03-11 - Roi Danton.
     * Remaded camera rotation - now cursor is locked, added "Walker Mode", 25-09-15 - LookForward.
     * Simple flycam I made, since I couldn't find any others made public.
     * Made simple to use (drag and drop, done) for regular keyboard layout
     * wasdrf : Basic movement
     * shift : Makes camera accelerate
     * space : Moves camera on X and Z axis only.  So camera doesn't gain any height
     * q : Change mode
     */

	public float mouseSensitivity     = 5.0f;        // Mouse rotation sensitivity.
	public float speed                = 10.0f;    // Regular speed.
	public float gravity            = 20.0f;    // Gravity force.
	public float shiftAdd            = 25.0f;    // Multiplied by how long shift is held.  Basically running.
	public float maxShift            = 100.0f;    // Maximum speed when holding shift.
	public bool  walkerMode         = false;    // Walker Mode.

	private float totalRun            = 1.0f;
	private float rotationY            = 0.0f;
	private float maximumY            = 90.0f;    // Not recommended to change
	private float minimumY            = -90.0f;    // these parameters.
	private CharacterController controller;

	void Start() {
		controller = GetComponent<CharacterController>();
		Cursor.lockState = CursorLockMode.Locked;
	}

	void Update() {
		if (Input.GetKeyUp(KeyCode.Q)) {
			// Toggle mode.
			walkerMode = !walkerMode;
		}
    }

	void FixedUpdate () {


        //JM: My addition
        if (Input.touchCount == 2)
        {
            Vector3 direction = transform.forward.normalized;
            direction = direction * Time.deltaTime * 200.0f;
            transform.Translate(direction, Space.World);
            return;
        }
        ////////////

        // Mouse commands.
        float rotationX = transform.localEulerAngles.y + Input.GetAxis("Mouse X") * mouseSensitivity;          
		rotationY += Input.GetAxis("Mouse Y") * mouseSensitivity;
		rotationY = Mathf.Clamp(rotationY, minimumY, maximumY);          
		transform.localEulerAngles = new Vector3(-rotationY, rotationX, 0.0f);

		// Keyboard commands.
		Vector3 p = getDirection();
		if (Input.GetKey(KeyCode.LeftShift)) {
			totalRun += Time.deltaTime;
			p  = p * totalRun * shiftAdd;
			p.x = Mathf.Clamp(p.x, -maxShift, maxShift);
			p.y = Mathf.Clamp(p.y, -maxShift, maxShift);
			p.z = Mathf.Clamp(p.z, -maxShift, maxShift);
		} else {
			totalRun = Mathf.Clamp(totalRun * 0.5f, 1.0f, 1000.0f);
			p = p * speed;
		}

		p = p * Time.deltaTime;
		Vector3 newPosition = transform.position;
		if (walkerMode) {
			// Walker Mode.
			p = transform.TransformDirection(p);
			p.y = 0.0f;
			p.y -= gravity * Time.deltaTime;
			controller.Move(p);
		} else {
			// Fly Mode.
			if (Input.GetButton("Jump")) { // If player wants to move on X and Z axis only (sliding)
				transform.Translate(p);
				newPosition.x = transform.position.x;
				newPosition.z = transform.position.z;
				transform.position = newPosition;
			} else {
				transform.Translate(p);
			}
		}
	}

	private Vector3 getDirection() {
		Vector3 p_Velocity = new Vector3(Input.GetAxis("Horizontal"), 0.0f, Input.GetAxis("Vertical"));
		// Strifing enabled only in Fly Mode.
		if (!walkerMode) {
			if (Input.GetKey(KeyCode.F)) {
				p_Velocity += new Vector3(0.0f, -1.0f, 0.0f);
			}
			if (Input.GetKey(KeyCode.R)) {
				p_Velocity += new Vector3(0.0f, 1.0f, 0.0f);
			}
		}
		return p_Velocity;
	}

	public void resetRotation(Vector3 lookAt) {
		transform.LookAt(lookAt);
	}
}