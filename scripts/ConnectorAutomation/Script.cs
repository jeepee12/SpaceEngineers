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
namespace ConnectorAutomation {

/*
 * Do not change this declaration because this is the game requirement.
 */
public sealed class Program : MyGridProgram {

    /*
     * Must be same as the namespace. Will be used for automatic script export.
     * The code inside this region is the ingame script.
     */
    #region ConnectorAutomation

    private Func<IMyTerminalBlock, bool> SameGridFilter;
    private IMyShipConnector MainShipConnector = null;
    private List<IMyBatteryBlock> AllBatteries = new List<IMyBatteryBlock>();
    private List<IMyThrust> AllThrusters = new List<IMyThrust>();
    private List<IMyGasTank> AllTanks = new List<IMyGasTank>();
    private List<IMyAirVent> AllAirVent = new List<IMyAirVent>();
    private List<IMyLightingBlock> AllLights = new List<IMyLightingBlock>();
    private List<IMyCockpit> AllCockpits = new List<IMyCockpit>();

    private MyShipConnectorStatus CurrentConnectorStatus = MyShipConnectorStatus.Unconnected;
    private bool IsRunAutomaticMode = false;
    private bool BlockListConstructed = false;

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
        if (!Me.CubeGrid.IsStatic)
        {
            SameGridFilter = block => block.CubeGrid == Me.CubeGrid;
            FindMainConnector();
            ConstructBlockLists();
            LoadFromStorage();
        }
        else
        {
            Echo("The grid is static, nothing loaded.");
        }
    }

    private void LoadFromStorage()
    {
        bool.TryParse(Storage, out IsRunAutomaticMode);
        Echo("Loading from storage. IsRunAutomaticMode:" + IsRunAutomaticMode);

        if (IsRunAutomaticMode)
        {
            Runtime.UpdateFrequency = UpdateFrequency.Update10;
        }
    }

    private void ConstructBlockLists()
    {
        if (BlockListConstructed)
            return;

        if (Me.CubeGrid.IsStatic)
        {
            // The MainConnector is suppose to be not null and connected
            IMyCubeGrid otherGrid = MainShipConnector?.OtherConnector?.CubeGrid;
            if (otherGrid == null)
            {
                Echo("The other grid was not found.");
                return;
            }
            SameGridFilter = block => block?.CubeGrid == otherGrid;
        }
        else
        {
            SameGridFilter = block => block.CubeGrid == Me.CubeGrid;
        }

        GridTerminalSystem.GetBlocksOfType(AllBatteries, SameGridFilter);
        GridTerminalSystem.GetBlocksOfType(AllThrusters, SameGridFilter);
        GridTerminalSystem.GetBlocksOfType(AllTanks, SameGridFilter);
        GridTerminalSystem.GetBlocksOfType(AllAirVent, SameGridFilter);
        GridTerminalSystem.GetBlocksOfType(AllLights, SameGridFilter);
        GridTerminalSystem.GetBlocksOfType(AllCockpits, SameGridFilter);
        BlockListConstructed = true;
    }

    private void FindMainConnector(string connectorName = null)
    {
        bool noName = String.IsNullOrWhiteSpace(connectorName);
        if (noName && Me.CubeGrid.IsStatic)
        {
            Echo("The grid is static and no connector name where given.");
            return;
        }

        List<IMyShipConnector> allConnectors = new List<IMyShipConnector>();
        // We cannot use SameGridFilter because it's not set in the case of a static grid
        GridTerminalSystem.GetBlocksOfType(allConnectors, block => block.CubeGrid == Me.CubeGrid);
        
        if (allConnectors.Count == 1)
        {
            MainShipConnector = allConnectors[0];
        }
        else
        {
            if (noName)
            {
                foreach (var connector in allConnectors)
                {
                    if (connector.CustomName.Contains("ToBase"))
                    {
                        MainShipConnector = connector;
                        break;
                    }
                }
            }
            else
            {
                MainShipConnector = GridTerminalSystem.GetBlockWithName(connectorName) as IMyShipConnector;
            }
            if (MainShipConnector == null)
            {
                Echo("ERR: " + allConnectors.Count + " connectors found. None contains ToBase.");
            }
        }
        
        if (MainShipConnector != null)
        {
            CurrentConnectorStatus = MainShipConnector.Status;
        }
    }


    /*
     * Called when the program needs to save its state. Use this method to save
     * your state to the Storage field or some other means. 
     * 
     * This method is optional and can be removed if not needed.
     */
    public void Save() 
    {
        Storage = IsRunAutomaticMode.ToString();
    }

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
        if (updateSource != UpdateType.Update10)
        {
            IsRunAutomaticMode = argument == "Update";
            Echo("Setting IsRunAutomaticMode to : " + IsRunAutomaticMode);
            if (!IsRunAutomaticMode)
            {
                if (Me.CubeGrid.IsStatic)
                {
                    Echo("Running from a static grid. Looking for connector named: " + argument);
                    ExecuteFromStaticGrid(argument);
                }
                else
                {
                    Echo("To set the script in automatic mode, set the argument to 'Update'");
                    ExecuteFromDirectRun();
                }
                Runtime.UpdateFrequency = UpdateFrequency.None;
            }
            else
            {
		        Runtime.UpdateFrequency = UpdateFrequency.Update10;
            }
        }
        else
        {
            ExecuteFromUpdate();
        }
    }

    private void ExecuteFromUpdate()
    {
        if (MainShipConnector.Status != CurrentConnectorStatus)
        {
            Echo("Status changed. Automatic update executing.");
            if (MainShipConnector.Status == MyShipConnectorStatus.Connected)
            {
                // Just Connected
                UpdateBlockStatus(true);
            }
            else if (CurrentConnectorStatus == MyShipConnectorStatus.Connected)
            {
                // Status changed and we were connected, so we just disconnected
                UpdateBlockStatus(false);
            }
            CurrentConnectorStatus = MainShipConnector.Status;
        }
    }

    private void ExecuteFromDirectRun()
    {
        if (MainShipConnector == null)
        {
            // No Matching Connector were found, exiting
            Echo("ERR: No connectors found.");
            return;
        }

        if (MainShipConnector.Status == MyShipConnectorStatus.Connected)
        {
            UpdateBlockStatus(false);
            MainShipConnector.Disconnect();
            Echo("Connector Disconnected");
        }
        else if (MainShipConnector.Status == MyShipConnectorStatus.Connectable)
        {
            UpdateBlockStatus(true);
            MainShipConnector.Connect();
            Echo("Connector Connected");
        }
    }

    private void ExecuteFromStaticGrid(string argument)
    {
        if (MainShipConnector == null)
        {
            FindMainConnector(argument);
            if (MainShipConnector == null)
            {
                Echo("No connector named " + argument + " were fround.");
                return;
            }
        }

        if (MainShipConnector.Status == MyShipConnectorStatus.Connected)
        {
            ConstructBlockLists();
            UpdateBlockStatus(false);
            MainShipConnector.Disconnect();
            Echo("Connector Disconnected");
        }
        else if (MainShipConnector.Status == MyShipConnectorStatus.Connectable)
        {
            MainShipConnector.Connect();
            if (SameGridFilter == null || !SameGridFilter(MainShipConnector.OtherConnector))
            {
                // We cannot keep our cache since a different ship docked.
                BlockListConstructed = false;
                ConstructBlockLists();
            }
            UpdateBlockStatus(true);
            Echo("Connector Connected");
        }
    }

    private void UpdateBlockStatus(bool justConnected)
    {
        UpdateBatteriesStatus(justConnected);
        UpdateThrustersStatus(justConnected);
        UpdateTanksStatus(justConnected);
        UpdateAirVentsStatus(justConnected);
        UpdateLights(justConnected);
        UpdateCockpits(justConnected);
    }

    private void UpdateBatteriesStatus(bool justConnected)
    {
        int batteryStatusChanged = 0;
        foreach (var battery in AllBatteries)
        {
            ++batteryStatusChanged;

            battery.ChargeMode = justConnected ? ChargeMode.Recharge : ChargeMode.Auto;
        }
        Echo("Number of batteries changed:" + batteryStatusChanged);
    }

    private void UpdateThrustersStatus(bool justConnected)
    {
        // Turn On/off all thruster (hydro, atmo and ion)
        foreach (var thruster in AllThrusters)
        {
            thruster.Enabled = !justConnected;
        }
    }

    private void UpdateTanksStatus(bool justConnected)
    {
        // Set O2 and H2 to stockpile On/Off
        foreach (var tank in AllTanks)
        {
            tank.Stockpile = justConnected;
        }
    }

    private void UpdateAirVentsStatus(bool justConnected)
    {
        // if contains "int." => pressu on disconnect
        // if contains "ext." => toggle on/off
        foreach (var airVent in AllAirVent)
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
        foreach (var light in AllLights)
        {
            light.Enabled = !justConnected;
        }
    }

    private void UpdateCockpits(bool justConnected)
    {
        foreach (var cockpit in AllCockpits)
        {
            cockpit.HandBrake = justConnected;
        }
    }


    #endregion // ConnectorAutomation
}}