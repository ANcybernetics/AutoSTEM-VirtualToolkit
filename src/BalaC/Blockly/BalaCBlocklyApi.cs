using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UBlockly;
using System;
using System.Collections.Generic;

public class BalaCBlocklyApi : MonoBehaviour
{
    public static BalaCBlocklyApi Instance;

    private class BodyData
    {
        public Rigidbody rb;
        public ArticulationBody ab;
        public Vector3 startPos;
        public Quaternion startRot;

        public BodyData(Rigidbody r)
        {
            rb = r;
            if (rb != null)
            {
                startPos = rb.transform.position;
                startRot = rb.transform.rotation;
                if (IsQuaternionInvalid(startRot)) startRot = Quaternion.identity;
            }
        }

        public BodyData(ArticulationBody a)
        {
            ab = a;
            if (ab != null)
            {
                startPos = ab.transform.position;
                startRot = ab.transform.rotation;
                if (IsQuaternionInvalid(startRot)) startRot = Quaternion.identity;
            }
        }

        private bool IsQuaternionInvalid(Quaternion q)
        {
            return q.x == 0 && q.y == 0 && q.z == 0 && q.w == 0;
        }
    }
    
    private List<BodyData> allBodies = new List<BodyData>();
    private Vector3 rb1StartPos;
    private Quaternion rb1StartRot;
    
    [Header("Stato Sistema")]
    [SerializeField] private bool isPoweredOn = false;

    [Header("Motori")]
    public HingeJoint wheelJointA; 
    public HingeJoint wheelJointB; 
    
    [Header("Parametri Motore")]
    public float maxMotorForce = 40f;   
    public float motorMultiplier = 2f;

    [Header("Fisica")]
    public Rigidbody rb1; 
    public ArticulationBody rb2; 
    public Rigidbody rb3;
    public Rigidbody rb4;
    
    // --- RIFERIMENTO PID ---
    [Header("Moduli Aggiuntivi")]
    public AutoBalancerPID autoBalancer; 

    [Header("Gestione Collisioni")]
    public bool enableCollisionDetection = true;
    public string obstacleTag = "Obstacle"; 
    
    [Header("UI Collisione")]
    public GameObject balaC_Body;
    public GameObject retryButton;
    public GameObject exitButton;
    public Image collisionText; 
    public Selectable menuButtonToDisable;
    public Selectable secondaryButtonToDisable;
    
    private bool _menuButtonOriginalState = true;
    private bool _secondaryButtonOriginalState = true;
    private bool pausedByCollision = false;
    private float previousTimeScale = 1f;

    [Header("Debug")]
    public bool showDebugUI = true;
    public bool IsBusy { get; private set; }
    private string currentAction = "Idle";
    private string lastCommand = "None";
    public event Action<string> OnStatusChanged;
    public string CurrentAction => currentAction;

    // --- VARIABILI PER IL TIMER DEI MOTORI ---
    private float motorUpdateTimer = 2f; // Lo facciamo partire da 2 così si aggiorna subito al primo frame
    private float displayMotorA = 0f;
    private float displayMotorB = 0f;

    void Awake()
    {
        Instance = this;

        RegisterBody(rb1);
        RegisterBody(rb2); 
        RegisterBody(rb3);
        RegisterBody(rb4);
        
        if (rb1 != null)
        {
            rb1StartPos = rb1.transform.position;
            rb1StartRot = rb1.transform.rotation;
        }
        else
        {
            rb1StartPos = transform.position;
            rb1StartRot = transform.rotation;
        }

        if (autoBalancer == null) autoBalancer = GetComponent<AutoBalancerPID>();

        if (retryButton != null) retryButton.SetActive(false);
        if (exitButton != null) exitButton.SetActive(false);
        if (collisionText != null) collisionText.gameObject.SetActive(false);
    }

    private void RegisterBody(Component c)
    {
        if (c == null) return;
        if (c is Rigidbody r) allBodies.Add(new BodyData(r));
        else if (c is ArticulationBody a) allBodies.Add(new BodyData(a));
    }

    void Start()
    {
        Reset(); 
    }

    // --- TIMER CHE SI AGGIORNA OGNI 2 SECONDI ---
    void Update()
    {
        motorUpdateTimer += Time.deltaTime;

        // Ogni 2 secondi fotografiamo il valore attuale dei motori
        if (motorUpdateTimer >= 1f)
        {
            if (autoBalancer != null)
            {
                displayMotorA = autoBalancer.GetMotorAPercent();
                displayMotorB = autoBalancer.GetMotorBPercent();
            }
            motorUpdateTimer = 0f; // Resettiamo il timer
        }
    }

    // --------------------------------------------------------
    // API PID
    // --------------------------------------------------------
    public static void EnablePID(float kp, float ki, float kd)
    {
        if (Instance != null && Instance.autoBalancer != null)
        {
            if (!Instance.isPoweredOn) Instance.SwitchOn();
            
            Instance.autoBalancer.ActivatePID(kp, ki, kd);
            Instance.SetStatus($"PID Active: P={kp} I={ki} D={kd}");
        }
    }

    // --------------------------------------------------------
    // API PUBBLICHE BASE
    // --------------------------------------------------------

    public void SwitchOn()
    {
        isPoweredOn = true;
        SetPhysicsState(true); 
        SetStatus("System Powered ON");
    }

    public void SwitchOff()
    {
        isPoweredOn = false;
        StopAllPhysics();      
        SetPhysicsState(false); 
        SetStatus("System Powered OFF");
    }

    public static void Reset()
    {
        if (Instance != null)
        {
            Instance.isPoweredOn = false;
            Instance.StopAllCoroutines();

            if (CSharp.Runner != null)
            {
                try { CSharp.Runner.Stop(); } catch { }
            }

            Instance.StopAllPhysics(); 
            Instance.ResetMotors();
            Instance.SetPhysicsState(false);
            Instance.ResetAllPositions();
            Physics.SyncTransforms(); 

            if (Instance.autoBalancer != null)
            {
                Instance.autoBalancer.DeactivatePID();
                Instance.autoBalancer.Calibrate(); 
            }

            Instance.SetPhysicsState(false);
            Instance.CleanupUI();
            Instance.SetStatus("System Reset & Power OFF");
        }
    }

    private void SetPhysicsState(bool active)
    {
        bool frozen = !active; 
        foreach (var body in allBodies)
        {
            if (body.rb != null)
            {
                body.rb.isKinematic = frozen;
                if(active) body.rb.WakeUp();
            }
            else if (body.ab != null)
            {
                body.ab.immovable = frozen;
                if(active) body.ab.WakeUp();
            }
        }
    }

    private void StopAllPhysics()
    {
        foreach (var body in allBodies)
        {
            if (body.rb != null)
            {
                #if UNITY_6000_0_OR_NEWER
                body.rb.linearVelocity = Vector3.zero;
                #else
                body.rb.velocity = Vector3.zero;
                #endif
                body.rb.angularVelocity = Vector3.zero;
            }
            else if (body.ab != null)
            {
                body.ab.linearVelocity = Vector3.zero;
                body.ab.angularVelocity = Vector3.zero;
                body.ab.jointVelocity = new ArticulationReducedSpace(0f);
            }
        }
    }

    private void ResetMotors()
    {
        ResetSingleMotor(wheelJointA);
        ResetSingleMotor(wheelJointB);
    }

    private void ResetSingleMotor(HingeJoint joint)
    {
        if (joint != null)
        {
            joint.useMotor = false;
            JointMotor motor = joint.motor;
            motor.targetVelocity = 0f;
            motor.force = 0f;
            joint.motor = motor;
        }
    }

    private void ResetAllPositions()
    {
        foreach (var body in allBodies)
        {
            if (body.startRot.x == 0 && body.startRot.y == 0 && body.startRot.z == 0 && body.startRot.w == 0)
                body.startRot = Quaternion.identity;

            if (body.rb != null)
            {
                body.rb.transform.position = body.startPos;
                body.rb.transform.rotation = body.startRot;
            }
            else if (body.ab != null)
            {
                body.ab.TeleportRoot(body.startPos, body.startRot);
            }
        }
    }

    public static void SetMotorSpeed(string motorName, float speed)
    {
        if (Instance == null) return;

        if (!Instance.isPoweredOn)
        {
            Instance.SwitchOn();
        }

        HingeJoint targetJoint = null;
        if (motorName == "MOTOR_A") targetJoint = Instance.wheelJointA;
        else if (motorName == "MOTOR_B") targetJoint = Instance.wheelJointB;

        if (targetJoint != null)
        {
            JointMotor motor = targetJoint.motor;
            motor.force = Instance.maxMotorForce * 2f;
            motor.targetVelocity = speed * Instance.motorMultiplier * 2f;
            motor.freeSpin = false;
            
            targetJoint.motor = motor;
            targetJoint.useMotor = true; 
        }
    }

    public static void StopMotor(string motorName)
    {
        SetMotorSpeed(motorName, 0);
    }

    public void ReportCollision(GameObject sender, Collision collision)
    {
        if (!enableCollisionDetection || pausedByCollision) return;

        Debug.Log($"[BalaCBlocklyApi] Ricevuta segnalazione crash da: {sender.name}");

        bool isValidBody = (balaC_Body != null && sender == balaC_Body) || sender.CompareTag("BalaC");

        if (isValidBody)
        {
            Debug.Log("[BalaCBlocklyApi] CRASH CONFERMATO!");
            
            if (GameDataRecorder.Instance != null) 
                GameDataRecorder.Instance.AggiungiCollisione();
        }
    }

    private void CleanupUI()
    {
        Time.timeScale = 1f;
        pausedByCollision = false;

        if (retryButton != null) retryButton.SetActive(false);
        if (exitButton != null) exitButton.SetActive(false);
        if (collisionText != null) collisionText.gameObject.SetActive(false);

        EnableMenuButtons();
    }

    public void GoToScene(string sceneName)
    {
        Time.timeScale = 1f;
        if (CSharp.Runner != null) try { CSharp.Runner.Stop(); } catch { }
        SceneManager.LoadScene(sceneName);
    }

    private void DisableMenuButtons()
    {
        if (menuButtonToDisable != null) { _menuButtonOriginalState = menuButtonToDisable.interactable; menuButtonToDisable.interactable = false; }
        if (secondaryButtonToDisable != null) { _secondaryButtonOriginalState = secondaryButtonToDisable.interactable; secondaryButtonToDisable.interactable = false; }
    }
    private void EnableMenuButtons()
    {
        if (menuButtonToDisable != null) menuButtonToDisable.interactable = _menuButtonOriginalState;
        if (secondaryButtonToDisable != null) secondaryButtonToDisable.interactable = _secondaryButtonOriginalState;
    }

    public Vector3 GetRelativeUIValues()
    {
        if (rb1 == null) return Vector3.zero;

        Vector3 currentPos = rb1.transform.position;
        Vector3 relativePos = currentPos - rb1StartPos;

        float userX = relativePos.z / 10f;
        float userY = relativePos.x / 10f;
        float userZ = relativePos.y / 10f;

        return new Vector3(userX, userY, userZ);
    }

    private float NormalizeAngle(float angle)
    {
        if (angle > 180f) angle -= 360f;
        return angle;
    }
    
    void OnGUI()
    {
        if (AutoBalancerPID.isFullScreen) return;

        if (!showDebugUI || rb1 == null) return;
        
        GUIStyle style = new GUIStyle();
        style.fontSize = 24;
        style.fontStyle = FontStyle.Bold;
        style.normal.textColor = isPoweredOn ? Color.green : Color.red; 

        float width = 1000f; 
        float height = 80f;
        float marginX = 20f;
        float marginY = 20f; 

        Rect guiArea = new Rect(marginX, Screen.height - height - marginY, width, height);

        GUILayout.BeginArea(guiArea);
        GUILayout.Label($"STATUS: {currentAction} [POWER: {(isPoweredOn ? "ON" : "OFF")}]", style);
        
        style.fontSize = 18;
        style.normal.textColor = Color.white;

        Vector3 currentPos = rb1.position;
        Vector3 relativePos = currentPos - rb1StartPos;

        float debugX = relativePos.z / 10f;
        float debugY = relativePos.x / 10f;
        float debugZ = relativePos.y / 10f;
        
        Quaternion currentRot = rb1.rotation;
        Quaternion relativeRot = Quaternion.Inverse(rb1StartRot) * currentRot;
        
        Vector3 relativeEuler = relativeRot.eulerAngles;
        relativeEuler.x = NormalizeAngle(relativeEuler.x);
        relativeEuler.y = NormalizeAngle(relativeEuler.y);
        relativeEuler.z = NormalizeAngle(relativeEuler.z);

        GUILayout.Label($"Position (X, Y, Z): {debugX:F2}, {debugY:F2}, {debugZ:F2}   |   Motor A: {displayMotorA:F0}%   Motor B: {displayMotorB:F0}%", style);
        GUILayout.Label($"Rotation: {relativeEuler.x:F1}°, {-relativeEuler.y:F1}°, {relativeEuler.z:F1}°", style);

        GUILayout.EndArea();
    }

    private void SetStatus(string action)
    {
        currentAction = action;
        lastCommand = action; 
        OnStatusChanged?.Invoke(currentAction);
    }
}