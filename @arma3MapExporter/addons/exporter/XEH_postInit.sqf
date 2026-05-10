#include "script_component.hpp"

INFO("PostInit");

addMissionEventHandler ["ExtensionCallback", {
	params ["_name", "_function", "_data"];
	if ( _name == "a3me" ) then {
		if( _function == "Info" ) exitWith {
			INFO(_data);
		};
		if( _function == "Error" ) exitWith {
			ERROR(_data);
		};
		if( _function == "Complete" ) exitWith {
			systemChat "Package is ready to be used !";
		};
	};
}];

"mapExportExtension" callExtension "Warmup";

a3me_export = {

	systemChat "Taking screenshots...";

	INFO("Export");

	private _center = getArray (configFile >> "CfgWorlds" >> worldName >> "centerPosition");
	private _title = getText (configFile >> "CfgWorlds" >> worldName >> "description");
	private _offsetX = getNumber (configFile >> "CfgWorlds" >> worldName >> "grid" >> "offsetX");
	private _offsetY = getNumber (configFile >> "CfgWorlds" >> worldName >> "grid" >> "offsetY");
	
	private _cities = []; 
	{
		_cities pushBack [text _x, locationPosition _x];
	} forEach nearestLocations [_center, ["NameCity", "NameCityCapital", "NameVillage"], 25000];

	if ( !visibleMap ) then {
		// Open the custom fullscreen topographic map dialog (no satellite layer, no cursor).
		createDialog "A3ME_MapExportDisplay";
		sleep 1;
	};

	INFO("Start");

	"mapExportExtension" callExtension ["start", [worldName, worldSize, _cities, _center, _title, _offsetX, _offsetY]];

	private _calibrateData = [1000] call FUNC(calibrate);

	_calibrateData call FUNC(screenShotLoop);

	systemChat "Save image...";
	sleep 0.2;

	INFO("Stop");
	"mapExportExtension" callExtension ["stop", [worldName, worldSize]];

	if ( worldSize < 40960 ) then {
	
		systemChat "Taking screenshots for HiRes...";

		INFO("Start");

		"mapExportExtension" callExtension ["histart", [worldName, worldSize]];

		(_calibrateData call FUNC(recalibrate)) call FUNC(screenShotLoop);

		systemChat "Save image for HiRes...";
		sleep 0.2;

		INFO("Stop");
		"mapExportExtension" callExtension ["histop", [worldName, worldSize]];

	};

	systemChat "Images are ready";

	"mapExportExtension" callExtension ["dispose", [worldName, worldSize]];

	// Close the export dialog.
	closeDialog 0;
};

#define DIK_HOME 0xC7 /* Home on arrow keypad */

["Arma3 Map Export", "Launch export", ["Launch export", "Launch export"], {}, { [] spawn a3me_export; }, [DIK_HOME, [false, false, false]]] call CBA_fnc_addKeybind;
