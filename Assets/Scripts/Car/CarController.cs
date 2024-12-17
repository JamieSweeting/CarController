using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using UnityEngine.ProBuilder.MeshOperations;
using UnityEngine.SceneManagement;
using TMPro;
using Unity.Profiling;

//car controller created with assistance from Nanousis Development (YouTube)
public class CarController : MonoBehaviour
{
    #region Variables

    [Header("Car Components")]
    public WheelColliders wheelColliders;
    public WheelMeshes wheelMeshes;
    public WheelParticles wheelParticles;
    public GameObject smokePrefab;
    private Rigidbody carRb;

    [Header("Player Inputs")]
    public float gasInput;
    public float steeringInput;
    public float brakeInput;

    [Header("Car Values")]
    public float RPM;
    public float redLine;
    public float idleRPM;
    //set motorPower in inspector
    public float motorPower;
    public float brakePower;
    //slip angle is the angle between where the wheel is pointing and the direction it is actually travelling
    public float slipAngle;
    private float speed;
    private float speedClamped;
    public float maxSpeed;
    public AnimationCurve steeringCurve;
    public int isEngineRunning;

    [Header("Gears")]
    public int currentGear;
    public float[] gearRatios;
    public float differentialRatio;
    private float currentTorque;
    private float clutch;
    private float wheelRPM;
    public AnimationCurve hpToRPMCurve;
    private GearState gearState;
    public float increaseGearRPM;
    public float decreaseGearRPM;
    public float changeGearTime = 0.5f;

    [Header("UI")]
    public TMP_Text rpmText;
    public TMP_Text speedText;
    public TMP_Text gearText;
    public Transform rpmNeedle;
    public float minNeedleRotation;
    public float maxNeedleRotation;

    #endregion

    #region Functions

    #region Car Movement
    void GatherInputs()
    {
        gasInput = Input.GetAxis("Vertical");
        if (Mathf.Abs(gasInput) > 0 && isEngineRunning == 0)
        {
            StartCoroutine(GetComponent<EngineAudio>().StartEngine());
            gearState = GearState.Running;
        }
        steeringInput = Input.GetAxis("Horizontal");

        //calcuates slip angle by comparing the forward vector of the car gameobject with the direction the rigidbody is actually travelling in
        slipAngle = Vector3.Angle(transform.forward, carRb.velocity-transform.forward);
        float movingDirection = Vector3.Dot(transform.forward, carRb.velocity);
        if (gearState != GearState.Changing)
        {
            if (gearState ==GearState.Neutral)
            {
                clutch = 0;
                if (gasInput > 0) gearState = GearState.Running;
            }
            else
            {
                //steadily applies the clutch, since swapping from 0-1 would cause wheels to lock up and lose all traction
                clutch = Input.GetKey(KeyCode.LeftShift) ? 0 : Mathf.Lerp(clutch, 1, Time.deltaTime);
            }
        }
        if (gearState != GearState.Changing)
        {
            //steadily applies the clutch, since swapping from 0-1 would cause wheels to lock up and lose all traction
            clutch = Input.GetKey(KeyCode.LeftShift) ? 0 : Mathf.Lerp(clutch, 1, Time.deltaTime);
        }
        else
        {
            clutch = 0f;
        }
        if (movingDirection < -0.5f && gasInput > 0)
        {
            brakeInput = Mathf.Abs(gasInput);
        }
        else if (movingDirection > 0.5f && gasInput < 0)
        {
            brakeInput += Mathf.Abs(gasInput);
        }
        else
        {
            brakeInput = 0;
        }

    }

    private void ResetCarPosition()
    {
        transform.position = Vector3.zero;
        transform.eulerAngles = Vector3.zero;
    }

    void ApplyWheelPositions()
    {
        UpdateWheel(wheelColliders.FRWheel, wheelMeshes.FRWheel);
        UpdateWheel(wheelColliders.FLWheel, wheelMeshes.FLWheel);
        UpdateWheel(wheelColliders.RRWheel, wheelMeshes.RRWheel);
        UpdateWheel(wheelColliders.RLWheel, wheelMeshes.RLWheel);
    }

    void UpdateWheel(WheelCollider wheelColl, MeshRenderer wheelMesh)
    {
        //takes the rotation and transform of the wheel colliders components and applies them to the meshes to show it visually
        //Quaternion quat;
        //Vector3 position;
        wheelColl.GetWorldPose(out Vector3 position, out Quaternion quat);
        wheelMesh.transform.position = position;
        wheelMesh.transform.rotation = quat;
    }

    void ApplyMotor()
    {
        currentTorque = CalculateTorque();
        //applies power to rear wheels if engine running - if max speed has been reached it will remove torque until currentspeed is less than maxspeed
        if (isEngineRunning > 1)
        {
            if (speed < maxSpeed)
            {
                wheelColliders.RRWheel.motorTorque = currentTorque * gasInput;
                wheelColliders.RLWheel.motorTorque = currentTorque * gasInput;
            }
            else
            {
                wheelColliders.RRWheel.motorTorque = 0;
                wheelColliders.RLWheel.motorTorque = 0;
            }
        }
    }

    float CalculateTorque()
    {
        float torque = 0;
        if (RPM < idleRPM + 200 && gasInput==0 && currentGear == 0)
        {
            gearState = GearState.Neutral;
        }
        if (gearState == GearState.Running && clutch > 0)
        {
            if (RPM > increaseGearRPM)
            {
                StartCoroutine(ChangeGear(1));
            }
            if (RPM < decreaseGearRPM)
            {
                StartCoroutine(ChangeGear(-1));
            }
        }

        //if clutch not engaged
        if (clutch < 0.1f)
        {
            //then RPM interpolates between whats bigger - idle RPM or how much throttle the player is pressing (engines do not just go down to 0 as soon as gas is released - it idles)
            RPM = Mathf.Lerp(RPM, Mathf.Max(idleRPM, redLine * gasInput) + UnityEngine.Random.Range(-50, 50), Time.deltaTime);
        }
        else
        {
            //determines wheel RPM as the rotations per minute of the rear wheels * the current gear * differentialRatio
            wheelRPM = Mathf.Abs((wheelColliders.RRWheel.rpm + wheelColliders.RLWheel.rpm) / 2f) * gearRatios[currentGear] * differentialRatio;
            RPM = Mathf.Lerp(RPM, Mathf.Max(idleRPM - 100, wheelRPM), Time.deltaTime * 3f);
            //torque equation: (HP(RPM) / RPM) * Current Gear * Diff. Ratio * 5252 * clutch   <--- (HP(RPM) / RPM) is where RPM and HP meet on the power curve, 5252 converts from pounds/foot to newton-meters for Unity
            torque = (hpToRPMCurve.Evaluate(RPM / redLine) * motorPower / RPM) * gearRatios[currentGear] * differentialRatio * 5252f * clutch;
        }
        return torque;
    }

    void ApplySteering()
    {
        //steeringAngle changes depending on where speed falls on the steeringCurve - higher speed = less ability to turn (realistic)
        float steeringAngle = steeringInput * steeringCurve.Evaluate(speed);
        //automatic countersteering
        steeringAngle += Vector3.SignedAngle(transform.forward, carRb.velocity + transform.forward, Vector3.up);
        //steeringAngle = Mathf.Clamp(steeringAngle, -90f, 90f);
        wheelColliders.FRWheel.steerAngle = steeringAngle;
        wheelColliders.FLWheel.steerAngle = steeringAngle;
    }

    void ApplyBrake()
    {
        //applies 70% of brakes to front wheels - since car leans forward when it slows down due to momentum it makes sense to apply more braking power to front wheels as they have more force applied to the ground
        wheelColliders.FRWheel.brakeTorque = brakeInput * brakePower * 0.7f;
        wheelColliders.FLWheel.brakeTorque = brakeInput * brakePower * 0.7f;

        wheelColliders.RRWheel.brakeTorque = brakeInput * brakePower * 0.3f;
        wheelColliders.RLWheel.brakeTorque = brakeInput * brakePower * 0.3f;
    }

    //creates a function that will gives a value between 0 and 1 of how much engine is being used
    public float GetSpeedRatio()
    {
        var gas = Mathf.Clamp(gasInput, 0.5f, 1f);
        return RPM * gas / redLine;
    }

    IEnumerator ChangeGear(int gearChange)
    {
        gearState = GearState.CheckingChange;
        if (currentGear + gearChange >= 0)
        {
            if (gearChange > 0)
            {
                //increase gear
                yield return new WaitForSeconds(0.7f);
                if (RPM < increaseGearRPM || currentGear >= gearRatios.Length - 1)
                {
                    gearState = GearState.Running;
                    yield break;
                }
            }
            if (gearChange < 0)
            {
                //decrease gear
                yield return new WaitForSeconds(0.1f);

                if (RPM > decreaseGearRPM || currentGear <= 0)
                {
                    gearState = GearState.Running;
                    yield break;
                }
            }
            gearState = GearState.Changing;
            yield return new WaitForSeconds(changeGearTime);
            currentGear += gearChange;
        }
        if (gearState != GearState.Neutral)
        {

        }
        gearState = GearState.Running;
    }

    #endregion

    #region Particles
    void InstantiateSmoke()
    {
        //instantiates new instance of smoke at the position of the wheel colliders
        wheelParticles.FRWheel = Instantiate(smokePrefab, wheelColliders.FRWheel.transform.position - Vector3.up * wheelColliders.FRWheel.radius, Quaternion.identity, wheelColliders.FRWheel.transform).GetComponent<ParticleSystem>();
        wheelParticles.FLWheel = Instantiate(smokePrefab, wheelColliders.FLWheel.transform.position - Vector3.up * wheelColliders.FLWheel.radius, Quaternion.identity, wheelColliders.FLWheel.transform).GetComponent<ParticleSystem>();
        wheelParticles.RRWheel = Instantiate(smokePrefab, wheelColliders.RRWheel.transform.position - Vector3.up * wheelColliders.RRWheel.radius, Quaternion.identity, wheelColliders.RRWheel.transform).GetComponent<ParticleSystem>();
        wheelParticles.RLWheel = Instantiate(smokePrefab, wheelColliders.RLWheel.transform.position - Vector3.up * wheelColliders.RLWheel.radius, Quaternion.identity, wheelColliders.RLWheel.transform).GetComponent<ParticleSystem>();
    }

    void CheckParticles()
    {
        WheelHit[] wheelHits = new WheelHit[4];
        wheelColliders.FRWheel.GetGroundHit(out wheelHits[0]);
        wheelColliders.FLWheel.GetGroundHit(out wheelHits[1]);

        wheelColliders.RRWheel.GetGroundHit(out wheelHits[2]);
        wheelColliders.RLWheel.GetGroundHit(out wheelHits[3]);

        //how much slip is allowed before particles spawn in
        float slipAllowance = 0.3f;

        //if wheel is slipping, play smoke particles - if not, stop smoke
        if ((Mathf.Abs(wheelHits[0].sidewaysSlip) + Mathf.Abs(wheelHits[0].forwardSlip) ) > slipAllowance)
        {
            wheelParticles.FRWheel.Play();
        }
        else
        {
            wheelParticles.FRWheel.Stop();
        }

        if ((Mathf.Abs(wheelHits[1].sidewaysSlip) + Mathf.Abs(wheelHits[1].forwardSlip)) > slipAllowance)
        {
            wheelParticles.FLWheel.Play();
        }
        else
        {
            wheelParticles.FLWheel.Stop();
        }

        if ((Mathf.Abs(wheelHits[2].sidewaysSlip) + Mathf.Abs(wheelHits[2].forwardSlip)) > slipAllowance)
        {
            wheelParticles.RRWheel.Play();
        }
        else
        {
            wheelParticles.RRWheel.Stop();
        }

        if ((Mathf.Abs(wheelHits[3].sidewaysSlip) + Mathf.Abs(wheelHits[3].forwardSlip)) > slipAllowance)
        {
            wheelParticles.RLWheel.Play();
        }
        else
        {
            wheelParticles.RLWheel.Stop();
        }
    }

    #endregion

    #endregion

    #region Start / Update

    private void Start()
    {
        carRb = gameObject.GetComponent<Rigidbody>();
        InstantiateSmoke();
    }
    private void Update()
    {
        rpmNeedle.rotation = Quaternion.Euler(0, 0, Mathf.Lerp(minNeedleRotation, maxNeedleRotation, RPM / (redLine * 1.1f)));
        rpmText.text = "RPM: " + RPM.ToString("0000");
        speedText.text = carRb.velocity.magnitude.ToString("0") + "KPH";
        if (gearState == GearState.Neutral)
        {
            gearText.text = ("N");
        }
        else
        {
            gearText.text = (currentGear + 1).ToString();
        }

        //magnitude of velocity is overall length of vector3 velocity - defines speed into a single value as opposed to x,y,z
        //speed = carRb.velocity.magnitude;
        //takes the amount of times the wheel spins as a means of calculating distance travelled by multiplying it with the circumference of the wheel
        speed = wheelColliders.RRWheel.rpm*wheelColliders.RRWheel.radius * 2f * Mathf.PI / 10f;
        speedClamped = Mathf.Lerp(speedClamped, speed, Time.deltaTime);

        if (Input.GetKeyDown(KeyCode.X))
        {
            ResetCarPosition();
        }

        GatherInputs();
        ApplyMotor();
        ApplySteering();
        ApplyBrake();
        CheckParticles();
        ApplyWheelPositions();
    }

    #endregion
}

#region Data Structures
//creates the data structures to store the wheel colliders
[System.Serializable]
public class WheelColliders
{
    public WheelCollider FRWheel;
    public WheelCollider FLWheel;
    public WheelCollider RRWheel;
    public WheelCollider RLWheel;
}

//creates the data structures to store the wheel meshes
[System.Serializable]
public class WheelMeshes
{
    public MeshRenderer FRWheel;
    public MeshRenderer FLWheel;
    public MeshRenderer RRWheel;
    public MeshRenderer RLWheel;
}

[System.Serializable]
public class WheelParticles
{
    public ParticleSystem FRWheel;
    public ParticleSystem FLWheel;
    public ParticleSystem RRWheel;
    public ParticleSystem RLWheel;
}

public enum GearState
{
    Neutral,
    Running,
    CheckingChange,
    Changing
}
#endregion
