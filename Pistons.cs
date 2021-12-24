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


        public void UpdateVerticalPistons()
        {
            if (status.IsDrilling && IsAtStablePosition())
            {
                ExtendVerticalPistons();
            }
            else
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
            double distanceToBottom = ComputeRaycastDistance(drillDistanceCamera);
            double requiredExtensionDelta = 0;
            if (distanceToBottom > 0)
            {
                requiredExtensionDelta = distanceToBottom - 3.5;
            }
            else
            {
                requiredExtensionDelta = 32;
            }
            Echo($"requiredExtensionDelta = {requiredExtensionDelta}");
            float velocity = (float)_pidVerticalPistons.Control(requiredExtensionDelta) / totalVerticalPistons ;
            Echo($"velocity = {velocity}");
            foreach (IMyPistonBase bloc in downPistonBlocks)
            {
                bloc.Velocity = velocity ;
            }
            foreach (IMyPistonBase bloc in upPistonBlocks)
            {
                bloc.Velocity = -velocity;
            }
            return;
        }

        public void RetractVerticalPistons()
        {
            double requiredExtensionDelta = GetCurrentPistonDistance(downPistonBlocks, upPistonBlocks); // basically, when retracting the error is the distance
            float velocity = (float)_pidRetractVerticalPistons.Control(requiredExtensionDelta);
            foreach (IMyPistonBase bloc in downPistonBlocks)
            {
                bloc.Velocity = -velocity / totalVerticalPistons;
            }
            foreach (IMyPistonBase bloc in upPistonBlocks)
            {
                bloc.Velocity = velocity / totalVerticalPistons;
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

        public float GetCurrentPistonDistance(List<IMyPistonBase> forwardPistons, List<IMyPistonBase> backwardPistons)
        {
            float currentDistance = 0.0f;
            foreach (IMyPistonBase bloc in forwardPistons)
            {
                currentDistance += bloc.CurrentPosition;
            }
            foreach (IMyPistonBase bloc in backwardPistons)
            {
                currentDistance += 10 - bloc.CurrentPosition;
            }
            return currentDistance;
        }
        public void ExtendPistons()
        {
            double distanceError = status.HorizontalDistance - GetCurrentPistonDistance();
            float velocity = (float)_pidHirzontalPistons.Control(distanceError);

            foreach (IMyPistonBase bloc in forwardPistonBlocks)
            {
                bloc.MaxLimit = Convert.ToSingle(status.HorizontalDistance / totalHorizontalPistons);
                bloc.MinLimit = bloc.MaxLimit;
                bloc.Velocity = velocity / totalHorizontalPistons;
            }
            foreach (IMyPistonBase bloc in reversePistonBlocks)
            {
                bloc.MinLimit = 10 - Convert.ToSingle(status.HorizontalDistance / totalHorizontalPistons);
                bloc.MaxLimit = bloc.MinLimit;
                bloc.Velocity = -velocity / totalHorizontalPistons;
            }
            return;
        }

        public void RetractPistons()
        {
            double distanceError = GetCurrentPistonDistance();
            float velocity = (float)_pidHirzontalPistons.Control(distanceError);
            foreach (IMyPistonBase bloc in forwardPistonBlocks)
            {
                bloc.MinLimit = minPistonDistance;
                bloc.Velocity = -velocity / totalHorizontalPistons;
            }
            foreach (IMyPistonBase bloc in reversePistonBlocks)
            {
                bloc.MinLimit = maxPistonDistance;
                bloc.Velocity = velocity / totalHorizontalPistons;
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
    }
}