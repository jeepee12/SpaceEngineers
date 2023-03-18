using System;

// Space Engineers DLLs
using Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
using Sandbox.Game.EntityComponents;
using VRageMath;
using VRage;
using VRage.Collections;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.GUI.TextPanel;
using VRage.Game.ModAPI.Ingame;
using VRage.Game.ObjectBuilders.Definitions;
using SpaceEngineers.Game.ModAPI.Ingame;
using System.Collections.Generic;

/*
 * Must be unique per each script project.
 * Prevents collisions of multiple `class Program` declarations.
 * Will be used to detect the ingame script region, whose name is the same.
 */
namespace ConnectorSimplifier {

/*
 * Do not change this declaration because this is the game requirement.
 */
public sealed class Program : MyGridProgram {

    /*
     * Must be same as the namespace. Will be used for automatic script export.
     * The code inside this region is the ingame script.
     */
    #region ConnectorSimplifier

    Func<IMyTerminalBlock, bool> SameGridFilter;

    /*
     * The constructor, called only once every session and always before any 
     * other method is called. Use it to initialize your script. 
     *    
     * The constructor is optional and can be removed if not needed.
     *
     * It's recommended to set RuntimeInfo.UpdateFrequency here, which will 
     * allow your script to run itself without a timer block.
     */
    public Program() 
    {
        SameGridFilter = block => block.CubeGrid == Me.CubeGrid;
    }

    /*
     * Called when the program needs to save its state. Use this method to save
     * your state to the Storage field or some other means. 
     * 
     * This method is optional and can be removed if not needed.
     */
    public void Save() {}

    /*
     * The main entry point of the script, invoked every time one of the 
     * programmable block's Run actions are invoked, or the script updates 
     * itself. The updateSource argument describes where the update came from.
     * 
     * The method itself is required, but the arguments above can be removed 
     * if not needed.
     */
    public void Main(string argument, UpdateType updateSource) 
    {
        IMyShipConnector mainShipConnector = FindMainConnector();

        if (mainShipConnector == null)
        {
            // No Matching Connector were found, exiting
			Echo("ERR: No connectors found.");
            return;
        }

        if (mainShipConnector.Status == MyShipConnectorStatus.Connected)
        {
            UpdateBlockStatus(false);
            mainShipConnector.Disconnect();
        }
        else if (mainShipConnector.Status == MyShipConnectorStatus.Connectable)
        {
            UpdateBlockStatus(true);
            mainShipConnector.Connect();
        }
    }

    private IMyShipConnector FindMainConnector()
    {
        IMyShipConnector mainShipConnector = null;
        List<IMyShipConnector> allConnectors = new List<IMyShipConnector>();
        GridTerminalSystem.GetBlocksOfType(allConnectors, SameGridFilter);
        
        if (allConnectors.Count == 1)
        {
            mainShipConnector = allConnectors[0];
        }
        else
        {
            foreach (var connector in allConnectors)
            {
                if (connector.CustomName.Contains("ToBase"))
                {
                    mainShipConnector = connector;
                }
            }
            if (mainShipConnector == null)
            {
                Echo("ERR: " + allConnectors.Count + " connectors found. None contains ToBase.");
            }
        }

        return mainShipConnector;
    }

    private void UpdateBlockStatus(bool justConnected)
    {
        UpdateBatteriesStatus(justConnected);
        UpdateThrustersStatus(justConnected);
        UpdateTanksStatus(justConnected);
        UpdateAirVentsStatus(justConnected);
        UpdateLights(justConnected);
    }

    private void UpdateBatteriesStatus(bool justConnected)
    {
        List<IMyBatteryBlock> allBatteries = new List<IMyBatteryBlock>();
        // TODO use lambda to check grid is same
        GridTerminalSystem.GetBlocksOfType(allBatteries, SameGridFilter);
        int batteryStatusChanged = 0;
        foreach (var battery in allBatteries)
        {
            ++batteryStatusChanged;

            if (justConnected)
            {
                battery.ChargeMode = ChargeMode.Recharge;
            }
            else
            {
                battery.ChargeMode = ChargeMode.Auto;
            }
        }
        Echo("Number of batteries changed:" + batteryStatusChanged);
    }

    private void UpdateThrustersStatus(bool justConnected)
    {
        // Turn On/off all thruster (hydro, atmo and ion)
        List<IMyThrust> allThrusters = new List<IMyThrust>();
        GridTerminalSystem.GetBlocksOfType(allThrusters, SameGridFilter);
        foreach (var thruster in allThrusters)
        {
            thruster.Enabled = !justConnected;
        }
    }

    private void UpdateTanksStatus(bool justConnected)
    {
        // set O2 and H2 to stockpile On/Off
        List<IMyGasTank> allTanks = new List<IMyGasTank>();
        GridTerminalSystem.GetBlocksOfType(allTanks, SameGridFilter);
        foreach (var tank in allTanks)
        {
            if (justConnected)
            {
                tank.Stockpile = true;
            }
            else
            {
                tank.Stockpile = false;
            }
        }
    }

    private void UpdateAirVentsStatus(bool justConnected)
    {
        // if contains "int." => pressu on disconnect
        // if contains "ext." => toggle on/off
        List<IMyAirVent> allAirVent = new List<IMyAirVent>();
        GridTerminalSystem.GetBlocksOfType(allAirVent, SameGridFilter);
        foreach (var airVent in allAirVent)
        {
            if (airVent.CustomName.Contains("int."))
            {
                if (!justConnected)
                {
                    airVent.Depressurize = false;
                    airVent.Enabled = true;
                }
            }
            else if (airVent.CustomName.Contains("ext."))
            {
                if (justConnected)
                {
                    airVent.Depressurize = true;
                }
                airVent.Enabled = justConnected;
            }
        }
    }

    private void UpdateLights(bool justConnected)
    {
        List<IMyLightingBlock> allLights = new List<IMyLightingBlock>();
        GridTerminalSystem.GetBlocksOfType(allLights, SameGridFilter);
        foreach (var light in allLights)
        {
            light.Enabled = !justConnected;
        }
    }

    #endregion // ConnectorSimplifier
}}