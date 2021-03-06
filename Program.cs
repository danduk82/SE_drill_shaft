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
 *   - backport power management script here
 *   - pause drilling if drills are full
 *   - activate ejectors if too much stone
 *   - PID to adjust orientation with wheels height
 *   - drill with state machine
 *   - automine based on schema
 *   - avoid area bounding box
 *   - drill size to compute the automine schema
 */

/* useful ressources:
 *   - https://github.com/paniha/Space_Engineers_Scripting -> mainly for the inventory demos codes
 */

/* CUSTOM DATA EXAMPLE 
[drill]
minDepth=0
maxDepth=30
drillsGroupName=Rover Miner - Drills
pistonDrillingSpeed=0.1
pistonRetractSpeed=5
pistonMovingSpeed=10
minHorizontalDistance=25
rotorDrillShaftName=Rover Miner - Drill Advanced Rotor
mainRotorName=Rover Miner - Main Advanced Rotor
forwardPistonsGroupName=Rover Miner - forward pistons
reversePistonsGroupName=Rover Miner - reverse pistons
downPistonsGroupName=Rover Miner - down pistons
upPistonsGroupName=Rover Miner - up pistons

[deploy]
hingesTopGroupName=Rover Miner - Hinges Top
hingesBottomGroupName=Rover Miner - Hinges Bottom
basculeRotorName=Rover Miner - Bascule Advanced Rotor

[cargo]
ejectorsGroupName=Rover Miner - ejectors
stoneSortersGroupName=Rover Miner - Converyor Sorters
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

        string rotorDrillShaftName = "Drill Advanced Rotor";
        private IMyMotorAdvancedStator rotorDrillShaft;

        string mainRotorName = "Main Advanced Rotor";
        private IMyMotorAdvancedStator mainRotor;

        string drillDistanceCameraName = "drill Camera";
        IMyCameraBlock drillDistanceCamera;

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


        // deployable drill shaft
        string hingesTopGroupName ="Hinges Top";
        private IMyBlockGroup hingesTopGroup;
        private List<IMyMotorAdvancedStator> hingesTopBlocks;
        string hingesBottomGroupName = "Hinges Bottom";
        private IMyBlockGroup hingesBottomGroup;
        private List<IMyMotorAdvancedStator> hingesBottomBlocks;
        string basculeRotorName = "Bascule Advanced Rotor";
        private IMyMotorAdvancedStator basculeRotor;

        // ejection system
        string ejectorsGroupName = "ejectors";
        private IMyBlockGroup ejectorsGroup;
        private List<IMyShipConnector> ejectorsBlocks;
        string stoneSortersGroupName = "sorters";
        private IMyBlockGroup stoneSortersGroup;
        private List<IMyConveyorSorter> stoneSortersBlocks;

        private List<bool> connectorsStatus;
        private Dictionary<string, Color> colors = new Dictionary<string, Color>();

        private Status status;

        const double TimeStep = 1.0 / 6.0; //  Update10 is 1/6th a second, change accordingly otherwise

        PID _pidMainRotor; // PID controller for main rotor orientation
        PID _pidVerticalPistons; // PID controller for vertical drills pistons when extending
        PID _pidRetractVerticalPistons; // PID controller for vertical drills pistons when retracting
        PID _pidHirzontalPistons; // PID controller for horizontal drills pistons

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
            
            // drill shaft "basscule" rotor (the rotor at the top that rotates the whole rig from horizontal to vertical)
            basculeRotor = GridTerminalSystem.GetBlockWithName(basculeRotorName) as IMyMotorAdvancedStator;
            // hinges to deploy the rig
            AllocateRessources(ref hingesTopBlocks, hingesTopGroupName, hingesTopGroup);
            AllocateRessources(ref hingesBottomBlocks, hingesBottomGroupName, hingesBottomGroup);

            // drill shaft rotor (the tip of the bore machine)
            drillDistanceCamera = GridTerminalSystem.GetBlockWithName(drillDistanceCameraName) as IMyCameraBlock;


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

            _pidMainRotor = new PID(2, 0, 0, TimeStep);
            _pidVerticalPistons = new PID(0.2, 0, 0, TimeStep);
            _pidRetractVerticalPistons = new PID(1, 0.1, 0, TimeStep);
            _pidHirzontalPistons = new PID(1, 0, 0, TimeStep);


            Echo("Compilation successful");
            Echo($"Total of vertical pistons: {totalVerticalPistons}");
            Echo($"Total of horizontal pistons: {totalHorizontalPistons}");

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
            drillDistanceCamera.EnableRaycast = false;
        }

        public void Park()
        {
            status.IsAutomining = false;
            SetDrillStatus(false, false);
            status.MainRotorAngle = 0;
            SetExtendingStatus(false, false);
            status.AbortRequested = true;
            ResetVerticalPistons();
            drillDistanceCamera.EnableRaycast = false;
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

    }
}
