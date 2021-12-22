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

/* TODO:
 *   - pause drilling if drills are full
 *   - PID to adjust orientation with wheels height
 *   - PID for main rotor orientation
 *   - drill with state machine
 *   - automine based on schema
 *   - avoid area bounding box
 *   - drill size to compute the automine schema
 */


namespace IngameScript
{
    partial class Program : MyGridProgram
    {
        private const Single rotorShaftDrillingSpeed = 4.0f;
        private const Single rotorShaftStoppingSpeed = 2.0f;
        private const Single mainRotorSpeed = 1.0f;
        private const Single minPistonDistance = 0.0f;
        private const Single maxPistonDistance = 10.0f;

        // vertical distances
        private Single minDepth = 7.0f;
        private Single maxDepth = 20.0f;
        
        // total speed to move the pistons around
        private Single pistonMovingSpeed = 2.0f;
        // total speed when retracting
        private Single pistonRetractSpeed = 2.0f;
        // total speed when drilling
        private Single pistonDrillingSpeed = 0.1f;

        private int totalVerticalPistons = 0;
        private float totalVerticalDistance = 0f;
        private int totalHorizontalPistons = 0;
        private float totalHorizontalDistance = 0f;

        // distance between the axis of rotor and the axis of drill,
        // when the horizontal pistons are fully retracted
        private float minHorizontalDistance = 0f;

        private List<IMyTerminalBlock> allBlocks;

        string drillsGroupName = "drills";
        private IMyBlockGroup drillsGroup;
        private List<IMyShipDrill> drillBlocks;

        string rotorDrillShaftName = "Rover Miner - Drill Advanced Rotor";
        private IMyMotorAdvancedStator rotorDrillShaft;

        string mainRotorName = "Rover Miner - Main Advanced Rotor";
        private IMyMotorAdvancedStator mainRotor;


        private DateTime timeChecker;
        private DateTime CurrentTime;
        

        // pistons to move
        string forwardPistonsGroupName = "forward pistons";
        private IMyBlockGroup forwardPistonsGroup;
        private List<IMyPistonBase> forwardPistonBlocks;
        string reversePistonsGroupName = "reverse pistons";
        private IMyBlockGroup reversePistonsGroup;
        private List<IMyPistonBase> reversePistonBlocks;

        // pistons to drill vertically
        string downPistonsGroupName = "down pistons";
        private IMyBlockGroup downPistonsGroup;
        private List<IMyPistonBase> downPistonBlocks;
        string upPistonsGroupName = "up pistons";
        private IMyBlockGroup upPistonsGroup;
        private List<IMyPistonBase> upPistonBlocks;


        private List<bool> connectorsStatus;

        private Dictionary<string, Color> colors = new Dictionary<string, Color>();



        private Status status;

        //IMyShipDrill : IMyFunctionalBlock, IMyTerminalBlock, IMyCubeBlock, IMyEntity

        MyCommandLine _commandLine = new MyCommandLine();
        Dictionary<string, Action> _commands = new Dictionary<string, Action>(StringComparer.OrdinalIgnoreCase);

        MyIni _ini = new MyIni();

        public Program()
        {
            // parse data in the "custom data" section of the PB
            ParseIniFile();

            // Associate the methods with the commands
            _commands["automine"] = Automine;
            _commands["stop"] = Stop;
            _commands["park"] = Park;
            _commands["rename"] = Rename;
            _commands["extend"] = Extend;
            _commands["retract"] = Retract;
            _commands["abort"] = Abort;
            _commands["goto"] = Goto;
            _commands["drill"] = Drill;

            // get a list of all blocks on the grid
            allBlocks = new List<IMyTerminalBlock>();
            GridTerminalSystem.GetBlocks(allBlocks);

            // drills
            AllocateRessources(ref drillBlocks, drillsGroupName, drillsGroup);

            // horizontal pistons
            AllocateRessources(ref forwardPistonBlocks, forwardPistonsGroupName, forwardPistonsGroup);
            AllocateRessources(ref reversePistonBlocks, reversePistonsGroupName, reversePistonsGroup);
            totalHorizontalPistons = reversePistonBlocks.Count + forwardPistonBlocks.Count;
            totalHorizontalDistance = 10.0f * totalHorizontalPistons;

            // vertical pistons
            AllocateRessources(ref downPistonBlocks, downPistonsGroupName, downPistonsGroup);
            AllocateRessources(ref upPistonBlocks, upPistonsGroupName, upPistonsGroup);
            totalVerticalPistons = upPistonBlocks.Count + downPistonBlocks.Count;
            totalVerticalDistance = 10f * totalVerticalPistons;

            // drill shaft rotor (the tip of the bore machine)
            rotorDrillShaft = GridTerminalSystem.GetBlockWithName(rotorDrillShaftName) as IMyMotorAdvancedStator;
            // drill shaft main rotor (the rotor at the base that determines the orientation)
            mainRotor = GridTerminalSystem.GetBlockWithName(mainRotorName) as IMyMotorAdvancedStator;
            
            if (! String.IsNullOrEmpty(Storage))
            {
                status = new Status(Storage);
            }
            else
            {
                status = new Status();
            }
            connectorsStatus = new List<bool> { false, false, false, false };

            colors["automine"] = new Color(252, 114, 0);
            colors["standby"] = new Color(255, 251, 247);
            colors["emergency"] = new Color(255, 8, 0);

            // set the frequency update
            Runtime.UpdateFrequency = UpdateFrequency.Update10;
            //IMyShipController.MoveIndicator
            timeChecker = DateTime.Now;
            CurrentTime = DateTime.Now;

            Echo("Compilation successful");
            Echo($"Total of vertical pistons: {totalVerticalPistons}");
            Echo($"Total of horizontal pistons: {totalHorizontalPistons}");

        }

        public void ParseIniFile()
        {
            MyIniParseResult result;
            if (!_ini.TryParse(Me.CustomData, out result))
                throw new Exception(result.ToString());

            if (!String.IsNullOrEmpty(_ini.Get("drill", "minDepth").ToString()))
            {
                minDepth = _ini.Get("drill", "minDepth").ToSingle();
            }

            if (!String.IsNullOrEmpty(_ini.Get("drill", "maxDepth").ToString()))
            {
                maxDepth = _ini.Get("drill", "maxDepth").ToSingle();
            }
            if (!String.IsNullOrEmpty(_ini.Get("drill", "drillsGroupName").ToString()))
            {
                drillsGroupName = _ini.Get("drill", "drillsGroupName").ToString();
            }
            if (!String.IsNullOrEmpty(_ini.Get("drill", "pistonRetractSpeed").ToString()))
            {
                pistonRetractSpeed = _ini.Get("drill", "pistonRetractSpeed").ToSingle();
            }
            if (!String.IsNullOrEmpty(_ini.Get("drill", "pistonMovingSpeed").ToString()))
            {
                pistonMovingSpeed = _ini.Get("drill", "pistonMovingSpeed").ToSingle();
            }
            if (!String.IsNullOrEmpty(_ini.Get("drill", "pistonDrillingSpeed").ToString()))
            {
                pistonDrillingSpeed = _ini.Get("drill", "pistonDrillingSpeed").ToSingle();
            }
            if (!String.IsNullOrEmpty(_ini.Get("drill", "minHorizontalDistance").ToString()))
            {
                minHorizontalDistance = _ini.Get("drill", "minHorizontalDistance").ToSingle();
            }

            

            Echo($"minDepth = {minDepth.ToString()}");
            Echo($"maxDepth = {maxDepth.ToString()}");
            Echo($"drillsGroupName = {drillsGroupName.ToString()}");
            Echo($"pistonRetractSpeed = {pistonRetractSpeed.ToString()}");
            Echo($"pistonMovingSpeed = {pistonMovingSpeed.ToString()}");
            Echo($"pistonDrillingSpeed = {pistonDrillingSpeed.ToString()}");
            Echo($"minHorizontalDistance = {minHorizontalDistance.ToString()}");
        }

        public void AllocateRessources<T>(ref List<T> blocs, String groupName, IMyBlockGroup group ) where T: class
        {
            blocs = new List<T>();
            try
            {
                group = GridTerminalSystem.GetBlockGroupWithName(groupName);
                group.GetBlocksOfType<T>(blocs);
            }
            catch (Exception e)
            {
                Echo($"not found: {groupName}");
            }
        }
        public void Save()
        {
            Storage = status.Save();
        }

        public void Main(string argument, UpdateType updateType)
        {
            // If the update source is from a trigger or a terminal,
            // this is an interactive command.
            if ((updateType & (UpdateType.Trigger | UpdateType.Terminal)) != 0)
            {
                if (_commandLine.TryParse(argument))
                {
                    Action commandAction;

                    // Retrieve the first argument. Switches are ignored.
                    string command = _commandLine.Argument(0);

                    // Now we must validate that the first argument is actually specified, 
                    // then attempt to find the matching command delegate.
                    if (command == null)
                    {
                        Echo("No command specified");
                    }
                    else if (_commands.TryGetValue(command, out commandAction))
                    {
                        // We have found a command. Invoke it.
                        commandAction();
                    }
                    else
                    {
                        Echo($"Unknown command {command}");
                    }
                }
            }

            // If the update source has this update flag, it means
            // that it's run from the frequency system, and we should
            // update our continuous logic.
            if ((updateType & (UpdateType.Update1 | UpdateType.Update10 | UpdateType.Update100)) != 0)
            {
                CurrentTime = DateTime.Now;
                UpdateDrill();
                UpdatePistons();
                UpdateVerticalPistons();
                UpdateMainRotor();
                UpdateAutomine();
                UpdateScreen();
            }

        }


        public void Rename()
        {
            string myNewPrefix = _commandLine.Argument(1);
            string myRegex = "^" + myNewPrefix + " - *";
            Echo("Renaming blocs");
            foreach (IMyTerminalBlock bloc in allBlocks)
            {
                if (!System.Text.RegularExpressions.Regex.IsMatch(bloc.CustomName, myRegex))
                {
                    string newName = myNewPrefix + " - " + bloc.CustomName;
                    Echo($"- {bloc.CustomName} -> {newName}");
                    bloc.CustomName = newName;
                }
            }
            return;
        }

        public void Drill()
        {
            string argument = _commandLine.Argument(1);
            switch (argument)
            {
                case "stop":
                    Echo("Stop drilling");
                    SetDrillStatus(false, false);
                    break;
                case "start":
                    Echo("Start drilling");
                    SetDrillStatus(true, true);
                    SetupVerticalPistons();
                    break;
                case "spin":
                    Echo("Start spinning");
                    SetDrillStatus(false, true);
                    break;
                default:
                    Echo($"ERROR: Drill, unsupported command: {argument}");
                    return;
            }
        }

        public void Goto()
        {
            float x = Convert.ToSingle(_commandLine.Argument(1));
            float y = Convert.ToSingle(_commandLine.Argument(2));
            Echo($" goto: x={x}, y={y}");
            Goto(x, y);
        }

        public void Extend()
        {
            //SetDrillStatus(true, true);
            SetExtendingStatus(true, false);
            status.AbortRequested = false;
        }

        public void Retract()
        {
            SetDrillStatus(false, false);
            SetExtendingStatus(false, true);
            status.AbortRequested = false;
        }

        public void Abort()
        {
            status.IsAutomining = false;
            SetDrillStatus(false, false);
            status.MainRotorAngle = 0;
            SetExtendingStatus(false, false);
            status.AbortRequested = true;
        }

        public void Park()
        {
            status.IsAutomining = false;
            SetDrillStatus(false, false);
            status.MainRotorAngle = 0;
            SetExtendingStatus(false, false);
            status.AbortRequested = true;
            ResetVerticalPistons();
        }

        public void Automine()
        {
            Echo("Starting auto-mining program");
            status.IsAutomining = true;
            status.AbortRequested = false;
            Echo($"PistonsExtended() = {PistonsExtended()}");
            Echo($"PistonRetracted() = {PistonRetracted()}");
        }

        public void Stop()
        {
            Echo("Emergency STOP requested");
            status.IsAutomining = false;
            StopPistons();
            SetExtendingStatus(false, false);
        }

        public void SetDrillStatus(bool isDrilling, bool isSpinning)
        {
            status.IsDrilling = isDrilling;
            status.IsSpinning = isSpinning;
        }

        public void SetExtendingStatus(bool isExtending, bool isRetracting)
        {
            status.IsExtending = isExtending;
            status.IsRetracting = isRetracting;
        }

        public bool PistonStopped()
        {
            return (PistonsExtended() || PistonRetracted());
        }
        public bool PistonsExtended()
        {
            bool[] tmpStatus = new bool[forwardPistonBlocks.Count];
            int c = 0;
            foreach (IMyPistonBase bloc in forwardPistonBlocks)
            {
                if (bloc.CurrentPosition >= bloc.MaxLimit + 0.1 ||
                    bloc.CurrentPosition >= bloc.MaxLimit - 0.1)
                {
                    tmpStatus[c] = true;
                }
                else
                {
                    tmpStatus[c] = false;
                }
                c++;
            }
            return !tmpStatus.Contains(false);
        }

        public bool PistonRetracted()
        {
            bool[] tmpStatus = new bool[forwardPistonBlocks.Count];
            int c = 0;
            foreach (IMyPistonBase bloc in forwardPistonBlocks)
            {
                if (bloc.CurrentPosition <= bloc.MinLimit + 0.1 ||
                    bloc.CurrentPosition <= bloc.MinLimit - 0.1)
                {
                    tmpStatus[c] = true;
                }
                else
                {
                    tmpStatus[c] = false;
                }
                c++;
            }
            return !tmpStatus.Contains(false);
        }

        public void UpdateAutomine()
        {
            if (status.IsAutomining)
            {
                if (PistonsExtended() && status.IsExtending)
                {
                    Retract();
                }
                else if (PistonRetracted() && status.IsRetracting)
                {
                    Extend();
                }
                else if (PistonsExtended() && !status.IsExtending && !status.IsRetracting)
                {
                    Retract();
                }
                else if (PistonRetracted() && !status.IsExtending && !status.IsRetracting)
                {
                    Extend();
                }
            }
        }

        public void UpdateVerticalPistons()
        {
            if (status.IsDrilling && IsAtStablePosition())
            {
                ExtendVerticalPistons();
            } else
            {
                RetractVerticalPistons();
            }
        }

        public void SetupVerticalPistons()
        {
            foreach (IMyPistonBase bloc in downPistonBlocks)
            {
                bloc.MinLimit = Convert.ToSingle(minDepth / totalVerticalPistons);
                bloc.MaxLimit = Convert.ToSingle(maxDepth / totalVerticalPistons);
            }
            foreach (IMyPistonBase bloc in upPistonBlocks)
            {
                bloc.MinLimit = Convert.ToSingle(10 - (maxDepth / totalVerticalPistons));
                bloc.MaxLimit = Convert.ToSingle(10 - (minDepth / totalVerticalPistons));
            }
        }

        public void ResetVerticalPistons()
        {
            foreach (IMyPistonBase bloc in downPistonBlocks)
            {
                bloc.MinLimit = 0.0f;
                bloc.MaxLimit = 10.0f;
            }
            foreach (IMyPistonBase bloc in upPistonBlocks)
            {
                bloc.MinLimit = 0.0f;
                bloc.MaxLimit = 10.0f;
            }
        }

        public void ExtendVerticalPistons()
        {
            foreach (IMyPistonBase bloc in downPistonBlocks)
            {
                bloc.Velocity = pistonDrillingSpeed / totalVerticalPistons;
            }
            foreach (IMyPistonBase bloc in upPistonBlocks)
            {
                bloc.Velocity = -pistonDrillingSpeed / totalVerticalPistons;
            }
            return;
        }

        public void RetractVerticalPistons()
        {
            foreach (IMyPistonBase bloc in downPistonBlocks)
            {
                bloc.Velocity = -pistonRetractSpeed / totalVerticalPistons;
            }
            foreach (IMyPistonBase bloc in upPistonBlocks)
            {
                bloc.Velocity = pistonRetractSpeed / totalVerticalPistons;
            }
            return;
        }
        public void UpdatePistons()
        {
            if (status.AbortRequested)
            {
                RetractPistons();
            }
            else if (status.IsExtending)
            {
                ExtendPistons();
            }
            else if (status.IsRetracting)
            {
                RetractPistons();
            }
            else
            {
                StopPistons();
            }

        }

        public float GetCurrentPistonDistance()
        {
            float currentDistance = 0.0f;
            foreach (IMyPistonBase bloc in forwardPistonBlocks)
            {
                currentDistance += bloc.CurrentPosition;
            }
            foreach (IMyPistonBase bloc in reversePistonBlocks)
            {
                currentDistance += 10 - bloc.CurrentPosition;
            }
            return currentDistance;
        }
        public void ExtendPistons()
        {
            float sign = (status.HorizontalDistance >= GetCurrentPistonDistance()) ? 1.0f : -1.0f;
            foreach (IMyPistonBase bloc in forwardPistonBlocks)
            {
                bloc.MaxLimit = Convert.ToSingle(status.HorizontalDistance / totalHorizontalPistons);
                bloc.MinLimit = bloc.MaxLimit;
                bloc.Velocity = sign * pistonMovingSpeed / totalHorizontalPistons;
            }
            foreach (IMyPistonBase bloc in reversePistonBlocks)
            {
                bloc.MinLimit = 10 - Convert.ToSingle(status.HorizontalDistance / totalHorizontalPistons);
                bloc.MaxLimit = bloc.MinLimit;
                bloc.Velocity = sign * -1.0f * pistonMovingSpeed / totalHorizontalPistons;
            }
            return;
        }

        public void RetractPistons()
        {
            foreach (IMyPistonBase bloc in forwardPistonBlocks)
            {
                bloc.MinLimit = minPistonDistance;
                bloc.Velocity = -pistonRetractSpeed / totalHorizontalPistons;
            }
            foreach (IMyPistonBase bloc in reversePistonBlocks)
            {
                bloc.MinLimit = maxPistonDistance;
                bloc.Velocity = pistonRetractSpeed / totalHorizontalPistons;
            }
            return;
        }

        public void StopPistons()
        {
            foreach (IMyPistonBase bloc in forwardPistonBlocks)
            {
                bloc.Velocity = 0;
            }
            foreach (IMyPistonBase bloc in reversePistonBlocks)
            {
                bloc.Velocity = 0;
            }
            return;
        }

        public void OnOff<T>(T bloc, bool isOn) where T : IMyFunctionalBlock
        {
            if (isOn)
            {
                bloc.ApplyAction("OnOff_On");
                bloc.Enabled = true;
            }
            else
            {
                bloc.ApplyAction("OnOff_Off");
                bloc.Enabled = false;
            }
            return;
        }

        public void OnOff<T>(List<T> blocs, bool isOn) where T : IMyFunctionalBlock
        {
            foreach (T bloc in blocs)
            {
                if (isOn)
                {
                    bloc.ApplyAction("OnOff_On");
                    bloc.Enabled = true;
                }
                else
                {
                    bloc.ApplyAction("OnOff_Off");
                    bloc.Enabled = false;
                }
            }
            return;
        }

        public void Goto(double x, double y)
        {
            Echo($" goto: x={x}, y={y}");
            status.AbortRequested = false;
            if (Math.Pow(x,2) + Math.Pow(y,2) <= Math.Pow(totalHorizontalDistance + minHorizontalDistance, 2)){
                status.HorizontalDistance = Math.Sqrt((Math.Pow(x, 2) + Math.Pow(y, 2))) - minHorizontalDistance; // FIXME: this shit with minHorizontalDistance can be done better
                SetExtendingStatus(true, false);
                float angle = (float)Math.Atan2(y, x);
                status.MainRotorAngle = angle > 0 ? angle : angle + 2* (float)Math.PI;
                Echo($"status.HorizontalDistance = {status.HorizontalDistance}");
                Echo($"status.MainRotorAngle = {status.MainRotorAngle}");
            }
        }


        //var console = GridTerminalSystem.GetBlockWithName("Wide LCD Panel") as IMyTextPanel; 
        //if (console != null) 
        //console.WriteText(someWords); 
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
        public void UpdateScreen()
        {
            string extra = Environment.NewLine;
            IMyTextSurface surface = ((IMyTextSurfaceProvider)Me).GetSurface(0);
            surface.ContentType = ContentType.TEXT_AND_IMAGE;
            surface.WriteText("", false);
            surface.WriteText($"mainRotor.Angle = {mainRotor.Angle}" + extra, true);
            surface.WriteText($"status.MainRotorAngle = {status.MainRotorAngle}" + extra, true);

            surface.WriteText($"rotorDrillShaft.Angle rad = {rotorDrillShaft.Angle}" + extra, true);
            surface.WriteText($"rotorDrillShaft.Angle deg = {rotorDrillShaft.Angle * 180 / (float)Math.PI}" + extra, true);
            surface.WriteText($"rotorDrillShaft velocity = {rotorDrillShaft.GetValue<float>("Velocity")}" + extra, true);
            surface.WriteText($"IsAtStablePosition = {IsAtStablePosition()}" + extra, true);
            surface.WriteText($"PistonStopped = {PistonStopped()}" + extra, true);
        }

        public void UpdateMainRotor()
        {
            UpdateRotor(mainRotor, mainRotorSpeed, status.MainRotorAngle, 0.01f);
        }
        public void UpdateDrill()
        {
            //if (status.IsDrilling && (status.HorizontalDistance >= (drillBlocks.Count * 2.5) / 2) && IsAtStablePosition())
            if (status.IsDrilling && IsAtStablePosition())
            {
                OnOff(drillBlocks, true);
            } else
            {
                OnOff(drillBlocks, false);
            }

            //if (status.IsSpinning && (status.HorizontalDistance >= (drillBlocks.Count * 2.5) / 2) && IsAtStablePosition())
            if (status.IsSpinning && IsAtStablePosition())
            {
                // just spin
                rotorDrillShaft.SetValue("Velocity", rotorShaftDrillingSpeed);
            } else
            {
                UpdateRotor(rotorDrillShaft, rotorShaftStoppingSpeed, 0f, 0.035f);
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
