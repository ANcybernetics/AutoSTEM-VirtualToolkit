using System.Collections;
using UnityEngine;
using System.Globalization; // <--- FONDAMENTALE

namespace UBlockly
{
    [CodeInterpreter(BlockType = "balac_set_motor_speed")]
    public class BalaC_SetMotorSpeed_Cmdtor : EnumeratorCmdtor
    {
        protected override IEnumerator Execute(Block block)
        {
            // Legge il nome del motore (Dropdown)
            string motor = block.GetFieldValue("MOTOR");
            
            // Legge la velocità in modo sicuro (CultureInfo.InvariantCulture)
            string speedStr = block.GetFieldValue("SPEED");
            float speed = 0f;

            if (!string.IsNullOrEmpty(speedStr))
            {
                float.TryParse(speedStr, NumberStyles.Any, CultureInfo.InvariantCulture, out speed);
            }

            // Esegue il comando
            BalaCBlocklyApi.SetMotorSpeed(motor, speed);
            
            yield break; // Fine esecuzione
        }
    }

    [CodeInterpreter(BlockType = "balac_stop_motor")]
    public class BalaC_StopMotor_Cmdtor : EnumeratorCmdtor
    {
        protected override IEnumerator Execute(Block block)
        {
            string motor = block.GetFieldValue("MOTOR");
            BalaCBlocklyApi.StopMotor(motor);
            yield break; 
        }
    }

    [CodeInterpreter(BlockType = "balac_pid_control")]
    public class BalaC_PID_Control_Cmdtor : EnumeratorCmdtor
    {
        protected override IEnumerator Execute(Block block)
        {
            // Legge i parametri
            float kp = 0f;
            float ki = 0f;
            float kd = 0f;

            string sKP = block.GetFieldValue("KP");
            string sKI = block.GetFieldValue("KI");
            string sKD = block.GetFieldValue("KD");

            if (!string.IsNullOrEmpty(sKP)) float.TryParse(sKP, NumberStyles.Any, CultureInfo.InvariantCulture, out kp);
            if (!string.IsNullOrEmpty(sKI)) float.TryParse(sKI, NumberStyles.Any, CultureInfo.InvariantCulture, out ki);
            if (!string.IsNullOrEmpty(sKD)) float.TryParse(sKD, NumberStyles.Any, CultureInfo.InvariantCulture, out kd);

            // Attiva il PID tramite l'API
            BalaCBlocklyApi.EnablePID(kp, ki, kd);

            yield break;
        }
    }
}