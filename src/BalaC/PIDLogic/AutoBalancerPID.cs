using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(BalaCBlocklyApi))]
public class AutoBalancerPID : MonoBehaviour
{
    [Header("Settings")]
    public float timeStep = 0.02f;

    [Header("Smoothing")]
    [Tooltip("0 = Nessun filtro, 0.9 = Molto morbido")]
    [Range(0f, 0.9f)]
    public float smoothing = 0.6f;

    [Header("Orientation Fix")]
    public bool invertVertical = false;
    
    [Header("Motor Direction")]
    public bool invertMotorA = true;  
    public bool invertMotorB = false;

    [Header("PID Factors")]
    public float KpFactor = 0.05f;  
    public float KiFactor = 0.001f;
    public float KdFactor = 5.0f;  

    [Header("Realism & Noise")]
    [Tooltip("Aggiunge un errore casuale all'angolo letto per simulare sensori reali imperfetti.")]
    public float sensorNoiseAmount = 0.5f;

    [Header("Fine Tuning (ANTI-DRIFT)")]
    public float balanceTrim = 0.0f;

    [Header("Interactive Testing")]
    public float clickPushForce = 5.0f;
    public Slider forceSlider;
    public Color clickEffectColor = new Color(0, 1, 1, 1f);

    [Header("System State Analysis")]
    public float steadyStateTolerance = 1.0f; 
    public float requiredSteadyTime = 0.5f;
    
    [Header("OnGUI Graph Settings")]
    public int guiX = 20;
    public int guiY = 20;
    public int graphWidth = 600; 
    public int graphHeight = 300;
    public float graphVerticalScale = 5f; 

    private bool hasReachedSteady = false;
    private float currentMotorPercent = 0f;
    
    public static bool isFullScreen = false;

    // Variabili PID
    public float Kp = 0f;
    public float Ki = 0f;
    public float Kd = 0f;

    // Stato
    private float integral = 0f;
    private float lastError = 0f;
    private bool isBalancing = false;
    private float currentSmoothedSpeed = 0f;

    // Calibrazione
    private Vector3 calibratedVertical;
    private BalaCBlocklyApi api;

    void Start()
    {
        if (forceSlider != null)
        {
            forceSlider.value = clickPushForce;
            forceSlider.onValueChanged.AddListener((val) => {
                clickPushForce = val;
            });
        }
    }

    void Awake()
    {
        api = GetComponent<BalaCBlocklyApi>();
        Calibrate();
    }

    public void Calibrate()
    {
        if (api != null && api.rb1 != null)
            calibratedVertical = invertVertical ? -api.rb1.transform.right : api.rb1.transform.right;
        else 
            calibratedVertical = Vector3.up;
        
        integral = 0f; lastError = 0f; currentSmoothedSpeed = 0f; 
    }

    public void ActivatePID(float kp, float ki, float kd)
    {
        this.Kp = kp;
        this.Ki = ki;
        this.Kd = kd;
        
        integral = 0f;
        lastError = 0f;
        currentSmoothedSpeed = 0f;
        isBalancing = true;
        LockWheelRotation();
    }

    public void DeactivatePID()
    {
        isBalancing = false;
        UnlockWheelRotation();
        if (api != null) {
            BalaCBlocklyApi.StopMotor("MOTOR_A");
            BalaCBlocklyApi.StopMotor("MOTOR_B");
        }
    }

    private void LockWheelRotation()
    {
        if (api == null) return;
        Rigidbody rbA = api.wheelJointA.GetComponent<Rigidbody>();
        Rigidbody rbB = api.wheelJointB.GetComponent<Rigidbody>();
        if (rbA != null) rbA.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationY;
        if (rbB != null) rbB.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationY;
    }

    private void UnlockWheelRotation()
    {
        if (api == null) return;
        Rigidbody rbA = api.wheelJointA.GetComponent<Rigidbody>();
        Rigidbody rbB = api.wheelJointB.GetComponent<Rigidbody>();
        if (rbA != null) rbA.constraints = RigidbodyConstraints.None;
        if (rbB != null) rbB.constraints = RigidbodyConstraints.None;
    }

    void Update()
    {
        //if (Input.GetKeyDown(KeyCode.Escape)) CloseGraph();

        if (Input.GetMouseButtonDown(0) && !isFullScreen) HandleMousePush();
    }

    private void HandleMousePush()
    {
        if (Camera.main == null || api == null || api.rb1 == null) return;

        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit))
        {
            if (hit.rigidbody == api.rb1 || hit.transform.IsChildOf(api.transform))
            {
                Vector3 pushDirection = ray.direction.normalized;
                api.rb1.AddForceAtPosition(pushDirection * clickPushForce, hit.point, ForceMode.Impulse);
                SpawnRingEffect(hit.point, hit.normal);
            }
        }
    }

    public class PokeRingEffect : MonoBehaviour
    {
        private float lifeTime = 0.6f;
        private float timer = 0f;
        private Color baseColor;
        private LineRenderer lr;

        public void Setup(Color color)
        {
            baseColor = color;
            lr = GetComponent<LineRenderer>();
            if(lr != null) { lr.startColor = baseColor; lr.endColor = baseColor; }
        }

        void Update()
        {
            timer += Time.deltaTime;
            float progress = timer / lifeTime;
            transform.localScale = Vector3.Lerp(Vector3.zero, Vector3.one * 3.0f, progress);

            if (lr != null)
            {
                Color fadedColor = baseColor;
                fadedColor.a = Mathf.Lerp(1f, 0f, progress * progress);
                lr.startColor = fadedColor;
                lr.endColor = fadedColor;
            }

            if (timer >= lifeTime) Destroy(gameObject);
        }
    }

    private void SpawnRingEffect(Vector3 position, Vector3 normal)
    {
        GameObject ringObj = new GameObject("PokeRing");
        ringObj.transform.position = position + (normal * 0.02f);
        ringObj.transform.rotation = Quaternion.LookRotation(normal);

        LineRenderer lr = ringObj.AddComponent<LineRenderer>();
        lr.useWorldSpace = false;
        lr.loop = true;
        lr.positionCount = 50;
        lr.startWidth = 0.05f;
        lr.endWidth = 0.05f;
        lr.material = new Material(Shader.Find("Sprites/Default"));

        float radius = 0.2f;
        for (int i = 0; i < 50; i++)
        {
            float angle = i * (2f * Mathf.PI) / 50;
            float x = Mathf.Cos(angle) * radius;
            float y = Mathf.Sin(angle) * radius;
            lr.SetPosition(i, new Vector3(x, y, 0));
        }

        var effect = ringObj.AddComponent<PokeRingEffect>();
        effect.Setup(clickEffectColor);
    }

    // =========================================================================
    // LOGICA PID REALE
    // =========================================================================
    void FixedUpdate()
    {
        if (!isBalancing || api == null || api.rb1 == null) return;

        Vector3 currentDir = invertVertical ? -api.rb1.transform.right : api.rb1.transform.right;
        
        float error = Vector3.SignedAngle(calibratedVertical, currentDir, api.rb1.transform.forward);
        error += balanceTrim;

        float noise = UnityEngine.Random.Range(-sensorNoiseAmount, sensorNoiseAmount);
        error += noise;

        integral += error * Time.fixedDeltaTime;
        integral = Mathf.Clamp(integral, -200f, 200f);

        float derivative = (error - lastError) / Time.fixedDeltaTime;

        float P = error * (Kp * KpFactor);
        float I = integral * (Ki * KiFactor);
        float D = derivative * (Kd * KdFactor);

        float rawOutput = P + I + D;

        ApplyMotorSpeed(rawOutput);
        lastError = error;
    }

    private void ApplyMotorSpeed(float rawPidOutput)
    {
        if (float.IsNaN(rawPidOutput) || float.IsInfinity(rawPidOutput)) rawPidOutput = 0;

        float clampedTarget = Mathf.Clamp(rawPidOutput, -1000f, 1000f);
        currentSmoothedSpeed = Mathf.Lerp(currentSmoothedSpeed, clampedTarget, 1f - smoothing);

        float speedA = invertMotorA ? -currentSmoothedSpeed : currentSmoothedSpeed;
        float speedB = invertMotorB ? -currentSmoothedSpeed : currentSmoothedSpeed;

        SetJointSpeed(api.wheelJointA, speedA);
        SetJointSpeed(api.wheelJointB, speedB);
        
        currentMotorPercent = (currentSmoothedSpeed / 1000f) * 100f;
        motorAPercent = (speedA / 1000f) * 100f;
        motorBPercent = (speedB / 1000f) * 100f;
    }

    private float motorAPercent = 0f;
    private float motorBPercent = 0f;
    public float GetMotorAPercent() { return motorAPercent; }
    public float GetMotorBPercent() { return motorBPercent; }

    private void SetJointSpeed(HingeJoint joint, float speed)
    {
        if (joint == null) return;
        var motor = joint.motor;
        motor.force = api.maxMotorForce;
        motor.targetVelocity = speed * api.motorMultiplier;
        motor.freeSpin = false;
        joint.motor = motor;
        joint.useMotor = true;
    }
}