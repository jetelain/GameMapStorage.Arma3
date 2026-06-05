#include "script_component.hpp"

INFO("Calibrate TopoHiRes");

params ["_zoom", "_deltaX", "_deltaY", "_h", "_w"];

_zoom = _zoom / 2;
_deltaX = _deltaX / 2;
_deltaY = _deltaY / 2;
_h = _h / 2;
_w = _w / 2;
private _dbg = [_zoom, _deltaX, _deltaY, _h, _w];
private _mapDisplay = if (!isNull (findDisplay 9801)) then { findDisplay 9801 } else { findDisplay 12 };
private _control = _mapDisplay displayCtrl 51;
_control ctrlMapAnimAdd [0, _zoom, [_deltaX,_deltaY]];
ctrlMapAnimCommit _control;
sleep 0.5;

private _posA = _control ctrlMapWorldToScreen [0,0];
private _posB = _control ctrlMapWorldToScreen [_w,_h];

INFO_2("Initial: zoom=%1 dx=%2",_zoom,(_posB select 0) - (_posA select 0));

_control ctrlMapAnimAdd [0, _zoom, [_deltaX,_deltaY]];
ctrlMapAnimCommit _control;
sleep 0.5;

_posA = _control ctrlMapWorldToScreen [0,0];
_posB = _control ctrlMapWorldToScreen [_w,_h];

INFO_2("Final: zoom=%1 dx=%2",_zoom,(_posB select 0) - (_posA select 0));

private _args = [
	_posA,
	_posB,
	_h
];

INFO_2("hicalibrate(%1) %2",_args,_dbg);
"mapExportExtension" callExtension ["hicalibrate", _args];

[_zoom, _deltaX, _deltaY, _h, _w]