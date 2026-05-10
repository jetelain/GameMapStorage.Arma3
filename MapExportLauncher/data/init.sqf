// Automated map export — executed by MapExportLauncher (via -autotest)
// The launcher places this file in the autotest mission folder.
// Arma 3 runs it automatically when the mission starts.

[] spawn {
    // Wait until the local player exists AND the exporter mod has finished initialising.
    // a3me_export is defined in XEH_postInit.sqf which runs after init.sqf,
    // so we must wait for it rather than calling it immediately.
    waitUntil { !isNull player && !isNil "a3me_export" };

    // Move the player to avoid spawning underground.
    player allowDamage false;
    player setPos [worldSize / 2, worldSize / 2, 0];
    sleep 2;

    // Register a listener for the "Complete" callback fired by the extension
    // after the zip has been written.
    missionNamespace setVariable ["a3me_autoLaunch_done", false];
    addMissionEventHandler ["ExtensionCallback", {
        params ["_name", "_function", "_data"];
        if (_name == "a3me" && _function == "Complete") then {
            missionNamespace setVariable ["a3me_autoLaunch_done", true];
        };
    }];

    // Start the export (defined in @arma3MapExporter XEH_postInit.sqf).
    // a3me_export opens and closes the custom map dialog itself.
    [] spawn a3me_export;

    // Block until PackAndUpload fires the Complete callback.
    waitUntil { missionNamespace getVariable ["a3me_autoLaunch_done", false] };
    sleep 2;

    // End the mission — autotest closes Arma 3 automatically after this.
    endMission "END1";
};
