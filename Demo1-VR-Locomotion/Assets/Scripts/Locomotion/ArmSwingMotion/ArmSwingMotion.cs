using UnityEngine;
using UnityEngine.XR;

public class ArmSwingMotion : MonoBehaviour
{
    //Game Objects
    [Header("Required Components")]
    [SerializeField] private CharacterController characterController; // for physics safe movmement
    [SerializeField] private GameObject LeftHand;
    [SerializeField] private GameObject RightHand;
    [SerializeField] private GameObject MainCamera;

    // positions
    private Vector3 PositionPreviousFrameLeftHand;
    private Vector3 PositionPreviousFrameRightHand;
    private Vector3 PlayerPositionPreviousFrame;
    private Vector3 PlayerPositionCurrentFrame;
    private Vector3 PositionCurrentFrameLeftHand;
    private Vector3 PositionCurrentFrameRightHand;

    // speed
    [Header("Movement Parameters")]
    [SerializeField] private float Speed = 70;
    [SerializeField] private float HandSpeed;

    void Start()
    {
        PlayerPositionPreviousFrame = transform.position; // set current current position
        PositionPreviousFrameLeftHand = LeftHand.transform.position;
        PositionPreviousFrameRightHand = RightHand.transform.position;
    }

    void Update()
    {
        Vector3 forwardDirection = MainCamera.transform.forward;
        forwardDirection.y = 0;
        forwardDirection.Normalize();

        PositionCurrentFrameLeftHand = LeftHand.transform.position;
        PositionCurrentFrameRightHand = RightHand.transform.position;
        PlayerPositionCurrentFrame = transform.position;

        var playerDistanceMoved = Vector3.Distance(PlayerPositionCurrentFrame, PlayerPositionPreviousFrame);
        var leftHandDistanceMoved = Vector3.Distance(PositionPreviousFrameLeftHand, PositionCurrentFrameLeftHand);
        var rightHandDistanceMoved = Vector3.Distance(PositionPreviousFrameRightHand, PositionCurrentFrameRightHand);

        HandSpeed = ((leftHandDistanceMoved - playerDistanceMoved) + (rightHandDistanceMoved - playerDistanceMoved));

        if (Time.timeSinceLevelLoad > 1f)
        {
            Vector3 movement = forwardDirection * HandSpeed * Speed * Time.deltaTime;
            characterController.Move(movement);
        }

        // update previous positions for the next frame
        PositionPreviousFrameLeftHand = PositionCurrentFrameLeftHand;
        PositionPreviousFrameRightHand = PositionCurrentFrameRightHand;
        PlayerPositionPreviousFrame = PlayerPositionCurrentFrame;

    }


}
