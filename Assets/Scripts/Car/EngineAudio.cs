using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EngineAudio : MonoBehaviour
{
    public AudioSource startingSound;

    public AudioSource runningSound;
    public float runningMaxVolume;
    public float runningMaxPitch;

    public AudioSource idleSound;
    public float idleMaxVolume;

    private float revLimiter;
    public float limiterSound = 1f;
    public float limiterFrequency = 3f;
    public float limiterEngage = 0.8f;

    private float speedRatio;

    public bool isEngineRunning = false;

    private CarController carController;

    private void Start()
    {
        carController = GetComponent<CarController>();
        idleSound.volume = 0;
        runningSound.volume = 0;
    }

    private void Update()
    {
        if (carController)
        {
            speedRatio = carController.GetSpeedRatio();
        }
        if (speedRatio > limiterEngage)
        {
            //sin = sin wave, oscillates up and down
            revLimiter = (Mathf.Sin(Time.time * limiterFrequency) + 1f) * limiterSound * (speedRatio - limiterEngage);
        }

        if (isEngineRunning) 
        {
            idleSound.volume = Mathf.Lerp(0.1f, idleMaxVolume, speedRatio);
            runningSound.volume = Mathf.Lerp(0.3f, runningMaxVolume, speedRatio);
            //runningSound.pitch = Mathf.Lerp(runningSound.pitch, Mathf.Lerp(0.3f, runningMaxPitch, speedRatio) + revLimiter, Time.deltaTime);
            runningSound.pitch = Mathf.Lerp(0.3f, runningMaxPitch, speedRatio);
        }
        else
        {
            idleSound.volume = 0;
            runningSound.volume = 0;
        }
    }

    public IEnumerator StartEngine()
    {
        startingSound.Play();
        //sets engine state to starting
        carController.isEngineRunning = 1;
        yield return new WaitForSeconds(0.6f);
        isEngineRunning = true;
        yield return new WaitForSeconds(0.4f);
        //sets engine state to running
        carController.isEngineRunning = 2;

    }
}
