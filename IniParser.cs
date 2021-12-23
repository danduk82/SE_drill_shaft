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

            if (!String.IsNullOrEmpty(_ini.Get("drill", "rotorDrillShaftName").ToString()))
            {
                rotorDrillShaftName = _ini.Get("drill", "rotorDrillShaftName").ToString();
            }
            if (!String.IsNullOrEmpty(_ini.Get("drill", "mainRotorName").ToString()))
            {
                mainRotorName = _ini.Get("drill", "mainRotorName").ToString();
            }
            if (!String.IsNullOrEmpty(_ini.Get("drill", "forwardPistonsGroupName").ToString()))
            {
                forwardPistonsGroupName = _ini.Get("drill", "forwardPistonsGroupName").ToString();
            }
            if (!String.IsNullOrEmpty(_ini.Get("drill", "reversePistonsGroupName").ToString()))
            {
                reversePistonsGroupName = _ini.Get("drill", "reversePistonsGroupName").ToString();
            }
            if (!String.IsNullOrEmpty(_ini.Get("drill", "downPistonsGroupName").ToString()))
            {
                downPistonsGroupName = _ini.Get("drill", "downPistonsGroupName").ToString();
            }
            if (!String.IsNullOrEmpty(_ini.Get("drill", "upPistonsGroupName").ToString()))
            {
                upPistonsGroupName = _ini.Get("drill", "upPistonsGroupName").ToString();
            }
            if (!String.IsNullOrEmpty(_ini.Get("drill", "drillDistanceCameraName").ToString()))
            {
                drillDistanceCameraName = _ini.Get("drill", "drillDistanceCameraName").ToString();
            }
            

            // drill shaft deploy stuff
            if (!String.IsNullOrEmpty(_ini.Get("deploy", "hingesTopGroupName").ToString()))
            {
                hingesTopGroupName = _ini.Get("deploy", "hingesTopGroupName").ToString();
            }
            if (!String.IsNullOrEmpty(_ini.Get("deploy", "hingesBottomGroupName").ToString()))
            {
                hingesBottomGroupName = _ini.Get("deploy", "hingesBottomGroupName").ToString();
            }
            if (!String.IsNullOrEmpty(_ini.Get("deploy", "basculeRotorName").ToString()))
            {
                basculeRotorName = _ini.Get("deploy", "basculeRotorName").ToString();
            }

            // cargo and stone ejections management
            if (!String.IsNullOrEmpty(_ini.Get("cargo", "ejectorsGroupName").ToString()))
            {
                ejectorsGroupName = _ini.Get("cargo", "ejectorsGroupName").ToString();
            }
            if (!String.IsNullOrEmpty(_ini.Get("cargo", "stoneSortersGroupName").ToString()))
            {
                stoneSortersGroupName = _ini.Get("cargo", "stoneSortersGroupName").ToString();
            }

            Echo("Current configuration:");
            Echo($"  minDepth = {minDepth.ToString()}");
            Echo($"  maxDepth = {maxDepth.ToString()}");
            Echo($"  drillsGroupName = {drillsGroupName.ToString()}");
            Echo($"  pistonRetractSpeed = {pistonRetractSpeed.ToString()}");
            Echo($"  pistonMovingSpeed = {pistonMovingSpeed.ToString()}");
            Echo($"  pistonDrillingSpeed = {pistonDrillingSpeed.ToString()}");
            Echo($"  minHorizontalDistance = {minHorizontalDistance.ToString()}");
            Echo($"  rotorDrillShaftName = {rotorDrillShaftName.ToString()}");
            Echo($"  mainRotorName = {mainRotorName.ToString()}");
            Echo($"  forwardPistonsGroupName = {forwardPistonsGroupName.ToString()}");
            Echo($"  reversePistonsGroupName = {reversePistonsGroupName.ToString()}");
            Echo($"  downPistonsGroupName = {downPistonsGroupName.ToString()}");
            Echo($"  upPistonsGroupName = {upPistonsGroupName.ToString()}");
            Echo($"  drillDistanceCameraName = {drillDistanceCameraName.ToString()}");
            Echo($"  hingesTopGroupName = {hingesTopGroupName.ToString()}");
            Echo($"  hingesBottomGroupName = {hingesBottomGroupName.ToString()}");
            Echo($"  basculeRotorName = {basculeRotorName.ToString()}");
            Echo($"  ejectorsGroupName = {ejectorsGroupName.ToString()}");
            Echo($"  stoneSortersGroupName = {stoneSortersGroupName.ToString()}");
        }
        public void Save()
        {
            Storage = status.Save();
        }
    }
}