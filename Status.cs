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
    partial class Program
    {
        public class Status
        {
            private bool _isDrilling;
            private bool _isSpinning;
            private bool _isExtending;
            private bool _isRetracting;
            private bool _abortRequested;
            private bool _isAutomining;
            private double _horizontalDistance;
            private double _verticalDistance;
            private float _mainRotorAngle;


            public bool IsDrilling
            {
                get
                {
                    return _isDrilling;
                }

                set
                {
                    _isDrilling = value;
                }
            }


            public bool IsSpinning
            {
                get
                {
                    return _isSpinning;
                }

                set
                {
                    _isSpinning = value;
                }
            }

            public bool IsExtending
            {
                get
                {
                    return _isExtending;
                }

                set
                {
                    _isExtending = value;
                }
            }

            public bool IsRetracting
            {
                get
                {
                    return _isRetracting;
                }

                set
                {
                    _isRetracting = value;
                }
            }

            public bool AbortRequested
            {
                get
                {
                    return _abortRequested;
                }

                set
                {
                    _abortRequested = value;
                }
            }

            public bool IsAutomining
            {
                get
                {
                    return _isAutomining;
                }

                set
                {
                    _isAutomining = value;
                }
            }

            public double HorizontalDistance
            {
                get
                {
                    return _horizontalDistance;
                }

                set
                {
                    _horizontalDistance = value;
                }
            }

            public double VerticalDistance
            {
                get
                {
                    return _verticalDistance;
                }

                set
                {
                    _verticalDistance = value;
                }
            }

            public float MainRotorAngle
            {
                get
                {
                    return _mainRotorAngle;
                }

                set
                {
                    _mainRotorAngle = value;
                }
            }



            public Status(
                    bool IsDrilling = false,
                    bool IsSpinning = false,
                    bool IsExtending = false,
                    bool IsRetracting = false,
                    bool AbortRequested = false,
                    bool IsAutomining = false,
                    double HorizontalDistance = 0,
                    double VerticalDistance = 0,
                    float MainRotorAngle = 0)
            {

                this.IsDrilling = IsDrilling;
                this.IsSpinning = IsSpinning;
                this.IsExtending = IsExtending;
                this.IsRetracting = IsRetracting;
                this.AbortRequested = AbortRequested;
                this.IsAutomining = IsAutomining;
                this.HorizontalDistance = HorizontalDistance;
                this.VerticalDistance = VerticalDistance;
                this.MainRotorAngle = MainRotorAngle;

            }

            public Status(string StorageString)
            {
                this.Load(StorageString);
            }

            public string Save()
            {

                string saveStatus = $"IsDrilling={ this.IsDrilling};IsSpinning={ this.IsSpinning};IsExtending={ this.IsExtending};IsRetracting={ this.IsRetracting};AbortRequested={ this.AbortRequested};IsAutomining={ this.IsAutomining};HorizontalDistance={ this.HorizontalDistance};VerticalDistance={ this.VerticalDistance};MainRotorAngle={ this.MainRotorAngle}";
                return saveStatus;
            }
            public void Load(string saveStatus)
            {
                string[] statusArray = saveStatus.Split(';');
                string key, value;
                try {
                    foreach (string s in statusArray){
                        string[] x = s.Split('=');
                    
                        key = x[0];
                        value = x[1];
                        switch (key)
                        {
                            case "IsDrilling":
                                this.IsDrilling = bool.Parse(value);
                                break;
                            case "IsSpinning":
                                this.IsSpinning = bool.Parse(value);
                                break;
                            case "IsExtending":
                                this.IsExtending = bool.Parse(value);
                                break;
                            case "IsRetracting":
                                this.IsRetracting = bool.Parse(value);
                                break;
                            case "AbortRequested":
                                this.AbortRequested = bool.Parse(value);
                                break;
                            case "IsAutomining":
                                this.IsAutomining = bool.Parse(value);
                                break;
                            case "HorizontalDistance":
                                this.HorizontalDistance = double.Parse(value);
                                break;
                            case "VerticalDistance":
                                this.VerticalDistance = double.Parse(value);
                                break;
                            case "MainRotorAngle":
                                this.MainRotorAngle = float.Parse(value);
                                break;
                        }
                    } 
                } catch (Exception) { }

            }
        }
    }
}
