using System.Collections.Generic;
using System.Collections;
using UnityEngine;
using Cinemachine;

public class GliderController : MonoBehaviour
{
    [Header("Control Parameters")] [SerializeField]
    List<AeroSurface> controlSurfaces = null;

    [SerializeField] float rollControlSensitivity = 0.2f;
    [SerializeField] float pitchControlSensitivity = 0.2f;
    [SerializeField] float yawControlSensitivity = 0.2f;
    readonly float[] sensitivitySaves = new float[3];
    public FlapController[] flaps;
    float[] flapAngles = {0, 0, 0, 0}; // top to bottom then left to right
    [Range(0, 1)] public float flapOpenSpeed = 0.08f;
    [Range(0, 1)] public float closeSpeed = 0.2f;

    [Header("Display Variables")] [Range(-1, 1)]
    public float Pitch;

    [Range(-1, 1)] public float Yaw;
    [Range(-1, 1)] public float Roll;
    [Range(-1, 1)] public float Flap;

    [Tooltip("Toggled by shift, this helps you do the stuffs")]
    public bool noobSettings;

    [Header("Jet Parameters")] public float thrustPercent;
    AircraftPhysics aircraftPhysics;
    Rigidbody rb;
    public ParticleSystem jet;
    public AnimationCurve proximityCurve;
    bool speeding = false;
    bool jetEmpty = false;
    public float jetAmount = 0f;
    public float decreaseTime = 200;
    public float increaseTime = 100;

    [Tooltip("Bigger values shrink the impact of velocity (increaseMultiplier = velocity/impactOfVelocity)")]
    public float impactOfVelocity = 5;

    [Header("Trails")] public TrailRenderer rightTrail;
    public TrailRenderer leftTrail;
    public Material trailNormal;
    public Material trailBoost;
    public Material trailGround;

    [Header("Dampening Parameters")] public float terminalVelocity = 200f;
    public ControlDampener controlDampener;

    [Header("Camera")] public CinemachineVirtualCamera followCam;
    public CinemachineVirtualCamera followCamRoll;
    public CinemachineVirtualCamera shakeCam;
    public CinemachineVirtualCamera shakeCamRoll;
    CinemachineVirtualCamera currentCam;

    [Header("Brakes")] public Transform[] brakes = new Transform[2];
    public float minVelocity = 30;

    [Header("Balancing")] public PlaneBalanceConfig balanceConfig;
    public bool overrideWithLocalValues = false;
    public SettingsConfig settings;

    [Header("Terrain Generation")] public TerrainGenerator terrain;
    public HeightMapSettings[] biomes;

    [Header("Other")] public HotkeyConfig hotkeys;
    public GameObject startTerrain;
    public GameHandler handler;
    bool dead = false;
    Vector3 startPos;
    Quaternion startRot;
    Vector3 startScale;
    Automation automation;
    bool launched = false;
    float[] groundNear = new float[1];
    float aliveSince = 0;

    //("Game loop do not touch")
    bool doNothing = false;
    [HideInInspector] public bool activateMenuPlease = false;

    [Header("Sounds")] public SoundManager soundManager;

    [Header("Score")] public int highScore = 0;
    public int lastScore = 0;
    public int currentScore = 0;
    int frozenTime = -1;

    private void Start()
    {
        StartCoroutine(AddToScore());
        aircraftPhysics = GetComponent<AircraftPhysics>();
        rb = GetComponent<Rigidbody>();

        if (!overrideWithLocalValues)
        {
            rollControlSensitivity = balanceConfig.rollControlSensitivity;
            pitchControlSensitivity = balanceConfig.pitchControlSensitivity;
            yawControlSensitivity = balanceConfig.yawControlSensitivity;
            proximityCurve = balanceConfig.proximityCurve;
            decreaseTime = balanceConfig.decreaseTime;
            increaseTime = balanceConfig.increaseTime;
            impactOfVelocity = balanceConfig.impactOfVelocity;
            aircraftPhysics.thrust = balanceConfig.thrust;
            minVelocity = balanceConfig.minVelocity;
            terminalVelocity = balanceConfig.terminalVelocity;
            controlDampener.pitchCurve = balanceConfig.pitchCurve;
            controlDampener.rollCurve = balanceConfig.rollCurve;
        }

        dead = false;
        jet.Stop();
        startPos = transform.position;
        startRot = transform.rotation;
        startScale = transform.localScale;
        automation = GetComponent<Automation>();
        rightTrail.emitting = false;
        leftTrail.emitting = false;
        sensitivitySaves[0] = rollControlSensitivity;
        sensitivitySaves[1] = pitchControlSensitivity;
        sensitivitySaves[2] = yawControlSensitivity;
        Respawn();
    }

    private void Update()
    {
        if (doNothing) return;
        if (!overrideWithLocalValues && balanceConfig.liveUpdate)
        {
            rollControlSensitivity = balanceConfig.rollControlSensitivity;
            pitchControlSensitivity = balanceConfig.pitchControlSensitivity;
            yawControlSensitivity = balanceConfig.yawControlSensitivity;
            proximityCurve = balanceConfig.proximityCurve;
            decreaseTime = balanceConfig.decreaseTime;
            increaseTime = balanceConfig.increaseTime;
            impactOfVelocity = balanceConfig.impactOfVelocity;
            aircraftPhysics.thrust = balanceConfig.thrust;
            minVelocity = balanceConfig.minVelocity;
            terminalVelocity = balanceConfig.terminalVelocity;
            controlDampener.pitchCurve = balanceConfig.pitchCurve;
            controlDampener.rollCurve = balanceConfig.rollCurve;
        }

        Pitch = Input.GetAxis("Vertical");
        Roll = Input.GetAxis("Horizontal");
        Yaw = 0;

        // Automation
        HandleNoob();
        if (noobSettings) automation.NoobSettings(ref Pitch, ref Yaw, ref Roll);

        controlDampener.Dampen(ref Pitch, ref Roll, rb.velocity.magnitude, terminalVelocity);

        // Restart
        if (Input.GetKey(hotkeys.respawn))
        {
            dead = true;
        }

        // Trails
        if ((int) rb.velocity.magnitude > 55)
        {
            rightTrail.material = trailNormal;
            leftTrail.material = trailNormal;
            rightTrail.emitting = true;
            leftTrail.emitting = true;
        }
        else
        {
            rightTrail.emitting = false;
            leftTrail.emitting = false;
        }

        // Jet
        if (Input.GetKey(hotkeys.useNitro) && !jetEmpty)
        {
            SetThrust(1, 0.1f);
            jetAmount -= (1 / decreaseTime) * Time.deltaTime;
        }

        if (jetAmount <= 0)
        {
            jetEmpty = true;
        }
        else if (jetAmount > 0.2f)
        {
            jetEmpty = false;
        }

        if (speeding)
        {
            jet.Play();
            rightTrail.material = trailBoost;
            leftTrail.material = trailBoost;
            rightTrail.emitting = true;
            leftTrail.emitting = true;
        }
        else
        {
            rightTrail.material = trailNormal;
            leftTrail.material = trailNormal;
            jet.Stop();
        }

        // Brakes
        Brake();

        // Camera
        float anglex = transform.eulerAngles.x;
        if (anglex > 180) anglex -= 360;
        float abspitch = (anglex / 180);

        CinemachineVirtualCamera cam = followCam;
        currentCam = currentCam == null ? cam : currentCam;
        bool roll = Mathf.Abs(abspitch) > 0.3f;
        if (thrustPercent > 0.6f)
        {
            cam = roll ? shakeCamRoll : shakeCam;
        }
        else if (roll)
        {
            cam = followCamRoll;
        }

        if (currentCam != cam)
        {
            currentCam.Priority = 1;
            cam.Priority = 2;
            currentCam = cam;
        }

        // Clamp Control
        automation.angleClamp = !roll;
        automation.autoCorrect = !roll;
        automation.autoTurn = !roll;

        // Get Distance from Terrain
        float maxSearchDistance = 500;
        Vector3[] dirs =
            {transform.forward, -transform.forward, transform.up, -transform.up, transform.right, -transform.right};
        groundNear = new float[dirs.Length];
        for (int i = 0; i < dirs.Length; i++)
        {
            Vector3 dir = dirs[i];
            if (Physics.Raycast(transform.position, dir, out RaycastHit hit, maxSearchDistance, 1 << 3))
            {
                groundNear[i] = (hit.distance);
            }
            else
            {
                groundNear[i] = maxSearchDistance;
            }
        }

        // Boost
        float increaseValue = 0;
        float heightValue = 0;
        if (!speeding && !Input.GetKey(hotkeys.useNitro))
        {
            for (int i = 0; i < groundNear.Length; i++)
            {
                float toIncrease = proximityCurve.Evaluate(Mathf.InverseLerp(0, maxSearchDistance / 5, groundNear[i]));
                heightValue = Mathf.Max(toIncrease, heightValue);
                if (toIncrease > 0.25f)
                {
                    increaseValue += toIncrease;
                }
            }

            if (increaseValue > 0.25f) // Trails
            {
                jetAmount += increaseValue * (1 / (increaseTime * 50)) * Time.deltaTime *
                             (rb.velocity.magnitude / impactOfVelocity);
                rightTrail.emitting = true;
                leftTrail.emitting = true;
                rightTrail.material = trailGround;
                leftTrail.material = trailGround;
            }
            else
            {
                rightTrail.material = trailNormal;
                leftTrail.material = trailNormal;
            }
        }

        jetAmount = Mathf.Clamp(jetAmount, 0, 1);
        thrustPercent = Mathf.Clamp(thrustPercent, 0, 1);

        // Score
        if (Time.timeScale != 0)
        {
            currentScore += Mathf.RoundToInt(increaseValue > 0.5f ? increaseValue : 0);
        }

        //if (heightValue > 0.5f)
        //{
        //    soundManager.FadeIn("inGame1", 1);
        //    soundManager.FadeOut("inGame2", 1);
        //} else
        //{
        //    soundManager.FadeIn("inGame2", 1);
        //    soundManager.FadeOut("inGame1", 1);
        //}

        // For HeartBeat Maybe
        //soundManager.ChangeVol("inGame1", heightValue);
        //soundManager.ChangeVol("inGame2", 1 - heightValue);

        // Death    
        if (dead)
        {
            activateMenuPlease = true;
            thrustPercent = 0;
            rb.constraints = RigidbodyConstraints.FreezeAll;
            jet.Stop();
            int seedVal = (int) Random.Range(-500, 500);
            settings.seed = seedVal;
            HeightMapSettings nextBiome = biomes[Random.Range(0, biomes.Length - 1)];
            terrain.heightMapSettings = nextBiome;
            terrain.ClearAllTerrain();
        }

        // Flaps
        for (int i = 0; i < flaps.Length; i++)
        {
            flaps[i].SetFlap(flapAngles[i]);
        }

        if (!launched) jetAmount = 0;
    }

    private void FixedUpdate()
    {
        if (!dead)
        {
            SetControlSurfacesAngles(Pitch, Roll, Yaw, Flap);
            aircraftPhysics.SetThrustPercent(thrustPercent);
        }
    }

    private void HandleNoob()
    {
        noobSettings = settings.noobMode;
    }

    public void SetControlSurfacesAngles(float pitch, float roll, float yaw, float flap)
    {
        float rightFlaps = 0;
        float leftFlaps = 0;
        foreach (var surface in controlSurfaces)
        {
            if (surface == null || !surface.IsControlSurface) continue;
            switch (surface.InputType)
            {
                case ControlInputType.Pitch:
                    surface.SetFlapAngle(pitch * pitchControlSensitivity * surface.InputMultiplyer);
                    rightFlaps += pitch * pitchControlSensitivity * surface.InputMultiplyer;
                    leftFlaps += pitch * pitchControlSensitivity * surface.InputMultiplyer;
                    break;
                case ControlInputType.Roll:
                    surface.SetFlapAngle(roll * rollControlSensitivity * surface.InputMultiplyer);
                    if (surface.InputMultiplyer > 0)
                    {
                        leftFlaps += roll * rollControlSensitivity * surface.InputMultiplyer * 2;
                    }
                    else if (surface.InputMultiplyer < 0)
                    {
                        rightFlaps += roll * rollControlSensitivity * surface.InputMultiplyer * 2;
                    }

                    break;
                case ControlInputType.Yaw:
                    surface.SetFlapAngle(yaw * yawControlSensitivity * surface.InputMultiplyer);
                    break;
                case ControlInputType.Flap:
                    surface.SetFlapAngle(Flap * surface.InputMultiplyer);
                    break;
            }
        }

        leftFlaps *= -300;
        rightFlaps *= -300;
        if (!Input.GetKey(hotkeys.brakes))
        {
            for (int i = 0; i < flapAngles.Length; i++)
            {
                flapAngles[i] = Mathf.Lerp(flapAngles[i], i < flapAngles.Length / 2 ? leftFlaps : rightFlaps,
                    closeSpeed);
            }
        }
    }

    public void SetThrust(float thrustPercent, float time = 0)
    {
        this.thrustPercent = thrustPercent;
        if (time != 0)
        {
            speeding = true;
            Invoke(nameof(ResetThrust), time);
        }
    }

    public void ResetThrust()
    {
        speeding = false;
        thrustPercent = 0;
    }

    public float GetTerminalVelocity()
    {
        return terminalVelocity;
    }

    public void Kill()
    {
        dead = true;
    }


    public bool IsDead()
    {
        return dead;
    }

    public void Respawn()
    {
        lastScore = currentScore;
        if (lastScore > highScore) highScore = lastScore;
        currentScore = 0;
        dead = false;
        transform.position = startPos;
        transform.rotation = startRot;
        transform.localScale = startScale;
        rb.constraints = RigidbodyConstraints.None;
        rb.velocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        rb.isKinematic = true;
        jetAmount = 0f;
        launched = false;
        ResetThrust();
        startTerrain.SetActive(true);
    }

    public void Brake()
    {
        if (Input.GetKey(hotkeys.brakes) && rb.velocity.magnitude > minVelocity)
        {
            foreach (Transform brake in brakes)
            {
                brake.gameObject.SetActive(true);
                brake.localRotation = Quaternion.Euler(brake.localEulerAngles.x, brake.localEulerAngles.y, 90);
            }

            for (int i = 0; i < flapAngles.Length; i++)
            {
                flapAngles[i] = (i % 2) switch
                {
                    0 => Mathf.Lerp(flapAngles[i], 90, flapOpenSpeed),
                    _ => Mathf.Lerp(flapAngles[i], -90, flapOpenSpeed),
                };
            }

            rollControlSensitivity = sensitivitySaves[0] * 2f;
            pitchControlSensitivity = sensitivitySaves[1] * 2f;
        }
        else
        {
            foreach (Transform brake in brakes)
            {
                brake.gameObject.SetActive(false);
                brake.localRotation = Quaternion.Euler(brake.localEulerAngles.x, brake.localEulerAngles.y, 0);
            }

            rollControlSensitivity = sensitivitySaves[0];
            pitchControlSensitivity = sensitivitySaves[1];
        }
    }

    public Rigidbody GetRB()
    {
        return rb;
    }

    public void SetLaunched(bool val)
    {
        launched = val;
        if (launched == true) aliveSince = Time.time;
    }

    public float GetMinDistance()
    {
        return Mathf.Min(groundNear);
    }

    public float GetAliveSince()
    {
        return aliveSince;
    }

    public bool GetLaunched()
    {
        return launched;
    }

    public void SetNothing(bool val)
    {
        doNothing = val;
    }

    public IEnumerator AddToScore()
    {
        yield return new WaitForSeconds(1f);
        if (launched)
        {
            currentScore += 1;
        }

        StartCoroutine(AddToScore());
    }
}