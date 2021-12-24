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
        public Vector3D LocalCoords<T>(Vector3D worldPos, T referenceBlock) where T : IMyFunctionalBlock
        {
            return Vector3D.TransformNormal(worldPos - referenceBlock.GetPosition(), MatrixD.Transpose(referenceBlock.WorldMatrix));
        }

        public double ComputeRaycastDistance(IMyCameraBlock camera)
        {
            MyDetectedEntityInfo hitInfo;
            double distance = -1.0f;  // if negative it means that it failed to retrieve a distance
            if (!camera.EnableRaycast)
            {
                camera.EnableRaycast = true; // we really want it to be enabled
            }
            hitInfo = camera.Raycast(32);
            if (!hitInfo.IsEmpty())
            {
                // distance = the length of the world coordinates of object - word coordinates of the camera
                distance = (hitInfo.HitPosition.Value - camera.GetPosition()).Length();
            }
            Echo($"raycast distance: {distance}");
            return distance;
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

        public void AllocateRessources<T>(ref List<T> blocs, String groupName, IMyBlockGroup group) where T : class
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

    }
}