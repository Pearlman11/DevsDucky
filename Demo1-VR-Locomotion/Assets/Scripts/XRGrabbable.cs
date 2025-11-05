using System;
using TMPro;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class XRGrabbable : MonoBehaviour
{
    int handHoverCount = 0;

    GameObject grabbedOffsetGO; // this is only used by method 1 (unity way)
	Matrix4x4 grabbedOffset; //this is only used by method 2 (math way)
	public HandController grabbedBy;
	public Rigidbody rb;

    public Material highlightMaterial;
    public InvertedHullOutline outline;

    public Action released; //called when completely released (free-fall)
    public Action grabbed; //called when first grabbed (previously free-fall)

    public AudioClip collisionSound;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        rb = GetComponent<Rigidbody>();
        rb.maxAngularVelocity = Mathf.Infinity;
    }

    // Update is called once per frame
    void Update()
    {
        if(handHoverCount > 0 && grabbedBy == null)
        {
            outline?.ShowOutline();
        }
        else
        {
            outline?.HideOutline();
        }
    }

	private void FixedUpdate()
	{
		if(grabbedBy != null)
        {


            //Matrix4x4 target = grabbedBy.transform.localToWorldMatrix * grabbedOffset;
            //Vector3 targetPos = new Vector3(target.m03,target.m13, target.m23);
            //Vector3 targetForward = new Vector3(target.m02, target.m12, target.m22).normalized;
            //Vector3 targetUp = new Vector3(target.m01, target.m11, target.m21).normalized;
            //Quaternion targetRot = Quaternion.LookRotation(targetForward, targetUp);

            Vector3 targetPos = grabbedOffsetGO.transform.position;
            Quaternion targetRot = grabbedOffsetGO.transform.rotation; 

            Vector3 toHand = targetPos - this.transform.position;
            rb.linearVelocity = toHand / Time.fixedDeltaTime;

            Quaternion toHandRot = targetRot * Quaternion.Inverse(this.transform.rotation);
            Vector3 axis;
            float angle;
            toHandRot.ToAngleAxis(out angle, out axis);

            rb.angularVelocity = angle * Mathf.Deg2Rad* axis / Time.fixedDeltaTime;
            
        }
	}

    //called by the hand controller, typically.  
	public void Grab(HandController hand) 
    {
        if(grabbedBy != null && grabbedBy != hand)
        {
            //what we should do in this circumstance is not exactly clear.  We just let whatever happen, and it'll probably work until we implement multigrabbing
        }
        grabbedBy = hand;
        
        grabbedOffsetGO = new GameObject(this.name + "_grabbed"); //method 1 way
        grabbedOffsetGO.transform.position = this.transform.position;
        grabbedOffsetGO.transform.rotation = this.transform.rotation;
        grabbedOffsetGO.transform.SetParent(hand.transform, true);

		grabbedOffset = hand.transform.worldToLocalMatrix * this.transform.localToWorldMatrix; //method 2 way

        grabbed?.Invoke(); //we call grabbed for other things to happen, if necessary
	}
	public void Release(HandController hand, Vector3 linearVelocity, Vector3 angularVelocity)
    {
        if(grabbedBy != hand)
        {
            return;
        }
        grabbedBy = null;
        if(grabbedOffsetGO != null)
        {
            Destroy(grabbedOffsetGO);
        }
        released?.Invoke(); //we call released for other things to happen if necessary
        //set the velocity and angular velocity
        rb.angularVelocity = angularVelocity;
        rb.linearVelocity = linearVelocity;
    }

    public void OnHoverEnter(HandController hand)
    {
        handHoverCount++;

        
    }
    public void OnHoverExit(HandController hand)
    {
        handHoverCount--;
        
    }

	public void OnCollisionEnter(Collision collision)
	{
        if (collisionSound != null) { 

            AudioSource.PlayClipAtPoint(collisionSound, collision.contacts[0].point, rb.linearVelocity.magnitude/10.0f); //play a sound on impact
        }
        if(grabbedBy != null)
        {
            //probably do a vibration
            grabbedBy.rumble(rb.linearVelocity.magnitude, .03f); 
        }
	}
}
