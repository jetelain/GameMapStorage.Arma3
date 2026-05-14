#include "script_component.hpp"

INFO("AerialLoop");

params ["_cam", "_camHeight", "_tileSizeM", "_aperture"];

private _x = 0;
while { _x <= worldSize } do {
	private _y = 0;
	while { _y <= worldSize } do {

		// Tile center in world coordinates
		private _cx = _x + _tileSizeM / 2;
		private _cy = _y + _tileSizeM / 2;

		// Place camera directly above tile center (direction set once at init)
		private _terrainH = (getTerrainHeightASL [_cx, _cy]) max 0;
		_cam camSetPos [_cx, _cy, _terrainH + _camHeight];
		_cam camCommit 0;
		setAperture _aperture;

		// Wait for LODs and lighting to settle
		sleep 0.5;

		// Measure exact screen positions of tile corners using the actual camera projection
		// D -- B
		// |    |
		// A -- C
		private _posA = _cam worldToScreen [_x, _y, 0]; // SW
		private _posB = _cam worldToScreen [_x + _tileSizeM, _y + _tileSizeM, 0]; // NE
		private _posC = _cam worldToScreen [_x + _tileSizeM, _y, 0]; // SE
		private _posD = _cam worldToScreen [_x, _y + _tileSizeM, 0]; // NW
		private _args = [_x, _y, _posA, _posB, _posC, _posD];
		INFO_1("aerialscreenshot(%1)", _args);
		"mapExportExtension" callExtension ["aerialscreenshot", _args];

		_y = _y + _tileSizeM;
	};
	_x = _x + _tileSizeM;
};
