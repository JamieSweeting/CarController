using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CameraController : MonoBehaviour
{
    public Transform player;
    private Rigidbody playerRb;
    public Vector3 camOffset;
    public float camSpeed;

    // Start is called before the first frame update
    void Start()
    {
        playerRb = player.GetComponent<Rigidbody>();
    }

    // Update is called once per frame
    void LateUpdate()
    {
        //determines what the current 'forward' is
        Vector3 playerForward = (playerRb.velocity + player.transform.forward).normalized;
        //camera moves to the player, taking the offset into account 
        transform.position = Vector3.Lerp(transform.position, player.position + player.transform.TransformVector(camOffset) + playerForward*(-5f), camSpeed*Time.deltaTime );
        //camera looks at player
        transform.LookAt(player);
    }
}
