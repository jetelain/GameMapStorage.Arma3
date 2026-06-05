#include "script_component.hpp"

INFO("Calibrate TopoBase");

params [["_size",1000]];

private _deltaX = _size/2;
private _deltaY = _size/2;
private _w = _size;
private _h = _size;

private _zoom1 = getNumber (configFile >> "CfgWorlds" >> worldName >> "Grid" >> "Zoom1" >> "zoomMax");
if ( _zoom1 < 0.001 ) then {
	_zoom1 = 0.5;
};
private _zoom = _zoom1;

private _mapDisplay = if (!isNull (findDisplay 9801)) then { findDisplay 9801 } else { findDisplay 12 };
private _control = _mapDisplay displayCtrl 51;
_control ctrlMapAnimAdd [0, _zoom, [_deltaX,_deltaY]];
ctrlMapAnimCommit _control;
sleep 0.5;

private _posA = _control ctrlMapWorldToScreen [0,0];
private _posB = _control ctrlMapWorldToScreen [_w,_h];

INFO_2("Initial: zoom=%1 dx=%2",_zoom,(_posB select 0) - (_posA select 0));

_zoom = ((_posB select 0) - (_posA select 0)) / 0.5 * _zoom;

_control ctrlMapAnimAdd [0, _zoom, [_deltaX,_deltaY]];
ctrlMapAnimCommit _control;
sleep 0.5;
	
_posA = _control ctrlMapWorldToScreen [0,0];
_posB = _control ctrlMapWorldToScreen [_w,_h];

INFO_2("Final: zoom=%1 dx=%2",_zoom,(_posB select 0) - (_posA select 0));

private _args = [
	_posA,
	_posB,
	_size
];
private _dbg = [_zoom, _deltaX, _deltaY, _h, _w];
INFO_2("calibrate(%1) %2",_args,_dbg);
"mapExportExtension" callExtension ["calibrate", _args];

[_zoom, _deltaX, _deltaY, _h, _w]