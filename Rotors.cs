using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
using SpaceEngineers.Game.ModAPI.Ingame;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using VRage;
using VRage.Collections;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.GUI.TextPanel;
using VRage.Game.ModAPI.Ingame;
using VRage.Game.ModAPI.Ingame.Utilities;
using VRage.Game.ObjectBuilders.Definitions;
using VRageMath;
namespace IngameScript
{
    partial class Program : MyGridProgram
    {
        //var console = GridTerminalSystem.GetBlockWithName("Wide LCD Panel") as IMyTextPanel; 
        //if (console != null) 
        //console.WriteText(someWords); 

        public void UpdateMainRotor()
        {
            UpdateRotor(mainRotor, mainRotorSpeed, status.MainRotorAngle, 0.01f);
        }
        public void UpdateRotor(IMyMotorAdvancedStator rotor, float velocity, float targetAngle, float tolerance = 0.035f)
        {
            float deltaAngle = (float)Math.Atan2(Math.Sin(targetAngle - rotor.Angle), Math.Cos(targetAngle - rotor.Angle));
            if (Math.Abs(deltaAngle) <= tolerance)
            {
                rotor.SetValue("Velocity", 0f);
            }
            else if (deltaAngle < 0)
            {
                rotor.SetValue("Velocity", -velocity);
            }
            else if (deltaAngle > 0)
            {
                rotor.SetValue("Velocity", velocity);
            }
        }
        public bool IsAtStablePosition()
        {
            return (Math.Abs(mainRotor.GetValue<float>("Velocity")) <= 0.01 && PistonStopped());
        }
        public void SetRotorVelocity(List<IMyTerminalBlock> rotors, float speed, bool isReversed)
        {
            if (isReversed)
            {
                speed = -speed;
            }
            foreach (IMyTerminalBlock rotor in rotors)
            {
                rotor.SetValue("Velocity", speed);
            }
            return;
        }
    }
}