using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;
using UnityEngine.XR.OpenXR.Input;
using UnityEngine.Rendering;



public class HandController : MonoBehaviour
{
    [SerializeField] CharacterController characterController;
    [SerializeField] Transform head;
    [SerializeField] Transform controller;
    [SerializeField] Transform rig;

    [SerializeField] bool useGoGo = true;
    [SerializeField] float goGoThreshold;
    [SerializeField] float goGoScale;

    XRGrabbable grabbedObject;
    Dictionary<XRGrabbable, List<Collider>> grabbablesInTrigger = new();

    [SerializeField] InputAction grabAction;
    [SerializeField] InputAction handVelocity;
    [SerializeField] InputAction handAngularVelocity;
    [SerializeField] InputAction vibration;
    [SerializeField] InputAction thumbstick;
    [SerializeField] float grabThreshold = .2f;
    

    [SerializeField] bool useTeleport = true;
    [SerializeField] float stickDeadZone = .5f;
    [SerializeField] float snapDegrees = 15;
    private bool teleportingActive = false;
    private bool snapActive = false;

    private Vector3 teleportingTarget;
    private bool teleportingValid;

    [SerializeField] bool useAirGrab = true;
    private bool isAirGrabbing = false;
    private Vector3 airGrabStartPositionWorld;

    [SerializeField] GameObject teleporterArcPrefab;
    [SerializeField] float teleporterDt = .1f; // time between ray casts, teleporter length
    GameObject[] arcPieces = new GameObject[50]; // how many ray casts, also teleporter length
    [SerializeField] float teleporterGravity = -1f; // lower is more curved



    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        grabAction.Enable();
        handVelocity.Enable();
        handAngularVelocity.Enable();
        vibration.Enable();
        thumbstick.Enable();

        if (characterController == null && rig != null)
        {
            characterController = rig.GetComponent<CharacterController>();
        }
    }

    // Update is called once per frame
    void Update()
    {
        if (useTeleport)
        {
            // handle snap rotation and teleportation
            var stick = thumbstick.ReadValue<Vector2>();

            if (Mathf.Abs(stick.x) < stickDeadZone * .9f && snapActive)
            {
                snapActive = false;
            }

            if (Mathf.Abs(stick.x) > stickDeadZone && !snapActive)
            {
                snapActive = true;
                // save our foot world pos
                var footWorld = projectToRigFloor(head.position);
                rig.Rotate(0, Mathf.Sign(stick.x) * snapDegrees, 0, Space.World); // head world pos will move
                var footWorldNew = projectToRigFloor(head.position);
                rig.Translate(footWorld - footWorldNew, Space.World); // move it back so the head didnt move
            }
            // Teleport release
            if (stick.y < stickDeadZone * .9f && teleportingActive)
            {
                teleportingActive = false;
                foreach (var part in arcPieces)
                {
                    if (part != null)
                    {
                        part.SetActive(false);
                    }
                }
                // do teleport now if location is valid
                if (teleportingValid)
                {
                    var offset = teleportingTarget - projectToRigFloor(head.transform.position);

                    if (characterController != null)
                    {
                        characterController.enabled = false;
                    }
                    rig.Translate(offset, Space.World);

                    if (characterController != null)
                    {
                        characterController.enabled = true;
                    }
                }
            }
            
            // Teleport start
            if (stick.y > stickDeadZone && !teleportingActive)
            {
                teleportingActive = true;
            }
            
            // Teleport arc rendering
            if (teleportingActive)
            {
                // simulate projectile motion shooting out of controller so we need
                // position, velocity and acceleration
                var p = transform.position;
                var v = transform.forward;
                var a = new Vector3(0, teleporterGravity, 0);

                teleportingValid = false; // assume not a valid pos

                for (var i = 0; i < arcPieces.Length; i++)
                {
                    if (arcPieces[i] == null) // create them if they dont exist 
                    {
                        arcPieces[i] = Instantiate<GameObject>(teleporterArcPrefab);
                    }
                    else // activate them, because they may be inactive
                    {
                        arcPieces[i].SetActive(true);
                    }

                    arcPieces[i].transform.position = p; // move this piece into position
                    var p_next = p + v * teleporterDt; // compute the next projectile position
                    var r = p_next - p; // the vector to check
                    arcPieces[i].transform.forward = r.normalized; // set the pieces to face it (make the arc looked curved)
                    arcPieces[i].transform.localScale = new Vector3(1, 1, r.magnitude); // and scale so it doesnt go past the point 
                    var hits = Physics.RaycastAll(p, r.normalized, r.magnitude); // do the raycast from the last pos

                    if (hits.Length > 0) // we got a hit
                    {
                        if (hits[0].normal.y > .7f) // roughly vertical
                        {
                            teleportingValid = true; // should be able to teleport now
                            teleportingTarget = hits[0].point; // where we will teleport
                            arcPieces[i].transform.localScale = new Vector3(1, 1, hits[0].distance); // problably overshot the vizualization a bit so move it back

                            // set the rest of the teleporter arc inactive
                            for (var j = i + 1; j < arcPieces.Length; j++)
                            {
                                if (arcPieces[j] != null)
                                {
                                    arcPieces[j].SetActive(false);
                                }
                            }
                            break;
                        }
                    }
                    v += a * teleporterDt; // compute new velocity
                    p = p_next; // and update the position
                }
            }

        }
        
          if (useGoGo)
            {
                Vector3 headToController = controller.position - head.position;
                float d = headToController.magnitude;
                if (d >= goGoThreshold)
                {
                    float e = (d - goGoThreshold); // the extra amount to move
                    transform.position = head.position + headToController.normalized * (d + goGoScale * e * e); // move it
                }
                else
                {
                    transform.localPosition = Vector3.zero; // no offset from the controller
                }
            }
            float grabber = grabAction.ReadValue<float>();

            if (!isAirGrabbing && grabber > grabThreshold && grabbedObject == null && grabbablesInTrigger.Count > 0)
            {
                grabbedObject = grabbablesInTrigger.FirstOrDefault().Key;
                grabbedObject.Grab(this);

                var renderers = this.GetComponentsInChildren<Renderer>();
                foreach (var r in renderers)
                {
                    r.enabled = false;
                }
            }
            if (grabber <= grabThreshold * .9f && grabbedObject != null)
            {
                grabbedObject.Release(this, handVelocity.ReadValue<Vector3>(), handAngularVelocity.ReadValue<Vector3>());
                var renderers = this.GetComponentsInChildren<Renderer>();
                foreach (var r in renderers)
                {
                    r.enabled = true;
                }
                grabbedObject = null;
            }
            if (useAirGrab)
            {
                if (grabber > grabThreshold && grabbedObject == null && !isAirGrabbing) // conditions to grip, only grip if no grabbed object
                {
                    isAirGrabbing = true;
                    airGrabStartPositionWorld = transform.position;
                }
                if (grabber < grabThreshold * .9f && isAirGrabbing)
                {
                    isAirGrabbing = false; // stop, maybe fling yoursel
                }
                if (isAirGrabbing)
                {
                    Vector3 handMovement = transform.position - airGrabStartPositionWorld;
                    rig.transform.position = rig.transform.position - handMovement; // move the rig to bring the hand back into pos
                }
            }
    }



    public void rumble(float magnitude, float duration)
    {
        if (vibration.controls.Count > 0)
        {
            (vibration.controls[0].device as XRControllerWithRumble)?.SendImpulse(magnitude, duration);
        }
    }

    Vector3 projectToRigFloor(Vector3 worldPos)
    {
        var rigLocal = rig.InverseTransformPoint(worldPos);
        rigLocal.y = 0;
        return rig.TransformPoint(rigLocal);
    }

    private void OnTriggerEnter(Collider other)
    {
        var g = other.attachedRigidbody?.GetComponent<XRGrabbable>();
        if (g != null)
        {
            if (!grabbablesInTrigger.ContainsKey(g))
            {
                grabbablesInTrigger[g] = new List<Collider> { };
                g.OnHoverEnter(this);
            }
            if (!grabbablesInTrigger[g].Contains(other))
            {
                grabbablesInTrigger[g].Add(other);
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        var g = other.attachedRigidbody?.GetComponent<XRGrabbable>();
        if(g != null)
        {
            if (grabbablesInTrigger.ContainsKey(g))
            {
                grabbablesInTrigger[g].Remove(other);
            }
            if (grabbablesInTrigger[g].Count == 0)
            {
                g.OnHoverExit(this);
                grabbablesInTrigger.Remove(g);
            }
        }
    }

  
}