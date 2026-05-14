#include "script_component.hpp"

INFO("AerialCalibrate");

// _this = [_zoom, _deltaX, _deltaY, _h, _w] — same layout as fnc_screenShotLoop
params ["_zoom", "_deltaX", "_deltaY", "_h", "_w"];

// Derive hires tile size in meters: _h is the topo tile height in meters (usualy 1000)
private _tileSizeM = _h / 4;

// Force noon for consistent lighting
// Use June in northern hemisphere, December in southern hemisphere (negative latitude)
// to ensure the sun is at its highest possible elevation
private _latitude = getNumber (configFile >> "CfgWorlds" >> worldName >> "latitude"); // Latitude on globe, positive is south 
private _summerMonth = if (_latitude > 0) then {12} else {6};
setDate [2024, _summerMonth, 15, 12, 0];

// Clear weather and fog
0 setOvercast 0;
0 setFog [0, 0, 0];
forceWeatherChange;

sleep 0.5; // Wait for weather to update

private _lighting = call FUNC(getLightingNewConfig);

// Hide HUD so it doesn't appear in screenshots
showHUD [false, false, false, false, false, false, false, false];

// Height: use 1.5x safety margin so the tile is always fully in-frame.
// Exact FOV no longer matters — we use worldToScreen per tile to measure exactly.
private _camHeight = 750; // _tileSizeM * 1.5 / (2 * tan 20);
private _fov = 0.5; // (_tileSizeM / 2) / _camHeight; // keeps a reasonable FOV value
private _aperture = getNumber(_lighting/"apertureStandard");
INFO_4("camHeight=%1 FOV=%2 tileSizeM=%3 _aperture=%4", _camHeight, _fov, _tileSizeM, _aperture);

// Create a straight-down camera
private _cam = "camera" camCreate [0, 0, 0];
showCinemaBorder false;
sleep 0.1;
_cam cameraEffect ["internal", "back"];
_cam camSetFocus [-1, -1];
_cam camSetFOV _fov;
_cam setVectorDirAndUp [[0, 0, -1], [0, 1, 0]];
_cam camCommit 0;
setAperture _aperture;
showCinemaBorder false;
sleep 2;


// Notify extension of tile size so it can allocate the full aerial image
"mapExportExtension" callExtension ["aerialcalibrate", [_tileSizeM]];

[_cam, _camHeight, _tileSizeM, _aperture]
